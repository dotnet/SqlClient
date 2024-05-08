using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SqlClientX.SqlValuesProcessing;
using Microsoft.Data.SqlClient.SqlClientX.Streams;

namespace simplesqlclient
{
    internal class SqlPhysicalConnection
    {
        private NetworkStream _tcpStream;
        private SslOverTdsStream _sslOverTdsStream;
        private SslStream _sslStream;
        
        //private BufferReader _bufferReader;

        private TdsWriteStream _writeStream;
        private TdsReadStream _readStream;
        private SqlValuesProcessor _sqlValuesProcessor;
        private string _hostname;
        private int _port;
        //private readonly string applicationName;
        private ConnectionSettings _connectionSettings;
        private readonly ProtocolMetadata _protocolMetadata;
        private bool _isMarsEnabled;
        private bool _serverSupportsFedAuth;
        private ParserFlags _flags;
        private readonly AuthenticationOptions _authOptions;
        private readonly string _database;

        static SqlPhysicalConnection()
        {
            // For CoreCLR, we need to register the ANSI Code Page encoding provider before attempting to get an Encoding from a CodePage
            // For a default installation of SqlServer the encoding exchanged during Login is 1252. This encoding is not loaded by default
            // See Remarks at https://msdn.microsoft.com/en-us/library/system.text.encodingprovider(v=vs.110).aspx 
            // SqlClient needs to register the encoding providers to make sure that even basic scenarios work with Sql Server.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public SqlPhysicalConnection(
            string hostname,
            int port,
            AuthenticationOptions authOptions,
            string database,
            ConnectionSettings connectionSettings)
        {
            _hostname = hostname;
            _port = port;
            _authOptions = authOptions;
            _database = database;
            _connectionSettings = connectionSettings;
            _protocolMetadata = new ProtocolMetadata();
            
        }

        public void TcpConnect()
        {
            // Resolve DNS 

            IEnumerable<IPAddress> ipAddresses = Dns.GetHostAddresses(_hostname);
            // Connect to the first IP address
            IPAddress ipToConnect = ipAddresses.First((ipaddr) => ipaddr.AddressFamily == AddressFamily.InterNetwork);

            Socket socket = new Socket(ipToConnect.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                Blocking = false // We want to block until the connection is established
            };

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 1);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);

