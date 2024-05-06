using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
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

        internal void ReadSqlStringValue(SqlBuffer value,
            byte type, 
            int length, 
            Encoding encoding, 
            bool isPlp,
            ProtocolMetadata protocolMetadata)
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
                    string stringValue = _readStream.ReadStringWithEncoding(length, encoding, isPlp);

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
                            s = _readStream.ReadString(length >> 1);
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
