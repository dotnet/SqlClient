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

        protected abstract void GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, ReadOnlySpan<string> serverSpns);

        internal void SSPIData(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter, string serverSpn)
            => SSPIData(receivedBuff, outgoingBlobWriter, new[] { serverSpn });

        internal void SSPIData(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter, string[] serverSpns)
        {
            using (TrySNIEventScope.Create(nameof(SSPIContextProvider)))
            {
                try
                {
                    GenerateSspiClientContext(receivedBuff, outgoingBlobWriter, serverSpns);
                }
                catch (Exception e)
                {
                    SSPIError(e.Message + Environment.NewLine + e.StackTrace, TdsEnums.GEN_CLIENT_CONTEXT);
                }
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
