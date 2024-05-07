#if NET6_0

using System;
using System.Buffers;
using Microsoft.Data.SqlClient.SNI;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class ManagedSSPIContextProvider : SSPIContextProvider
    {
        private SspiClientContextStatus? _sspiClientContextStatus;

        protected override void GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, byte[][] _sniSpnBuffer)
        {
            _sspiClientContextStatus ??= new SspiClientContextStatus();
            SNIProxy.GenSspiClientContext(_sspiClientContextStatus, incomingBlob, outgoingBlobWriter, _sniSpnBuffer);
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}", nameof(ManagedSSPIContextProvider), nameof(GenerateSspiClientContext), _physicalStateObj.SessionId);
        }
    }
}
#endif
