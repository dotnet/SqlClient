using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    internal static class SSPIContextManager
    {
#if NETFRAMEWORK
        public static void Invoke(SqlInternalConnectionTds Connection, byte[] output, ref uint outputLength)
#else
        public static void Invoke(SqlInternalConnectionTds Connection, ref byte[] output, ref uint outputLength)
#endif
        {
            Debug.Assert(Connection.Connection.SSPIContextCallback is not null);

            var result = Invoke(Connection);

#if !NETFRAMEWORK
            output = new byte[result.Length];
#endif
            result.CopyTo(output);
            outputLength = (uint)result.Length;
        }

        private static ReadOnlyMemory<byte> Invoke(SqlInternalConnectionTds Connection)
        {
            var auth = new SqlAuthenticationParameters.Builder(Connection.ConnectionOptions.Authentication, Connection.ConnectionOptions.ObtainWorkstationId(), "auth", Connection.ConnectionOptions.DataSource, Connection.ConnectionOptions.InitialCatalog);

            if (Connection.ConnectionOptions.UserID is { } userId)
            {
                auth.WithUserId(userId);
            }

            if (Connection.ConnectionOptions.Password is { } password)
            {
                auth.WithPassword(password);
            }

            using var cts = Connection.CreateCancellationTokenSource();

            return Connection.Connection.SSPIContextCallback(auth, cts.Token);
        }
    }
}
