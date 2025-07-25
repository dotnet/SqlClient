using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Common;

#nullable enable

#if WINDOWS
using System.Management;
#endif

namespace Microsoft.Data.SqlClient.UserAgent;

/// <summary>
/// Gathers driver + environment info, enforces size constraints,
/// and serializes into a UTF-8 JSON payload.
/// The spec document can be found at: https://microsoft.sharepoint-df.com/:w:/t/sqldevx/ERIWTt0zlCxLroNHyaPlKYwBI_LNSff6iy_wXZ8xX6nctQ?e=0hTJX7
/// </summary>
internal static class UserAgentInfo
{
    /// <summary>
    /// Maximum number of characters allowed for the driver name.
    /// </summary>
    private const int DriverNameMaxChars = 16;

    /// <summary>
    /// Maximum number of characters allowed for the driver version.
    /// </summary>
    private const int VersionMaxChars = 16;

    /// <summary>
    /// Maximum number of characters allowed for the operating system type.
    /// </summary>
    private const int OsTypeMaxChars = 16;

    /// <summary>
    /// Maximum number of characters allowed for the operating system details.
    /// </summary>  
    private const int OsDetailsMaxChars = 128;

    /// <summary>
    /// Maximum number of characters allowed for the system architecture.
    /// </summary>
    private const int ArchMaxChars = 16;

    /// <summary>
    /// Maximum number of characters allowed for the driver runtime.
    /// </summary>
    private const int RuntimeMaxChars = 128;

    /// <summary>
    /// Maximum number of bytes allowed for the user agent json payload.
    /// Payloads larger than this may be rejected by the server.
    /// </summary>
    public const int JsonPayloadMaxBytes = 2047;

    private const string DefaultJsonValue = "Unknown";
    private const string DriverName = "MS-MDS";

    private static readonly UserAgentInfoDto _dto;
    public static readonly byte[] _cachedPayload;

    private enum OsType
    {
        Windows,
        Linux,
        macOS,
        FreeBSD,
        Android,
        Unknown
    }

    static UserAgentInfo()
    {
        _dto = BuildDto();
        _cachedPayload = AdjustJsonPayloadSize(_dto);
    }

    static UserAgentInfoDto BuildDto()
    {
        // Note: We serialize 6 fields in total:
        // - 4 fields with up to 16 characters each
        // - 2 fields with up to 128 characters each
        //
        // For estimating **on-the-wire UTF-8 size** of the serialized JSON:
        // 1) For the 4 fields of 16 characters:
        //    - In worst case (all characters require escaping in JSON, e.g., quotes, backslashes, control chars),
        //      each character may expand to 2–6 bytes in the JSON string (e.g., \" = 2 bytes, \uXXXX = 6 bytes)
        //    - Assuming full escape with \uXXXX form (6 bytes per char): 4 × 16 × 6 = 384 bytes (extreme worst case)
        //    - For unescaped high-plane Unicode (e.g., emojis), UTF-8 uses up to 4 bytes per character:
        //      4 × 16 × 4 = 256 bytes (UTF-8 max)
        //
        //    Conservative max estimate for these fields = **384 bytes**
        //
        // 2) For the 2 fields of 128 characters:
        //    - Worst-case with \uXXXX escape sequences: 2 × 128 × 6 = 1,536 bytes
        //    - Worst-case with high Unicode: 2 × 128 × 4 = 1,024 bytes
        //
        //    Conservative max estimate for these fields = **1,536 bytes**
        //
        // Combined worst-case for value content = 384 + 1536 = **1,920 bytes**
        //
        // 3) The rest of the serialized JSON payload (object braces, field names, quotes, colons, commas) is fixed.
        //    Based on measurements, it typically adds to about **81 bytes**.
        //
        // Final worst-case estimate for total payload on the wire (UTF-8 encoded):  
        //    1,920 + 81 = **2,001 bytes**
        //
        // This is still below our spec limit of 2,047 bytes.
        //
        // TDS Prelogin7 packets support up to 65,535 bytes (including headers), but many server versions impose
        // stricter limits for prelogin payloads.
        //
        // As a safety measure:
        // - If the serialized payload exceeds **10 KB**, we fallback to transmitting only essential fields:
        //   'driver', 'version', and 'os.type'
        // - If the payload exceeds 2,047 bytes but remains within sensible limits, we still send it, but note that
        //   some servers may silently drop or reject such packets — behavior we may use for future probing or diagnostics.
        // - If payload exceeds 10KB even after dropping fields , we send an empty payload.
        var driverName = TruncateOrDefault(DriverName, DriverNameMaxChars);
        var version = TruncateOrDefault(ADP.GetAssemblyVersion().ToString(), VersionMaxChars);
        var osType = TruncateOrDefault(DetectOsType().ToString(), OsTypeMaxChars);
        var osDetails = TruncateOrDefault(DetectOsDetails(), OsDetailsMaxChars);
        var architecture = TruncateOrDefault(DetectArchitecture(), ArchMaxChars);
        var runtime = TruncateOrDefault(DetectRuntime(), RuntimeMaxChars);

        // Instantiate DTO before serializing
        return new UserAgentInfoDto
        {
            Driver = driverName,
            Version = version,
            OS = new UserAgentInfoDto.OsInfo
            { 
                Type = osType,
                Details = osDetails
            },
            Arch = architecture,
            Runtime = runtime

        };

    }

