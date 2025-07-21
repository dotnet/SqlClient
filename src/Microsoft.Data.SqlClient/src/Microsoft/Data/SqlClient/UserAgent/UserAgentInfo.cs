using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.Common;

#if WINDOWS
using System.Management;
#endif

namespace Microsoft.Data.SqlClient.UserAgent
{
    /// <summary>
    /// Gathers driver + environment info, enforces size constraints,
    /// and serializes into a UTF-8 JSON payload.
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
        /// payloads larger than this may be rejected by the server.
        /// </summary>
        public const int JsonPayloadMaxBytesSpec = 2047;

        /// <summary>
        /// Maximum number of bytes allowed before we drop multiple fields 
        /// and only send bare minimum useragent info.
        /// </summary>
        public const int UserAgentPayloadMaxBytes = 10000;


        private const string DefaultJsonValue = "Unknown";
        private const string DefaultDriverName = "MS-MDS";

        // JSON Payload for UserAgent
        private static readonly string driverName;
        private static readonly string version;
        private static readonly string osType;
        private static readonly string osDetails;
        private static readonly string architecture;
        private static readonly string runtime;
        private static readonly byte[] _cachedPayload;

        private enum OsType
        {
            Windows,
            Linux,
            macOS,
            FreeBSD,
            Android,
            Unknown
        }

        // P/Invoke signature for glibc detection
        [DllImport("libc", EntryPoint = "gnu_get_libc_version", CallingConvention = CallingConvention.Cdecl)]
        private static extern nint gnu_get_libc_version();

        static UserAgentInfo()
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
            driverName = TruncateOrDefault(DefaultDriverName, DriverNameMaxChars);
            version = TruncateOrDefault(ADP.GetAssemblyVersion().ToString(), VersionMaxChars);
            var osVal = DetectOsType();
            osType = TruncateOrDefault(osVal.ToString(), OsTypeMaxChars);
            osDetails = TruncateOrDefault(DetectOsDetails(osVal), OsDetailsMaxChars);
            architecture = TruncateOrDefault(DetectArchitecture(), ArchMaxChars);
            runtime = TruncateOrDefault(DetectRuntime(), RuntimeMaxChars);

            // Instantiate DTO before serializing
            var dto = new UserAgentInfoDto
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

            // Check/Adjust payload before caching it
            _cachedPayload = AdjustJsonPayloadSize(dto);
        }

        /// <summary>
        /// This function returns the appropriately sized json payload 
        /// We check the size of encoded json payload, if it is within limits we return the dto to be cached
        /// other wise we drop some fields to reduce the size of the payload.
        /// </summary>
        /// <param name="dto"> Data Transfer Object for the json payload </param>
        /// <returns>Serialized UTF-8 encoded json payload version of DTO within size limit</returns>
        private static byte[] AdjustJsonPayloadSize(UserAgentInfoDto dto)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(dto, options);

            // Note: server will likely reject payloads larger than 2047 bytes
            // Try if the payload fits the max allowed bytes
            if (payload.Length <= JsonPayloadMaxBytesSpec)
            { 
                return payload;
            }
            if (payload.Length > UserAgentPayloadMaxBytes)
            {
                // If the payload is over 10KB, we only send the bare minimum fields
                dto.OS.Details = null; // drop OS.Details
                dto.Runtime = null; // drop Runtime
                dto.Arch = null; // drop Arch
                payload = JsonSerializer.SerializeToUtf8Bytes(dto, options);
            }

            // Last check to ensure we are within the limits(in case remaining fields are still too large)
            return payload.Length > UserAgentPayloadMaxBytes
                ? JsonSerializer.SerializeToUtf8Bytes(new { }, options)
                : payload;

        }

        /// <summary>
        /// Truncates a string to the specified maximum length or returns a default value if input is null or empty.
        /// </summary>
        /// <param name="jsonStringVal">The string value to truncate</param>
        /// <param name="maxChars">Maximum number of characters allowed</param>
        /// <returns>Truncated string or default value if input is invalid</returns>
        private static string TruncateOrDefault(string jsonStringVal, int maxChars)
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
                // second we fallback to OSplatform checks
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return OsType.Windows;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return OsType.Linux;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return OsType.macOS;

                // final fallback is inspecting OSdecription
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
        /// Given an OsType enum, returns the edition/distro string.
        /// passing the enum makes search less expensive
        /// </summary>
        private static string DetectOsDetails(OsType os)
        {
            try
            {
                switch (os)
                {
                    case OsType.Windows:
#if WINDOWS
                    // WMI query for “Caption”
                    // https://learn.microsoft.com/en-us/windows/win32/wmisdk/about-wmi
                    using var searcher = 
                        new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
                    foreach (var o in searcher.Get())
                    {
                        var caption = o["Caption"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(caption))
                            return caption;
                    }
#endif
                        break;

                    case OsType.Linux:
                        const string file = "/etc/os-release";
                        if (File.Exists(file))
                        {
                            foreach (var line in File.ReadAllLines(file))
                            {
                                if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                                {
                                    var parts = line.Split('=');
                                    if (parts.Length >= 2)
                                    {
                                        return parts[1].Trim().Trim('"');
                                    }
                                }
                            }
                        }
                        break;

                    case OsType.macOS:
                        return "macOS " + RuntimeInformation.OSDescription;

                        // FreeBSD, Android, Unknown fall through
                }

                // fallback for FreeBSD, Android, Unknown or if above branches fail
                var fallback = RuntimeInformation.OSDescription;
                if (!string.IsNullOrWhiteSpace(fallback))
                    return fallback;
            }
            catch
            {
                // swallow all exceptions
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
                // Returns “X86”, “X64”, “Arm”, “Arm64”, etc.
                // This is the architecture of the guest process it's running in
                // it does not see through to the physical host. 
                return RuntimeInformation.ProcessArchitecture.ToString();
            }
            catch
            {
                // In case RuntimeInformation isn’t available or something unexpected happens
            }
            return DefaultJsonValue;
        }

        /// <summary>
        /// Reads the Microsoft.Data.SqlClient assembly’s informational version
        /// or falls back to its AssemblyName.Version.
        /// </summary>
        private static string DetectRuntime()
        {
            // 1) Try the built-in .NET runtime description
            try
            {
                string fw = RuntimeInformation.FrameworkDescription;
                if (!string.IsNullOrWhiteSpace(fw))
                    return fw.Trim();
            }
            catch
            {
                // ignore and fall back
            }

            // 2) On Linux, ask glibc what version it is
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    // P/Invoke into libc
                    nint ptr = gnu_get_libc_version();
                    string glibc = Marshal.PtrToStringAnsi(ptr);
                    if (!string.IsNullOrWhiteSpace(glibc))
                        return "glibc " + glibc.Trim();
                }
                catch
                {
                    // ignore
                }
            }

            // 3) If running under Mono, grab its internal display name
            try
            {
                var mono = Type.GetType("Mono.Runtime");
                if (mono != null)
                {
                    // Mono.Runtime.GetDisplayName() is a private static method
                    var mi = mono.GetMethod(
                        "GetDisplayName",
                        BindingFlags.NonPublic | BindingFlags.Static
                    );
                    if (mi != null)
                    {
                        string name = mi.Invoke(null, null) as string;
                        if (!string.IsNullOrWhiteSpace(name))
                            return name.Trim();
                    }
                }
            }
            catch
            {
                // ignore
            }

            // 4) Nothing matched, give up
            return DefaultJsonValue;
        }

    }
}

