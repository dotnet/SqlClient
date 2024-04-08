#if NET7_0_OR_GREATER

using System;
using System.Text;
using System.Net.Security;
using System.Buffers;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSSPIContextProvider : SSPIContextProvider
    {
        private NegotiateAuthentication? _negotiateAuth = null;

        protected override void GenerateSspiClientContext(ReadOnlyMemory<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, byte[][] _sniSpnBuffer)
        {
            _negotiateAuth ??= new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = Encoding.Unicode.GetString(_sniSpnBuffer[0]) });
            var result = _negotiateAuth.GetOutgoingBlob(incomingBlob.Span, out NegotiateAuthenticationStatusCode statusCode)!;
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
