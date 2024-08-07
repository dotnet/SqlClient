// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.FedAuthInfo
{
    internal class FedAuthInfoTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            uint tokenLength = await tdsStream.TdsReader.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
            ByteBuffer buffer = await tdsStream.TdsReader.ReadBufferAsync((int)tokenLength, isAsync, ct).ConfigureAwait(false);
            int offset = 0;

            uint countOfIds = buffer.ReadUInt32LE(offset);
            offset += sizeof(uint);

            string spn = null;
            string stsUrl = null;

            for (int i = 0; i < countOfIds; i++)
            {
                byte fedAuthInfoId = buffer.ReadUInt8(offset);
                offset += sizeof(byte);

                uint fedAuthInfoDataLength = buffer.ReadUInt32LE(offset);
                offset += sizeof(uint);

                uint fedAuthInfoDataOffset = buffer.ReadUInt32LE(offset);
                offset += sizeof(uint);

                if (fedAuthInfoId == (byte)FedAuthInfoId.SPN)
                {
                    spn = Encoding.Unicode.GetString(buffer.ToArraySegment().Array, (int)fedAuthInfoDataOffset, (int)fedAuthInfoDataLength);
                }
                else if (fedAuthInfoId == (byte)FedAuthInfoId.STSUrl)
                {
                    stsUrl = Encoding.Unicode.GetString(buffer.ToArraySegment().Array, (int)fedAuthInfoDataOffset, (int)fedAuthInfoDataLength);
                }
            }

            return new FedAuthInfoToken(spn, stsUrl);
        }
    }
}
