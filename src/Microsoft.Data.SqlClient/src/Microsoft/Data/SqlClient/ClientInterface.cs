// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    // ========================================================================
    /// <summary>
    ///   <para>
    ///     This class uses runtime environment information to produce a value
    ///     suitable for use in the TDS LOGIN7 Client Interface Name field.
    ///   </para>
    ///   <para>
    ///     See the spec here:
    ///
    ///     <see href="https://microsoft.sharepoint-df.com/:w:/t/sqldevx/EZYUHqdnoXhKoQv0kxcM1QUB2V_uTCWN9GHfrDQY2tojmA?e=1Coybd">
    ///       SQL Drivers Client Interface Name Specification
    ///     </see>
    ///   </para>
    /// </summary>
    internal static class ClientInterface
    {
        #region Properties
        
        // ====================================================================
        // Properties

        /// <summary>
        ///   <para>
        ///     The Client Interface Name, never null, never empty, and never 
        ///     larger than
        ///     <see cref="TdsEnums.MAXLEN_CLIENTINTERFACE">
        ///       MAXLEN_CLIENTINTERFACE
        ///     </see>
        ///     (currently 128) characters.
        ///   </para>
        ///   <para> 
        ///     The format is pipe ('|') delimited into 5 parts:
        ///
        ///     <code>MS-MDS|{OS Type}|{Arch}|{OS Info}|{Runtime Info}</code>
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
        ///
        ///   <para> 
        ///     The <c>{Arch}</c> part will be the process architecture, either
        ///     the bare metal hardware architecture or the virtualized
        ///     architecture.  See
        ///     <see cref="RuntimeInformation.ProcessArchitecture">
        ///       ProcessArchitecture
        ///     </see>
        ///     for possible values.  This value will never be longer than 10
        ///     characters.
        ///   </para>
        ///   <para>
        ///     The <c>{OS Info}</c> part will be sourced from the the
        ///     <see cref="RuntimeInformation.OSDescription">
        ///       OSDescription
        ///     </see> 
        ///     value, or "Unknown" if that value is empty or all whitespace.
        ///   </para>
        ///   <para>
        ///     The <c>{Runtime Info}</c> part will be sourced from the
        ///     <see cref="RuntimeInformation.FrameworkDescription">
        ///       FrameworkDescription
        ///    </see>
        ///     value, or "Unknown" if that value is empty or all whitespace.
        ///   </para>
        ///   <para>
        ///     This adheres to the TDS v37.0 spec, which specifies that the
        ///     Client Interface Name has a maximum length as noted above.  If
        ///     the fully formed Name length is beyond that limit, it will be
        ///     truncated to the maximum with no regard for preserving certain
        ///     parts or pipe ('|') delimiters.
        ///   </para>
        ///   <para>
        ///     The maximum length is expected to be sufficient to accommodate
        ///     the driver name, <c>{OS Type}</c>, and <c>{Arch}</c> parts, but
        ///     those parts will be truncated as described above if necessary.
        ///   </para>
        ///   <para> 
        ///     The <c>{OS Info}</c> and <c>{Runtime Info}</c> parts will share
        ///     any remaining space as evenly as possible, being truncated
        ///     equally if both are longer than half of the remaining space.
        ///     If one of these parts is shorter than half of the remaining
        ///     space, the other part will consume as much remaining space as
        ///     possible.
        ///   </para>
        ///   <para>
        ///     Any characters that are not one of the following are replaced
        ///     with underscore ('_'):
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
        public static string Name => _name;

        #endregion Properties
        
        #region Helpers

        // ====================================================================
        // Helpers

        /// <summary>
        ///   <para>Static construction builds the Client Interface Name.</para>
        ///   <para>All known exceptions are consumed.</para>
        /// </summary>
        static ClientInterface()
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
#endif // NET

            // Build it!
            _name = Build(
                MaxLenOverall,
                DriverName,
                osType,
                RuntimeInformation.ProcessArchitecture,
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription);
        }

        /// <summary>
        ///   <para>Build the Client Interface Name and return it.</para>
        ///   <para>
        ///     The length of the returned value will never be longer than
        ///     <paramref name="maxLen"/>.
        ///   </para>
        ///   <para>All known exceptions are consumed.</para>
        /// </summary>
        /// <param name="maxLen">
        ///   The maximum length of the returned value.
        /// </param>
        /// <param name="driverName">
        ///   The value of the driver name part. 
        /// </param>
        /// <param name="osType">
        ///   The value of the OS Type part. 
        /// </param>
        /// <param name="arch">
        ///   The value of the Architecture part.
        /// </param>
        /// <param name="osInfo">
        ///   The value of the OS Info part. 
        /// </param>
        /// <param name="runtimeInfo">
        ///   The value of the Runtime Info part. 
        /// </param>
        /// <returns>
        ///   The Client Interface Name value, never null, never empty, and
        ///   never longer than <paramref name="maxLen"/>.
        /// </returns>
        public static string Build(
            ushort maxLen,
            string driverName,
            string osType,
            Architecture arch,
            string osInfo,
            string runtimeInfo)
        {
            string result;

            // Clean and truncate the driver name.  We will need it for error
            // handling.
            driverName = Truncate(Clean(driverName), MaxLenDriverName);

            try
            {
                // Expect to build a string whose length is up to our max
                // length.
                //
                // This isn't a max capacity, but a hint for initial buffer
                // allocation.  We will truncate to our max length after all of
                // the pieces have been appended.
                //
                StringBuilder name = new StringBuilder(maxLen);

                // Start with the (clean) driver name.
                name.Append(driverName);
                name.Append('|');

                // Add the OS Type, truncating to its max length.
                name.Append(Truncate(Clean(osType), MaxLenOSType));
                name.Append('|');

                // Add the Architecture, truncating to its max length.
                name.Append(Truncate(Clean(arch.ToString()), MaxLenArch));
                name.Append('|');
                
                // String.Length is a signed 32-bit integer, but the API
                // guarantees it will never be negative.  We will not explicitly
                // check for negative values during arithmetic operations.

                // We should have appended at most 39 characters so far:
                //
                //  16 (driver name)
                //   1 (pipe)
                //  10 (OS name)
                //   1 (pipe)
                //  10 (architecture)
                //   1 (pipe)
                //
                // This leaves us with at least 89 characters for the OS and
                // Runtime Info.
                //
                Debug.Assert(name.Length <= 39);

                // Obtain cleaned versions of OS and Runtime Info.
                osInfo = Clean(osInfo);
                runtimeInfo = Clean(runtimeInfo);

                // How many more characters can we append?
                ushort remaining = 0;
                if (name.Length < maxLen)
                {
                    remaining = (ushort)(maxLen - name.Length);
                }

                // Do we have any remaining space?
                if (remaining > 0)
                {
                    // Yes, so we want to end up with OS and Runtime Info
                    // lengths like this:
                    //
                    //  Remaining | OS | Pipe | Runtime
                    //  ----------|----|------| -------
                    //          1 |  1 |    0 |       0
                    //          2 |  1 |    1 |       0
                    //          3 |  1 |    1 |       1
                    //          4 |  2 |    1 |       1
                    //          5 |  2 |    1 |       2
                    //          6 |  3 |    1 |       2
                    //          7 |  3 |    1 |       3
                    //
                    // And so on.
                    //
                    // If remaining is odd, we'll give the extra character
                    // to the OS Info.  Runtime Info is likely to have suitable
                    // fidelity within its first 45 characters.

                    // If we have at least 2 characters left, then we will need
                    // to leave room for the pipe character, so decrement
                    // remaining accordingly.
                    if (remaining >= 2)
                    {
                        --remaining;
                    }

                    // Will both Info parts together be too long?
                    if (
                        // If the addition of both lengths would overflow, then
                        // they are definitely too long.
                        int.MaxValue - osInfo.Length < runtimeInfo.Length
                        // Otherwise, check their sum versus remaining.
                        || osInfo.Length + runtimeInfo.Length > remaining)
                    {
                        // Yes, so we will have to truncate something.
                        //
                        // We want to keep the Info as balanced as possible, so
                        // we'll truncate them each to no shorter than half of
                        // the remaining space.
                        //
                        ushort osHalf = (ushort)(remaining / 2);
                        ushort runtimeHalf = osHalf;

                        // If there's a remainder, give it to the OS Info.
                        if (osHalf + runtimeHalf < remaining)
                        {
                            ++osHalf;
                        }
                        
                        Debug.Assert(osHalf + runtimeHalf == remaining);
                        
                        // Will the OS Info fit as-is?
                        if (osInfo.Length <= osHalf)
                        {
                            // Yes, so the Runtime Info must be too long.
                            // Truncate it as little as possible.
                            runtimeInfo =
                                runtimeInfo.Substring(
                                    0,
                                    remaining - osInfo.Length);
                        }
                        // Will the Runtime Info fit as-is?
                        else if (runtimeInfo.Length <= runtimeHalf)
                        {
                            // Yes, so the OS Info must be too long.  Truncate
                            // it as little as possible.
                            osInfo =
                                osInfo.Substring(
                                    0,
                                    remaining - runtimeInfo.Length);
                        }
                        // Otherwise, we need to truncate them both.
                        else
                        {
                            osInfo = osInfo.Substring(0, osHalf);
                            runtimeInfo = runtimeInfo.Substring(0, runtimeHalf);
                        }

                        Debug.Assert(
                            osInfo.Length + runtimeInfo.Length <= remaining);
                    }

                    // Append them now that they've been truncated if necessary.
                    name.Append(osInfo);
                    name.Append('|');
                    name.Append(runtimeInfo);
                }

                // Remember the name we've built up.
                result = name.ToString();
            }
            catch (ArgumentOutOfRangeException)
            {
                // StringBuilder failed in an unexpected way, so use our
                // fallback value.
                result =
                    $"{driverName}|{Unknown}|{Unknown}|{Unknown}|{Unknown}";
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
        ///     Clean the given value of any disallowed characters, replacing
        ///     them with underscore ('_'), and return the cleaned value.
        ///   </para>
        ///   <para>Leading and trailing whitespace are removed.</para>
        ///   <para>
        ///     Each disallowed character is replaced with an underscore,
        ///     preserving the original length of the value.  No effort is made
        ///     to collapse adjacent disallowed characters.
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
        ///     If the given value is null, empty, or all whitespace, or an
        ///     error occurs, the fallback value is returned.
        ///   </para>
        /// </summary>
        /// <param name="value">The value to clean.</param>
        /// <returns>The cleaned value, or the fallback value.</returns>
        public static string Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)
#if NETFRAMEWORK
                // .NET Framework doesn't consider IsNullOrWhiteSpace()
                // sufficient for nullable checks, so add an explicit check for
                // null.
                || value == null
#endif // NETFRAMEWORK
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
                // We expect the value to be short, and this code is called only
                // a few times per process.  Robustness and simplicity are more
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
#endif // NET
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
                // StringBuilder failed in an unexpected way, so use our
                // fallback value.
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
        ///   The truncated value, or the original <paramref name="value"/>
        ///   if no truncation occurred.
        /// </returns>
        public static string Truncate(string value, ushort maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            // We know this won't throw ArgumentOutOfRangeException because
            // we've already confirmed that Length is greater than maxLength.
            return value.Substring(0, maxLength);
        }

        #endregion Helpers

        #region Private Fields

        // ====================================================================
        // Private Fields

        // The client interface name.
        private static readonly string _name;

        // Our well-known .NET driver name.
        private const string DriverName = "MS-MDS";

        // The overall maximum length of Name.
        //
        // C# doesn't have compile-time type traits, so we can't confirm that
        // MAXLEN_CLIENTINTERFACE is unsigned.  Instead, we capture it into a
        // ushort, and let the compiler decide if that is a permitted operation.
        //
        private const ushort MaxLenOverall = TdsEnums.MAXLEN_CLIENTINTERFACE;
        
        // Maximum part lengths as promised in our API.
        private const ushort MaxLenDriverName = 16;
        private const ushort MaxLenOSType = 10;
        private const ushort MaxLenArch = 10;

        // The OS Type values we promise in our API.
        private const string Windows = "Windows";
        private const string Linux = "Linux";
        private const string macOS = "macOS";
// The FreeBSD platform doesn't exist in .NET Framework at all.
#if NET
        private const string FreeBSD = "FreeBSD";
#endif // NET

        // A fallback value for parts of the client interface name that are
        // unknown, invalid, or when errors occur.
        private const string Unknown = "Unknown";

        #endregion Private Fields
    }
}
