using System;
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

        internal virtual uint MaxSSPILength => 4096; // TODO: what is a good default here?

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

        internal abstract void GenerateSspiClientContext(ReadOnlyMemory<byte> input, ref byte[] sendBuff, ref uint sendLength, byte[][] _sniSpnBuffer);

        internal void SSPIData(ReadOnlyMemory<byte> receivedBuff, ref byte[] sendBuff, ref UInt32 sendLength, byte[] sniSpnBuffer)
            => SSPIData(receivedBuff, ref sendBuff, ref sendLength, new[] { sniSpnBuffer });

        internal void SSPIData(ReadOnlyMemory<byte> receivedBuff, ref byte[] sendBuff, ref UInt32 sendLength, byte[][] sniSpnBuffer)
        {
            using (TrySNIEventScope.Create(nameof(SSPIContextProvider)))
            {
                try
                {
                    GenerateSspiClientContext(receivedBuff, ref sendBuff, ref sendLength, sniSpnBuffer);
                }
                catch (Exception e)
                {
                    SSPIError(e.Message + Environment.NewLine + e.StackTrace, TdsEnums.GEN_CLIENT_CONTEXT);
                }
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
