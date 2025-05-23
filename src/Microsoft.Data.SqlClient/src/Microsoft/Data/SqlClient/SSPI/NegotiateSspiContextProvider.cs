#if NET

using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Security;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSspiContextProvider : SspiContextProvider, IDisposable
    {
        private NegotiateAuthentication? _negotiateAuth;

        protected override bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
        {
            NegotiateAuthenticationStatusCode statusCode = NegotiateAuthenticationStatusCode.UnknownCredentials;

            _negotiateAuth = GetNegotiateAuthenticationForParams(authParams);

            var sendBuff = _negotiateAuth.GetOutgoingBlob(incomingBlob, out statusCode)!;

            // Log session id, status code and the actual SPN used in the negotiation
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}, SPN={4}", nameof(NegotiateSspiContextProvider),
                nameof(GenerateSspiClientContext), _physicalStateObj.SessionId, statusCode, _negotiateAuth.TargetName);

            if (statusCode == NegotiateAuthenticationStatusCode.Completed || statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                outgoingBlobWriter.Write(sendBuff);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            _negotiateAuth?.Dispose();
        }

        private NegotiateAuthentication GetNegotiateAuthenticationForParams(SspiAuthenticationParameters authParams)
        {
            if (_negotiateAuth is { })
            {
                if (string.Equals(_negotiateAuth.TargetName, authParams.Resource, StringComparison.Ordinal))
                {
                    return _negotiateAuth;
                }

                // Dispose of it since we're not going to use it now
                _negotiateAuth.Dispose();
            }

            return _negotiateAuth = new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = authParams.Resource });
        }
    }
}
#endif
