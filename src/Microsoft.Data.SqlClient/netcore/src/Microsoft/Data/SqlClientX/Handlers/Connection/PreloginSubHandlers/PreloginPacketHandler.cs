// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    internal class PreloginPacketHandler : IHandler<PreloginHandlerContext>
    {
        const int GUID_SIZE = 16;

        const int PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES = 4;

        const int PAYLOAD_LENGTH_SIZE_IN_BYTES = 2;

        private static readonly int[] s_optionsLength = new int[(int)PreLoginOptions.NUMOPT];

        private static int s_optionsPayloadLength;

        /// <summary>
        /// Static constructor for initializing the Prelogin options length.
        /// </summary>
        static PreloginPacketHandler()
        {
            s_optionsLength[(int)PreLoginOptions.VERSION] = 6;
            s_optionsLength[(int)PreLoginOptions.ENCRYPT] = 1; // ENCRYPT
            s_optionsLength[(int)PreLoginOptions.INSTANCE] = 1; // INSTANCE
            s_optionsLength[(int)PreLoginOptions.THREADID] = 4; // THREADID
            s_optionsLength[(int)PreLoginOptions.MARS] = 1; // MARS
            s_optionsLength[(int)PreLoginOptions.TRACEID] = GUID_SIZE + GUID_SIZE + sizeof(uint); // TRACEID
            s_optionsLength[(int)PreLoginOptions.FEDAUTHREQUIRED] = 1; // FEDAUTHREQUIRED
            
            // Calculate the payload length.
            for (int i = 0; i < (int)PreLoginOptions.NUMOPT; i++)
            {
                s_optionsPayloadLength += s_optionsLength[i];
            }
        }

        // EventSource counter
        private static int s_objectTypeCount;

        internal readonly int _objectID = Interlocked.Increment(ref s_objectTypeCount);

        internal int ObjectID => _objectID;

        public IHandler<PreloginHandlerContext> NextHandler { get; set; }

        public async ValueTask Handle(PreloginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            await PreloginPacketHandler.CreatePreloginAndSend(context, isAsync, ct).ConfigureAwait(false);
            
            await ReadPreloginResponse(context, isAsync, ct).ConfigureAwait(false);

            if (NextHandler is not null)
            {
                await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Consume the Prelogin response from the server.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ReadPreloginResponse(PreloginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            context.ConnectionContext.MarsCapable = context.ConnectionContext.ConnectionString.MARS; // Assign default value
            context.ConnectionContext.FedAuthRequired = false;

            TdsStream tdsStream = context.ConnectionContext.TdsStream;

            // Force a read on the TDS stream;
            _ = await tdsStream.PeekByteAsync(isAsync, ct).ConfigureAwait(false);
            
            byte[] preloginPayload = new byte[tdsStream.PacketDataLeft];

            ct.ThrowIfCancellationRequested();
            if (isAsync)
            {
                _ = await tdsStream.ReadAsync(preloginPayload.AsMemory(), ct).ConfigureAwait(false);
            }
            else
            {
                tdsStream.Read(preloginPayload);
            }

            int payloadOffset = 0;
            int offset = 0;
            PreLoginOptions option = (PreLoginOptions)preloginPayload[offset++];
            while (option != PreLoginOptions.LASTOPT)
            {
                switch (option)
                {
                    case PreLoginOptions.VERSION:
                        // Nothing to do with the version.
                        offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES; // Skip the payload offset and length
                        break;

                    case PreLoginOptions.ENCRYPT:
                        if (context.IsTlsFirst)
                        {
                            // Can skip/ignore this option if we are doing TDS 8.
                            // The internal encryption option is set to NOT_SUP in this case.
                            // And it shouldn't be changed.
                            offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES;
                            break;
                        }

                        payloadOffset = BinaryPrimitives.ReadUInt16BigEndian(preloginPayload.AsSpan(offset, 2));
                        offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES; // Skip the payload length

                        EncryptionOptions serverOption = (EncryptionOptions)preloginPayload[payloadOffset];

                        // Any response other than NOT_SUP means the server supports encryption.
                        context.ServerSupportsEncryption = serverOption != EncryptionOptions.NOT_SUP;

                        // If server doesn't support encryption, then we can't proceed, since encryption is always needed
                        // for login.
                        if (!context.ServerSupportsEncryption)
                        {
                            SqlErrorCollection errors = context.ConnectionContext.ErrorCollection;
                            string serverName = context.ConnectionContext.ServerInfo.ResolvedServerName;
                            errors.Add(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, serverName, SQLMessage.EncryptionNotSupportedByServer(), "", 0));
                            throw SqlException.CreateException(errors, null);
                        }

                        switch (context.InternalEncryptionOption)
                        {
                            case (EncryptionOptions.OFF):
                                if (serverOption == EncryptionOptions.OFF)
                                {
                                    // Encrypt login even if the server doesn't enforce encryption.
                                    context.InternalEncryptionOption = EncryptionOptions.LOGIN;
                                }
                                else if (serverOption == EncryptionOptions.REQ)
                                {
                                    // Server has enforced encryption, hence we encrypt everything.
                                    context.InternalEncryptionOption = EncryptionOptions.ON;
                                }
                                // NOT_SUP: No encryption.
                                break;

                            case (EncryptionOptions.NOT_SUP):
                                if (serverOption == EncryptionOptions.REQ)
                                {
                                    SqlErrorCollection collection = context.ConnectionContext.ErrorCollection;
                                    string serverName = context.ConnectionContext.ServerInfo.ResolvedServerName;
                                    collection.Add(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, serverName, SQLMessage.EncryptionNotSupportedByClient(), "", 0));
                                    throw SqlException.CreateException(collection, null);
                                }
                                break;
                            default:
                                break;
                        }

                        break;

                    case PreLoginOptions.INSTANCE:
                        payloadOffset = BinaryPrimitives.ReadUInt16BigEndian(preloginPayload.AsSpan(offset, 2));
                        offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES;

                        byte ERROR_INST = 0x1;
                        byte instanceResult = preloginPayload[payloadOffset];

                        if (instanceResult == ERROR_INST)
                        {
                            // Check if server says ERROR_INST. That either means the cached info
                            // we used to connect is not valid or we connected to a named instance
                            // listening on default params.
                            context.HandshakeStatus = PreLoginHandshakeStatus.InstanceFailure;
                        }

                        break;

                    case PreLoginOptions.THREADID:
                        // DO NOTHING FOR THREADID
                        offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES;
                        break;

                    case PreLoginOptions.MARS:
                        payloadOffset = BinaryPrimitives.ReadUInt16BigEndian(preloginPayload.AsSpan(offset, 2));
                        offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES;

                        context.ConnectionContext.MarsCapable = (preloginPayload[payloadOffset] != 0);
                        Debug.Assert(preloginPayload[payloadOffset] == 0 || preloginPayload[payloadOffset] == 1, "Value for Mars PreLoginHandshake option not equal to 1 or 0!");
                        break;

                    case PreLoginOptions.TRACEID:
                        // DO NOTHING FOR TRACEID
                        offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES;
                        break;

                    case PreLoginOptions.FEDAUTHREQUIRED:
                        payloadOffset = BinaryPrimitives.ReadUInt16BigEndian(preloginPayload.AsSpan(offset, 2));
                        offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES;

                        // Only 0x00 and 0x01 are accepted values from the server.
                        if (preloginPayload[payloadOffset] != 0x00 && preloginPayload[payloadOffset] != 0x01)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}|ERR> {1}, " +
                                "Server sent an unexpected value for FedAuthRequired PreLogin Option. Value was {2}.", nameof(ReadPreloginResponse), ObjectID, (int)preloginPayload[payloadOffset]);
                            throw SQL.ParsingErrorValue(ParsingErrorState.FedAuthRequiredPreLoginResponseInvalidValue, (int)preloginPayload[payloadOffset]);
                        }

                        // We must NOT use the response for the FEDAUTHREQUIRED PreLogin option, if the connection string option
                        // was not using the new Authentication keyword or in other words, if Authentication=NotSpecified
                        // Or AccessToken is not null, mean token based authentication is used.
                        if ((context.ConnectionContext.ConnectionString != null
                            && context.ConnectionContext.ConnectionString.Authentication != SqlAuthenticationMethod.NotSpecified)
                            || context.ConnectionContext.AccessTokenInBytes != null || context.ConnectionContext.AccessTokenCallback != null)
                        {
                            context.ConnectionContext.FedAuthRequired = preloginPayload[payloadOffset] == 0x01;
                        }
                        break;

                    default:
                        Debug.Fail("UNKNOWN option in ConsumePreLoginHandshake, option:" + option);
                        // DO NOTHING FOR THESE UNKNOWN OPTIONS
                        offset += PAYLOAD_OFFSET_AND_LENGTH_SIZE_IN_BYTES;
                        break;
                }

                if (offset < preloginPayload.Length)
                {
                    option = (PreLoginOptions)preloginPayload[offset++];
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Constructs the Prelogin packet and sends it to the server.
        /// </summary>
        /// <param name="context">The Prelogin handler context</param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private static async Task CreatePreloginAndSend(PreloginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(context.ConnectionContext.TdsStream != null, "A Tds Stream is expected");

            TdsStream tdsStream = context.ConnectionContext.TdsStream;
            tdsStream.PacketHeaderType = TdsStreamPacketType.PreLogin;

            // Initialize option offset into payload buffer
            // 5 bytes for each option (1 byte length, 2 byte offset, 2 byte payload length)
            int optionsHeaderSize = (int)PreLoginOptions.NUMOPT * 5;

            int offset = optionsHeaderSize + 1;

            // Create the payload buffer with the options header and options payload length, which 
            // is precalculated.
            byte[] payload = new byte[optionsHeaderSize + 1 + s_optionsPayloadLength];

            for (int option = (int)PreLoginOptions.VERSION; option < (int)PreLoginOptions.NUMOPT; option++)
            {
                int optionHeaderOffset = option * 5;
                payload[optionHeaderOffset] = (byte)option;
                payload[optionHeaderOffset + 1] = (byte)((offset & 0xff00) >> 8);
                payload[optionHeaderOffset + 2] = (byte)(offset & 0x00ff);
                payload[optionHeaderOffset + 3] = (byte)((s_optionsLength[option] & 0xff00) >> 8);
                payload[optionHeaderOffset + 4] = (byte)(s_optionsLength[option] & 0x00ff);
                
                switch (option)
                {
                    case (int)PreLoginOptions.VERSION:
                        Version systemDataVersion = ADP.GetAssemblyVersion();

                        // Major and minor
                        payload[offset++] = (byte)(systemDataVersion.Major & 0xff);
                        payload[offset++] = (byte)(systemDataVersion.Minor & 0xff);

                        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(offset, 2), (ushort)systemDataVersion.Build);
                        offset += 2;
                        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), (ushort)systemDataVersion.Revision);
                        offset += 2;
                        break;

                    case (int)PreLoginOptions.ENCRYPT:
                        if (context.InternalEncryptionOption == EncryptionOptions.NOT_SUP)
                        {
                            //If OS doesn't support encryption and encryption is not required, inform server "not supported" by client.
                            payload[offset++] = (byte)EncryptionOptions.NOT_SUP;
                        }
                        else
                        {
                            // Else, inform server of user request.
                            if (context.ConnectionEncryptionOption == SqlConnectionEncryptOption.Mandatory)
                            {
                                payload[offset++] = (byte)EncryptionOptions.ON;
                                context.InternalEncryptionOption = EncryptionOptions.ON;
                            }
                            else
                            {
                                payload[offset++] = (byte)EncryptionOptions.OFF;
                                context.InternalEncryptionOption = EncryptionOptions.OFF;
                            }
                        }

                        break;

                    case (int)PreLoginOptions.INSTANCE:
                        // We send an empty instance name to the server. 
                        payload[offset++] = 0; // null terminate
                        break;

                    case (int)PreLoginOptions.THREADID:
                        int threadID = TdsParserStaticMethods.GetCurrentThreadIdForTdsLoginOnly();
                        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(offset, 4), (uint)threadID);
                        offset += 4;
                        break;

                    case (int)PreLoginOptions.MARS:
                        payload[offset++] = (byte)(context.ConnectionContext.ConnectionString.MARS ? 1 : 0);
                        break;

                    case (int)PreLoginOptions.TRACEID:
                        context.ConnectionContext.ConnectionId.TryWriteBytes(payload.AsSpan(offset, GUID_SIZE));
                        offset += GUID_SIZE;

                        ActivityCorrelator.ActivityId actId = ActivityCorrelator.Next();
                        actId.Id.TryWriteBytes(payload.AsSpan(offset, GUID_SIZE));
                        offset += GUID_SIZE;
                        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), actId.Sequence);
                        offset += 4;
                        SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.SendPreLoginHandshake|INFO> ClientConnectionID {0}, ActivityID {1}",
                            context.ConnectionContext.ConnectionId, actId);
                        break;

                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        payload[offset++] = 0x01;
                        break;

                    default:
                        Debug.Fail("UNKNOWN option in SendPreLoginHandshake");
                        break;
                }
            }

            // Write out last option.
            payload[optionsHeaderSize] = (byte)PreLoginOptions.LASTOPT;

            // Write the packet to the stream.
            ct.ThrowIfCancellationRequested();
            if (isAsync)
            {
                await tdsStream.WriteAsync(payload.AsMemory(0, offset), ct).ConfigureAwait(false);
            }
            else
            {
                tdsStream.Write(payload.AsSpan(0, offset));
            }

            // Flush packet
            ct.ThrowIfCancellationRequested();
            if (isAsync)
            {
                await tdsStream.FlushAsync(ct).ConfigureAwait(false);
            }
            else
            {
                tdsStream.Flush();
            }
        }
    }
}
