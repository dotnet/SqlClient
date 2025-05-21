using System;
using System.Buffers;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal abstract class SspiContextProvider
    {
        private TdsParser _parser = null!;
        private ServerInfo _serverInfo = null!;

        private SspiAuthenticationParameters? _authParam;

        private protected TdsParserStateObject _physicalStateObj = null!;

        internal void Initialize(
            ServerInfo serverInfo,
            TdsParserStateObject physicalStateObj,
            TdsParser parser,
            string serverSpn
            )
        {
            _parser = parser;
            _physicalStateObj = physicalStateObj;
            _serverInfo = serverInfo;

            var options = parser.Connection.ConnectionOptions;

            SqlClientEventSource.Log.StateDumpEvent("<SspiContextProvider> Initializing provider {0} with SPN={1}", GetType().FullName, serverSpn);

            _authParam = new SspiAuthenticationParameters(options.DataSource, serverSpn)
            {
                DatabaseName = options.InitialCatalog,
                UserId = options.UserID,
                Password = options.Password,
            };

            Initialize();
        }

        private protected virtual void Initialize()
        {
        }

        protected abstract bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams);

        internal void WriteSSPIContext(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter)
        {
            using var _ = TrySNIEventScope.Create(nameof(SspiContextProvider));

            if (!(_authParam is { } && RunGenerateSspiClientContext(receivedBuff, outgoingBlobWriter, _authParam)))
            {
                // If we've hit here, the SSPI context provider implementation failed to generate the SSPI context.
                SSPIError(SQLMessage.SSPIGenerateError(), TdsEnums.GEN_CLIENT_CONTEXT);
            }
        }

        private bool RunGenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
        {
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | SPN={1}", GetType().FullName, nameof(GenerateSspiClientContext), authParams.Resource);

                return GenerateSspiClientContext(incomingBlob, outgoingBlobWriter, authParams);
            }
            catch (Exception e)
            {
                SSPIError(e.Message + Environment.NewLine + e.StackTrace, TdsEnums.GEN_CLIENT_CONTEXT);
                return false;
            }
        }

        protected void SSPIError(string error, string procedure)
        {
            Debug.Assert(!string.IsNullOrEmpty(procedure), "TdsParser.SSPIError called with an empty or null procedure string");
            Debug.Assert(!string.IsNullOrEmpty(error), "TdsParser.SSPIError called with an empty or null error string");

            _physicalStateObj.AddError(new SqlError(0, (byte)0x00, (byte)TdsEnums.MIN_ERROR_CLASS, _serverInfo.ResolvedServerName, error, procedure, 0));
            _parser.ThrowExceptionAndWarning(_physicalStateObj);
        }
    }
}
