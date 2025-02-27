#if NET

using System;
using System.Net.Security;
using System.Buffers;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSSPIContextProvider : SSPIContextProvider
    {
        private NegotiateAuthentication? _negotiateAuth = null;

        protected override void GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, ReadOnlySpan<string> serverSpns)
        {
            NegotiateAuthenticationStatusCode statusCode = NegotiateAuthenticationStatusCode.UnknownCredentials;

            for (int i = 0; i < serverSpns.Length; i++)
            {
                _negotiateAuth ??= new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = serverSpns[i] });
                var sendBuff = _negotiateAuth.GetOutgoingBlob(incomingBlob, out statusCode)!;

                // Log session id, status code and the actual SPN used in the negotiation
                SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}, SPN={4}", nameof(NegotiateSSPIContextProvider),
                    nameof(GenerateSspiClientContext), _physicalStateObj.SessionId, statusCode, _negotiateAuth.TargetName);
                if (statusCode == NegotiateAuthenticationStatusCode.Completed || statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
                {
                    outgoingBlobWriter.Write(sendBuff);
                    break; // Successful case, exit the loop with current SPN.
                }
                else
                {
                    _negotiateAuth = null; // Reset _negotiateAuth to be generated again for next SPN.
                }
            }

            if (statusCode is not NegotiateAuthenticationStatusCode.Completed and not NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                throw new InvalidOperationException(SQLMessage.SSPIGenerateError() + Environment.NewLine + statusCode);
            }
        }
    }
}
#endif
