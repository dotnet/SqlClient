using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    internal struct RuntimeHelpers
    {
        /// <summary>
        /// This is a no-op in netcore version. Only needed for merging with netfx codebase.
        /// </summary>
        [Conditional("NETFRAMEWORK")]
        internal static void PrepareConstrainedRegions()
        {
        }
    }
}
