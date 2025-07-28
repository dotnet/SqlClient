using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient.UserAgent;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="UserAgentInfo"/> and its companion DTO.
    /// Focus areas:
    ///   1. Field truncation logic
    ///   2. Truncation verification
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
            byte[] payload = UserAgentInfo._cachedPayload;
            Assert.NotNull(payload);
            Assert.InRange(payload.Length, 1, UserAgentInfo.JsonPayloadMaxBytes);
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
        [Fact]
        public void AdjustJsonPayloadSize_StripsLowPriorityFields_When_PayloadTooLarge()
        {
            // Build an inflated DTO so AdjustJsonPayloadSize
            // must fall back to its “drop fields” logic.
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

            // Run the field‑dropping logic
            byte[] payload = UserAgentInfo.AdjustJsonPayloadSize(dto);
            Assert.NotEmpty(payload);
            Assert.True(payload.Length < original.Length);   // verify shrinkage

            // Structural checks using JsonDocument
            using JsonDocument doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;

            // High‑priority fields must survive.
            Assert.True(root.TryGetProperty(UserAgentInfoDto.DriverJsonKey, out _));
            Assert.True(root.TryGetProperty(UserAgentInfoDto.VersionJsonKey, out _));

            // Low‑priority fields must be gone.
            Assert.False(root.TryGetProperty(UserAgentInfoDto.ArchJsonKey, out _));
            Assert.False(root.TryGetProperty(UserAgentInfoDto.RuntimeJsonKey, out _));

            // If the "os" object survived, only its "type" sub‑field may remain.
            if (root.TryGetProperty(UserAgentInfoDto.OsJsonKey, out JsonElement os))
            {
                Assert.True(os.TryGetProperty(UserAgentInfoDto.OsInfo.TypeJsonKey, out _));
                Assert.False(os.TryGetProperty(UserAgentInfoDto.OsInfo.DetailsJsonKey, out _));
            }
        }

        // 4. DTO JSON contract - verify names and values(parameterized)
        [Theory]
        [InlineData("d", "v", "t", "dd", "a", "r")]
        [InlineData("DeReaver", "1.2", "linux", "kernel", "", "")]
        [InlineData("LongDrv", "2.0", "win", null, null, null)]
        [InlineData("Driver", "Version", null, null, null, null)] // all optional fields null
        public void Dto_JsonPropertyNames_MatchConstants(
            string driver,
            string version,
            string? osType,
            string? osDetails,
            string? arch,
            string? runtime)
        {
            var dto = new UserAgentInfoDto
            {
                Driver = driver,
                Version = version,
                OS = new UserAgentInfoDto.OsInfo
                {
                    Type = osType,
                    Details = string.IsNullOrEmpty(osDetails) ? null : osDetails
                },
                Arch = string.IsNullOrEmpty(arch) ? null : arch,
                Runtime = string.IsNullOrEmpty(runtime) ? null : runtime
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            string json = JsonSerializer.Serialize(dto, options);
            JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // always expected
            Assert.Equal(driver,
                root.GetProperty(UserAgentInfoDto.DriverJsonKey).GetString());
            Assert.Equal(version,
                root.GetProperty(UserAgentInfoDto.VersionJsonKey).GetString());

            // optional Arch
            if (dto.Arch is null)
            {
                Assert.False(root.TryGetProperty(UserAgentInfoDto.ArchJsonKey, out _));
            }
            else
            {
                Assert.Equal(dto.Arch,
                    root.GetProperty(UserAgentInfoDto.ArchJsonKey).GetString());
            }

            // optional Runtime
            if (dto.Runtime is null)
            {
                Assert.False(root.TryGetProperty(UserAgentInfoDto.RuntimeJsonKey, out _));
            }
            else
            {
                Assert.Equal(dto.Runtime,
                    root.GetProperty(UserAgentInfoDto.RuntimeJsonKey).GetString());
            }

            // nested OS object
            JsonElement os = root.GetProperty(UserAgentInfoDto.OsJsonKey);

            if (dto.OS!.Type is null)
            {
                Assert.False(os.TryGetProperty(UserAgentInfoDto.OsInfo.TypeJsonKey, out _));
            }
            else
            {
                Assert.Equal(dto.OS.Type,
                    os.GetProperty(UserAgentInfoDto.OsInfo.TypeJsonKey).GetString());
            }

            if (dto.OS!.Details is null)
            {
                Assert.False(os.TryGetProperty(UserAgentInfoDto.OsInfo.DetailsJsonKey, out _));
            }
            else
            {
                Assert.Equal(dto.OS.Details,
                    os.GetProperty(UserAgentInfoDto.OsInfo.DetailsJsonKey).GetString());
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
