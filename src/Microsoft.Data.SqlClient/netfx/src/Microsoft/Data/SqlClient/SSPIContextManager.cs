using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Context for callback
    /// </summary>
    public class NegotiateCallbackContext
    {
        internal NegotiateCallbackContext(SqlAuthenticationParameters parameters, ReadOnlyMemory<byte> lastReceived)
        {
            LastReceived = lastReceived;
            AuthenticationParameters = parameters;
        }

        /// <summary>
        /// Gets the last received data
        /// </summary>
        public ReadOnlyMemory<byte> LastReceived { get; }

        /// <summary>
        /// Gets the auth parameters
        /// </summary>
        public SqlAuthenticationParameters AuthenticationParameters { get; }
    }

    internal static class SSPIContextManager
    {
#if NETFRAMEWORK
        public static void Invoke(SqlInternalConnectionTds Connection, ReadOnlyMemory<byte> lastReceived, byte[] output, ref uint outputLength)
#else
        public static void Invoke(SqlInternalConnectionTds Connection, ReadOnlyMemory<byte> lastReceived, ref byte[] output, ref uint outputLength)
#endif
        {
            Debug.Assert(Connection._negotiateCallback is not null);

            var result = Invoke(Connection, lastReceived);

#if !NETFRAMEWORK
            output = new byte[result.Length];
#endif
            result.CopyTo(output);
            outputLength = (uint)result.Length;
        }

        private static ReadOnlyMemory<byte> Invoke(SqlInternalConnectionTds Connection, ReadOnlyMemory<byte> lastReceived)
        {
            var auth = new SqlAuthenticationParameters.Builder(Connection.ConnectionOptions.Authentication, "resource", "auth", Connection.ConnectionOptions.ObtainWorkstationId(), Connection.ConnectionOptions.InitialCatalog);

            if (Connection.ConnectionOptions.UserID is { } userId)
            {
                auth.WithUserId(userId);
            }

            if (Connection.ConnectionOptions.Password is { } password)
            {
                auth.WithPassword(password);
            }

            using var cts = Connection.CreateCancellationTokenSource();

            // TODO - can we run this in a proper async context?
            return Connection._negotiateCallback(new(auth, lastReceived), cts.Token).GetAwaiter().GetResult();
        }
    }
}
