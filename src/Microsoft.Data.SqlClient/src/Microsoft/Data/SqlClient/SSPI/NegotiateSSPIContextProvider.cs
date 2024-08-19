#if NET8_0_OR_GREATER

using System;
using System.Buffers;
using System.Net.Security;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSSPIContextProvider : SSPIContextProvider
    {
        protected override bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SqlAuthenticationParameters authParams, ReadOnlySpan<string> serverNames)
        {
            NegotiateAuthenticationStatusCode statusCode = default;

            for (int i = 0; i < serverNames.Length; i++)
            {
                var negotiateAuth = new NegotiateAuthentication(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = serverNames[i] });
                var result = negotiateAuth.GetOutgoingBlob(incomingBlob, out statusCode);

                // Log session id, status code and the actual SPN used in the negotiation
                SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}, SPN={4}", nameof(NegotiateSSPIContextProvider),
                    nameof(GenerateSspiClientContext), _physicalStateObj.SessionId, statusCode, negotiateAuth.TargetName);

                if (statusCode == NegotiateAuthenticationStatusCode.Completed || statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
                {
                    outgoingBlobWriter.Write(result);
                    return true;
                }
            }

            throw new InvalidOperationException(SQLMessage.SSPIGenerateError() + Environment.NewLine + statusCode);
        }
    }
}
#endif
