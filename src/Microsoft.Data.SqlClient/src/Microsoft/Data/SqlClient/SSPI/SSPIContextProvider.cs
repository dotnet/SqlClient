using System;
using System.Buffers;
using System.Diagnostics;
using Microsoft.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal abstract class SSPIContextProvider
    {
        private TdsParser _parser = null!;
        private ServerInfo _serverInfo = null!;
        private protected TdsParserStateObject _physicalStateObj = null!;
        private SqlAuthenticationParameters? _parameters;
        private protected string[] _serverNames = Array.Empty<string>();

        internal void Initialize(ServerInfo serverInfo, TdsParserStateObject physicalStateObj, TdsParser parser, params string[] serverNames)
        {
            if (serverNames is null)
            {
                throw new ArgumentNullException(nameof(serverNames));
            }

            Debug.Assert(serverNames.Length > 0);

            _parser = parser;
            _physicalStateObj = physicalStateObj;
            _serverInfo = serverInfo;
            _serverNames = serverNames;

            _parameters = InitializeAuthenticationParameters(parser.Connection, serverNames[0]);

            Initialize();
        }

        private static SqlAuthenticationParameters InitializeAuthenticationParameters(SqlInternalConnectionTds connection, string serverName)
        {
            var auth = new SqlAuthenticationParameters.Builder(
                authenticationMethod: connection.ConnectionOptions.Authentication,
                resource: null,
                authority: null,
                serverName: serverName,
                connection.ConnectionOptions.InitialCatalog);

            if (connection.ConnectionOptions.UserID is { } userId)
            {
                auth.WithUserId(userId);
            }

            if (connection.ConnectionOptions.Password is { } password)
            {
                auth.WithPassword(password);
            }

            return auth;
        }

        private protected virtual void Initialize()
        {
        }

        /// <summary>
        /// Gets the authentication parameters for the current connection.
        /// </summary>
        protected SqlAuthenticationParameters AuthenticationParameters => _parameters ?? throw new InvalidOperationException("SSPI context provider has not been initialized");

        protected abstract void GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter);

        internal void SSPIData(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter)
        {
            try
            {
                GenerateSspiClientContext(receivedBuff, outgoingBlobWriter);
            }
            catch (Exception e)
            {
                SSPIError(e.Message + Environment.NewLine + e.StackTrace, TdsEnums.GEN_CLIENT_CONTEXT);
            }
        }

        protected void SSPIError(string error, string procedure)
        {
            Debug.Assert(!ADP.IsEmpty(procedure), "TdsParser.SSPIError called with an empty or null procedure string");
            Debug.Assert(!ADP.IsEmpty(error), "TdsParser.SSPIError called with an empty or null error string");

            _physicalStateObj.AddError(new SqlError(0, (byte)0x00, (byte)TdsEnums.MIN_ERROR_CLASS, _serverInfo.ResolvedServerName, error, procedure, 0));
            _parser.ThrowExceptionAndWarning(_physicalStateObj);
        }
    }
}
