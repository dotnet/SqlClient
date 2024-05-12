using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using Microsoft.Data.SqlClient.SqlClientX.TDS;
using Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.SqlValuesProcessing
{
    internal class SqlValuesProcessor
    {
        private readonly TdsReadStream _readStream;

        public SqlValuesProcessor(TdsReadStream readStream)
        {
            this._readStream = readStream;
        }

        

        internal async ValueTask ReadSqlStringValueAsync(SqlBuffer value,
            byte type, 
            int length, 
            Encoding encoding, 
            bool isPlp,
            StreamExecutionState executionState,
            ProtocolMetadata protocolMetadata,
            bool isAsync,
            CancellationToken ct)
        {
            switch (type)
            {
                case TdsEnums.SQLCHAR:
                case TdsEnums.SQLBIGCHAR:
                case TdsEnums.SQLVARCHAR:
                case TdsEnums.SQLBIGVARCHAR:
                case TdsEnums.SQLTEXT:
                    // If bigvarchar(max), we only read the first chunk here,
                    // expecting the caller to read the rest
                    if (encoding == null)
                    {
                        // if hitting 7.0 server, encoding will be null in metadata for columns or return values since
                        // 7.0 has no support for multiple code pages in data - single code page support only
                        encoding = protocolMetadata.DefaultEncoding;
                    }
                    string stringValue = await _readStream.ReadStringWithEncodingAsync(length, 
                        encoding, 
                        isPlp,
                        executionState,
                        isAsync, 
                        ct).ConfigureAwait(false);

                    value.SetToString(stringValue);
                    break;

                case TdsEnums.SQLNCHAR:
                case TdsEnums.SQLNVARCHAR:
                case TdsEnums.SQLNTEXT:
                    {
                        string s = null;

                        if (isPlp)
                        {
                            //char[] cc = null;
                            //bool buffIsRented = false;
                            //bool result = TryReadPlpUnicodeChars(ref cc, 0, length >> 1, stateObj, out length, supportRentedBuff: true, rentedBuff: ref buffIsRented);

                            //if (result)
                            //{
                            //    if (length > 0)
                            //    {
                            //        s = new string(cc, 0, length);
                            //    }
                            //    else
                            //    {
                            //        s = "";
                            //    }
                            //}

                            //if (buffIsRented)
                            //{
                            //    // do not use clearArray:true on the rented array because it can be massively larger
                            //    // than the space we've used and we would incur performance clearing memory that
                            //    // we haven't used and can't leak out information.
                            //    // clear only the length that we know we have used.
                            //    cc.AsSpan(0, length).Clear();
                            //    ArrayPool<char>.Shared.Return(cc, clearArray: false);
                            //    cc = null;
                            //}

                            //if (!result)
                            //{
                            //    return false;
                            //}
                            throw new NotImplementedException();
                        }
                        else
                        {
                            s = await _readStream.ReadStringAsync(length >> 1, isAsync, ct);
                        }

                        value.SetToString(s);
                        break;
                    }

                default:
                    Debug.Fail("Unknown tds type for SqlString!" + type.ToString(CultureInfo.InvariantCulture));
                    break;
            }
        }
    }
}
