﻿#if NET8_0_OR_GREATER

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
            _negotiateAuth ??= new(new NegotiateAuthenticationClientOptions { Package = "Negotiate", TargetName = Encoding.Unicode.GetString(_sniSpnBuffer[0]) });
            sendBuff = _negotiateAuth.GetOutgoingBlob(received.Span, out NegotiateAuthenticationStatusCode statusCode)!;
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}", nameof(NegotiateSSPIContextProvider),
                nameof(GenerateSspiClientContext), _physicalStateObj.SessionId, statusCode);
            if (statusCode is not NegotiateAuthenticationStatusCode.Completed and not NegotiateAuthenticationStatusCode.ContinueNeeded)
            {
                throw new InvalidOperationException(SQLMessage.SSPIGenerateError() + Environment.NewLine + statusCode);
            }
            sendLength = (uint)(sendBuff != null ? sendBuff.Length : 0);
        }
    }
}
#endif
