// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.EnvChange
{
    internal class EnvChangeTokenParser : TokenParser
    {
        private static readonly Dictionary<EnvChangeTokenSubType, Func<TdsStream, bool, CancellationToken, ValueTask<Token>>> subTypeParsers
            = new Dictionary<EnvChangeTokenSubType, Func<TdsStream, bool, CancellationToken, ValueTask<Token>>>
            {
                [EnvChangeTokenSubType.Database] = async (tdsStream, isAsync, ct) =>
                new DatabaseEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.PacketSize] = async (tdsStream, isAsync, ct) =>
                new PacketSizeEnvChangeToken(
                    newValue: int.Parse(await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false)),
                    oldValue: int.Parse(await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false))),

                [EnvChangeTokenSubType.CharacterSet] = async (tdsStream, isAsync, ct) =>
                new CharsetEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.DatabaseMirroringPartner] = async (tdsStream, isAsync, ct) =>
                new DatabaseMirroringPartnerEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.Language] = async (tdsStream, isAsync, ct) =>
                new LanguageEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarCharAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.SqlCollation] = async (tdsStream, isAsync, ct) =>
                new SqlCollationEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.BeginTransaction] = async (tdsStream, isAsync, ct) =>
                new BeginTransactionEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.CommitTransaction] = async (tdsStream, isAsync, ct) =>
                new CommitTransactionEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.RollbackTransaction] = async (tdsStream, isAsync, ct) =>
                new RollbackTransactionEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.ResetConnection] = async (tdsStream, isAsync, ct) =>
                new ResetConnectionEnvChangeToken(
                    newValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false),
                    oldValue: await tdsStream.TdsReader.ReadBVarByteAsync(isAsync, ct).ConfigureAwait(false)),

                [EnvChangeTokenSubType.Routing] = ParseRoutingEnvChange

            };

        private static async ValueTask<Token> ParseRoutingEnvChange(TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            ushort length = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            ByteBuffer buffer = await tdsStream.TdsReader.ReadBufferAsync(length, isAsync, ct).ConfigureAwait(false);

            byte protocol = buffer.ReadUInt8();
            ushort port = buffer.ReadUInt16LE(1);
            ushort serverLength = buffer.ReadUInt16LE(3);
            string server = Encoding.Unicode.GetString(buffer.Slice(5, serverLength * 2).ToArraySegment().Array);

            RoutingInfo newRoutingInfo = new RoutingInfo(protocol, port, server);

            length = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            RoutingInfo oldRoutingInfo = null;

            if (length > 0)
            {
                buffer = await tdsStream.TdsReader.ReadBufferAsync(length, isAsync, ct).ConfigureAwait(false);

                protocol = buffer.ReadUInt8();
                port = buffer.ReadUInt16LE();
                serverLength = buffer.ReadUInt16LE(3);
                server = Encoding.Unicode.GetString(buffer.Slice(5, serverLength * 2).ToArraySegment().Array);

                oldRoutingInfo = new RoutingInfo(protocol, port, server);
            }

            return new RoutingEnvChangeToken(oldRoutingInfo, newRoutingInfo);
        }


        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            ushort length = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            byte subType = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            if (!Enum.IsDefined(typeof(EnvChangeTokenSubType), subType))
            {
                throw new InvalidOperationException($"Unsupported EnvChange Token type: {subType}");
            }

            EnvChangeTokenSubType tokenSubType = (EnvChangeTokenSubType)subType;

            if (!subTypeParsers.ContainsKey(tokenSubType))
            {
                throw new InvalidOperationException($"Unsupported EnvChange Token type: {tokenSubType}");
            }

            return await subTypeParsers[tokenSubType](tdsStream, isAsync, ct).ConfigureAwait(false);
        }
    }
}
