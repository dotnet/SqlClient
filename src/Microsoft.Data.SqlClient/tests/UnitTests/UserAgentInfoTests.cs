using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.UserAgent;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="UserAgentInfo"/> and its companion DTO.
    /// Focus areas:
    ///   1. Cached payload size and non-nullability
    ///   2. Default expected value check for payload fields
    ///   3. Payload size adjustment and field dropping
    ///   4. DTO JSON contract (key names and values)
    ///   5. Combined truncation, adjustment, and serialization
    /// </summary>
    public class UserAgentInfoTests
    {
        // 1. Cached payload is within the 2,047‑byte spec and never null
        [Fact]
        public void CachedPayload_IsNotNull_And_WithinSpecLimit()
        {
            byte[] payload = UserAgentInfo.CachedPayload;
            Assert.NotNull(payload);
            Assert.InRange(payload.Length, 1, UserAgentInfo.JsonPayloadMaxBytes);
        }

        // 2. Cached payload contains the expected values for driver name and version
        [Fact]
        public void CachedPayload_Contains_Correct_DriverName_And_Version()
        {
            // Arrange: retrieve the raw JSON payload bytes and determine what we expect
            byte[] payload = UserAgentInfo.CachedPayload;
            Assert.NotNull(payload); // guard against null payload

            // compute the expected driver and version
            string expectedDriver = UserAgentInfo.DriverName;
            string expectedVersion = ADP.GetAssemblyVersion().ToString();

            // Act: turn the bytes back into JSON and pull out the fields
            string json = Encoding.UTF8.GetString(payload);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string actualDriver = root.GetProperty(UserAgentInfoDto.DriverJsonKey).GetString()!;
            string actualVersion = root.GetProperty(UserAgentInfoDto.VersionJsonKey).GetString()!;

            // Assert: the driver and version in the payload match the expected values
            Assert.Equal(expectedDriver, actualDriver);
            Assert.Equal(expectedVersion, actualVersion);
        }

        // 2. TruncateOrDefault respects null, empty, fit, and overflow cases
        [Theory]
        [InlineData(null, 5, "Unknown")]        // null returns default
        [InlineData("", 5, "Unknown")]          // empty returns default
        [InlineData("abc", 5, "abc")]           // within limit unchanged
        [InlineData("abcde", 5, "abcde")]       // exact max chars
        [InlineData("abcdef", 5, "abcde")]      // overflow truncated
        public void TruncateOrDefault_Behaviour(string? input, int max, string expected)
        {
            string actual = UserAgentInfo.TruncateOrDefault(input!, max);
            Assert.Equal(expected, actual);
        }

        // 3. AdjustJsonPayloadSize drops low‑priority fields when required

        /// <summary>
        /// Verifies that AdjustJsonPayloadSize truncates the DTO’s JSON when it exceeds the maximum size.
        /// High-priority fields (Driver, Version) must remain, low-priority fields (Arch, Runtime) are removed,
        /// and within OS only the Type sub-field survives if the OS block remains.
        /// </summary>
        [Fact]
        public void AdjustJsonPayloadSize_StripsLowPriorityFields_When_PayloadTooLarge()
        {
            // Arrange: create a DTO whose serialized JSON is guaranteed to exceed the max size
            string huge = new string('x', 20_000);
            var dto = new UserAgentInfoDto
            {
                Driver = huge,
                Version = huge,
                OS = new UserAgentInfoDto.OsInfo
                {
                    Type = huge,
                    Details = huge
                },
                Arch = huge,
                Runtime = huge
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            // Capture the size before the helper mutates the DTO
            byte[] original = JsonSerializer.SerializeToUtf8Bytes(dto, options);
            Assert.True(original.Length > UserAgentInfo.JsonPayloadMaxBytes);

            // Act: apply the size-adjustment helper
            byte[] payload = UserAgentInfo.AdjustJsonPayloadSize(dto);

            // Assert: payload is smaller and not empty
            Assert.NotEmpty(payload);
            Assert.True(payload.Length < original.Length);

            // Structural checks using JsonDocument
            using JsonDocument doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;

            // High-priority fields must still be present(driver name and version)
            Assert.True(root.TryGetProperty(UserAgentInfoDto.DriverJsonKey, out _));
            Assert.True(root.TryGetProperty(UserAgentInfoDto.VersionJsonKey, out _));

            // Low-priority fields should have been removed(arch and runtime)
            Assert.False(root.TryGetProperty(UserAgentInfoDto.ArchJsonKey, out _));
            Assert.False(root.TryGetProperty(UserAgentInfoDto.RuntimeJsonKey, out _));

            // if OS block remains, only its Type sub-field may survive
            if (root.TryGetProperty(UserAgentInfoDto.OsJsonKey, out JsonElement os))
            {
                Assert.True(os.TryGetProperty(UserAgentInfoDto.OsInfo.TypeJsonKey, out _));
                Assert.False(os.TryGetProperty(UserAgentInfoDto.OsInfo.DetailsJsonKey, out _));
            }
        }

        // 4. DTO JSON contract - verify names and values(parameterized)

        /// <summary>
        /// Verifies that UserAgentInfoDto serializes according to its JSON contract:
        /// required fields always appear with correct values, optional fields
        /// and the nested OS object are only emitted when non-null,
        /// and all JSON property names match the defined constants.
        /// </summary>
        [Theory]
        [InlineData("d", "v", "t", "dd", "a", "r")]
        [InlineData("DeReaver", "1.2", "linux", "kernel", "", "")]
        [InlineData("LongDrv", "2.0", "win", null, null, null)]
        [InlineData("Driver", "Version", null, null, null, null)] // drop OsInfo entirely
        public void Dto_JsonPropertyNames_MatchConstants(
            string driver,
            string version,
            string? osType,
            string? osDetails,
            string? arch,
            string? runtime)
        {
            // Arrange: build the DTO, dropping the OS object if osType is null
            var dto = new UserAgentInfoDto
            {
                Driver = driver,
                Version = version,
                OS = osType == null
                            ? null
                            : new UserAgentInfoDto.OsInfo
                            {
                                Type = osType,
                                Details = string.IsNullOrEmpty(osDetails) ? null : osDetails
                            },
                Arch = string.IsNullOrEmpty(arch) ? null : arch,
                Runtime = string.IsNullOrEmpty(runtime) ? null : runtime
            };

            // Arrange: configure JSON serialization to omit nulls and use exact property names
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            // Act: serialize the DTO and parse it back into a JsonDocument
            string json = JsonSerializer.Serialize(dto, options);
            using var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // Assert: required properties always present with correct values
            Assert.Equal(driver, root.GetProperty(UserAgentInfoDto.DriverJsonKey).GetString());
            Assert.Equal(version, root.GetProperty(UserAgentInfoDto.VersionJsonKey).GetString());

            // Assert: Arch is only present if non-null
            if (dto.Arch == null)
            {
                Assert.False(root.TryGetProperty(UserAgentInfoDto.ArchJsonKey, out _));
            }
            else
            {
                Assert.Equal(dto.Arch, root.GetProperty(UserAgentInfoDto.ArchJsonKey).GetString());
            }

            // Assert: Runtime is only present if non-null
            if (dto.Runtime == null)
            {
                Assert.False(root.TryGetProperty(UserAgentInfoDto.RuntimeJsonKey, out _));
            }
            else
            {
                Assert.Equal(dto.Runtime, root.GetProperty(UserAgentInfoDto.RuntimeJsonKey).GetString());
            }

            // Assert: OS object may be omitted entirely
            if (dto.OS == null)
            {
                Assert.False(root.TryGetProperty(UserAgentInfoDto.OsJsonKey, out _));
            }
            else
            {
                JsonElement os = root.GetProperty(UserAgentInfoDto.OsJsonKey);

                // OS.Type must always be present when OS is not null
                Assert.Equal(dto.OS.Type, os.GetProperty(UserAgentInfoDto.OsInfo.TypeJsonKey).GetString());

                // OS.Details is optional
                if (dto.OS.Details == null)
                {
                    Assert.False(os.TryGetProperty(UserAgentInfoDto.OsInfo.DetailsJsonKey, out _));
                }
                else
                {
                    Assert.Equal(dto.OS.Details, os.GetProperty(UserAgentInfoDto.OsInfo.DetailsJsonKey).GetString());
                }
            }
        }

        // 5. End-to-end test that combines truncation, adjustment, and serialization
        [Fact]
        public void EndToEnd_Truncate_Adjust_Serialize_Works()
        {
            string raw = new string('x', 2_000);
            const int Max = 100;

            string driver = UserAgentInfo.TruncateOrDefault(raw, Max);
            string version = UserAgentInfo.TruncateOrDefault(raw, Max);
            string osType = UserAgentInfo.TruncateOrDefault(raw, Max);

            var dto = new UserAgentInfoDto
            {
                Driver = driver,
                Version = version,
                OS = new UserAgentInfoDto.OsInfo { Type = osType, Details = raw },
                Arch = raw,
                Runtime = raw
            };

            byte[] payload = UserAgentInfo.AdjustJsonPayloadSize(dto);
            string json = Encoding.UTF8.GetString(payload);

            using JsonDocument doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(driver, root.GetProperty(UserAgentInfoDto.DriverJsonKey).GetString());
            Assert.Equal(version, root.GetProperty(UserAgentInfoDto.VersionJsonKey).GetString());

            JsonElement os = root.GetProperty(UserAgentInfoDto.OsJsonKey);
            Assert.Equal(osType, os.GetProperty(UserAgentInfoDto.OsInfo.TypeJsonKey).GetString());
        }
    }
}
