using System;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient.UserAgent;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="UserAgentInfo"/> and its companion DTO.
    /// Focus areas:
    ///   1. Field truncation logic
    ///   2. Payload sizing and field‑dropping policy
    ///   3. DTO JSON contract (key names)
    ///   4. Cached payload invariants
    /// </summary>
    public class UserAgentInfoTests
    {
        // 1. Cached payload is within the 2,047‑byte spec and never null
        [Fact]
        public void CachedPayload_IsNotNull_And_WithinSpecLimit()
        {
            var field = typeof(UserAgentInfo).GetField(
                    name: "_cachedPayload",
                    bindingAttr: BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);

            byte[] payload = (byte[])field!.GetValue(null)!;
            Assert.NotNull(payload);
            Assert.InRange(payload.Length, 1, UserAgentInfo.JsonPayloadMaxBytesSpec);
        }

        // 2. TruncateOrDefault respects null, empty, fit, and overflow cases
        [Theory]
        [InlineData(null, 5, "Unknown")]              // null returns default
        [InlineData("", 5, "Unknown")]                // empty returns default
        [InlineData("abc", 5, "abc")]                // within limit unchanged
        [InlineData("abcdef", 5, "abcde")]          // overflow truncated
        public void TruncateOrDefault_Behaviour(string? input, int max, string expected)
        {
            var mi = typeof(UserAgentInfo).GetMethod(
                name: "TruncateOrDefault",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);

            string actual = (string)mi!.Invoke(null, new object?[] { input, max })!;
            Assert.Equal(expected, actual);
        }

        // 3. AdjustJsonPayloadSize drops low‑priority fields when required
        [Fact]
        public void AdjustJsonPayloadSize_StripsLowPriorityFields_When_PayloadTooLarge()
        {
            // Build an inflated DTO so the raw JSON exceeds 10 KB.
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

            var mi = typeof(UserAgentInfo).GetMethod(
                name: "AdjustJsonPayloadSize",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);

            byte[] payload = (byte[])mi!.Invoke(null, new object?[] { dto })!;

            // Final payload must satisfy limits
            Assert.InRange(payload.Length, 1, UserAgentInfo.UserAgentPayloadMaxBytes);

            // Convert to string for field presence checks
            string json = Encoding.UTF8.GetString(payload);

            // We either receive the minimal payload with only high‑priority fields,
            // or we receive an empty payload in case of overflow despite dropping fields.
            if (payload.Length <= 2)
            {
                Assert.Equal("{}", json.Trim());
                return;
            }

            // High‑priority fields remain
            Assert.Contains(UserAgentInfoDto.DriverJsonKey, json);
            Assert.Contains(UserAgentInfoDto.VersionJsonKey, json);
            Assert.Contains(UserAgentInfoDto.OsJsonKey, json);

            // Low‑priority fields removed
            Assert.DoesNotContain(UserAgentInfoDto.ArchJsonKey, json);
            Assert.DoesNotContain(UserAgentInfoDto.RuntimeJsonKey, json);
            Assert.DoesNotContain(UserAgentInfoDto.OsInfo.DetailsJsonKey, json);
        }

        // 4. DTO serializes with expected JSON property names
        [Fact]
        public void Dto_JsonPropertyNames_MatchConstants()
        {
            var dto = new UserAgentInfoDto
            {
                Driver = "d",
                Version = "v",
                OS = new UserAgentInfoDto.OsInfo { Type = "t", Details = "dd" },
                Arch = "a",
                Runtime = "r"
            };

            string json = JsonSerializer.Serialize(dto);
            using JsonDocument doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty(UserAgentInfoDto.DriverJsonKey, out _));
            Assert.True(root.TryGetProperty(UserAgentInfoDto.VersionJsonKey, out _));
            Assert.True(root.TryGetProperty(UserAgentInfoDto.OsJsonKey, out var osElement));
            Assert.True(osElement.TryGetProperty(UserAgentInfoDto.OsInfo.TypeJsonKey, out _));
            Assert.True(osElement.TryGetProperty(UserAgentInfoDto.OsInfo.DetailsJsonKey, out _));
            Assert.True(root.TryGetProperty(UserAgentInfoDto.ArchJsonKey, out _));
            Assert.True(root.TryGetProperty(UserAgentInfoDto.RuntimeJsonKey, out _));
        }
    }
}
