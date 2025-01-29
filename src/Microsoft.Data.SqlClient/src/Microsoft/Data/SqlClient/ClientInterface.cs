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
        // for possible values.  This value will never be longer than 15
        // characters.
        //
        // The {OS Info} will be sourced from the the
        // System.Runtime.InteropServices.RuntimeInformation.OSDescription
        // value, or "Unknown" if that value is empty or all whitespace.  This
        // field participates in truncation if the entire Name is too long, and
        // may be as short as 42 characters.
        //
        // The {Framework Info} will be sourced from the
        // System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        // value, or "Unknown" if that value is empty or all whitespace.  This
        // field participates in truncation if the entire Name is too long, and
        // may be as short as 42 characters.
        //
        // This adheres to the TDS v37.0 spec, which specifies that the client
        // interface name has a maximum length as noted above.  If the fully
        // formed Name length is beyond that limit, it will be truncated to the
        // maximum by reducing the length of the {OS Info} and {Framework Info}
        // fields as evenly as possible.
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

        // ====================================================================
        // Helpers

        // Static construction builds the client interface name.
        //
        // We make a best effort to avoid allowing known exceptions to escape.
        //
        static ClientInterface()
        {
            // The max length must not be negative, or our assumptions below
            // won't hold.
            //
            // C# doesn't have compile-time type traits, so we can't confirm
            // that MAXLEN_CLIENTINTERFACE is unsigned.  Instead, we capture
            // it into a ushort, and let the compiler decide if that is a
            // permitted operation.
            //
            ushort maxLen = TdsEnums.MAXLEN_CLIENTINTERFACE;

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

                // Start with the well-known name prefix of this driver.
                name.Append(DriverName);
                name.Append('|');

                // Add the OS name, in order of likelihood.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    name.Append("Windows");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    name.Append("Linux");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    name.Append("MacOS");
                }
// The FreeBSD platform doesn't exist in .NET Framework at all.
#if NET
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                {
                    name.Append("FreeBSD");
                }
#endif // NET
                else
                {
                    name.Append(Unknown);
                }

                name.Append('|');

                // Add the architecture.
                //
                // We don't expect the architecture to be very long; 15
                // characters max should be plenty.
                //
                var arch =
                    Clean(RuntimeInformation.ProcessArchitecture.ToString());
                if (arch.Length > 15)
                {
                    arch = arch.Substring(0, 15);
                }
                name.Append(arch);
                name.Append('|');

                // We should have appended no more 44 characters here:
                //
                //  - DriverName "Microsoft SqlClient" (19)
                //  - OS Name "Windows" (max 7)
                //  - Arch (max 15)
                //  - 3 pipe characters
                //
                Debug.Assert(name.Length <= 44);

                // Obtain cleaned versions of OS and framework descriptions.
                var osDesc = Clean(RuntimeInformation.OSDescription);
                var frameworkDesc =
                    Clean(RuntimeInformation.FrameworkDescription);

                // How many more characters can we append?
                Debug.Assert(name.Length <= maxLen);
                ushort remaining =
                    (ushort)
                    ((name.Length > maxLen ? 0 : maxLen - name.Length)
                     // Subtract 1 to accommodate the pipe character between
                     // the OS and framework descriptions.
                     - 1);

                // Can we fit both descriptions as-is?
                if (osDesc.Length + frameworkDesc.Length > remaining)
                {
                    // No, so we will have to truncate something.
                    //
                    // We want to keep the descriptions as balanced as possible,
                    // so we'll truncate them to no shorter than half of the
                    // remaining space each.
                    //
                    // If remaining is odd, we'll give the extra character to
                    // the OS description if we need to truncate both.
                    //
                    ushort half = (ushort)(remaining / 2);
                    
                    // Do we need to truncate the OS description?
                    if (osDesc.Length <= half)
                    {
                        // No, so truncate the framework description as little
                        // as possible.
                        frameworkDesc =
                            frameworkDesc.Substring(
                                0,
                                remaining - osDesc.Length);
                    }
                    // Do we need to truncate the framework description?
                    else if (frameworkDesc.Length <= half)
                    {
                        // No, so truncate the OS description as little as
                        // possible.
                        osDesc =
                            osDesc.Substring(
                                0,
                                remaining - frameworkDesc.Length);
                    }
                    // Otherwise, we need to truncate them both.
                    else
                    {
                        frameworkDesc = frameworkDesc.Substring(0, half);

                        // Give the extra character to the OS description if
                        // remaining was odd.
                        if (remaining % 2 != 0)
                        {
                            half++;
                        }
                        osDesc = osDesc.Substring(0, half);
                    }

                    Debug.Assert(
                        osDesc.Length + frameworkDesc.Length <= remaining);
                }

                // Append them.
                name.Append(osDesc);
                name.Append('|');
                name.Append(frameworkDesc);

                // Remember the final name.
                _name = name.ToString();

                Debug.Assert(_name.Length <= maxLen);
            }
            catch (ArgumentOutOfRangeException)
            {
                // StringBuilder failed in an unexpected way, so use our
                // fallback value.
                _name = $"{DriverName}|{Unknown}|{Unknown}|{Unknown}|{Unknown}";
            }

            // Truncate to our max length.
            //
            // This is a paranoia step, as we've already been careful to ensure
            // that we don't exceed the max length.
            //
            if (_name.Length > maxLen)
            {
                // We know this won't throw ArgumentOutOfRangeException because
                // we've already confirmed that Length is greater than the
                // maximum.
                _name = _name.Substring(0, maxLen);
            }
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

        // ====================================================================
        // Private Fields

        // The client interface name.
        private static readonly string _name;

        // Our well-known .NET driver name.
        private const string DriverName = "Microsoft SqlClient";

        // A fallback value for parts of the client interface name that are
        // unknown, invalid, or when errors occur.
        private const string Unknown = "Unknown";
    }
}
