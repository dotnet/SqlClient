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

        internal void Initialize(ServerInfo serverInfo, TdsParserStateObject physicalStateObj, TdsParser parser)
        {
            _parser = parser;
            _physicalStateObj = physicalStateObj;
            _serverInfo = serverInfo;

            Initialize();
        }

        private protected virtual void Initialize()
        {
        }

        protected abstract bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SqlAuthenticationParameters authParams, ReadOnlySpan<string> serverNames);

        internal void SSPIData(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter, string serverNames)
            => SSPIData(receivedBuff, outgoingBlobWriter, new[] { serverNames });

        internal void SSPIData(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter, string[] serverNames)
        {
            using (TrySNIEventScope.Create(nameof(SSPIContextProvider)))
            {
                try
                {
                    if (GenerateSspiClientContext(receivedBuff, outgoingBlobWriter, CreateSqlAuthParams(_parser.Connection, serverNames[0]), serverNames))
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    SSPIError(e.Message + Environment.NewLine + e.StackTrace, TdsEnums.GEN_CLIENT_CONTEXT);
                }
            }
        }

        private static SqlAuthenticationParameters CreateSqlAuthParams(SqlInternalConnectionTds connection, string serverName)
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

        protected void SSPIError(string error, string procedure)
        {
            Debug.Assert(!ADP.IsEmpty(procedure), "TdsParser.SSPIError called with an empty or null procedure string");
            Debug.Assert(!ADP.IsEmpty(error), "TdsParser.SSPIError called with an empty or null error string");

            _physicalStateObj.AddError(new SqlError(0, (byte)0x00, (byte)TdsEnums.MIN_ERROR_CLASS, _serverInfo.ResolvedServerName, error, procedure, 0));
            _parser.ThrowExceptionAndWarning(_physicalStateObj);
        }
    }
}
