using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets
{
    internal static class SqlValuesHandler
    {
        public static async ValueTask ReadSqlDecimalAsync(this TdsReadStream stream,
            SqlBuffer value,
            int length,
            byte precision,
            byte scale,
            bool isAsync,
            CancellationToken ct)
        {
            byte byteValue = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            bool fPositive = (1 == byteValue);
            length = checked((int)length - 1);
            
            if (isAsync)
            {

            }
            int[] bits = await stream.ReadDecimalBitsAsync(length, isAsync, ct);

            value.SetToDecimal(precision, scale, fPositive, bits);
        }

        private static async ValueTask<int[]> ReadDecimalBitsAsync(
            this TdsReadStream stream,
            int length, 
            bool isAsync,
            CancellationToken ct)
        {
            int[] bits = stream.Scratchpad._decimalBits;
            

            for (int i = 0; i < bits.Length; i++)
                bits[i] = 0;
            
            Debug.Assert((length > 0) &&
                         (length <= TdsEnums.MAX_NUMERIC_LEN - 1) &&
                         (length % 4 == 0),
                         "decimal should have 4, 8, 12, or 16 bytes of data");

            int decLength = length >> 2;

            for (int i = 0; i < decLength; i++)
            {
                bits[i] = await stream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
            }

            return bits;
        }

        internal static async ValueTask<SqlCollation> ProcessCollationAsync(
            this TdsReadStream stream,
            bool isAsync,
            CancellationToken ct)
        {
            uint info = await stream.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);

            byte sortId = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            // ToDO cache the collation and then return it. 
            // its going to reduce allocations.
            return new SqlCollation(info, sortId);
        }

        internal static async ValueTask ReadSqlDateTimeAsync(
            this TdsReadStream stream,
            SqlBuffer value, 
            byte tdsType, 
            int length, 
            byte scale,
            bool isAsync,
            CancellationToken ct)
        {

            byte[] datetimeBuffer = new byte[length];

            _ = isAsync ? await stream.ReadAsync(datetimeBuffer.AsMemory(),ct).ConfigureAwait(false)
                : stream.Read(datetimeBuffer.AsSpan());
            
            switch (tdsType)
            {
                case TdsEnums.SQLDATE:
                    Debug.Assert(length == 3, "invalid length for date type!");
                    value.SetToDate(datetimeBuffer);
                    break;

                case TdsEnums.SQLTIME:
                    Debug.Assert(3 <= length && length <= 5, "invalid length for time type!");
                    value.SetToTime(datetimeBuffer, scale, scale);
                    break;

                case TdsEnums.SQLDATETIME2:
                    Debug.Assert(6 <= length && length <= 8, "invalid length for datetime2 type!");
                    value.SetToDateTime2(datetimeBuffer, scale, scale);
                    break;

                case TdsEnums.SQLDATETIMEOFFSET:
                    Debug.Assert(8 <= length && length <= 10, "invalid length for datetimeoffset type!");
                    value.SetToDateTimeOffset(datetimeBuffer, scale, scale);
                    break;

                default:
                    Debug.Fail("ReadSqlDateTime is called with the wrong tdsType");
                    break;
            }
        }

        internal static async ValueTask ReadSqlVariantAsync(
            this TdsReadStream stream,
            SqlBuffer value, 
            int lenTotal, 
            bool isAsync,
            CancellationToken ct)
        {
            // get the SQLVariant type
            byte type = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            ushort lenMax = 0; // maximum lenData of value inside variant

            // read cbPropBytes
            byte cbPropsActual = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);

            MetaType mt = MetaType.GetSqlDataType(type, 0 /*no user datatype*/, 0 /* no lenData, non-nullable type */);
            byte cbPropsExpected = mt.PropBytes;

            int lenConsumed = TdsEnums.SQLVARIANT_SIZE + cbPropsActual; // type, count of propBytes, and actual propBytes
            int lenData = lenTotal - lenConsumed; // length of actual data

            // read known properties and skip unknown properties
            Debug.Assert(cbPropsActual >= cbPropsExpected, "cbPropsActual is less that cbPropsExpected!");

            //
            // now read the value
            //
            switch (type)
            {
                case TdsEnums.SQLBIT:
                case TdsEnums.SQLINT1:
                case TdsEnums.SQLINT2:
                case TdsEnums.SQLINT4:
                case TdsEnums.SQLINT8:
                case TdsEnums.SQLFLT4:
                case TdsEnums.SQLFLT8:
                case TdsEnums.SQLMONEY:
                case TdsEnums.SQLMONEY4:
                case TdsEnums.SQLDATETIME:
                case TdsEnums.SQLDATETIM4:
                case TdsEnums.SQLUNIQUEID:
                    await stream.ReadSqlValuesInternalAsync(value, type, lenData, isAsync, ct).ConfigureAwait(false);
                    break;

                case TdsEnums.SQLDECIMALN:
                case TdsEnums.SQLNUMERICN:
                    {
                        Debug.Assert(cbPropsExpected == 2, "SqlVariant: invalid PropBytes for decimal/numeric type!");

                        byte precision = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        
                        byte scale = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        
                        // skip over unknown properties
                        if (cbPropsActual > cbPropsExpected)
                        {
                            await stream.SkipBytesAsync(cbPropsActual - cbPropsExpected, isAsync, ct).ConfigureAwait(false);
                        }
                        await stream.ReadSqlDecimalAsync(value, TdsEnums.MAX_NUMERIC_LEN, precision, scale, isAsync, ct).ConfigureAwait(false);
                        break;
                    }

                case TdsEnums.SQLBIGBINARY:
                case TdsEnums.SQLBIGVARBINARY:
                    //Debug.Assert(TdsEnums.VARNULL == lenData, "SqlVariant: data length for Binary indicates null?");
                    Debug.Assert(cbPropsExpected == 2, "SqlVariant: invalid PropBytes for binary type!");

                    lenMax = await stream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                    Debug.Assert(lenMax != TdsEnums.SQL_USHORTVARMAXLEN, "bigvarbinary(max) in a sqlvariant");

                    // skip over unknown properties
                    if (cbPropsActual > cbPropsExpected)
                    {
                        await stream.SkipBytesAsync(cbPropsActual - cbPropsExpected, isAsync, ct).ConfigureAwait(false);
                    }

                    goto case TdsEnums.SQLBIT;

                case TdsEnums.SQLBIGCHAR:
                case TdsEnums.SQLBIGVARCHAR:
                case TdsEnums.SQLNCHAR:
                case TdsEnums.SQLNVARCHAR:
                    {
                        Debug.Assert(cbPropsExpected == 7, "SqlVariant: invalid PropBytes for character type!");

                        SqlCollation collation = await stream.ProcessCollationAsync(isAsync, ct).ConfigureAwait(false);

                        lenMax = await stream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);

                        Debug.Assert(lenMax != TdsEnums.SQL_USHORTVARMAXLEN, "bigvarchar(max) or nvarchar(max) in a sqlvariant");

                        // skip over unknown properties
                        if (cbPropsActual > cbPropsExpected)
                        {
                            await stream.SkipBytesAsync(cbPropsActual - cbPropsExpected, isAsync, ct).ConfigureAwait(false);
                        }

                        Encoding encoding = Encoding.GetEncoding(SqlPhysicalConnection.GetCodePage(collation));
                        await stream.ReadSqlStringValueAsync(value, type, lenData, encoding, false, isAsync, ct).ConfigureAwait(false);
                        break;
                    }
                case TdsEnums.SQLDATE:
                    await stream.ReadSqlDateTimeAsync(value, type, lenData, 0, isAsync, ct);
                    break;

                case TdsEnums.SQLTIME:
                case TdsEnums.SQLDATETIME2:
                case TdsEnums.SQLDATETIMEOFFSET:
                    {
                        Debug.Assert(cbPropsExpected == 1, "SqlVariant: invalid PropBytes for time/datetime2/datetimeoffset type!");

                        byte scale = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                        
                        // skip over unknown properties
                        if (cbPropsActual > cbPropsExpected)
                        {
                            await stream.SkipBytesAsync(cbPropsActual - cbPropsExpected, isAsync, ct).ConfigureAwait(false);
                        }
                        await stream.ReadSqlDateTimeAsync(value, type, lenData, scale, isAsync, ct).ConfigureAwait(false);
                        break;
                    }

                default:
                    Debug.Fail("Unknown tds type in SqlVariant!" + type.ToString(CultureInfo.InvariantCulture));
                    break;
            } // switch

        }


        public static async ValueTask ReadSqlValuesInternalAsync(this TdsReadStream stream,
            SqlBuffer value,
            byte tdsType,
            int length,
            bool isAsync,
            CancellationToken ct)
        {
            switch (tdsType)
            {
                case TdsEnums.SQLBIT:
                case TdsEnums.SQLBITN:
                    Debug.Assert(length == 1, "invalid length for SqlBoolean type!");
                    byte byteValue = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                    value.Boolean = (byteValue != 0);
                    break;

                case TdsEnums.SQLINTN:
                    if (length == 1)
                    {
                        goto case TdsEnums.SQLINT1;
                    }
                    else if (length == 2)
                    {
                        goto case TdsEnums.SQLINT2;
                    }
                    else if (length == 4)
                    {
                        goto case TdsEnums.SQLINT4;
                    }
                    else
                    {
                        goto case TdsEnums.SQLINT8;
                    }

                case TdsEnums.SQLINT1:
                    Debug.Assert(length == 1, "invalid length for SqlByte type!");
                    value.Byte = await stream.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                    break;

                case TdsEnums.SQLINT2:
                    Debug.Assert(length == 2, "invalid length for SqlInt16 type!");
                    short shortValue = await stream.ReadInt16Async(isAsync, ct).ConfigureAwait(false);
                    value.Int16 = shortValue;
                    break;

                case TdsEnums.SQLINT4:
                    Debug.Assert(length == 4, "invalid length for SqlInt32 type!");
                    int intValue = await stream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                    value.Int32 = intValue;
                    break;

                case TdsEnums.SQLINT8:
                    Debug.Assert(length == 8, "invalid length for SqlInt64 type!");
                    long longValue = await stream.ReadInt64Async(isAsync, ct).ConfigureAwait(false);
                    value.Int64 = longValue;
                    break;

                case TdsEnums.SQLFLTN:
                    if (length == 4)
                    {
                        goto case TdsEnums.SQLFLT4;
                    }
                    else
                    {
                        goto case TdsEnums.SQLFLT8;
                    }

                case TdsEnums.SQLFLT4:
                    Debug.Assert(length == 4, "invalid length for SqlSingle type!");

                    value.Single = await stream.ReadSingleAsync(isAsync, ct).ConfigureAwait(false);
                    break;

                case TdsEnums.SQLFLT8:
                    Debug.Assert(length == 8, "invalid length for SqlDouble type!");
                    double doubleValue = await stream.ReadDoubleAsync(isAsync, ct).ConfigureAwait(false);
                    value.Double = doubleValue;
                    break;

                case TdsEnums.SQLMONEYN:
                    if (length == 4)
                    {
                        goto case TdsEnums.SQLMONEY4;
                    }
                    else
                    {
                        goto case TdsEnums.SQLMONEY;
                    }

                case TdsEnums.SQLMONEY:
                    {
                        int mid = await stream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                        uint lo = await stream.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
                        long l = (((long)mid) << 0x20) + ((long)lo);
                        value.SetToMoney(l);
                        break;
                    }

                case TdsEnums.SQLMONEY4:
                    intValue = await stream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                    value.SetToMoney(intValue);
                    break;

                case TdsEnums.SQLDATETIMN:
                    if (length == 4)
                    {
                        goto case TdsEnums.SQLDATETIM4;
                    }
                    else
                    {
                        goto case TdsEnums.SQLDATETIME;
                    }

                case TdsEnums.SQLDATETIM4:
                    ushort daypartShort = await stream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                    ushort timepartShort = await stream.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                    value.SetToDateTime(daypartShort, timepartShort * SqlDateTime.SQLTicksPerMinute);
                    break;

                case TdsEnums.SQLDATETIME:
                    int daypart = await stream.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                    uint timepart = await stream.ReadUInt32Async(isAsync, ct).ConfigureAwait(false);
                    value.SetToDateTime(daypart, (int)timepart);
                    break;

                case TdsEnums.SQLUNIQUEID:
                    {
                        Debug.Assert(length == 16, "invalid length for SqlGuid type!");
                        byte[] b = new byte[16];
                        if (isAsync)
                        {
                            await stream.ReadAsync(b.AsMemory(), ct).ConfigureAwait(false);
                        }
                        {
                            stream.Read(b.AsSpan());
                        }
                        value.Guid = new Guid(b);
                        break;
                    }

                case TdsEnums.SQLBINARY:
                case TdsEnums.SQLBIGBINARY:
                case TdsEnums.SQLBIGVARBINARY:
                case TdsEnums.SQLVARBINARY:
                case TdsEnums.SQLIMAGE:
                    {
                        // Note: Better not come here with plp data!!
                        Debug.Assert(length <= TdsEnums.MAXSIZE);
                        byte[] b = new byte[length];
                        if (isAsync)
                        {
                            await stream.ReadAsync(b.AsMemory(), ct).ConfigureAwait(false);
                        }
                        {
                            stream.Read(b.AsSpan());
                        }

                        value.SqlBinary = SqlBinary.WrapBytes(b);

                        break;
                    }

                case TdsEnums.SQLVARIANT:
                    await stream.ReadSqlVariantAsync(value, length, isAsync, ct).ConfigureAwait(false);
                    break;

                default:
                    Debug.Fail("Unknown SqlType!" + tdsType.ToString(CultureInfo.InvariantCulture));
                    break;
            } // switch
        }

        internal static async ValueTask ReadSqlStringValueAsync(
            this TdsReadStream stream,
            SqlBuffer value, byte type, int length, Encoding encoding, bool isPlp,
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
                        Debug.Fail("Encoding support not imlpemetned");
                        //encoding = _defaultEncoding;
                    }
                    string stringValue = await stream.ReadStringWithEncodingAsync(length, encoding, isPlp, isAsync, ct).ConfigureAwait(false);

                    value.SetToString(stringValue);
                    break;

                case TdsEnums.SQLNCHAR:
                case TdsEnums.SQLNVARCHAR:
                case TdsEnums.SQLNTEXT:
                    {
                        string s = null;

                        if (isPlp)
                        {
                            throw new NotImplementedException("Not implemented yet");

                        
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

                        }
                        else
                        {
                            s = await stream.ReadStringAsync(length >> 1, isAsync, ct);
                        }

                        value.SetToString(s);
                        break;
                    }

                default:
                    Debug.Fail("Unknown tds type for SqlString!" + type.ToString(CultureInfo.InvariantCulture));
                    break;
            }
        }

        internal static ValueTask ReadPlpUnicodeCharsAsync(
            this TdsReadStream stream,
            char[] buff, int offst, int len,
            bool isAsync, CancellationToken ct)
        {
            //int charsRead = 0;
            //int charsLeft = 0;
            //char[] newbuf;

            throw new NotImplementedException("Read Plp unicode is not implemented");
            //if (stateObj._longlen == 0)
            //{
            //    Debug.Assert(stateObj._longlenleft == 0);
            //    totalCharsRead = 0;
            //    return true;       // No data
            //}

            //Debug.Assert(((ulong)stateObj._longlen != TdsEnums.SQL_PLP_NULL), "Out of sync plp read request");

            //Debug.Assert((buff == null && offst == 0) || (buff.Length >= offst + len), "Invalid length sent to ReadPlpUnicodeChars()!");
            //charsLeft = len;

            //// If total length is known up front, the length isn't specified as unknown 
            //// and the caller doesn't pass int.max/2 indicating that it doesn't know the length
            //// allocate the whole buffer in one shot instead of realloc'ing and copying over each time
            //if (buff == null && stateObj._longlen != TdsEnums.SQL_PLP_UNKNOWNLEN && len < (int.MaxValue >> 1))
            //{
            //    if (supportRentedBuff && len < 1073741824) // 1 Gib
            //    {
            //        buff = ArrayPool<char>.Shared.Rent((int)Math.Min((int)stateObj._longlen, len));
            //        rentedBuff = true;
            //    }
            //    else
            //    {
            //        buff = new char[(int)Math.Min((int)stateObj._longlen, len)];
            //        rentedBuff = false;
            //    }
            //}

            //if (stateObj._longlenleft == 0)
            //{
            //    ulong ignored;
            //    if (!stateObj.TryReadPlpLength(false, out ignored))
            //    {
            //        totalCharsRead = 0;
            //        return false;
            //    }
            //    if (stateObj._longlenleft == 0)
            //    { // Data read complete
            //        totalCharsRead = 0;
            //        return true;
            //    }
            //}

            //totalCharsRead = 0;

            //while (charsLeft > 0)
            //{
            //    charsRead = (int)Math.Min((stateObj._longlenleft + 1) >> 1, (ulong)charsLeft);
            //    if ((buff == null) || (buff.Length < (offst + charsRead)))
            //    {
            //        bool returnRentedBufferAfterCopy = rentedBuff;
            //        if (supportRentedBuff && (offst + charsRead) < 1073741824) // 1 Gib
            //        {
            //            newbuf = ArrayPool<char>.Shared.Rent(offst + charsRead);
            //            rentedBuff = true;
            //        }
            //        else
            //        {
            //            newbuf = new char[offst + charsRead];
            //            rentedBuff = false;
            //        }

            //        if (buff != null)
            //        {
            //            Buffer.BlockCopy(buff, 0, newbuf, 0, offst * 2);
            //            if (returnRentedBufferAfterCopy)
            //            {
            //                buff.AsSpan(0, offst).Clear();
            //                ArrayPool<char>.Shared.Return(buff, clearArray: false);
            //            }
            //        }
            //        buff = newbuf;
            //    }
            //    if (charsRead > 0)
            //    {
            //        if (!TryReadPlpUnicodeCharsChunk(buff, offst, charsRead, stateObj, out charsRead))
            //        {
            //            return false;
            //        }
            //        charsLeft -= charsRead;
            //        offst += charsRead;
            //        totalCharsRead += charsRead;
            //    }
            //    // Special case single byte left
            //    if (stateObj._longlenleft == 1 && (charsLeft > 0))
            //    {
            //        byte b1;
            //        if (!stateObj.TryReadByte(out b1))
            //        {
            //            return false;
            //        }
            //        stateObj._longlenleft--;
            //        ulong ignored;
            //        if (!stateObj.TryReadPlpLength(false, out ignored))
            //        {
            //            return false;
            //        }
            //        Debug.Assert((stateObj._longlenleft != 0), "ReadPlpUnicodeChars: Odd byte left at the end!");
            //        byte b2;
            //        if (!stateObj.TryReadByte(out b2))
            //        {
            //            return false;
            //        }
            //        stateObj._longlenleft--;
            //        // Put it at the end of the array. At this point we know we have an extra byte.
            //        buff[offst] = (char)(((b2 & 0xff) << 8) + (b1 & 0xff));
            //        offst = checked((int)offst + 1);
            //        charsRead++;
            //        charsLeft--;
            //        totalCharsRead++;
            //    }
            //    if (stateObj._longlenleft == 0)
            //    { // Read the next chunk or cleanup state if hit the end
            //        ulong ignored;
            //        if (!stateObj.TryReadPlpLength(false, out ignored))
            //        {
            //            return false;
            //        }
            //    }

            //    if (stateObj._longlenleft == 0)   // Data read complete
            //        break;
            //}
            //return true;
        }
    }
}
