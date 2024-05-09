using System;
using System.Buffers;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SqlClientX;
using Microsoft.Data.SqlClient.SqlClientX.SqlValuesProcessing;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets;

namespace simplesqlclient
{
    internal class SqlPhysicalConnection
    {
        private NetworkStream _tcpStream;
        private SslOverTdsStream _sslOverTdsStream;
        private SslStream _sslStream;
        
        //private BufferReader _bufferReader;

        private TdsWriteStream _writeStream;
        private TdsPreLoginHandler _preloginHandler;
        private TdsReadStream _readStream;
        private SqlValuesProcessor _sqlValuesProcessor;
        private string _hostname;
        private int _port;
        //private readonly string applicationName;
        private ConnectionSettings _connectionSettings;
        private readonly ProtocolMetadata _protocolMetadata;
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

        public async ValueTask TcpConnect(bool isAsync, CancellationToken ct)
        {
            var handler = new TcpHandler(_hostname, _port);
            _tcpStream = await handler.Connect(isAsync, ct).ConfigureAwait(false);

            _sslOverTdsStream = new SslOverTdsStream(_tcpStream);

            _sslStream = new SslStream(_sslOverTdsStream,
                true, 
                new RemoteCertificateValidationCallback(ValidateServerCertificate), 
                null);

            _writeStream = new TdsWriteStream(_tcpStream);

            _readStream = new TdsReadStream(_tcpStream);

            _preloginHandler = new TdsPreLoginHandler(_writeStream, _readStream);

            _sqlValuesProcessor = new SqlValuesProcessor(_readStream);
        }

