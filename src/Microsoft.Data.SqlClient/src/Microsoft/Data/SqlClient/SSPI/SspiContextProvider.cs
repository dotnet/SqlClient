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

        protected abstract bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams);

        internal void SSPIData(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter, ReadOnlySpan<string> serverSpns)
        {
            using var _ = TrySNIEventScope.Create(nameof(SspiContextProvider));

            foreach (var serverSpn in serverSpns)
            {
                if (RunGenerateSspiClientContext(receivedBuff, outgoingBlobWriter, serverSpn))
                {
                    return;
                }
            }

            // If we've hit here, the SSPI context provider implementation failed to generate the SSPI context.
            SSPIError(SQLMessage.SSPIGenerateError(), TdsEnums.GEN_CLIENT_CONTEXT);
        }

        private bool RunGenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, string serverSpn)
        {
            var options = _parser.Connection.ConnectionOptions;
            var authParams = new SspiAuthenticationParameters(options.DataSource, serverSpn)
            {
                DatabaseName = options.InitialCatalog,
                UserId = options.UserID,
                Password = options.Password,
            };

            try
            {
                SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | SPN={1}", GetType().FullName, nameof(GenerateSspiClientContext), serverSpn);

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
