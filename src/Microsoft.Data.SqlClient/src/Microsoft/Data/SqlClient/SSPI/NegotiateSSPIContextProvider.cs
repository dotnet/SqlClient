#if NET8_0_OR_GREATER

using System;
using System.Text;
using System.Net.Security;
using System.Buffers;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSSPIContextProvider : SSPIContextProvider
    {
        protected override void GenerateSspiClientContext(ReadOnlyMemory<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, byte[][] _sniSpnBuffer)
        {
            for (int i = 0; i < _sniSpnBuffer.Length; i++)
            {
                var negotiateAuth = new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = Encoding.Unicode.GetString(_sniSpnBuffer[i]) });
                var result = _negotiateAuth.GetOutgoingBlob(incomingBlob.Span, out var statusCode)!;
                // Log session id, status code and the actual SPN used in the negotiation
                SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}, SPN={4}", nameof(NegotiateSSPIContextProvider),
                    nameof(GenerateSspiClientContext), _physicalStateObj.SessionId, statusCode, _negotiateAuth.TargetName);
                if (statusCode == NegotiateAuthenticationStatusCode.Completed || statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
                {
                    outgoingBlobWriter.Write(result);
                }
            }

            throw new InvalidOperationException(SQLMessage.SSPIGenerateError() + Environment.NewLine + statusCode);
        }
    }
}
#endif
