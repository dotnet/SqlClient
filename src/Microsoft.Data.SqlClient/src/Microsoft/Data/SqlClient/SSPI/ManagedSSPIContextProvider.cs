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

        protected override bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SqlAuthenticationParameters authParams, ReadOnlySpan<string> serverNames)
        {
            _sspiClientContextStatus ??= new SspiClientContextStatus();
            SNIProxy.GenSspiClientContext(_sspiClientContextStatus, incomingBlob, outgoingBlobWriter, serverNames.ToArray());
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}", nameof(ManagedSSPIContextProvider), nameof(GenerateSspiClientContext), _physicalStateObj.SessionId);
            return true;
        }
    }
}
#endif