            try
            {
                // Now we have a TCP connection to the server.
                socket.Connect(ipToConnect, _port);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock)
                    throw;
            }

            var write = new List<Socket> { socket };
            var error = new List<Socket> { socket };
            Socket.Select(null, write, error, 30000000); // Wait for 30 seconds 
            if (write.Count > 0)
            {
                // Connection established
                socket.Blocking = true;
            }
            else
            {
                throw new Exception("Connection failed");
            }
            //socket.NoDelay = true;

            _tcpStream = new NetworkStream(socket, true);

            _sslOverTdsStream = new SslOverTdsStream(_tcpStream);

            _sslStream = new SslStream(_sslOverTdsStream, true, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        #region Prelogin
        internal void SendPrelogin()
        {
            // 5 bytes for each option (1 byte length, 2 byte offset, 2 byte payload length)
            int preloginOptionsCount = 7;
            int offset = 36; // 7 * 5 + 1 add 1 to start after the first 40 bytes
            // The payload is the bytes for all the options and the maximum length of the payload
            byte[] payload = new byte[preloginOptionsCount * 5 + TdsConstants.MAX_PRELOGIN_PAYLOAD_LENGTH];
            int payLoadIndex = 0;
            _writeStream = new TdsWriteStream(_tcpStream, TdsConstants.DEFAULT_LOGIN_PACKET_SIZE);
            _writeStream.PacketHeaderType = TdsEnums.MT_PRELOGIN;

            for (int option = 0; option < preloginOptionsCount; option++)
            {
                int optionDataSize = 0;

                // Fill in the option
                _writeStream.WriteByte((byte)option);

                // Fill in the offset of the option data
                _writeStream.WriteByte((byte)((offset & 0xff00) >> 8)); // send upper order byte
                _writeStream.WriteByte((byte)(offset & 0x00ff)); // send lower order byte

                switch (option)
                {
                    case 0:
                        Version.TryParse("6.0.0.0", out Version systemDataVersion);

                        // Major and minor
                        payload[payLoadIndex++] = (byte)(systemDataVersion.Major & 0xff);
                        payload[payLoadIndex++] = (byte)(systemDataVersion.Minor & 0xff);

                        // Build (Big Endian)
                        payload[payLoadIndex++] = (byte)((systemDataVersion.Build & 0xff00) >> 8);
                        payload[payLoadIndex++] = (byte)(systemDataVersion.Build & 0xff);

                        // Sub-build (Little Endian)
                        payload[payLoadIndex++] = (byte)(systemDataVersion.Revision & 0xff);
                        payload[payLoadIndex++] = (byte)((systemDataVersion.Revision & 0xff00) >> 8);
                        offset += 6;
                        optionDataSize = 6;
                        break;

                    case 1:

                        // Assume that the encryption is off
                        payload[payLoadIndex] = (byte)0;

                        payLoadIndex += 1;
                        offset += 1;
                        optionDataSize = 1;
                        break;

                    case 2:
                        int i = 0;
                        // Assume we dont need to send the instance name.
                        byte[] instanceName = new byte[1];
                        while (instanceName[i] != 0)
                        {
                            payload[payLoadIndex] = instanceName[i];
                            payLoadIndex++;
                            i++;
                        }

                        payload[payLoadIndex] = 0; // null terminate
                        payLoadIndex++;
                        i++;

                        offset += i;
                        optionDataSize = i;
                        break;

                    case (int)PreLoginOptions.THREADID:
                        int threadID = 1234; // Hard code some thread on the client side.

                        payload[payLoadIndex++] = (byte)((0xff000000 & threadID) >> 24);
                        payload[payLoadIndex++] = (byte)((0x00ff0000 & threadID) >> 16);
                        payload[payLoadIndex++] = (byte)((0x0000ff00 & threadID) >> 8);
                        payload[payLoadIndex++] = (byte)(0x000000ff & threadID);
                        offset += 4;
                        optionDataSize = 4;
                        break;

                    case (int)PreLoginOptions.MARS:
                        payload[payLoadIndex++] = (byte)(0); // Turn off MARS
                        offset += 1;
                        optionDataSize += 1;
                        break;

                    case (int)PreLoginOptions.TRACEID:
                        Guid connectionId = new Guid();
                        connectionId.TryWriteBytes(payload.AsSpan(payLoadIndex, TdsConstants.GUID_SIZE)); // 16 is the size of a GUID
                        payLoadIndex += TdsConstants.GUID_SIZE;
                        offset += TdsConstants.GUID_SIZE;
                        optionDataSize = TdsConstants.GUID_SIZE;

                        Guid activityId = new Guid();
                        uint sequence = 123;
                        activityId.TryWriteBytes(payload.AsSpan(payLoadIndex, 16)); // 16 is the size of a GUID
                        payLoadIndex += TdsConstants.GUID_SIZE;
                        payload[payLoadIndex++] = (byte)(0x000000ff & sequence);
                        payload[payLoadIndex++] = (byte)((0x0000ff00 & sequence) >> 8);
                        payload[payLoadIndex++] = (byte)((0x00ff0000 & sequence) >> 16);
                        payload[payLoadIndex++] = (byte)((0xff000000 & sequence) >> 24);
                        int actIdSize = TdsConstants.GUID_SIZE + sizeof(uint);
                        offset += actIdSize;
                        optionDataSize += actIdSize;
                        break;

                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        payload[payLoadIndex++] = 0x01;
                        offset += 1;
                        optionDataSize += 1;
                        break;

                    default:
                        Debug.Fail("UNKNOWN option in SendPreLoginHandshake");
                        break;
                }

                // Write data length
                _writeStream.WriteByte((byte)((optionDataSize & 0xff00) >> 8));
                _writeStream.WriteByte((byte)(optionDataSize & 0x00ff));
            }
            _writeStream.WriteByte((byte)255);
            _writeStream.Write(payload.AsSpan(0, payLoadIndex));
            _writeStream.Flush();

        }


        internal void EnableSsl()
        {
            _sslStream.AuthenticateAsClient(_hostname, null, System.Security.Authentication.SslProtocols.None, false);
            if (_sslOverTdsStream is not null)
            {
                _sslOverTdsStream.FinishHandshake();
            }

            _writeStream.UpdateStream(_sslStream);
            _readStream.UpdateStream(_sslStream);
        }

        private void DisableSsl()
        {
            _sslStream?.Dispose();
            _sslOverTdsStream?.Dispose();
            _sslStream = null;
            _sslOverTdsStream = null;
            _writeStream.UpdateStream(_tcpStream);
            _readStream.UpdateStream(_tcpStream);
        }

        internal bool TryConsumePrelogin()
        {
            byte[] payload = new byte[TdsConstants.DEFAULT_LOGIN_PACKET_SIZE];
            if (_readStream == null)
            {
                _readStream = new TdsReadStream(_tcpStream);
                _sqlValuesProcessor = new SqlValuesProcessor(_readStream);
            }
            //TdsPacketHeader header = _bufferReader.ProcessPacketHeader();
            //Debug.Assert(header.PacketType == (byte)PacketType.SERVERSTREAM);

            Span<PreLoginOption> options = stackalloc PreLoginOption[7];
            for (int i = 0; i < 8; i++)
            {
                PreLoginOption option;
                option.Option = (byte)_readStream.ReadByte();
                if (option.Option == (int)PreLoginOptions.LASTOPT)
                {
                    break;
                }
                option.Offset = _readStream.ReadByte() << 8 | _readStream.ReadByte() - 36;
                option.Length = _readStream.ReadByte() << 8 | _readStream.ReadByte();
                options[i] = option;
            }

            int optionsDataLength = 0;
            foreach (PreLoginOption option in options)
            {
                optionsDataLength += option.Length;
            }

            Span<byte> preLoginPacket = stackalloc byte[optionsDataLength];
            _readStream.Read(preLoginPacket);

            for (int i = 0; i < 7; i++)
            {
                PreLoginOption currentOption = options[i];
                switch (currentOption.Option)
                {
                    case (int)PreLoginOptions.VERSION:
                        byte major = preLoginPacket[currentOption.Offset];
                        byte minor = preLoginPacket[currentOption.Offset + 1];
                        ushort build = (ushort)(preLoginPacket[currentOption.Offset + 2] << 8 | preLoginPacket[currentOption.Offset + 3]);
                        ushort revision = (ushort)(preLoginPacket[currentOption.Offset + 4] << 8 | preLoginPacket[currentOption.Offset + 5]);
                        break;
                    case (int)PreLoginOptions.ENCRYPT:
                        byte encrypt = preLoginPacket[currentOption.Offset];
                        if ((SqlEncryptionOptions)encrypt == SqlEncryptionOptions.NOT_SUP)
                        {
                            throw new Exception("SErver does not support encryption, cannot go ahead with connection.");
                        }
                        break;
                    case (int)PreLoginOptions.INSTANCE:
                        // Ignore this 
                        Span<byte> instance = stackalloc byte[currentOption.Length];

                        break;
                    case (int)PreLoginOptions.THREADID:
                        // Ignore 
                        break;
                    case (int)PreLoginOptions.MARS:
                        _isMarsEnabled = preLoginPacket[currentOption.Offset] == 1;
                        break;
                    case (int)PreLoginOptions.TRACEID:
                        // Ignore
                        break;
                    case (int)PreLoginOptions.FEDAUTHREQUIRED:
                        _serverSupportsFedAuth = preLoginPacket[currentOption.Offset] == 1;
                        break;
                    default:
                        Debug.Fail("Unknown option");
                        break;
                }
            }

            return true;
        }
        #endregion

        byte[] temp = new byte[100];
        /// <summary>
        /// This needs to be a producer of information. The 
        /// information produced will be handed out to the listeners of the information
        /// The information production can be controlled by passing down flags.
        /// </summary>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NotImplementedException"></exception>
        public async ValueTask ProcessTokenStreamPacketsAsync(ParsingBehavior parsingBehavior,
            bool isAsync,
            CancellationToken ct,
            byte? expectedTdsToken = null,
            bool resetPacket = true)
        {
            if (resetPacket )
                _readStream.ResetPacket();
            do
            {
                // Read a 1 byte token
                TdsToken token = await _readStream.ProcessTokenAsync(isAsync, ct).ConfigureAwait(false);

                if (expectedTdsToken != null && expectedTdsToken != token.TokenType)
                {
                    //throw new Exception("Expected token type " + expectedTdsToken + " but got " + token.TokenType);
                } 
                    
                SqlEnvChange envChange = null;
                switch (token.TokenType)
                {
                    case TdsTokens.SQLENVCHANGE:
                        byte envType = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        switch (envType)
                        {
                            case TdsEnums.ENV_DATABASE:
                            case TdsEnums.ENV_LANG:
                                envChange = await ReadTwoStrings(isAsync, ct).ConfigureAwait(false);
                                break;
                            case TdsEnums.ENV_PACKETSIZE:
                                envChange = await ReadTwoStrings(isAsync, ct).ConfigureAwait(false);
                                // Read 
                                break;
                            case TdsEnums.ENV_COLLATION:
                                int newLen = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                                if (newLen == 5)
                                { 
                                    _ = await _readStream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                                    _ = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                                }
                                int oldLen = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                                if (oldLen == 5)
                                { 
                                    _ = await _readStream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                                    _ = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                                }
                                break;
                        }
                        break;
                    case TdsTokens.SQLERROR:
                    // TODO : Process error


                    case TdsTokens.SQLINFO:
                        simplesqlclient.SqlError error = await _readStream.ProcessErrorAsync(token, isAsync, ct).ConfigureAwait(false);
                        if (token.TokenType == TdsTokens.SQLERROR)
                        {
                            throw new Exception("Error received from server " + error.Message);
                        }
                        if (token.TokenType == TdsTokens.SQLINFO)
                        {
                            // TODO: Accumulate the information packet to be dispatched later
                            // to SqlConnection.
                        }
                        break;
                    case TdsTokens.SQLLOGINACK:
                        // TODO: Login ack needs to be processed to have some server side information 
                        // readily 
                        // Right now simply read it and ignore it.
                        // First byte skip
                        await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        // TdsEnums.Version_size skip
                        await _readStream.SkipBytesAsync(4, isAsync, ct).ConfigureAwait(false);
                        // One byte length skip
                        byte lenSkip = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        // skip length * 2 bytes
                        //_readStream.Read(temp.Slice(0, lenSkip * 2));
                        await _readStream.SkipBytesAsync(lenSkip * 2, isAsync, ct).ConfigureAwait(false);
                        // skip major version byte
                        await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        // skip minor version byte
                        await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        // skip build version byte
                        await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        // skip sub build version byte
                        await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        // Fix 
                        // Do nothing.
                        break;
                    case TdsTokens.SQLDONE:
                        ushort status = await _readStream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                        ushort curCmd = await _readStream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                        long longCount = await _readStream.ReadInt64Async(isAsync, ct).ConfigureAwait(false);
                        int count = (int)longCount;

                        if (TdsEnums.DONE_MORE != (status & TdsEnums.DONE_MORE))
                        {
                        }
                        break;
                    case TdsTokens.SQLCOLMETADATA:
                        if (token.Length != TdsEnums.VARNULL) // TODO: What does this mean? 
                        {
                            _SqlMetaDataSet metadataSet = 
                                await ProcessMetadataSetAsync(token.Length, isAsync, ct).ConfigureAwait(false);
                            _protocolMetadata.LastReadMetadata = metadataSet;
                        }
                        break;
                    case TdsTokens.SQLFEATUREEXTACK:
                        byte featureId;
                        do
                        {
                            featureId = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                            if (featureId != 0xff)
                            {
                                uint datalen = await _readStream.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);

                                byte[] buffer = ArrayPool<byte>.Shared.Rent((int)datalen);
                                _ = isAsync ? await _readStream.ReadAsync(buffer.AsMemory(0, (int)datalen), ct).ConfigureAwait(false) :
                                    _readStream.Read(buffer.AsSpan(0, (int)datalen));
                                _protocolMetadata.AddFeature(featureId, buffer.AsSpan(0, (int)datalen));
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        } while (featureId != 0xff);
                        break;
                    case TdsTokens.SQLROW:
                        bool bulkCopyHandler = false;
                        if (bulkCopyHandler)
                        {
                            await ProcessRowAsync(_protocolMetadata.LastReadMetadata, isAsync, ct).ConfigureAwait(false);
                        }
                        break;
                    
                    // The not implemented ones are here.
                    case TdsTokens.SQLCOLINFO:
                        // code omitted for brevity
                    case TdsTokens.SQLDONEPROC:
                    case TdsTokens.SQLDONEINPROC:
                    case TdsTokens.SQLORDER:

                    case TdsTokens.SQLFEDAUTHINFO:
                    // code omitted for brevity


                    case TdsTokens.SQLSESSIONSTATE:
                    // code omitted for brevity

                    case TdsTokens.SQLNBCROW:
                    // code omitted for brevity

                    case TdsTokens.SQLRETURNSTATUS:
                    // code omitted for brevity

                    case TdsTokens.SQLRETURNVALUE:
                    // code omitted for brevity

                    case TdsTokens.SQLSSPI:
                    // code omitted for brevity

                    case TdsTokens.SQLTABNAME:
                    // code omitted for brevity

                    case TdsTokens.SQLRESCOLSRCS:
                    // code omitted for brevity

                    case TdsTokens.SQLALTMETADATA:
                    // code omitted for brevity

                    case TdsTokens.SQLALTROW:
                    // code omitted for brevity

                    default:
                        throw new NotImplementedException("The token type is not implemented. " + (byte)token.TokenType);
                }
            } while(_readStream.PacketDataLeft > 0 && parsingBehavior != ParsingBehavior.RunOnce);
        }

        private async ValueTask<SqlBuffer> ProcessRowAsync(_SqlMetaDataSet columns,
            bool isAsync,
            CancellationToken ct)
        {
            SqlBuffer data = new SqlBuffer();

            for (int i = 0; i < columns.Length; i++)
            {
                _SqlMetaData column = columns[i];

                Tuple<bool, int> tuple = await ProcessColumnHeaderAsync(column, isAsync, ct).ConfigureAwait(false);
                bool isNull = tuple.Item1;
                int length = tuple.Item2;
                if (tuple.Item1)
                {
                    throw new NotImplementedException("Null values are not implemented");
                }
                else
                {
                    await ReadSqlValueAsync(data, 
                        column, 
                        column.metaType.IsPlp ? (Int32.MaxValue) : (int)length,
                        SqlCommandColumnEncryptionSetting.Disabled /*Column Encryption Disabled for Bulk Copy*/,
                        column.column,
                        isAsync,
                        ct).ConfigureAwait(false);
                }
                data.Clear();
            }

            return data;

        }

        internal async ValueTask ReadSqlValueAsync(
            SqlBuffer value,
            SqlMetaDataPriv md,
            int length, 
            SqlCommandColumnEncryptionSetting columnEncryptionOverride, 
            string columnName, 
            bool isAsync,
            CancellationToken ct,
            SqlCommand command = null)
        {
            bool isPlp = md.metaType.IsPlp;
            byte tdsType = md.tdsType;

            Debug.Assert(isPlp || !Utilities.IsNull(md.metaType, length), "null value should not get here!");
            if (isPlp)
            {
                // We must read the column value completely, no matter what length is passed in
                length = int.MaxValue;
            }

            //DEVNOTE: When modifying the following routines (for deserialization) please pay attention to
            // deserialization code in DecryptWithKey () method and modify it accordingly.
            switch (tdsType)
            {
                case TdsEnums.SQLDECIMALN:
                case TdsEnums.SQLNUMERICN:
                    throw new NotImplementedException("SQLDECIMALN and SQLNUMERICN are not implemented");
                    //if (!TryReadSqlDecimal(value, length, md.precision, md.scale, stateObj))
                case TdsEnums.SQLUDT:
                case TdsEnums.SQLBINARY:
                case TdsEnums.SQLBIGBINARY:
                case TdsEnums.SQLBIGVARBINARY:
                case TdsEnums.SQLVARBINARY:
                case TdsEnums.SQLIMAGE:
                    throw new NotImplementedException("Binary types are not implemented");
                    //byte[] b = null;

                    //// If varbinary(max), we only read the first chunk here, expecting the caller to read the rest
                    //if (isPlp)
                    //{
                    //    // If we are given -1 for length, then we read the entire value,
                    //    // otherwise only the requested amount, usually first chunk.
                    //    int ignored;
                    //    if (!stateObj.TryReadPlpBytes(ref b, 0, length, out ignored))
                    //    {
                    //        return false;
                    //    }
                    //}
                    //else
                    //{
                    //    //Debug.Assert(length > 0 && length < (long)(Int32.MaxValue), "Bad length for column");
                    //    b = new byte[length];
                    //    if (!stateObj.TryReadByteArray(b, length))
                    //    {
                    //        return false;
                    //    }
                    //}

                    //if (md.isEncrypted
                    //    && (columnEncryptionOverride == SqlCommandColumnEncryptionSetting.Enabled
                    //         || columnEncryptionOverride == SqlCommandColumnEncryptionSetting.ResultSetOnly
                    //         || (columnEncryptionOverride == SqlCommandColumnEncryptionSetting.UseConnectionSetting
                    //            && _connHandler != null && _connHandler.ConnectionOptions != null
                    //            && _connHandler.ConnectionOptions.ColumnEncryptionSetting == SqlConnectionColumnEncryptionSetting.Enabled)))
                    //{
                    //    try
                    //    {
                    //        // CipherInfo is present, decrypt and read
                    //        byte[] unencryptedBytes = SqlSecurityUtility.DecryptWithKey(b, md.cipherMD, _connHandler.Connection, command);

                    //        if (unencryptedBytes != null)
                    //        {
                    //            DeserializeUnencryptedValue(value, unencryptedBytes, md, stateObj, md.NormalizationRuleVersion);
                    //        }
                    //    }
                    //    catch (Exception e)
                    //    {
                    //        if (stateObj is not null)
                    //        {
                    //            // call to decrypt column keys has failed. The data wont be decrypted.
                    //            // Not setting the value to false, forces the driver to look for column value.
                    //            // Packet received from Key Vault will throws invalid token header.
                    //            stateObj.HasPendingData = false;
                    //        }
                    //        throw SQL.ColumnDecryptionFailed(columnName, null, e);
                    //    }
                    //}
                    //else
                    //{
                    //    value.SqlBinary = SqlBinary.WrapBytes(b);
                    //}
                    //break;

                case TdsEnums.SQLCHAR:
                case TdsEnums.SQLBIGCHAR:
                case TdsEnums.SQLVARCHAR:
                case TdsEnums.SQLBIGVARCHAR:
                case TdsEnums.SQLTEXT:
                case TdsEnums.SQLNCHAR:
                case TdsEnums.SQLNVARCHAR:
                case TdsEnums.SQLNTEXT:
                    await _sqlValuesProcessor.ReadSqlStringValueAsync(value, 
                        tdsType, 
                        length, 
                        md.encoding, 
                        isPlp, 
                        _protocolMetadata,
                        isAsync,
                        ct).ConfigureAwait(false);
                    break;

                case TdsEnums.SQLXMLTYPE:
                    throw new NotImplementedException("XML type is not implemented");
                    // We store SqlCachedBuffer here, so that we can return either SqlBinary, SqlString or SqlXmlReader.
                    //SqlCachedBuffer sqlBuf;
                    //if (!SqlCachedBuffer.TryCreate(md, this, stateObj, out sqlBuf))
                    //{
                    //    return false;
                    //}

                    //value.SqlCachedBuffer = sqlBuf;
                    //break;

                case TdsEnums.SQLDATE:
                case TdsEnums.SQLTIME:
                case TdsEnums.SQLDATETIME2:
                case TdsEnums.SQLDATETIMEOFFSET:
                    throw new NotImplementedException("Date and time types are not implemented");
                    //if (!TryReadSqlDateTime(value, tdsType, length, md.scale, stateObj))
                    //{
                    //    return false;
                    //}
                    //break;

                default:
                    Debug.Assert(!isPlp, "ReadSqlValue calling ReadSqlValueInternal with plp data");
                    throw new NotImplementedException("Unknown type " + tdsType.ToString("X2") + " is not implemented yet");
                    //if (!TryReadSqlValueInternal(value, tdsType, length, stateObj))
                    //{
                    //    return false;
                    //}
            }

        }

        internal async ValueTask<Tuple<bool, int>> ProcessColumnHeaderAsync(_SqlMetaData col,
            bool isAsync, 
            CancellationToken ct)
        {
            bool isNull = false;
            int length = 0;
            if (col.metaType.IsLong && !col.metaType.IsPlp)
            {
                //
                // we don't care about TextPtrs, simply go after the data after it
                //
                byte textPtrLen = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

                if (textPtrLen != 0)
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(
                        Math.Max((int)textPtrLen, TdsEnums.TEXT_TIME_STAMP_LEN));
                    // Skip past the text pointer.

                    // TODO: Revisit for allocations
                    _ = isAsync ? await this._readStream.ReadAsync(
                                buffer.AsMemory().Slice(0, textPtrLen).ToArray(),
                                ct).ConfigureAwait(false)
                        : _readStream.Read(buffer.AsMemory(0, textPtrLen).ToArray());

                    // Skip past the Timestamp length
                    _ = isAsync ? await this._readStream.ReadAsync(
                                buffer.AsMemory().Slice(0, TdsEnums.TEXT_TIME_STAMP_LEN).ToArray(),
                                ct).ConfigureAwait(false)
                        : _readStream.Read(buffer.AsMemory(0, TdsEnums.TEXT_TIME_STAMP_LEN).ToArray());

                    ArrayPool<byte>.Shared.Return(buffer);
                    isNull = false; // Why ? 

                    length = await Utilities.GetSpecialTokenLengthAsync(
                                        col.tdsType,
                                        _readStream,
                                        isAsync,
                                        ct).ConfigureAwait(false);

                    return Tuple.Create(isNull, length);
                }
                else
                {
                    return Tuple.Create(true, 0);
                }
            }
            else
            {
                length = await Utilities.GetSpecialTokenLengthAsync(col.tdsType, this._readStream,
                    isAsync, ct).ConfigureAwait(false);
                isNull = Utilities.IsNull(col.metaType, length);

                length = isNull ? 0 : length;

                return Tuple.Create(isNull, length);
            }    

        }

        private async ValueTask<_SqlMetaDataSet> ProcessMetadataSetAsync(
            int columnCount, 
            bool isAsync,
            CancellationToken ct)
        {
            _SqlMetaDataSet newMetaData = new _SqlMetaDataSet(columnCount, null);
            SqlTceCipherInfoTable cipherTable = (_protocolMetadata.IsFeatureSupported(TdsEnums.FEATUREEXT_TCE)) ?
                await ProcessCipherInfoTableAsync(isAsync, ct).ConfigureAwait(false) : null;
                
            for (int i = 0; i < columnCount; i++)
            {
                await CommonProcessMetaDataAsync(newMetaData[i], isAsync, ct).ConfigureAwait(false);
            }

            return newMetaData;
        }

        private async ValueTask<SqlTceCipherInfoTable> ProcessCipherInfoTableAsync(
            bool isAsync,
            CancellationToken ct)
        {
            // Read count
            short tableSize = await _readStream.ReadInt16Async(isAsync, ct).ConfigureAwait(false);
            SqlTceCipherInfoTable cipherTable = null;
            
            if (0 != tableSize)
            {
                SqlTceCipherInfoTable tempTable = new SqlTceCipherInfoTable(tableSize);

                // Read individual entries
                for (int i = 0; i < tableSize; i++)
                {
                    SqlTceCipherInfoEntry entry = await ReadCipherInfoEntryAsync(isAsync, ct).ConfigureAwait(false);
                    tempTable[i] = entry;
                }

                cipherTable = tempTable;
            }

            return cipherTable;
        }

        private async ValueTask<SqlTceCipherInfoEntry> ReadCipherInfoEntryAsync(
            bool isAsync, 
            CancellationToken ct)
        {
            byte cekValueCount = 0;
            SqlTceCipherInfoEntry entry = new SqlTceCipherInfoEntry(ordinal: 0);

            // Read the DB ID
            int dbId = await _readStream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
            int keyId = await _readStream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);

            // Read the key version
            int keyVersion = await _readStream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
            
            // Read the key MD Version
            byte[] keyMDVersion = new byte[8];
            _readStream.Read(keyMDVersion.AsSpan());
            
            cekValueCount = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            
            for (int i = 0; i < cekValueCount; i++)
            {
                // Read individual CEK values
                ushort shortValue = await _readStream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                byte[] encryptedCek = new byte[shortValue];

                _readStream.Read(encryptedCek.AsSpan());
                
                int length = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                string keyStoreName = await _readStream.ReadStringAsync(length, isAsync, ct).ConfigureAwait(false);

                shortValue = await _readStream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                string keyPath = await _readStream.ReadStringAsync(shortValue, isAsync, ct).ConfigureAwait(false);

                byte algorithmLength = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                string algorithmName = await _readStream.ReadStringAsync(algorithmLength, isAsync, ct).ConfigureAwait(false);

                entry.Add(encryptedCek,
                    databaseId: dbId,
                    cekId: keyId,
                    cekVersion: keyVersion,
                    cekMdVersion: keyMDVersion,
                    keyPath: keyPath,
                    keyStoreName: keyStoreName,
                    algorithmName: algorithmName);
            }
            return entry;
        }

        private async ValueTask CommonProcessMetaDataAsync(_SqlMetaData col, bool isAsync, CancellationToken ct)
        {
            uint userType = await _readStream.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
            byte flags = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            col.Updatability = (byte)((flags & TdsEnums.Updatability) >> 2);
            col.IsNullable = (TdsEnums.Nullable == (flags & TdsEnums.Nullable));
            col.IsIdentity = (TdsEnums.Identity == (flags & TdsEnums.Identity));

            flags = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            col.IsColumnSet = (TdsEnums.IsColumnSet == (flags & TdsEnums.IsColumnSet));

            await ProcessTypeInfoAsync(col, userType, isAsync, ct).ConfigureAwait(false);


            // Read table name
            if (col.metaType.IsLong && !col.metaType.IsPlp)
            {
                int unusedLen = 0xFFFF;      //We ignore this value
                var tuple = await ProcessOneTableAsync(unusedLen, isAsync, ct).ConfigureAwait(false);
                col.multiPartTableName = tuple.Item1;
                unusedLen = tuple.Item2;
            }

            byte byteLen = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            col.column = await _readStream.ReadStringAsync(byteLen, isAsync, ct).ConfigureAwait(false) ;
            UpdateFlags(ParserFlags.HasReceivedColumnMetadata, true);
        }

        private async ValueTask<Tuple<MultiPartTableName, int>> ProcessOneTableAsync(int length, 
            bool isAsync, 
            CancellationToken ct)
        {
            ushort tableLen;
            MultiPartTableName mpt;
            string value;

            MultiPartTableName multiPartTableName = default(MultiPartTableName);

            mpt = new MultiPartTableName();
            byte nParts = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            length--;
            if (nParts == 4)
            {
                tableLen = await _readStream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                length -= 2;
                value = await _readStream.ReadStringAsync(tableLen, isAsync, ct).ConfigureAwait(false);
                mpt.ServerName = value;
                nParts--;
                length -= (tableLen * 2); // wide bytes
            }
            if (nParts == 3)
            {
                tableLen = await _readStream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                length -= 2;
                value = await _readStream.ReadStringAsync(tableLen, isAsync, ct).ConfigureAwait(false);
                mpt.CatalogName = value;
                length -= (tableLen * 2); // wide bytes
                nParts--;
            }
            if (nParts == 2)
            {
                tableLen = await _readStream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                length -= 2;
                value = await _readStream.ReadStringAsync(tableLen, isAsync, ct).ConfigureAwait(false);
                mpt.SchemaName = value;
                length -= (tableLen * 2); // wide bytes
                nParts--;
            }
            if (nParts == 1)
            {
                tableLen = await _readStream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                length -= 2;
                value = await _readStream.ReadStringAsync(tableLen, isAsync, ct).ConfigureAwait(false);
                mpt.TableName = value;
                length -= (tableLen * 2); // wide bytes
                nParts--;
            }
            Debug.Assert(nParts == 0, "ProcessTableName:Unidentified parts in the table name token stream!");

            multiPartTableName = mpt;
            return Tuple.Create(multiPartTableName, length);
        }

        private void UpdateFlags(ParserFlags flag, bool value)
        {
            _flags = value ? _flags | flag : _flags & ~flag;
        }

        private async ValueTask ProcessTypeInfoAsync(_SqlMetaData col, uint userType, bool isAsync, CancellationToken ct)
        {
            byte tdsType = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            
            if (tdsType == TdsEnums.SQLXMLTYPE)
            {
                col.length = TdsEnums.SQL_USHORTVARMAXLEN;
            } 
            else if (Utilities.IsVarTimeTds(tdsType))
            {
                col.length = 0;
            }
            else if (tdsType == TdsEnums.SQLDATE)
            {
                col.length = 3;
            }
            else
            {
                col.length = await Utilities.GetSpecialTokenLengthAsync(
                    tdsType, _readStream,
                    isAsync,
                    ct).ConfigureAwait(false);
            }

            col.metaType = MetaType.GetSqlDataType(tdsType, userType, col.length);
            col.type = col.metaType.SqlDbType;
            col.tdsType = (col.IsNullable ? col.metaType.NullableType : col.metaType.TDSType);

            if (TdsEnums.SQLUDT == tdsType)
            {
                throw new NotImplementedException("Udt type is not implemented");
            }

            if (col.length == TdsEnums.SQL_USHORTVARMAXLEN)
            {
                Debug.Assert(tdsType == TdsEnums.SQLXMLTYPE ||
                             tdsType == TdsEnums.SQLBIGVARCHAR ||
                             tdsType == TdsEnums.SQLBIGVARBINARY ||
                             tdsType == TdsEnums.SQLNVARCHAR ||
                             tdsType == TdsEnums.SQLUDT,
                             "Invalid streaming datatype");

                col.metaType = MetaType.GetMaxMetaTypeFromMetaType(col.metaType);
                Debug.Assert(col.metaType.IsLong, "Max datatype not IsLong");
                col.length = int.MaxValue;

                byte byteLen;
                if (tdsType == TdsEnums.SQLXMLTYPE)
                {
                    byte schemapresent = (byte)_readStream.ReadByte();
                    
                    if ((schemapresent & 1) != 0)
                    {
                        byteLen = (byte)_readStream.ReadByte();
                        col.xmlSchemaCollection = new SqlMetaDataXmlSchemaCollection();
                        col.xmlSchemaCollection.Database = await _readStream.ReadStringAsync(byteLen, isAsync, ct).ConfigureAwait(false);
                        
                        byteLen = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

                        col.xmlSchemaCollection.OwningSchema = await _readStream.ReadStringAsync(byteLen, isAsync, ct).ConfigureAwait(false);
                        
                        short shortLen = await _readStream.ReadInt16Async(isAsync, ct).ConfigureAwait(false);
                        col.xmlSchemaCollection.Name = await _readStream.ReadStringAsync(shortLen, isAsync, ct).ConfigureAwait(false);
                    }
                }
            }

            if (col.type == System.Data.SqlDbType.Decimal)
            {
                col.precision = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                col.scale = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            }

            if (col.metaType.IsVarTime)
            {
                col.scale = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                Debug.Assert(0 <= col.scale && col.scale <= 7);

                switch (col.metaType.SqlDbType)
                {
                    case SqlDbType.Time:
                        col.length = MetaType.GetTimeSizeFromScale(col.scale);
                        break;
                    case SqlDbType.DateTime2:
                        // Date in number of days (3 bytes) + time
                        col.length = 3 + MetaType.GetTimeSizeFromScale(col.scale);
                        break;
                    case SqlDbType.DateTimeOffset:
                        // Date in days (3 bytes) + offset in minutes (2 bytes) + time
                        col.length = 5 + MetaType.GetTimeSizeFromScale(col.scale);
                        break;

                    default:
                        Debug.Fail("Unknown VariableTime type!");
                        break;
                }

            }

            if (col.metaType.IsCharType && (tdsType != TdsEnums.SQLXMLTYPE))
            {
                col.collation = await ProcessCollationAsync(isAsync, ct).ConfigureAwait(false);

                // UTF8 collation
                if (col.collation.IsUTF8)
                {
                    col.encoding = Encoding.UTF8;
                }
                else
                {
                    int codePage = GetCodePage(col.collation);

                    if (codePage == _protocolMetadata.DefaultCodePage)
                    {
                        col.codePage = _protocolMetadata.DefaultCodePage;
                        col.encoding = _protocolMetadata.DefaultEncoding;
                    }
                    else
                    {
                        col.codePage = codePage;
                        col.encoding = System.Text.Encoding.GetEncoding(col.codePage);
                    }
                }
            }
        }

        private async ValueTask<SqlCollation> ProcessCollationAsync(bool isAsync, CancellationToken ct)
        {
            uint info = await _readStream.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
            byte sortId = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            SqlCollation collation = null;
            if (SqlCollation.Equals(_protocolMetadata.Collation, info, sortId))
            {
                collation = _protocolMetadata.Collation;
            }
            else
            {
                collation = new SqlCollation(info, sortId);
                _protocolMetadata.Collation = collation;
            }
            return collation;
        }

        internal int GetCodePage(SqlCollation collation)
        {
            int codePage = 0;

            if (0 != collation._sortId)
            {
                codePage = TdsEnums.CODE_PAGE_FROM_SORT_ID[collation._sortId];
                Debug.Assert(0 != codePage, "GetCodePage accessed codepage array and produced 0!, sortID =" + ((Byte)(collation._sortId)).ToString((IFormatProvider)null));
            }
            else
            {
                int cultureId = collation.LCID;
                bool success = false;

                try
                {
                    codePage = CultureInfo.GetCultureInfo(cultureId).TextInfo.ANSICodePage;

                    // SqlHot 50001398: CodePage can be zero, but we should defer such errors until
                    //  we actually MUST use the code page (i.e. don't error if no ANSI data is sent).
                    success = true;
                }
                catch (ArgumentException)
                {
                }

                // If we failed, it is quite possible this is because certain culture id's
                // were removed in Win2k and beyond, however Sql Server still supports them.
                // In this case we will mask off the sort id (the leading 1). If that fails,
                // or we have a culture id other than the cases below, we throw an error and
                // throw away the rest of the results.

                //  Sometimes GetCultureInfo will return CodePage 0 instead of throwing.
                //  This should be treated as an error and functionality switches into the following logic.
                if (!success || codePage == 0)
                {
                    switch (cultureId)
                    {
                        case 0x10404: // zh-TW
                        case 0x10804: // zh-CN
                        case 0x10c04: // zh-HK
                        case 0x11004: // zh-SG
                        case 0x11404: // zh-MO
                        case 0x10411: // ja-JP
                        case 0x10412: // ko-KR
                                      // If one of the following special cases, mask out sortId and
                                      // retry.
                            cultureId = cultureId & 0x03fff;

                            try
                            {
                                codePage = new CultureInfo(cultureId).TextInfo.ANSICodePage;
                                success = true;
                            }
                            catch (ArgumentException)
                            {
                            }
                            break;
                        case 0x827:     // Mapping Non-supported Lithuanian code page to supported Lithuanian.
                            try
                            {
                                codePage = new CultureInfo(0x427).TextInfo.ANSICodePage;
                                success = true;
                            }
                            catch (ArgumentException)
                            {
                            }
                            break;
                        case 0x43f:
                            codePage = 1251;  // Kazakh code page based on SQL Server
                            break;
                        case 0x10437:
                            codePage = 1252;  // Georgian code page based on SQL Server
                            break;
                        default:
                            break;
                    }

                    if (!success)
                    {
                        throw new Exception("Unsupported collation. Drain data.");
                    }

                    Debug.Assert(codePage >= 0,
                        $"Invalid code page. codePage: {codePage}. cultureId: {cultureId}");
                }
            }

            return codePage;
        }

        private async ValueTask<SqlEnvChange> ReadTwoStrings(bool isAsync, CancellationToken ct)
        {
            SqlEnvChange env = new SqlEnvChange();
            // Used by ProcessEnvChangeToken
            byte newLength = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            string newValue = await _readStream.ReadStringAsync(newLength, isAsync, ct).ConfigureAwait(false);
            byte oldLength = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            string oldValue = await _readStream.ReadStringAsync(oldLength, isAsync, ct).ConfigureAwait(false);

            env._newLength = newLength;
            env._newValue = newValue;
            env._oldLength = oldLength;
            env._oldValue = oldValue;

            // env.length includes 1 byte type token
            env._length = 3 + env._newLength * 2 + env._oldLength * 2;
            return env;
        }

        public void SendLogin()
        {
            LoginPacket packet = new LoginPacket();
            packet.ApplicationName = _connectionSettings.ApplicationName;
            packet.ClientHostName = _connectionSettings.WorkstationId;
            packet.ServerHostname = _hostname;
            packet.ClientInterfaceName = TdsEnums.SQL_PROVIDER_NAME;
            packet.Database = _database;
            packet.PacketSize = _connectionSettings.PacketSize;
            packet.ProcessIdForTdsLogin = Utilities.GetCurrentProcessIdForTdsLoginOnly();
            packet.UserName = _authOptions.AuthDetails.UserName;
            packet.ObfuscatedPassword = _authOptions.AuthDetails.EncryptedPassword;
            packet.Login7Flags = 0;
            packet.IsIntegratedSecurity = false;
            packet.UserInstance = string.Empty;
            packet.NewPassword = new byte[0];
            packet.FeatureExtensionData = new FeatureExtensionsData();
            packet.FeatureExtensionData.fedAuthFeature.AccessToken = null;
            packet.FeatureExtensionData.fedAuthFeature.FedAuthLibrary = default;


            TdsEnums.FeatureExtension requestedFeatures = TdsEnums.FeatureExtension.None;
            requestedFeatures |= TdsEnums.FeatureExtension.GlobalTransactions
                | TdsEnums.FeatureExtension.DataClassification
                | TdsEnums.FeatureExtension.Tce
                | TdsEnums.FeatureExtension.UTF8Support
                | TdsEnums.FeatureExtension.SQLDNSCaching;

            packet.RequestedFeatures = requestedFeatures;
            packet.FeatureExtensionData.requestedFeatures = requestedFeatures;

            _writeStream.PacketHeaderType = TdsEnums.MT_LOGIN7;
            int length = packet.Length;
            _writeStream.WriteInt(length);
            // Write TDS Version. We support 7.4
            _writeStream.WriteInt(packet.ProtocolVersion);
            // Negotiate the packet size.
            _writeStream.WriteInt(packet.PacketSize);
            // Client Prog Version
            _writeStream.WriteInt(packet.ClientProgramVersion);
            // Current Process Id
            _writeStream.WriteInt(packet.ProcessIdForTdsLogin);
            // Unused Connection Id 
            _writeStream.WriteInt(0);

            int log7Flags = 0;

            /*
             Current snapshot from TDS spec with the offsets added:
                0) fByteOrder:1,                // byte order of numeric data types on client
                1) fCharSet:1,                  // character set on client
                2) fFloat:2,                    // Type of floating point on client
                4) fDumpLoad:1,                 // Dump/Load and BCP enable
                5) fUseDb:1,                    // USE notification
                6) fDatabase:1,                 // Initial database fatal flag
                7) fSetLang:1,                  // SET LANGUAGE notification
                8) fLanguage:1,                 // Initial language fatal flag
                9) fODBC:1,                     // Set if client is ODBC driver
               10) fTranBoundary:1,             // Transaction boundary notification
               11) fDelegatedSec:1,             // Security with delegation is available
               12) fUserType:3,                 // Type of user
               15) fIntegratedSecurity:1,       // Set if client is using integrated security
               16) fSQLType:4,                  // Type of SQL sent from client
               20) fOLEDB:1,                    // Set if client is OLEDB driver
               21) fSpare1:3,                   // first bit used for read-only intent, rest unused
               24) fResetPassword:1,            // set if client wants to reset password
               25) fNoNBCAndSparse:1,           // set if client does not support NBC and Sparse column
               26) fUserInstance:1,             // This connection wants to connect to a SQL "user instance"
               27) fUnknownCollationHandling:1, // This connection can handle unknown collation correctly.
               28) fExtension:1                 // Extensions are used
               32 - total
            */

            // first byte
            log7Flags |= TdsEnums.USE_DB_ON << 5;
            log7Flags |= TdsEnums.INIT_DB_FATAL << 6;
            log7Flags |= TdsEnums.SET_LANG_ON << 7;

            // second byte
            log7Flags |= TdsEnums.INIT_LANG_FATAL << 8;
            log7Flags |= TdsEnums.ODBC_ON << 9;

            // No SSPI usage
            if (_connectionSettings.UseSSPI)
            {
                log7Flags |= TdsEnums.SSPI_ON << 15;
            }

            // third byte
            if (_connectionSettings.ReadOnlyIntent)
            {
                log7Flags |= TdsEnums.READONLY_INTENT_ON << 21; // read-only intent flag is a first bit of fSpare1
            }

            // Always say that we are using Feature extensions
            log7Flags |= 1 << 28;

            _writeStream.WriteInt(log7Flags);
            // Time Zone
            _writeStream.WriteInt(0);

            // LCID
            _writeStream.WriteInt(0);

            int offset = TdsEnums.SQL2005_LOG_REC_FIXED_LEN;

            _writeStream.WriteShort((short)offset);

            _writeStream.WriteShort((short)packet.ClientHostName.Length);

            offset += packet.ClientHostName.Length * 2;

            // Support User name and password
            if (_authOptions.AuthenticationType == AuthenticationType.SQLAUTH)
            {
                _writeStream.WriteShort((short)offset);
                _writeStream.WriteShort((short)_authOptions.AuthDetails.UserName.Length);
                offset += _authOptions.AuthDetails.UserName.Length * 2;

                _writeStream.WriteShort((short)offset);
                _writeStream.WriteShort((short)_authOptions.AuthDetails.EncryptedPassword.Length / 2);
                offset += _authOptions.AuthDetails.EncryptedPassword.Length;
            }
            else
            {
                _writeStream.WriteShort(0);  // userName offset
                _writeStream.WriteShort(0);
                _writeStream.WriteShort(0);  // password offset
                _writeStream.WriteShort(0);
            }

            _writeStream.WriteShort((short)offset);
            _writeStream.WriteShort((short)_connectionSettings.ApplicationName.Length);
            offset += _connectionSettings.ApplicationName.Length * 2;

            _writeStream.WriteShort((short)offset);
            _writeStream.WriteShort((short)_hostname.Length);
            offset += _hostname.Length * 2;

            _writeStream.WriteShort(offset);
            // Feature extension being used 
            _writeStream.WriteShort(4);

            offset += 4;

            _writeStream.WriteShort(offset);
            _writeStream.WriteShort(packet.ClientInterfaceName.Length);
            offset += packet.ClientInterfaceName.Length * 2;

            _writeStream.WriteShort(offset);
            _writeStream.WriteShort(packet.Language.Length);
            offset += packet.Language.Length * 2;

            _writeStream.WriteShort(offset);
            _writeStream.WriteShort(packet.Database.Length);
            offset += packet.Database.Length * 2;

            byte[] nicAddress = new byte[TdsEnums.MAX_NIC_SIZE];
            Random random = new Random();
            random.NextBytes(nicAddress);
            _writeStream.Write(nicAddress);

            _writeStream.WriteShort(offset);

            // No Integrated Auth
            _writeStream.WriteShort(0);

            // Attach DB Filename
            _writeStream.WriteShort(offset);
            _writeStream.WriteShort(string.Empty.Length);
            offset += string.Empty.Length * 2;

            _writeStream.WriteShort(offset);
            _writeStream.WriteShort(packet.NewPassword.Length / 2);

            // reserved for chSSPI
            _writeStream.WriteInt(0);

            _writeStream.WriteString(packet.ClientHostName);

            // Consider User Name auth only
            _writeStream.WriteString(packet.UserName);
            _writeStream.Write(packet.ObfuscatedPassword);

            _writeStream.WriteString(packet.ApplicationName);
            _writeStream.WriteString(packet.ServerHostname);

            _writeStream.WriteInt(packet.Length - packet.FeatureExtensionData.Length);
            _writeStream.WriteString(packet.ClientInterfaceName);
            _writeStream.WriteString(packet.Language);
            _writeStream.WriteString(packet.Database);
            // Attach DB File Name
            _writeStream.WriteString(string.Empty);

            _writeStream.Write(packet.NewPassword);
            // Apply feature extension data


            FeatureExtensionsData featureExtensionData = packet.FeatureExtensionData;

            Span<byte> tceData = stackalloc byte[5];
            featureExtensionData.colEncryptionData.FillData(tceData);
            _writeStream.WriteByte((byte)ColumnEncryptionData.FeatureExtensionFlag);
            _writeStream.Write(tceData);


            Span<byte> globalTransaction = stackalloc byte[4];
            featureExtensionData.globalTransactionsFeature.FillData(globalTransaction);
            _writeStream.WriteByte((byte)featureExtensionData.globalTransactionsFeature.FeatureExtensionFlag);
            _writeStream.Write(globalTransaction);

            Span<byte> dataClassificationFeatureData = stackalloc byte[5];
            packet.FeatureExtensionData.dataClassificationFeature.FillData(dataClassificationFeatureData);
            _writeStream.WriteByte((byte)packet.FeatureExtensionData.dataClassificationFeature.FeatureExtensionFlag);
            _writeStream.Write(dataClassificationFeatureData);

            Span<byte> utf8SupportData = stackalloc byte[4];
            packet.FeatureExtensionData.uTF8SupportFeature.FillData(utf8SupportData);
            _writeStream.WriteByte((byte)packet.FeatureExtensionData.uTF8SupportFeature.FeatureExtensionFlag);
            _writeStream.Write(utf8SupportData);

            Span<byte> dnsCaching = stackalloc byte[4];
            packet.FeatureExtensionData.sQLDNSCaching.FillData(dnsCaching);
            _writeStream.WriteByte((byte)packet.FeatureExtensionData.sQLDNSCaching.FeatureExtensionFlag);
            _writeStream.Write(dnsCaching);

            _writeStream.WriteByte(0xFF);
            _writeStream.Flush();

            DisableSsl();
        }

        public void SendQuery(string query)
        {
            //_readStream.ResetPacket();
            int marsHeaderSize = 18;
            int notificationHeaderSize = 0; // TODO: Needed for sql notifications feature. Not implemetned yet
            int totalHeaderLength = 4 + marsHeaderSize + notificationHeaderSize;
            _writeStream.WriteInt(totalHeaderLength);

            _writeStream.WriteInt(marsHeaderSize);

            // Write the MARS header data. 
            _writeStream.WriteShort(TdsEnums.HEADERTYPE_MARS);
            int transactionId = 0; // TODO: Needed for txn support
            _writeStream.WriteLong(transactionId);

            int resultCount = 0;
            // TODO Increment and add the open results count per connection.
            _writeStream.WriteInt(++resultCount);
            _writeStream.PacketHeaderType = TdsEnums.MT_SQL;

            // TODO: Add the enclave support. The server doesnt support Enclaves yet.

            _writeStream.WriteString(query);
            _writeStream.Flush();
        }

        internal async ValueTask<_SqlMetaDataSet> ProcessMetadataAsync(bool isAsync, CancellationToken ct)
        {
            await ProcessTokenStreamPacketsAsync(ParsingBehavior.RunOnce, 
                isAsync,
                ct,
                TdsTokens.SQLCOLMETADATA, 
                resetPacket : true).ConfigureAwait(false);
            return _protocolMetadata.LastReadMetadata;
        }

        /// <summary>
        /// Advances the parser to reading past the SQLROW token.
        /// </summary>
        internal async ValueTask AdvancePastRowAsync(bool isAsync, CancellationToken ct)
        {
            await ProcessTokenStreamPacketsAsync(ParsingBehavior.RunOnce, 
                isAsync,
                ct, 
                TdsTokens.SQLROW, 
                resetPacket: false).ConfigureAwait(false);
        }
    }
}
