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

        protected abstract void GenerateSspiClientContext(ReadOnlyMemory<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, byte[][] _sniSpnBuffer);

        internal void SSPIData(ReadOnlyMemory<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter, byte[] sniSpnBuffer)
            => SSPIData(receivedBuff, outgoingBlobWriter, new[] { sniSpnBuffer });

        internal void SSPIData(ReadOnlyMemory<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter, byte[][] sniSpnBuffer)
        {
            try
            {
                GenerateSspiClientContext(receivedBuff, outgoingBlobWriter, sniSpnBuffer);
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
