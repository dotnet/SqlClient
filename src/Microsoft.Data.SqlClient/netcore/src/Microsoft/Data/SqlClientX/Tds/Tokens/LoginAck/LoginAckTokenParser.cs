// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.LoginAck
{
    internal class LoginAckTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            _ = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false); // length

            byte type = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            SqlInterfaceType interfaceType = LoginAckTokenParser.GetSqlInterfaceType(type);

            uint version = await tdsStream.TdsReader.ReadUInt32BEAsync(isAsync, ct).ConfigureAwait(false);
            TdsVersion tdsVersion = LoginAckTokenParser.GetTdsVersion(version);
            tdsStream.TdsVersion = tdsVersion;

            string progName = await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false);
            byte major = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            byte minor = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            byte buildHi = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            byte buildLow = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            ProgVersion progVersion = new ProgVersion(major, minor, buildHi, buildLow);

            return new LoginAckToken(interfaceType, tdsVersion, progName, progVersion);
        }

        private static TdsVersion GetTdsVersion(uint tdsVersion)
        {
            if (Enum.IsDefined(typeof(TdsVersion), tdsVersion))
            {
                return (TdsVersion)tdsVersion;
            }

            // TODO Use proper exception format
            throw new InvalidOperationException($"Unknown Tds Version: {tdsVersion:X}");
        }

        private static SqlInterfaceType GetSqlInterfaceType(byte interfaceType)
        {
            if (interfaceType == (byte)SqlInterfaceType.Default)
            {
                return SqlInterfaceType.Default;
            }
            else if (interfaceType == (byte)SqlInterfaceType.TSql)
            {
                return SqlInterfaceType.TSql;
            }

            // TODO Use proper exception format
            throw new InvalidOperationException($"Unknown Sql Interface type: {interfaceType}");
        }
    }
}
