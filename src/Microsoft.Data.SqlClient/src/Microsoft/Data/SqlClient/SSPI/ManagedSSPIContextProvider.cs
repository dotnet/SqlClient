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

        protected override void GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter)
        {
            _sspiClientContextStatus ??= new SspiClientContextStatus();

            SNIProxy.GenSspiClientContext(_sspiClientContextStatus, incomingBlob, outgoingBlobWriter, _serverNames);
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.GenerateSspiClientContext | Info | Session Id {0}", _physicalStateObj.SessionId);
        }
    }
}
#endif
