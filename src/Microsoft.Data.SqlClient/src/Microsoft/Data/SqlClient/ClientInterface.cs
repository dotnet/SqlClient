using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    // ========================================================================
    // This class uses runtime environment information to produce a value
    // suitable for use in the TDS LOGIN7 Client Interface Name field.
    //
    internal static class ClientInterface
    {
        #region Properties
        
        // ====================================================================
        // Properties

        // The client interface name, never null, never empty, and never larger
        // than TdsEnum.MAXLEN_CLIENTINTERFACE (currently 128) characters.
        //
        // Format:
        //
        //   Microsoft SqlClient|{OS Name}|{Arch}|{OS Info}|{Framework Info}
        //
        // The {OS Name} will be one of the following strings:
        //
        //   Windows
        //   Linux
        //   MacOS
        //   FreeBSD
        //   Unknown
        //
        // The {Arch} will be the process architecture, either the bare metal
        // hardware architecture or the virtualized architecture.  See
        // System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
        // for possible values.  This value will never be longer than 11
        // characters.
        //
        // The {OS Info} will be sourced from the the
        // System.Runtime.InteropServices.RuntimeInformation.OSDescription
        // value, or "Unknown" if that value is empty or all whitespace.
        //
        // The {Framework Info} will be sourced from the
        // System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        // value, or "Unknown" if that value is empty or all whitespace.
        //
        // This adheres to the TDS v37.0 spec, which specifies that the client
        // interface name has a maximum length as noted above.  If the fully
        // formed Name length is beyond that limit, it will be truncated to the
        // maximum with no regard for preserving certain fields or pipe ('|')
        // delimiters.
        //
        // The maximum length is expected to be sufficient to accommodate the
        // driver name, {OS Name}, and {Arch} fields, but those fields will
        // be truncated as described above if necessary.
        //
        // The {OS Info} and {Framework Info} fields will share any remaining
        // space as evenly as possible, being truncated equally if both are
        // longer than half of the remaining space.  If one of these fields is
        // shorter than half of the remaining space, the other field will
        // consume as much remaining space as possible.
        //
        // Any characters that are not one of the following are replaced with 
        // underscore ('_'):
        //
        //   - ASCII letters ([A-za-z])
        //   - ASCII digits ([0-9])
        //   - Space (' ')
        //   - Period ('.')
        //   - Plus ('+')
        //   - Underscore ('_')
        //   - Hyphen ('-')
        //
        // All known exceptions are caught and handled by injecting the fallback
        // value of "Unknown".  However, no effort is made to catch all
        // exceptions, for example process-fatal memory allocation errors.
        // 
        public static string Name => _name;

        #endregion Properties

        #region Helpers

        // ====================================================================
        // Helpers

        // Static construction builds the client interface name.
        //
        // All known exceptions are consumed.
        //
        static ClientInterface()
        {
            // The max length must not be negative.
            //
            // C# doesn't have compile-time type traits, so we can't confirm
            // that MAXLEN_CLIENTINTERFACE is unsigned.  Instead, we capture
            // it into a ushort, and let the compiler decide if that is a
            // permitted operation.
            //
            ushort maxLen = TdsEnums.MAXLEN_CLIENTINTERFACE;

            // Determine the OS type.
            //
            // RuntimeInformation doesn't have an enumeration for OS type, which
            // makes it impossible to inject that value for testing.  We define
            // our own enum and choose the correct value here.
            //
            string osType = Unknown;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                osType = "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                osType = "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                osType = "MacOS";
            }
// The FreeBSD platform doesn't exist in .NET Framework at all.
#if NET
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                osType = "FreeBSD";
            }