        private bool ValidateServerCertificate(object sender, 
            X509Certificate certificate, 
            X509Chain chain, 
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        internal async ValueTask SendAndConsumePrelogin(bool isAsync, CancellationToken ct)
        {
            await _preloginHandler.Send(isAsync, ct).ConfigureAwait(false);
            await _preloginHandler.Consume(false, CancellationToken.None).ConfigureAwait(false);
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

        byte[] temp = new byte[100];

        public async ValueTask ProcessTokenStreamPacketOnce(bool isAsync, CancellationToken ct)
        {
            await ProcessTokenStreamPacketsAsync(ParsingBehavior.RunOnce, isAsync, ct).ConfigureAwait(false);
        }
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
                            case LucidTdsEnums.ENV_DATABASE:
                            case LucidTdsEnums.ENV_LANG:
                                envChange = await ReadTwoStrings(isAsync, ct).ConfigureAwait(false);
                                break;
                            case LucidTdsEnums.ENV_PACKETSIZE:
                                envChange = await ReadTwoStrings(isAsync, ct).ConfigureAwait(false);
                                // Read 
                                break;
                            case LucidTdsEnums.ENV_COLLATION:
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

                        if (LucidTdsEnums.DONE_MORE != (status & LucidTdsEnums.DONE_MORE))
                        {
                        }
                        break;
                    case TdsTokens.SQLCOLMETADATA:
                        if (token.Length != LucidTdsEnums.VARNULL) // TODO: What does this mean? 
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

        internal async ValueTask<byte> PeekToken(bool isAsync, CancellationToken ct)
        {
            return await _readStream.PeekTokenAsync(isAsync, ct).ConfigureAwait(false);
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
                case LucidTdsEnums.SQLDECIMALN:
                case LucidTdsEnums.SQLNUMERICN:
                    throw new NotImplementedException("SQLDECIMALN and SQLNUMERICN are not implemented");
                    //if (!TryReadSqlDecimal(value, length, md.precision, md.scale, stateObj))
                case LucidTdsEnums.SQLUDT:
                case LucidTdsEnums.SQLBINARY:
                case LucidTdsEnums.SQLBIGBINARY:
                case LucidTdsEnums.SQLBIGVARBINARY:
                case LucidTdsEnums.SQLVARBINARY:
                case LucidTdsEnums.SQLIMAGE:
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

                case LucidTdsEnums.SQLCHAR:
                case LucidTdsEnums.SQLBIGCHAR:
                case LucidTdsEnums.SQLVARCHAR:
                case LucidTdsEnums.SQLBIGVARCHAR:
                case LucidTdsEnums.SQLTEXT:
                case LucidTdsEnums.SQLNCHAR:
                case LucidTdsEnums.SQLNVARCHAR:
                case LucidTdsEnums.SQLNTEXT:
                    await _sqlValuesProcessor.ReadSqlStringValueAsync(value, 
                        tdsType, 
                        length, 
                        md.encoding, 
                        isPlp, 
                        _protocolMetadata,
                        isAsync,
                        ct).ConfigureAwait(false);
                    break;

                case LucidTdsEnums.SQLXMLTYPE:
                    throw new NotImplementedException("XML type is not implemented");
                    // We store SqlCachedBuffer here, so that we can return either SqlBinary, SqlString or SqlXmlReader.
                    //SqlCachedBuffer sqlBuf;
                    //if (!SqlCachedBuffer.TryCreate(md, this, stateObj, out sqlBuf))
                    //{
                    //    return false;
                    //}

                    //value.SqlCachedBuffer = sqlBuf;
                    //break;

                case LucidTdsEnums.SQLDATE:
                case LucidTdsEnums.SQLTIME:
                case LucidTdsEnums.SQLDATETIME2:
                case LucidTdsEnums.SQLDATETIMEOFFSET:
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
                        Math.Max((int)textPtrLen, LucidTdsEnums.TEXT_TIME_STAMP_LEN));
                    // Skip past the text pointer.

                    // TODO: Revisit for allocations
                    _ = isAsync ? await this._readStream.ReadAsync(
                                buffer.AsMemory().Slice(0, textPtrLen).ToArray(),
                                ct).ConfigureAwait(false)
                        : _readStream.Read(buffer.AsMemory(0, textPtrLen).ToArray());

                    // Skip past the Timestamp length
                    _ = isAsync ? await this._readStream.ReadAsync(
                                buffer.AsMemory().Slice(0, LucidTdsEnums.TEXT_TIME_STAMP_LEN).ToArray(),
                                ct).ConfigureAwait(false)
                        : _readStream.Read(buffer.AsMemory(0, LucidTdsEnums.TEXT_TIME_STAMP_LEN).ToArray());

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
            SqlTceCipherInfoTable cipherTable = (_protocolMetadata.IsFeatureSupported(LucidTdsEnums.FEATUREEXT_TCE)) ?
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

            col.Updatability = (byte)((flags & LucidTdsEnums.Updatability) >> 2);
            col.IsNullable = (LucidTdsEnums.Nullable == (flags & LucidTdsEnums.Nullable));
            col.IsIdentity = (LucidTdsEnums.Identity == (flags & LucidTdsEnums.Identity));

            flags = await _readStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            col.IsColumnSet = (LucidTdsEnums.IsColumnSet == (flags & LucidTdsEnums.IsColumnSet));

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
            
            if (tdsType == LucidTdsEnums.SQLXMLTYPE)
            {
                col.length = LucidTdsEnums.SQL_USHORTVARMAXLEN;
            } 
            else if (Utilities.IsVarTimeTds(tdsType))
            {
                col.length = 0;
            }
            else if (tdsType == LucidTdsEnums.SQLDATE)
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

            if (LucidTdsEnums.SQLUDT == tdsType)
            {
                throw new NotImplementedException("Udt type is not implemented");
            }

            if (col.length == LucidTdsEnums.SQL_USHORTVARMAXLEN)
            {
                Debug.Assert(tdsType == LucidTdsEnums.SQLXMLTYPE ||
                             tdsType == LucidTdsEnums.SQLBIGVARCHAR ||
                             tdsType == LucidTdsEnums.SQLBIGVARBINARY ||
                             tdsType == LucidTdsEnums.SQLNVARCHAR ||
                             tdsType == LucidTdsEnums.SQLUDT,
                             "Invalid streaming datatype");

                col.metaType = MetaType.GetMaxMetaTypeFromMetaType(col.metaType);
                Debug.Assert(col.metaType.IsLong, "Max datatype not IsLong");
                col.length = int.MaxValue;

                byte byteLen;
                if (tdsType == LucidTdsEnums.SQLXMLTYPE)
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

            if (col.metaType.IsCharType && (tdsType != LucidTdsEnums.SQLXMLTYPE))
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
                codePage = LucidTdsEnums.CODE_PAGE_FROM_SORT_ID[collation._sortId];
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

        public async ValueTask SendLoginAsync(bool isAsync, CancellationToken ct)
        {
            //LucidTdsEnums.FeatureExtension requestedFeatures = LucidTdsEnums.FeatureExtension.None;
            //requestedFeatures |= LucidTdsEnums.FeatureExtension.GlobalTransactions
            //    | LucidTdsEnums.FeatureExtension.DataClassification
            //    | LucidTdsEnums.FeatureExtension.Tce
            //    | LucidTdsEnums.FeatureExtension.UTF8Support
            //    | LucidTdsEnums.FeatureExtension.SQLDNSCaching;

            
            LoginPacket packet = new LoginPacket();
            packet.ApplicationName = _connectionSettings.ApplicationName;
            packet.ClientHostName = _connectionSettings.WorkstationId;
            packet.ServerHostname = _hostname;
            packet.ClientInterfaceName = LucidTdsEnums.SQL_PROVIDER_NAME;
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
            packet.UseSSPI = _connectionSettings.UseSSPI;
            packet.ReadOnlyIntent = _connectionSettings.ReadOnlyIntent;

            TdsLoginHandler loginHandler = new TdsLoginHandler(this._writeStream, _authOptions);

            await loginHandler.Send(packet, isAsync, ct).ConfigureAwait(false);

            DisableSsl();
        }

        public async ValueTask SendQuery(string query, bool isAsync, CancellationToken ct)
        {
            //_readStream.ResetPacket();
            int marsHeaderSize = 18;
            int notificationHeaderSize = 0; // TODO: Needed for sql notifications feature. Not implemetned yet
            int totalHeaderLength = 4 + marsHeaderSize + notificationHeaderSize;
            await _writeStream.WriteIntAsync(totalHeaderLength, isAsync, ct).ConfigureAwait(false);

            await _writeStream.WriteIntAsync(marsHeaderSize, isAsync, ct).ConfigureAwait(false);

            // Write the MARS header data. 
            await _writeStream.WriteShortAsync(LucidTdsEnums.HEADERTYPE_MARS, isAsync, ct).ConfigureAwait(false);
            int transactionId = 0; // TODO: Needed for txn support
            await _writeStream.WriteLongAsync(transactionId, isAsync, ct).ConfigureAwait(false);

            int resultCount = 0;
            // TODO Increment and add the open results count per connection.
            await _writeStream.WriteIntAsync(++resultCount, isAsync, ct).ConfigureAwait(false);
            _writeStream.PacketHeaderType = LucidTdsEnums.MT_SQL;

            // TODO: Add the enclave support. The server doesnt support Enclaves yet.

            await _writeStream.WriteStringAsync(query, isAsync, ct).ConfigureAwait(false);
            await _writeStream.FlushAsync(ct, isAsync, true).ConfigureAwait(false);
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
