// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        protected override bool GenerateContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
        {
            var negotiateAuth = GetNegotiateAuthenticationForParams(authParams);
            var sendBuff = negotiateAuth.GetOutgoingBlob(incomingBlob, out var statusCode)!;

            // Log session id, status code and the actual SPN used in the negotiation
            SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | Session Id {2}, StatusCode={3}, SPN={4}", nameof(NegotiateSspiContextProvider),
                nameof(GenerateContext), _physicalStateObj.SessionId, statusCode, negotiateAuth.TargetName);

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
