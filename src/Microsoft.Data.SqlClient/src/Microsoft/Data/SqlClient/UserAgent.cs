// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <summary>
///   This class uses runtime environment information to produce a value
///   suitable for use in the TDS LOGIN7 USERAGENT Feature Extension
///   payload.
///   
///   See the spec here:
///
///   <see href="https://microsoft.sharepoint-df.com/:w:/t/sqldevx/IQASFk7dM5QsS66DR8mj5SmMASPyzUn3-osv8F2fMV-p3LU?e=FTph5o">
///       SQL Drivers User Agent V1
///   </see>
/// </summary>
internal static class UserAgent
{
    #region Properties
    
    /// <summary>
    ///   <para>
    ///     The pipe-delimited payload as a string, never null, never empty, and
    ///     never larger than 256 characters.
    ///   </para>
    ///   <para> 
    ///     The format is pipe ('|') delimited into 7 parts:
    ///
    ///     <code>1|MS-MDS|{Driver Version}|{Arch}|{OS Type}|{OS Info}|{Runtime Info}</code>
    ///   </para>
    ///   <para>
    ///     The <c>{Driver Version}</c> part is the version of the driver,
    ///     sourced from the MDS NuGet package version in SemVer 2.0 format.
    ///     Maximum length is 24 characters.
    ///   </para>
    ///   <para>
    ///     The <c>{Arch}</c> part will be the process architecture, either
    ///     the bare metal hardware architecture or the virtualized
    ///     architecture.  See
    ///     <see cref="RuntimeInformation.ProcessArchitecture">
    ///       ProcessArchitecture
    ///     </see>
    ///     for possible values.  Maximum length is 10 characters.
    ///   </para>
    ///   <para>
    ///     The <c>{OS Type}</c> part will be one of the following strings:
    ///     <list type="bullet">
    ///       <item><description>Windows</description></item>
    ///       <item><description>Linux</description></item>
    ///       <item><description>macOS</description></item>
    ///       <item><description>FreeBSD</description></item>
    ///       <item><description>Unknown</description></item>
    ///     </list>
    ///   </para>
    ///   <para>
    ///     The <c>{OS Info}</c> part will be sourced from the
    ///     <see cref="RuntimeInformation.OSDescription">
    ///       OSDescription
    ///     </see> 
    ///     value, or "Unknown" if that value is empty or all whitespace.
    ///     Maximum length is 44 characters.
    ///   </para>
    ///   <para>
    ///     The <c>{Runtime Info}</c> part will be sourced from the
    ///     <see cref="RuntimeInformation.FrameworkDescription">
    ///       FrameworkDescription
    ///    </see>
    ///     value, or "Unknown" if that value is empty or all whitespace.
    ///     Maximum length is 44 characters.
    ///   </para>
    ///   <para>
    ///     Any characters from the sourced values that are not one of the
    ///     following are replaced with underscore ('_'):
    ///     <list type="bullet">
    ///       <item>
    ///         <description>ASCII letters ([A-za-z])</description>
    ///       </item>
    ///       <item><description>ASCII digits ([0-9])</description></item>
    ///       <item><description>Space (' ')</description></item>
    ///       <item><description>Period ('.')</description></item>
    ///       <item><description>Plus ('+')</description></item>
    ///       <item><description>Underscore ('_')</description></item>
    ///       <item><description>Hyphen ('-')</description></item>
    ///     </list>
    ///   </para>
    ///   <para>
    ///     All known exceptions are caught and handled by injecting the
    ///     fallback value of "Unknown".  However, no effort is made to
    ///     catch all exceptions, for example process-fatal memory
    ///     allocation errors.
    ///   </para>
    /// </summary>
    internal static string Value { get; }

    /// <summary>
    /// The Value as UCS-2 encoded bytes.
    /// </summary>
    internal static ReadOnlyMemory<byte> Ucs2Bytes { get; }

    #endregion Properties
    
    #region Helpers

