// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.FeatureExtAck
{
    internal sealed class FeatureExtAckTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            byte byteFeatureId;
            List<FeatureExtAckToken> features = new();
            do
            {
                byteFeatureId = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

                if (byteFeatureId != TdsEnums.FEATUREEXT_TERMINATOR)
                {
                    if (!Enum.IsDefined(typeof(FeatureId), byteFeatureId))
                    {
                        // TODO Log and continue
                    }
                    FeatureId featureId = (FeatureId)byteFeatureId;

                    uint dataLength = await tdsStream.TdsReader.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
                    ByteBuffer featureData = dataLength > 0
                        ? await tdsStream.TdsReader.ReadBufferAsync((int)dataLength, isAsync, ct).ConfigureAwait(false)
                        : null;

                    features.Add(new FeatureExtAckToken((FeatureId)featureId, featureData));
                }
            } while (byteFeatureId != TdsEnums.FEATUREEXT_TERMINATOR);

            return new FeatureExtAckTokens(features);
        }
    }
}
