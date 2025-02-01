#if NET

using System;
using System.Buffers;
using System.Net.Security;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSSPIContextProvider : SSPIContextProvider
    {
        protected override bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SqlAuthenticationParameters authParams)
        {
            NegotiateAuthenticationStatusCode statusCode = NegotiateAuthenticationStatusCode.UnknownCredentials;

            var negotiateAuth = new NegotiateAuthentication(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = authParams.ServerName });
            var sendBuff = negotiateAuth.GetOutgoingBlob(incomingBlob, out statusCode)!;

            // Log session id, status code and the actual SPN used in the negotiation
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}, SPN={4}", nameof(NegotiateSSPIContextProvider),
                nameof(GenerateSspiClientContext), _physicalStateObj.SessionId, statusCode, negotiateAuth.TargetName);

            if (statusCode == NegotiateAuthenticationStatusCode.Completed || statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                outgoingBlobWriter.Write(sendBuff);
                return true; // Successful case, exit the loop with current SPN.
            }

            return false;
        }
    }
}
#endif