    /// <summary>
    ///   Static construction builds the Client Interface Name.  All known
    ///   exceptions are consumed.
    /// </summary>
    static UserAgent()
    {
        // Determine the OS type.
        //
        // This is done outside of Build() to allow tests to inject
        // specific values.
        //
        string osType = Unknown;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            osType = Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            osType = Linux;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            osType = macOS;
        }
        // The FreeBSD platform doesn't exist in .NET Framework at all.
        #if NET
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            osType = FreeBSD;
        }
        #endif

        // Build it!
        Value = Build(
            MaxLenOverall,
            PayloadVersion,
            DriverName,
            System.ThisAssembly.NuGetPackageVersion,
            RuntimeInformation.ProcessArchitecture,
            osType,
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription);
        
        // Convert it to UCS-2 bytes.
        //
        // The default Unicode instance doesn't throw if encoding fails, so
        // there is nothing to catch here.
        Ucs2Bytes = Encoding.Unicode.GetBytes(Value);
    }

    /// <summary>
    ///   <para>Build the payload string value and return it.</para>
    ///   <para>
    ///     The length of the returned value will never be longer than
    ///     <paramref name="maxLen"/>.
    ///   </para>
    ///   <para>All known exceptions are consumed.</para>
    /// </summary>
    /// <param name="maxLen">
    ///   The maximum length of the returned value.
    /// </param>
    /// <param name="payloadVersion">
    ///   The value of the payload version part. 
    /// </param>
    /// <param name="driverName">
    ///   The value of the driver name part. 
    /// </param>
    /// <param name="driverVersion">
    ///   The value of the driver version part. 
    /// </param>
    /// <param name="arch">
    ///   The value of the Architecture part.
    /// </param>
    /// <param name="osType">
    ///   The value of the OS Type part. 
    /// </param>
    /// <param name="osInfo">
    ///   The value of the OS Info part. 
    /// </param>
    /// <param name="runtimeInfo">
    ///   The value of the Runtime Info part. 
    /// </param>
    /// <returns>
    ///   The payload string value, never null, never empty, and never longer
    ///   than <paramref name="maxLen"/>.
    /// </returns>
    internal static string Build(
        ushort maxLen,
        string payloadVersion,
        string driverName,
        string driverVersion,
        Architecture arch,
        string osType,
        string osInfo,
        string runtimeInfo)
    {
        string result;

        // Clean and truncate the payload version and driver name.  We will need
        // them for error handling.
        payloadVersion = Truncate(Clean(payloadVersion), MaxLenPayloadVersion);
        driverName = Truncate(Clean(driverName), MaxLenDriverName);

        try
        {
            // Expect to build a string whose length is up to our max length.
            //
            // This isn't a max capacity, but a hint for initial buffer
            // allocation.  We will truncate to our max length after all of the
            // pieces have been appended.
            //
            StringBuilder name = new StringBuilder(maxLen);

            // Start with the (clean) payload version and driver name.
            name.Append(payloadVersion);
            name.Append('|');
            name.Append(driverName);
            name.Append('|');

            // Add the Driver Version, truncating to its max length.
            name.Append(Truncate(Clean(driverVersion), MaxLenDriverVersion));
            name.Append('|');

            // Add the Architecture, truncating to its max length.
            name.Append(Truncate(Clean(arch.ToString()), MaxLenArch));
            name.Append('|');

            // Add the OS Type, truncating to its max length.
            name.Append(Truncate(Clean(osType), MaxLenOsType));
            name.Append('|');
            
            // Add the OS Info, truncating to its max length.
            name.Append(Truncate(Clean(osInfo), MaxLenOsInfo));
            name.Append('|');
            
            // Add the Runtime Info, truncating to its max length.
            name.Append(Truncate(Clean(runtimeInfo), MaxLenRuntimeInfo));

            // Remember the name we've built up.
            result = name.ToString();
        }
        catch (ArgumentOutOfRangeException)
        {
            // StringBuilder failed in an unexpected way, so use our fallback
            // value.
            result =
                $"{payloadVersion}|{driverName}|{Unknown}|{Unknown}|" +
                $"{Unknown}|{Unknown}|{Unknown}";
        }

        // Truncate to our max length if necessary.
        //
        // This is a paranoia check to ensure we don't violate our API
        // promise.
        //
        if (result.Length > maxLen)
        {
            // We know this won't throw ArgumentOutOfRangeException because
            // we've already confirmed that Length is greater than maxLen.
            result = result.Substring(0, maxLen);
        }

        return result;
    }

    /// <summary>
    ///   <para>
    ///     Clean the given value of any disallowed characters, replacing them
    ///     with underscore ('_'), and return the cleaned value.
    ///   </para>
    ///   <para>Leading and trailing whitespace are removed.</para>
    ///   <para>
    ///     Each disallowed character is replaced with an underscore, preserving
    ///     the original length of the value.  No effort is made to collapse
    ///     adjacent disallowed characters.
    ///   </para>
    ///   <para>
    ///     Permitted characters are:
    ///     <list type="bullet">
    ///       <item>
    ///         <description>ASCII letters ([A-za-z])</description>
    ///       </item>
    ///       <item><description>ASCII digits ([0-9])</description></item>
    ///       <item><description>Space (' ')</description></item>
    ///       <item><description>Period ('.')</description></item>
    ///       <item><description>Plus ('+')</description></item>
    ///       <item><description>Underscore ('_')</description></item>
    ///       <item><description>Hyphen ('-')</description></item>
    ///     </list>
    ///   </para>
    ///   <para>
    ///     If the given value is null, empty, or all whitespace, or an error
    ///     occurs, the fallback value is returned.
    ///   </para>
    /// </summary>
    /// <param name="value">The value to clean.</param>
    /// <returns>
    ///   The cleaned value, or the fallback value if any errors occur.
    /// </returns>
    internal static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            // .NET Framework doesn't consider IsNullOrWhiteSpace()
            // sufficient for nullable checks, so add an explicit check for
            // null.
            #if NETFRAMEWORK
            || value == null
            #endif
            )
        {
            return Unknown;
        }
        
        // Remove any leading and trailing whitespace.
        value = value.Trim();

        try
        {
            // Build the cleaned value by hand, avoiding the overhead and
            // failure scenarios of regexes or other more complex solutions.
            //
            // We expect the value to be short, and this code is called only a
            // few times per process.  Robustness and simplicity are more
            // important than performance here.
            //
            StringBuilder cleaned = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                // Is it a permitted character?
                if (
                    #if NET
                    char.IsAsciiLetter(c)
                    || char.IsAsciiDigit(c)
                    #else
                    (c >= 'A' && c <= 'Z')
                    || (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    #endif 
                    || c == ' '
                    || c == '.'
                    || c == '+'
                    || c == '_'
                    || c == '-')
                {
                    // Yes, so append it as-is.
                    cleaned.Append(c);
                }
                else
                {
                    // No, so replace it with an underscore.
                    cleaned.Append('_');
                }
            }

            return cleaned.ToString();
        }
        catch (ArgumentOutOfRangeException)
        {
            // StringBuilder failed in an unexpected way, so use our fallback
            // value.
            return Unknown;
        }
    }

    /// <summary>
    ///   Truncate the given value to the given max length, and return the
    ///   result.
    /// </summary>
    /// <param name="value">The value to truncate.</param>
    /// <param name="maxLength">The maximum length to truncate to.</param>
    /// <returns>
    ///   The truncated value, or the original <paramref name="value"/> if no
    ///   truncation occurred.
    /// </returns>
    internal static string Truncate(string value, ushort maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        // We know this won't throw ArgumentOutOfRangeException because we've
        // already confirmed that Length is greater than maxLength.
        return value.Substring(0, maxLength);
    }

    #endregion Helpers

    #region Private Fields

    // Our payload format version.
    private const string PayloadVersion = "1";

    // Our well-known .NET driver name.
    private const string DriverName = "MS-MDS";

    // The overall maximum length of Value.
    private const ushort MaxLenOverall = 256;
    
    // Maximum part lengths as promised in our API.
    private const ushort MaxLenPayloadVersion = 2;
    private const ushort MaxLenDriverName = 12;
    private const ushort MaxLenDriverVersion = 24;
    private const ushort MaxLenArch = 10;
    private const ushort MaxLenOsType = 10;
    private const ushort MaxLenOsInfo = 44;
    private const ushort MaxLenRuntimeInfo = 44;

    // The OS Type values we promise in our API.
    private const string Windows = "Windows";
    private const string Linux = "Linux";
    private const string macOS = "macOS";
    // The FreeBSD platform doesn't exist in .NET Framework at all.
    #if NET
    private const string FreeBSD = "FreeBSD";
    #endif

    // A fallback value for parts of the client interface name that are
    // unknown, invalid, or when errors occur.
    private const string Unknown = "Unknown";

    #endregion Private Fields
}