    /// <summary>
    /// This function returns the appropriately sized json payload 
    /// We check the size of encoded json payload, if it is within limits we return the dto to be cached
    /// other wise we drop some fields to reduce the size of the payload.
    /// </summary>
    /// <param name="dto"> Data Transfer Object for the json payload </param>
    /// <returns>Serialized UTF-8 encoded json payload version of DTO within size limit</returns>
    internal static byte[] AdjustJsonPayloadSize(UserAgentInfoDto dto)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(dto, options);

        // We try to send the payload if it is within the limits.
        // Otherwise we drop some fields to reduce the size of the payload and try one last time
        // Note: server will reject payloads larger than 2047 bytes
        // Try if the payload fits the max allowed bytes
        if (payload.Length <= JsonPayloadMaxBytes)
        { 
            return payload;
        }

        dto.Runtime = null; // drop Runtime
        dto.Arch = null; // drop Arch
        if (dto.OS != null)
        {
            dto.OS.Details = null; // drop OS.Details
        }

        payload = JsonSerializer.SerializeToUtf8Bytes(dto, options);
        if (payload.Length <= JsonPayloadMaxBytes)
        {
            return payload;
        }
            
        dto.OS = null; // drop OS entirely
        // Last attempt to send minimal payload driver + version only
        return JsonSerializer.SerializeToUtf8Bytes(dto, options);
    }

    /// <summary>
    /// Truncates a string to the specified maximum length or returns a default value if input is null or empty.
    /// </summary>
    /// <param name="jsonStringVal">The string value to truncate</param>
    /// <param name="maxChars">Maximum number of characters allowed</param>
    /// <returns>Truncated string or default value if input is invalid</returns>
    internal static string TruncateOrDefault(string jsonStringVal, int maxChars)
    {
        try
        {
            if (string.IsNullOrEmpty(jsonStringVal))
            {
                return DefaultJsonValue;
            }

            if (jsonStringVal.Length <= maxChars)
            {
                return jsonStringVal;
            }

            return jsonStringVal.Substring(0, maxChars);
        }
        catch
        {
            // Silently consume all exceptions
            return DefaultJsonValue;
        }
    }

    /// <summary>
    /// Detects the OS platform and returns the matching OsType enum.
    /// </summary>
    private static OsType DetectOsType()
    {
        try
        {
            // first we try with built-in checks (Android and FreeBSD also report Linux so they are checked first)
#if NET6_0_OR_GREATER
            if (OperatingSystem.IsAndroid())  return OsType.Android;
            if (OperatingSystem.IsFreeBSD())  return OsType.FreeBSD;
            if (OperatingSystem.IsWindows()) return OsType.Windows;
            if (OperatingSystem.IsLinux())   return OsType.Linux;
            if (OperatingSystem.IsMacOS())   return OsType.macOS;
#endif
            // second we fallback to OSPlatform checks
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
            if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                return OsType.FreeBSD;
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")))
                return OsType.FreeBSD;
#endif
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OsType.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OsType.Linux;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OsType.macOS;

            // Final fallback is inspecting OSDecription
            // Note: This is not based on any formal specification,
            // that is why we use it as a last resort.
            // The string values are based on trial and error.
            var desc = RuntimeInformation.OSDescription?.ToLowerInvariant() ?? "";
            if (desc.Contains("android"))
                return OsType.Android;
            if (desc.Contains("freebsd"))
                return OsType.FreeBSD;
            if (desc.Contains("windows"))
                return OsType.Windows;
            if (desc.Contains("linux"))
                return OsType.Linux;
            if (desc.Contains("darwin") || desc.Contains("mac os"))
                return OsType.macOS;
        }
        catch
        {
            // swallow any unexpected errors
        }

        return OsType.Unknown;
    }

    /// <summary>
    /// Retrieves the operating system details based on RuntimeInformation.
    /// </summary>
    private static string DetectOsDetails()
    {
        var osDetails = RuntimeInformation.OSDescription;
        if (!string.IsNullOrWhiteSpace(osDetails))
        {
            return osDetails;
        }
        
        return DefaultJsonValue;
    }

    /// <summary>
    /// Detects and reports whatever CPU architecture the guest OS exposes
    /// </summary>
    private static string DetectArchitecture()
    {
        try
        {
            // Returns the architecture of the current process (e.g., "X86", "X64", "Arm", "Arm64").
            // Note: This reflects the architecture of the running process, not the physical host system.
            return RuntimeInformation.ProcessArchitecture.ToString();
        }
        catch
        {
            // In case RuntimeInformation isn’t available or something unexpected happens
        }
        return DefaultJsonValue;
    }

    /// <summary>
    /// Returns the framework description as a string.
    /// </summary>
    private static string DetectRuntime()
    {
        // FrameworkDescription is never null, but IsNullOrWhiteSpace covers it anyway
        var desc = RuntimeInformation.FrameworkDescription;
        if (string.IsNullOrWhiteSpace(desc))
            return DefaultJsonValue;

        // at this point, desc is non‑null, non‑empty (after trimming)
        return desc.Trim();
    }
}

