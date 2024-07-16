#if NET6_0

using System;
using Microsoft.Data.SqlClient.SNI;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class ManagedSSPIContextProvider : SSPIContextProvider
    {
        private SspiClientContextStatus? _sspiClientContextStatus;

        internal override void GenerateSspiClientContext(ReadOnlyMemory<byte> received, ref byte[] sendBuff, ref uint sendLength, byte[][] _sniSpnBuffer)
        {
            _sspiClientContextStatus ??= new SspiClientContextStatus();

            SNIProxy.GenSspiClientContext(_sspiClientContextStatus, received, ref sendBuff, _sniSpnBuffer);
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}", nameof(ManagedSSPIContextProvider), nameof(GenerateSspiClientContext), _physicalStateObj.SessionId);
            sendLength = (uint)(sendBuff != null ? sendBuff.Length : 0);
        }
    }
}
#endif
