#if NET8_0_OR_GREATER

using System;
using System.Text;
using System.Net.Security;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class NegotiateSSPIContextProvider : SSPIContextProvider
    {
        private NegotiateAuthentication? _negotiateAuth = null;

        internal override void GenerateSspiClientContext(ReadOnlyMemory<byte> received, ref byte[] sendBuff, ref uint sendLength, byte[][] _sniSpnBuffer)
        {
            NegotiateAuthenticationStatusCode statusCode = NegotiateAuthenticationStatusCode.UnknownCredentials;

            for (int i = 0; i < _sniSpnBuffer.Length; i++)
            {
                string spnName = Encoding.Unicode.GetString(_sniSpnBuffer[i]);
                _negotiateAuth ??= new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = spnName });
                sendBuff = _negotiateAuth.GetOutgoingBlob(received.Span, out statusCode)!;
                // Log session id, status code and the actual SPN used in the negotiation
                SqlClientEventSource.Log.TryTraceEvent("TdsParserStateObjectManaged.GenerateSspiClientContext | Info | Session Id {0}, StatusCode={1}, SPN={2}", _physicalStateObj.SessionId, statusCode, _negotiateAuth.TargetName);

                if (statusCode == NegotiateAuthenticationStatusCode.Completed || statusCode == NegotiateAuthenticationStatusCode.ContinueNeeded)
                    break; // Successful case, exit the loop with current SPN.
                else
                    _negotiateAuth = null; // Reset _negotiateAuth to be generated again for next SPN.
            }

            if (statusCode is not NegotiateAuthenticationStatusCode.Completed and not NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                throw new InvalidOperationException(SQLMessage.SSPIGenerateError() + Environment.NewLine + statusCode);
            }

            sendLength = (uint)(sendBuff != null ? sendBuff.Length : 0);
        }
    }
}
#endif