#endif // NET

            // Build it!
            _name = Build(
                maxLen,
                DriverName,
                osType,
                RuntimeInformation.ProcessArchitecture,
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription);
        }

        // Build the client interface name and return it.
        //
        // The length of the returned value will never be longer than maxLen.
        //
        // All known exceptions are consumed.
        //
        public static string Build(
            ushort maxLen,
            string driverName,
            string osType,
            Architecture arch,
            string osDesc,
            string frameworkDesc)
        {
            string result;

            // Clean and truncate the driver name, max length 10 (arbitrarily
            // chosen - should be plenty).  We will need it for error handling.
            driverName = Trunc(Clean(driverName), 10);

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

                // Add the OS name, max length 7 (because that's the current
                // length of the longest value- see the constructor above).
                name.Append(Trunc(Clean(osType), 7));
                name.Append('|');

                // Add the architecture, max length 11 (enough to hold all
                // current enum values).
                name.Append(Trunc(Clean(arch.ToString()), 11));
                name.Append('|');

                // We should have appended at most 31 characters so far:
                //
                //  10 (driver name)
                //   1 (pipe)
                //   7 (OS name)
                //   1 (pipe)
                //  11 (architecture)
                //   1 (pipe)
                //
                Debug.Assert(name.Length <= 31);

                // Obtain cleaned versions of OS and framework descriptions.
                osDesc = Clean(osDesc);
                frameworkDesc = Clean(frameworkDesc);

                // How many more characters can we append?
                ushort remaining = 0;
                if (name.Length < maxLen)
                {
                    remaining = (ushort)(maxLen - name.Length);
                }

                // Do we have any remaining space?
                if (remaining > 0)
                {
                    // Yes, so we want to end up with OS and Framework
                    // description lengths like this:
                    //
                    //  Remaining | OS | Pipe | Framework
                    //  ----------|----|------| ---------
                    //          1 |  1 |    0 |         0
                    //          2 |  1 |    1 |         0
                    //          3 |  1 |    1 |         1
                    //          4 |  2 |    1 |         1
                    //          5 |  2 |    1 |         2
                    //          6 |  3 |    1 |         2
                    //          7 |  3 |    1 |         3
                    //
                    // And so on.
                    //
                    // If remaining is odd, we'll give the extra character
                    // to the OS description.

                    // If we have at least 2 characters left, then we will need
                    // to leave room for the pipe character, so decrement
                    // remaining accordingly.
                    if (remaining >= 2)
                    {
                        --remaining;
                    }

                    // Will both descriptions together be too long?
                    if (osDesc.Length + frameworkDesc.Length > remaining)
                    {
                        // Yes, so we will have to truncate something.
                        //
                        // We want to keep the descriptions as balanced as
                        // possible, so we'll truncate them to no shorter than
                        // half of the remaining space each.
                        //
                        ushort osHalf = (ushort)(remaining / 2);
                        ushort frameworkHalf = osHalf;

                        // Give the OS description the extra character, if
                        // necessary.
                        if (remaining % 2 != 0)
                        {
                            osHalf++;
                        }
                        
                        Debug.Assert(osHalf + frameworkHalf == remaining);
                        
                        // Will the OS description fit as-is?
                        if (osDesc.Length <= osHalf)
                        {
                            // Yes, so the framework description must be too
                            // long.  Truncate it as little as possible.
                            frameworkDesc =
                                frameworkDesc.Substring(
                                    0,
                                    remaining - osDesc.Length);
                        }
                        // Will the framework description fit as-is?
                        else if (frameworkDesc.Length <= frameworkHalf)
                        {
                            // Yes, so the OS description must be too long.
                            // Truncate it as little as possible.
                            osDesc =
                                osDesc.Substring(
                                    0,
                                    remaining - frameworkDesc.Length);
                        }
                        // Otherwise, we need to truncate them both.
                        else
                        {
                            osDesc = osDesc.Substring(0, osHalf);
                            frameworkDesc =
                                frameworkDesc.Substring(0, frameworkHalf);
                        }

                        Debug.Assert(
                            osDesc.Length + frameworkDesc.Length <= remaining);
                    }

                    // Append them now that they've been truncated if necessary.
                    name.Append(osDesc);
                    // Note that we didn't specifically accommodate this pipe
                    // character in our remaining space calculations.  It may
                    // cause us to exceed the max length, but we perform a
                    // last-resort truncation below anyway.
                    name.Append('|');
                    name.Append(frameworkDesc);
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
            // There are cases above where the max length may have been
            // exceeded, and rather than deal with them individually, we rely
            // on this final truncation check for simplicity.
            //
            if (result.Length > maxLen)
            {
                // We know this won't throw ArgumentOutOfRangeException because
                // we've already confirmed that Length is greater than maxLen.
                result = result.Substring(0, maxLen);
            }

            return result;
        }

        // Clean the given value of any disallowed characters, replacing them
        // with underscore ('_'), and return the cleaned value.
        //
        // Each disallowed character is replaced with an underscore, preserving
        // the original length of the value.  No effort is made to collapse
        // adjacent disallowed characters.
        //
        // Permitted characters are:
        //
        //   - ASCII letters ([A-za-z])
        //   - ASCII digits ([0-9])
        //   - Space (' ')
        //   - Period ('.')
        //   - Plus ('+')
        //   - Underscore ('_')
        //   - Hyphen ('-')
        //
        // If the given value is null, empty, or all whitespace, or an error
        // occurs, the fallback value is returned.
        //
        public static string Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Unknown;
            }

            try
            {
                // Build the cleaned value by hand, avoiding the overhead and
                // failure scenarios of regexes or other more complex solutions.
                //
                // We expect the value to be short, and this code a few times
                // per process.  Robustness and simplicity are more important
                // than performance here.
                //
                StringBuilder cleaned = new StringBuilder(value.Length);
                foreach (char c in value)
                {
                    // Is it a permitted character?
                    if (char.IsAsciiLetter(c)
                        || char.IsAsciiDigit(c)
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

        // Truncate the given value to the given max length, and return the
        // result.
        public static string Trunc(string value, ushort maxLength)
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

        // A fallback value for parts of the client interface name that are
        // unknown, invalid, or when errors occur.
        private const string Unknown = "Unknown";

        #endregion Private Fields
    }
}
