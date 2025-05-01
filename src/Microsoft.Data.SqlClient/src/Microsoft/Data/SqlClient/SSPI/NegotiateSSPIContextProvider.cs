#if NET

using System;
using System.Buffers;
using System.Net.Security;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSSPIContextProvider : SSPIContextProvider
    {
        private NegotiateAuthentication? _negotiateAuth = null;

        protected override bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
        {
            NegotiateAuthenticationStatusCode statusCode = NegotiateAuthenticationStatusCode.UnknownCredentials;

            _negotiateAuth ??= new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = authParams.ServerName });
            var sendBuff = _negotiateAuth.GetOutgoingBlob(incomingBlob, out statusCode)!;

            // Log session id, status code and the actual SPN used in the negotiation
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}, SPN={4}", nameof(NegotiateSSPIContextProvider),
                nameof(GenerateSspiClientContext), _physicalStateObj.SessionId, statusCode, _negotiateAuth.TargetName);

            if (statusCode == NegotiateAuthenticationStatusCode.Completed || statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                outgoingBlobWriter.Write(sendBuff);
                return true;
            }

            return false;
        }
    }
}
#endif
