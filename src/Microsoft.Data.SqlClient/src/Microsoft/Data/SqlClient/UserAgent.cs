// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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
    ///     The format is pipe ('|') delimited into 9 parts:
    ///
    ///     <code>2|MS-MDS|{Driver Version}|{Arch}|{OS Type}|{OS Info}|{Runtime Info}|{App Id}|{Driver Properties}</code>
    ///   </para>
    ///   <para>
    ///     This is the base value, whose <c>{App Id}</c> part is always
    ///     <c>0000</c>.  The payload actually sent at login carries the
    ///     identifier set on the connection; see
    ///     <see cref="GetUcs2Bytes">GetUcs2Bytes</see>.
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
    ///     The <c>{App Id}</c> part is the identifier of the application
    ///     middleware using the driver, serialized as exactly four uppercase
    ///     hexadecimal characters, zero-padded.  It is always present;
    ///     <c>0000</c> means no application identity was reported.  Maximum
    ///     length is 4 characters.
    ///   </para>
    ///   <para>
    ///     The <c>{Driver Properties}</c> part is a driver-owned feature flag
    ///     value, serialized as exactly four uppercase hexadecimal characters,
    ///     zero-padded.  It is always present.  Maximum length is 4
    ///     characters.
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
    ///   <para>
    ///     Returns the UCS-2 encoded payload reporting the given application
    ///     identifier.
    ///   </para>
    ///   <para>
    ///     When <paramref name="app"/> is
    ///     <see cref="SqlClientApp.Unknown"/>, <see cref="Ucs2Bytes"/> is
    ///     returned, whose App Id part is <c>0000</c>.
    ///   </para>
    /// </summary>
    /// <param name="app">
    ///   The application identifier set on the connection being logged in.
    /// </param>
    /// <returns>The UCS-2 encoded payload bytes.</returns>
    internal static ReadOnlyMemory<byte> GetUcs2Bytes(SqlClientApp app)
    {
        if (app == SqlClientApp.Unknown)
        {
            return Ucs2Bytes;
        }

        // Most processes report a single application identifier, so a single
        // cached entry serves every login.  The pair is cached behind one
        // reference so readers never observe a torn ReadOnlyMemory<byte>.
        AppPayload? cached = Volatile.Read(ref s_appPayload);
        if (cached is not null && cached.App == app)
        {
            return cached.Ucs2Bytes;
        }

        ReadOnlyMemory<byte> bytes = Encoding.Unicode.GetBytes(BuildPayload(app));
        Volatile.Write(ref s_appPayload, new AppPayload(app, bytes));

        return bytes;
    }

    /// <summary>
    ///   Build the payload string from the current runtime environment,
    ///   reporting the given application identifier.
    /// </summary>
    /// <param name="app">The application identifier to report.</param>
    /// <returns>The payload string value.</returns>
    private static string BuildPayload(SqlClientApp app) =>
        Build(
            MaxLenOverall,
            PayloadVersion,
            DriverName,
            ThisAssembly.PackageVersion,
            RuntimeInformation.ProcessArchitecture,
            s_osType,
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            ToAppId(app),
            (ushort)SqlClientDriverPropertiesResolver.Current);

    /// <summary>
    ///   Narrow an application identifier to the 16 bits the payload reports.
    /// </summary>
    /// <remarks>
    ///   <see cref="SqlConnection.SqlClientAppId"/> rejects values outside the
    ///   16-bit range, so this conversion is always lossless.
    /// </remarks>
    /// <param name="app">The application identifier to narrow.</param>
    /// <returns>The narrowed application identifier.</returns>
    private static ushort ToAppId(SqlClientApp app)
    {
        Debug.Assert((int)app >= 0 && (int)app <= ushort.MaxValue);

        return (ushort)app;
    }

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
        if (OsConstants.IsWindows)
        {
            osType = Windows;
        }
        else if (OsConstants.IsLinux)
        {
            osType = Linux;
        }
        else if (OsConstants.IsMacOS)
        {
            osType = macOS;
        }
#if NET
        else if (OsConstants.IsFreeBSD)
        {
            osType = FreeBSD;
        }
#endif

        // Remember it for agent payloads built later.
        s_osType = osType;

        // Build it!
        Value = BuildPayload(SqlClientApp.Unknown);

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
    /// <param name="appId">
    ///   The value of the App Id part, serialized as four uppercase
    ///   hexadecimal characters.
    /// </param>
    /// <param name="driverProperties">
    ///   The value of the Driver Properties part, serialized as four uppercase
    ///   hexadecimal characters.
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
        string runtimeInfo,
        ushort appId = 0,
        ushort driverProperties = 0)
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
            name.Append('|');

            // Add the App Id.  It is fixed-width hexadecimal, so it can never
            // exceed its maximum length and needs no cleaning.
            string appIdPart = FormatHex(appId);
            Debug.Assert(appIdPart.Length == MaxLenAppId);
            name.Append(appIdPart);
            name.Append('|');

            // Add the Driver Properties, on the same terms as the App Id.
            string driverPropertiesPart = FormatHex(driverProperties);
            Debug.Assert(driverPropertiesPart.Length == MaxLenDriverProperties);
            name.Append(driverPropertiesPart);

            // Remember the name we've built up.
            result = name.ToString();
        }
        catch (ArgumentOutOfRangeException)
        {
            // StringBuilder failed in an unexpected way, so use our fallback
            // value.
            result =
                $"{payloadVersion}|{driverName}|{Unknown}|{Unknown}|" +
                $"{Unknown}|{Unknown}|{Unknown}|{FormatHex(appId)}|" +
                $"{FormatHex(driverProperties)}";
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
    ///   Format the given value as exactly four uppercase hexadecimal
    ///   characters, zero-padded.
    /// </summary>
    /// <remarks>
    ///   A <see cref="ushort"/> never needs more than four hexadecimal
    ///   characters, so the result is always exactly
    ///   <see cref="MaxLenAppId"/> characters and can never be truncated.
    /// </remarks>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted value.</returns>
    internal static string FormatHex(ushort value) =>
        value.ToString("X4", CultureInfo.InvariantCulture);

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
    //
    // Version 2 adds the optional Agent Id part.
    private const string PayloadVersion = "2";

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
    private const ushort MaxLenAppId = 4;
    private const ushort MaxLenDriverProperties = 4;

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

    // The OS Type resolved during static construction, retained so payloads
    // built later use the same value as Value.
    private static readonly string s_osType;

    // The payload for the most recently requested application identifier,
    // built on first use.
    private static AppPayload? s_appPayload;

    /// <summary>
    ///   Pairs a payload with the application identifier it was built for.
    /// </summary>
    private sealed class AppPayload
    {
        internal AppPayload(SqlClientApp app, ReadOnlyMemory<byte> ucs2Bytes)
        {
            App = app;
            Ucs2Bytes = ucs2Bytes;
        }

        internal SqlClientApp App { get; }

        internal ReadOnlyMemory<byte> Ucs2Bytes { get; }
    }

    #endregion Private Fields
}
