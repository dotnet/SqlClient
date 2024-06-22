// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    internal class PreloginPacketHandler : IHandler<PreLoginHandlerContext>
    {
        const int GUID_SIZE = 16;

        // EventSource counter
        private static int s_objectTypeCount;

        internal readonly int _objectID = Interlocked.Increment(ref s_objectTypeCount);

        internal int ObjectID => _objectID;

        public IHandler<PreLoginHandlerContext> NextHandler { get; set; }

        public async ValueTask Handle(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            await PreloginPacketHandler.CreatePreLoginAndSend(context, isAsync, ct).ConfigureAwait(false);
            if (context.HasError)
            {
                return;
            }
            await ReadPreLoginresponse(context, isAsync, ct).ConfigureAwait(false);

            if (context.HasError)
            {
                return;
            }

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
        private async Task ReadPreLoginresponse(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            context.ConnectionContext.MarsCapable = context.ConnectionContext.ConnectionString.MARS; // Assign default value
            context.ConnectionContext.FedAuthRequired = false;

            TdsStream tdsStream = context.ConnectionContext.TdsStream;

            // Look into the first byte to see if we are connecting to a 6.5 or earlier server.
            int option = await tdsStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            if (option == 0xaa)
            {
                // If the first byte is 0xAA, we are connecting to a 6.5 or earlier server, which
                // is not supported.
                throw SQL.InvalidSQLServerVersionUnknown();
            }

            byte[] payload = new byte[tdsStream.PacketDataLeft];

            int payloadOffset = 0;
            int offset = 0;

            while (option != (byte)PreLoginOptions.LASTOPT)
            {
                switch (option)
                {
                    case (int)PreLoginOptions.VERSION:

                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        offset += 2; // Skip the payload length

                        byte majorVersion = payload[payloadOffset];

                        bool is2005OrLater = majorVersion >= 9;
                        if (!is2005OrLater)
                        {
                            context.ConnectionContext.MarsCapable = false;  // If pre-2005, MARS not supported.
                        }

                        break;

                    case (int)PreLoginOptions.ENCRYPT:
                        if (context.IsTlsFirst)
                        {
                            // Can skip/ignore this option if we are doing TDS 8.
                            // The internal encryption option is set to NOT_SUP in this case.
                            // And it shouldn't be changed.
                            offset += 4;
                            break;
                        }

                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        offset += 2; // Skip the payload length

                        EncryptionOptions serverOption = (EncryptionOptions)payload[payloadOffset];

                        // Any response other than NOT_SUP means the server supports encryption.
                        context.ServerSupportsEncryption = serverOption != EncryptionOptions.NOT_SUP;

                        // If server doesn't support encyrption, then we can't proceed, since encryption is always needed
                        // for login.
                        if (!context.ServerSupportsEncryption)
                        {
                            //    _physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByServer(), "", 0));
                            //    _physicalStateObj.Dispose();
                            //    ThrowExceptionAndWarning(_physicalStateObj);
                            context.ConnectionContext.Error = new Exception("Encryption not supported by server");
                            return;
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
                                    // Server has enforced encryption, but client does not support it.
                                    //_physicalStateObj.AddError(new SqlError(TdsEnums.ENCRYPTION_NOT_SUPPORTED, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, _server, SQLMessage.EncryptionNotSupportedByClient(), "", 0));
                                    //_physicalStateObj.Dispose();
                                    //ThrowExceptionAndWarning(_physicalStateObj);
                                    // TODO : Error handling needs to happen here. Till then 
                                    // adding an exception and returning 
                                    context.ConnectionContext.Error = new Exception("Encryption not supported by server");
                                    return;
                                }
                                break;
                            default:
                                break;
                        }

                        break;

                    case (int)PreLoginOptions.INSTANCE:
                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        offset += 2; // Skip the payload length

                        byte ERROR_INST = 0x1;
                        byte instanceResult = payload[payloadOffset];

                        if (instanceResult == ERROR_INST)
                        {
                            // Check if server says ERROR_INST. That either means the cached info
                            // we used to connect is not valid or we connected to a named instance
                            // listening on default params.
                            context.HandshakeStatus = PreLoginHandshakeStatus.InstanceFailure;
                        }

                        break;

                    case (int)PreLoginOptions.THREADID:
                        // DO NOTHING FOR THREADID
                        offset += 4;
                        break;

                    case (int)PreLoginOptions.MARS:
                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        offset += 2; // Skip the payload length
                        context.ConnectionContext.MarsCapable = (payload[payloadOffset] != 0);
                        Debug.Assert(payload[payloadOffset] == 0 || payload[payloadOffset] == 1, "Value for Mars PreLoginHandshake option not equal to 1 or 0!");
                        break;

                    case (int)PreLoginOptions.TRACEID:
                        // DO NOTHING FOR TRACEID
                        offset += 4;
                        break;

                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        payloadOffset = payload[offset++] << 8 | payload[offset++];
                        offset += 2; // Skip the payload length

                        // Only 0x00 and 0x01 are accepted values from the server.
                        if (payload[payloadOffset] != 0x00 && payload[payloadOffset] != 0x01)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}|ERR> {1}, " +
                                "Server sent an unexpected value for FedAuthRequired PreLogin Option. Value was {2}.", nameof(ReadPreLoginresponse), ObjectID, (int)payload[payloadOffset]);
                            throw SQL.ParsingErrorValue(ParsingErrorState.FedAuthRequiredPreLoginResponseInvalidValue, (int)payload[payloadOffset]);
                        }

                        // We must NOT use the response for the FEDAUTHREQUIRED PreLogin option, if the connection string option
                        // was not using the new Authentication keyword or in other words, if Authentication=NotSpecified
                        // Or AccessToken is not null, mean token based authentication is used.
                        if ((context.ConnectionContext.ConnectionString != null
                            && context.ConnectionContext.ConnectionString.Authentication != SqlAuthenticationMethod.NotSpecified)
                            || context.ConnectionContext.AccessTokenInBytes != null || context.ConnectionContext.AccessTokenCallback != null)
                        {
                            context.ConnectionContext.FedAuthRequired = payload[payloadOffset] == 0x01;
                        }
                        break;

                    default:
                        Debug.Fail("UNKNOWN option in ConsumePreLoginHandshake, option:" + option);
                        // DO NOTHING FOR THESE UNKNOWN OPTIONS
                        offset += 4;
                        break;
                }

                if (offset < payload.Length)
                {
                    option = payload[offset++];
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
        private static async Task CreatePreLoginAndSend(PreLoginHandlerContext context, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(context.ConnectionContext.TdsStream != null, "A Tds Stream is expected");

            TdsStream tdsStream = context.ConnectionContext.TdsStream;
            tdsStream.PacketHeaderType = TdsStreamPacketType.PreLogin;

            // Initialize option offset into payload buffer
            // 5 bytes for each option (1 byte length, 2 byte offset, 2 byte payload length)
            int offset = (int)PreLoginOptions.NUMOPT * 5 + 1;

            byte[] payload = new byte[(int)PreLoginOptions.NUMOPT * 5 + TdsEnums.MAX_PRELOGIN_PAYLOAD_LENGTH];
            int payloadLength = 0;

            byte[] instanceName = new byte[1];

            for (int option = (int)PreLoginOptions.VERSION; option < (int)PreLoginOptions.NUMOPT; option++)
            {
                int optionDataSize = 0;

                // Fill in the option
                await tdsStream.WriteByteAsync((byte)option, isAsync, ct);

                // Fill in the offset of the option data
                await tdsStream.WriteByteAsync((byte)((offset & 0xff00) >> 8), isAsync, ct); // send upper order byte
                await tdsStream.WriteByteAsync((byte)(offset & 0x00ff), isAsync, ct); // send lower order byte

                switch (option)
                {
                    case (int)PreLoginOptions.VERSION:
                        Version systemDataVersion = ADP.GetAssemblyVersion();

                        // Major and minor
                        payload[payloadLength++] = (byte)(systemDataVersion.Major & 0xff);
                        payload[payloadLength++] = (byte)(systemDataVersion.Minor & 0xff);

                        // Build (Big Endian)
                        payload[payloadLength++] = (byte)((systemDataVersion.Build & 0xff00) >> 8);
                        payload[payloadLength++] = (byte)(systemDataVersion.Build & 0xff);

                        // Sub-build (Little Endian)
                        payload[payloadLength++] = (byte)(systemDataVersion.Revision & 0xff);
                        payload[payloadLength++] = (byte)((systemDataVersion.Revision & 0xff00) >> 8);
                        offset += 6;
                        optionDataSize = 6;
                        break;

                    case (int)PreLoginOptions.ENCRYPT:
                        if (context.InternalEncryptionOption == EncryptionOptions.NOT_SUP)
                        {
                            //If OS doesn't support encryption and encryption is not required, inform server "not supported" by client.
                            payload[payloadLength] = (byte)EncryptionOptions.NOT_SUP;
                        }
                        else
                        {
                            // Else, inform server of user request.
                            if (context.ConnectionEncryptionOption == SqlConnectionEncryptOption.Mandatory)
                            {
                                payload[payloadLength] = (byte)EncryptionOptions.ON;
                                context.InternalEncryptionOption = EncryptionOptions.ON;
                            }
                            else
                            {
                                payload[payloadLength] = (byte)EncryptionOptions.OFF;
                                context.InternalEncryptionOption = EncryptionOptions.OFF;
                            }
                        }

                        payloadLength += 1;
                        offset += 1;
                        optionDataSize = 1;
                        break;

                    case (int)PreLoginOptions.INSTANCE:
                        int i = 0;

                        while (instanceName[i] != 0)
                        {
                            payload[payloadLength] = instanceName[i];
                            payloadLength++;
                            i++;
                        }

                        payload[payloadLength] = 0; // null terminate
                        payloadLength++;
                        i++;

                        offset += i;
                        optionDataSize = i;
                        break;

                    case (int)PreLoginOptions.THREADID:
                        int threadID = TdsParserStaticMethods.GetCurrentThreadIdForTdsLoginOnly();

                        payload[payloadLength++] = (byte)((0xff000000 & threadID) >> 24);
                        payload[payloadLength++] = (byte)((0x00ff0000 & threadID) >> 16);
                        payload[payloadLength++] = (byte)((0x0000ff00 & threadID) >> 8);
                        payload[payloadLength++] = (byte)(0x000000ff & threadID);
                        offset += 4;
                        optionDataSize = 4;
                        break;

                    case (int)PreLoginOptions.MARS:
                        payload[payloadLength++] = (byte)(context.ConnectionContext.ConnectionString.MARS ? 1 : 0);
                        offset += 1;
                        optionDataSize += 1;
                        break;

                    case (int)PreLoginOptions.TRACEID:
                        context.ConnectionContext.ConnectionId.TryWriteBytes(payload.AsSpan(payloadLength, GUID_SIZE));
                        payloadLength += GUID_SIZE;
                        offset += GUID_SIZE;
                        optionDataSize = GUID_SIZE;

                        ActivityCorrelator.ActivityId actId = ActivityCorrelator.Next();
                        actId.Id.TryWriteBytes(payload.AsSpan(payloadLength, GUID_SIZE));
                        payloadLength += GUID_SIZE;
                        payload[payloadLength++] = (byte)(0x000000ff & actId.Sequence);
                        payload[payloadLength++] = (byte)((0x0000ff00 & actId.Sequence) >> 8);
                        payload[payloadLength++] = (byte)((0x00ff0000 & actId.Sequence) >> 16);
                        payload[payloadLength++] = (byte)((0xff000000 & actId.Sequence) >> 24);
                        int actIdSize = GUID_SIZE + sizeof(uint);
                        offset += actIdSize;
                        optionDataSize += actIdSize;
                        SqlClientEventSource.Log.TryTraceEvent("<sc.TdsParser.SendPreLoginHandshake|INFO> ClientConnectionID {0}, ActivityID {1}",
                            context.ConnectionContext.ConnectionId, actId);
                        break;

                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        payload[payloadLength++] = 0x01;
                        offset += 1;
                        optionDataSize += 1;
                        break;

                    default:
                        Debug.Fail("UNKNOWN option in SendPreLoginHandshake");
                        break;
                }

                await tdsStream.WriteByteAsync((byte)((optionDataSize & 0xff00) >> 8), isAsync, ct).ConfigureAwait(false);
                await tdsStream.WriteByteAsync((byte)(optionDataSize & 0x00ff), isAsync, ct).ConfigureAwait(false);
            }

            // Write out last option - to let server know the second part of packet completed
            await tdsStream.WriteByteAsync((byte)PreLoginOptions.LASTOPT, isAsync, ct).ConfigureAwait(false);

            if (isAsync)
            {
                // Write out payload
                await tdsStream.WriteAsync(payload.AsMemory(0, payloadLength), ct).ConfigureAwait(false);
            }
            else
            {
                tdsStream.Write(payload.AsSpan(0, payloadLength));
            }
            // Flush packet

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
