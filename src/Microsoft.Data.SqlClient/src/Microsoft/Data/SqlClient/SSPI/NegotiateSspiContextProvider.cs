#if NET

using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Security;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSspiContextProvider : SspiContextProvider
    {
        private NegotiateAuthentication? _negotiateAuth = null;

        protected override bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
        {
            NegotiateAuthenticationStatusCode statusCode = NegotiateAuthenticationStatusCode.UnknownCredentials;

            _negotiateAuth ??= new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = authParams.Resource });

            Debug.Assert(_negotiateAuth.TargetName == authParams.Resource, "SSPI resource does not match TargetName");

            var sendBuff = _negotiateAuth.GetOutgoingBlob(incomingBlob, out statusCode)!;

            // Log session id, status code and the actual SPN used in the negotiation
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}, SPN={4}", nameof(NegotiateSspiContextProvider),
                nameof(GenerateSspiClientContext), _physicalStateObj.SessionId, statusCode, _negotiateAuth.TargetName);

            if (statusCode == NegotiateAuthenticationStatusCode.Completed || statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                outgoingBlobWriter.Write(sendBuff);
                return true;
            }

            // Reset _negotiateAuth to be generated again for next SPN.
            _negotiateAuth = null;
            return false;
        }
    }
}
#endif
