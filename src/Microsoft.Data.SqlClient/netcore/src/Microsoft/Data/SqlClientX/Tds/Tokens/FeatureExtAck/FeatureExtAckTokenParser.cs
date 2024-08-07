// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.FeatureExtAck
{
    internal sealed class FeatureExtAckTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            byte byteFeatureId = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            if (!Enum.IsDefined(typeof(FeatureId), byteFeatureId))
            {
                throw new InvalidOperationException($"Invalid FeatureId: 0x{byteFeatureId:X2}");
            }

            FeatureId featureId = (FeatureId)byteFeatureId;

            if (featureId == FeatureId.Terminator)
            {
                return new FeatureExtAckToken((FeatureId)featureId);
            }

            uint dataLength = await tdsStream.TdsReader.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
            return new FeatureExtAckToken((FeatureId)featureId, await tdsStream.TdsReader.ReadBufferAsync((int)dataLength, isAsync, ct).ConfigureAwait(false));
        }
    }
}
