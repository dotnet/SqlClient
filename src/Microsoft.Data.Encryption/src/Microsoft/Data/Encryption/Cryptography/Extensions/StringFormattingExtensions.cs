using System.Globalization;

namespace Microsoft.Data.Encryption
{
    internal static class StringFormattingExtensions
    {
        internal static string FormatInvariant(this string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
    }
}
