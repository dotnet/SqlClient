#if NET8_0_OR_GREATER

using System;
using System.Net.Security;
using System.Buffers;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSSPIContextProvider : SSPIContextProvider
    {
        private NegotiateAuthentication? _negotiateAuth = null;

        protected override void GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter)
        {
            _negotiateAuth ??= new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = AuthenticationParameters.ServerName });
            var result = _negotiateAuth.GetOutgoingBlob(incomingBlob, out NegotiateAuthenticationStatusCode statusCode)!;
            SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.GenerateSspiClientContext | Info | Session Id {0}, StatusCode={1}", _physicalStateObj.SessionId, statusCode);
            if (statusCode is not NegotiateAuthenticationStatusCode.Completed and not NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                throw new InvalidOperationException(SQLMessage.SSPIGenerateError() + Environment.NewLine + statusCode);
            }

            if (result is { })
            {
                outgoingBlobWriter.Write(result);
            }
        }
    }
}
#endif
