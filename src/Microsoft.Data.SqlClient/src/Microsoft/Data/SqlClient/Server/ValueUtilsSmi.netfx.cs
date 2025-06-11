using System;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Data.Common;
using Microsoft.Data.SqlTypes;

namespace Microsoft.Data.SqlClient.Server
{
    // Utilities for manipulating values with the Smi interface.
    //
    //  THIS CLASS IS BUILT ON TOP OF THE SMI INTERFACE -- SMI SHOULD NOT DEPEND ON IT!
    //
    //  These are all based off of knowing the clr type of the value
    //  as an ExtendedClrTypeCode enum for rapid access (lookup in static array is best, if possible).
    internal static partial class ValueUtilsSmi
    {

        internal static SqlSequentialStreamSmi GetSequentialStream(SmiEventSink_Default sink, ITypedGettersV3 getters, int ordinal, SmiMetaData metaData, bool bypassTypeCheck = false)
        {
            Debug.Assert(!ValueUtilsSmi.IsDBNull_Unchecked(sink, getters, ordinal), "Should not try to get a SqlSequentialStreamSmi on a null column");
            ThrowIfITypedGettersIsNull(sink, getters, ordinal);
            if ((!bypassTypeCheck) && (!CanAccessGetterDirectly(metaData, ExtendedClrTypeCode.Stream)))
            {
                throw ADP.InvalidCast();
            }

            // This will advance the column to ordinal
            long length = GetBytesLength_Unchecked(sink, getters, ordinal);
            return new SqlSequentialStreamSmi(sink, getters, ordinal, length);
        }

        internal static SqlSequentialTextReaderSmi GetSequentialTextReader(SmiEventSink_Default sink, ITypedGettersV3 getters, int ordinal, SmiMetaData metaData)
        {
            Debug.Assert(!ValueUtilsSmi.IsDBNull_Unchecked(sink, getters, ordinal), "Should not try to get a SqlSequentialTextReaderSmi on a null column");
            ThrowIfITypedGettersIsNull(sink, getters, ordinal);
            if (!CanAccessGetterDirectly(metaData, ExtendedClrTypeCode.TextReader))
            {
                throw ADP.InvalidCast();
            }

            // This will advance the column to ordinal
            long length = GetCharsLength_Unchecked(sink, getters, ordinal);
            return new SqlSequentialTextReaderSmi(sink, getters, ordinal, length);
        }

        internal static Stream GetStream(SmiEventSink_Default sink, ITypedGettersV3 getters, int ordinal, SmiMetaData metaData, bool bypassTypeCheck = false)
        {
            bool isDbNull = ValueUtilsSmi.IsDBNull_Unchecked(sink, getters, ordinal);

            // If a sql_variant, get the internal type
            if (!bypassTypeCheck)
            {
                if ((!isDbNull) && (metaData.SqlDbType == SqlDbType.Variant))
                {
                    metaData = getters.GetVariantType(sink, ordinal);
                }
                // If the SqlDbType is still variant, then it must contain null, so don't throw InvalidCast
                if ((metaData.SqlDbType != SqlDbType.Variant) && (!CanAccessGetterDirectly(metaData, ExtendedClrTypeCode.Stream)))
                {
                    throw ADP.InvalidCast();
                }
            }

            byte[] data;
            if (isDbNull)
            {
                // "null" stream
                data = new byte[0];
            }
            else
            {
                // Read all data
                data = GetByteArray_Unchecked(sink, getters, ordinal);
            }

            // Wrap data in pre-built object
            return new MemoryStream(data, writable: false);
        }

        internal static TextReader GetTextReader(SmiEventSink_Default sink, ITypedGettersV3 getters, int ordinal, SmiMetaData metaData)
        {
            bool isDbNull = ValueUtilsSmi.IsDBNull_Unchecked(sink, getters, ordinal);

            // If a sql_variant, get the internal type
            if ((!isDbNull) && (metaData.SqlDbType == SqlDbType.Variant))
            {
                metaData = getters.GetVariantType(sink, ordinal);
            }
            // If the SqlDbType is still variant, then it must contain null, so don't throw InvalidCast
            if ((metaData.SqlDbType != SqlDbType.Variant) && (!CanAccessGetterDirectly(metaData, ExtendedClrTypeCode.TextReader)))
            {
                throw ADP.InvalidCast();
            }

            string data;
            if (isDbNull)
            {
                // "null" textreader
                data = string.Empty;
            }
            else
            {
                // Read all data
                data = GetString_Unchecked(sink, getters, ordinal);
            }

            // Wrap in pre-built object
            return new StringReader(data);
        }

        // calling GetTimeSpan on possibly v100 SMI
        internal static TimeSpan GetTimeSpan(SmiEventSink_Default sink, ITypedGettersV3 getters, int ordinal, SmiMetaData metaData, bool gettersSupport2008DateTime)
        {
            if (gettersSupport2008DateTime)
            {
                return GetTimeSpan(sink, (SmiTypedGetterSetter)getters, ordinal, metaData);
            }
            ThrowIfITypedGettersIsNull(sink, getters, ordinal);
            object obj = GetValue(sink, getters, ordinal, metaData);
            if (obj == null)
            {
                throw ADP.InvalidCast();
            }
            return (TimeSpan)obj;
        }

        internal static SqlBuffer.StorageType SqlDbTypeToStorageType(SqlDbType dbType)
        {
            int index = unchecked((int)dbType);
            Debug.Assert(index >= 0 && index < s_dbTypeToStorageType.Length, string.Format(CultureInfo.InvariantCulture, "Unexpected dbType value: {0}", dbType));
            return s_dbTypeToStorageType[index];
        }

        private static void GetNullOutputParameterSmi(SmiMetaData metaData, SqlBuffer targetBuffer, ref object result)
        {
            if (SqlDbType.Udt == metaData.SqlDbType)
            {
                result = NullUdtInstance(metaData);
            }
            else
            {
                SqlBuffer.StorageType stype = SqlDbTypeToStorageType(metaData.SqlDbType);
                if (SqlBuffer.StorageType.Empty == stype)
                {
                    result = DBNull.Value;
                }
                else if (SqlBuffer.StorageType.SqlBinary == stype)
                {
                    // special case SqlBinary, 'cause tds parser never sets SqlBuffer to null, just to empty!
                    targetBuffer.SqlBinary = SqlBinary.Null;
                }
                else if (SqlBuffer.StorageType.SqlGuid == stype)
                {
                    targetBuffer.SqlGuid = SqlGuid.Null;
                }
                else
                {
                    targetBuffer.SetToNullOfType(stype);
                }
            }
        }
        
        /// <summary>
        /// UDTs and null variants come back via return value, all else is via targetBuffer.
        /// implements SqlClient 2.0-compatible output parameter semantics. 
        /// </summary>
        /// <param name="sink">Event sink for errors</param>
        /// <param name="getters">Getters interface to grab value from</param>
        /// <param name="ordinal">Parameter within getters</param>
        /// <param name="metaData">Getter's type for this ordinal</param>
        /// <param name="targetBuffer">Destination</param>
        internal static object GetOutputParameterV3Smi(
            SmiEventSink_Default sink, 
            ITypedGettersV3 getters, 
            int ordinal, 
            SmiMetaData metaData, 
            SqlBuffer targetBuffer)
        {
            object result = null;   // Workaround for UDT hack in non-Smi code paths.
            if (IsDBNull_Unchecked(sink, getters, ordinal))
            {
                GetNullOutputParameterSmi(metaData, targetBuffer, ref result);
            }
            else
            {
                switch (metaData.SqlDbType)
                {
                    case SqlDbType.BigInt:
                        targetBuffer.Int64 = GetInt64_Unchecked(sink, getters, ordinal);
                        break;
                    case SqlDbType.Binary:
                    case SqlDbType.Image:
                    case SqlDbType.Timestamp:
                    case SqlDbType.VarBinary:
                        targetBuffer.SqlBinary = GetSqlBinary_Unchecked(sink, getters, ordinal);
                        break;
                    case SqlDbType.Bit:
                        targetBuffer.Boolean = GetBoolean_Unchecked(sink, getters, ordinal);
                        break;
                    case SqlDbType.NChar:
                    case SqlDbType.NText:
                    case SqlDbType.NVarChar:
                    case SqlDbType.Char:
                    case SqlDbType.VarChar:
                    case SqlDbType.Text:
                        targetBuffer.SetToString(GetString_Unchecked(sink, getters, ordinal));
                        break;
                    case SqlDbType.DateTime:
                    case SqlDbType.SmallDateTime:
                        {
                            SqlDateTime dt = new(GetDateTime_Unchecked(sink, getters, ordinal));
                            targetBuffer.SetToDateTime(dt.DayTicks, dt.TimeTicks);
                            break;
                        }
                    case SqlDbType.Decimal:
                        {
                            SqlDecimal dec = GetSqlDecimal_Unchecked(sink, getters, ordinal);
                            targetBuffer.SetToDecimal(dec.Precision, dec.Scale, dec.IsPositive, dec.Data);
                            break;
                        }
                    case SqlDbType.Float:
                        targetBuffer.Double = GetDouble_Unchecked(sink, getters, ordinal);
                        break;
                    case SqlDbType.Int:
                        targetBuffer.Int32 = GetInt32_Unchecked(sink, getters, ordinal);
                        break;
                    case SqlDbType.Money:
                    case SqlDbType.SmallMoney:
                        targetBuffer.SetToMoney(GetInt64_Unchecked(sink, getters, ordinal));
                        break;
                    case SqlDbType.Real:
                        targetBuffer.Single = GetSingle_Unchecked(sink, getters, ordinal);
                        break;
                    case SqlDbType.UniqueIdentifier:
                        targetBuffer.SqlGuid = new SqlGuid(GetGuid_Unchecked(sink, getters, ordinal));
                        break;
                    case SqlDbType.SmallInt:
                        targetBuffer.Int16 = GetInt16_Unchecked(sink, getters, ordinal);
                        break;
                    case SqlDbType.TinyInt:
                        targetBuffer.Byte = GetByte_Unchecked(sink, getters, ordinal);
                        break;
                    case SqlDbType.Variant:
                        // For variants, recur using the current value's sqldbtype
                        metaData = getters.GetVariantType(sink, ordinal);
                        sink.ProcessMessagesAndThrow();
                        Debug.Assert(SqlDbType.Variant != metaData.SqlDbType, "Variant-within-variant not supposed to be possible!");
                        GetOutputParameterV3Smi(sink, getters, ordinal, metaData, targetBuffer);
                        break;
                    case SqlDbType.Udt:
                        result = GetUdt_LengthChecked(sink, getters, ordinal, metaData);
                        break;
                    case SqlDbType.Xml:
                        targetBuffer.SqlXml = GetSqlXml_Unchecked(sink, getters, ordinal, null);
                        break;
                    default:
                        Debug.Assert(false, "Unexpected SqlDbType");
                        break;
                }
            }

            return result;
        }
        
        /// <summary>
        /// UDTs and null variants come back via return value, all else is via targetBuffer.
        ///  implements SqlClient 1.1-compatible output parameter semantics. 
        /// </summary>
        /// <param name="sink">Event sink for errors</param>
        /// <param name="getters">Getters interface to grab value from</param>
        /// <param name="ordinal">Parameter within getters</param>
        /// <param name="metaData">Getter's type for this ordinal</param>
        /// <param name="targetBuffer">Destination</param>
        internal static object GetOutputParameterV200Smi(
            SmiEventSink_Default sink, 
            SmiTypedGetterSetter getters, 
            int ordinal, 
            SmiMetaData metaData, 
            SqlBuffer targetBuffer)
        {
            object result = null;   // Workaround for UDT hack in non-Smi code paths.
            if (IsDBNull_Unchecked(sink, getters, ordinal))
            {
                GetNullOutputParameterSmi(metaData, targetBuffer, ref result);
            }
            else
            {
                switch (metaData.SqlDbType)
                {
                    // new types go here
                    case SqlDbType.Variant: // Handle variants specifically for v200, since they could contain v200 types
                        // For variants, recur using the current value's sqldbtype
                        metaData = getters.GetVariantType(sink, ordinal);
                        sink.ProcessMessagesAndThrow();
                        Debug.Assert(SqlDbType.Variant != metaData.SqlDbType, "Variant-within-variant not supposed to be possible!");
                        GetOutputParameterV200Smi(sink, getters, ordinal, metaData, targetBuffer);
                        break;
                    case SqlDbType.Date:
                        targetBuffer.SetToDate(GetDateTime_Unchecked(sink, getters, ordinal));
                        break;
                    case SqlDbType.DateTime2:
                        targetBuffer.SetToDateTime2(GetDateTime_Unchecked(sink, getters, ordinal), metaData.Scale);
                        break;
                    case SqlDbType.Time:
                        targetBuffer.SetToTime(GetTimeSpan_Unchecked(sink, getters, ordinal), metaData.Scale);
                        break;
                    case SqlDbType.DateTimeOffset:
                        targetBuffer.SetToDateTimeOffset(GetDateTimeOffset_Unchecked(sink, getters, ordinal), metaData.Scale);
                        break;
                    default:
                        result = GetOutputParameterV3Smi(sink, getters, ordinal, metaData, targetBuffer);
                        break;
                }
            }

            return result;
        }

        private static readonly SqlBuffer.StorageType[] s_dbTypeToStorageType = new SqlBuffer.StorageType[] {
            SqlBuffer.StorageType.Int64,            // BigInt
            SqlBuffer.StorageType.SqlBinary,        // Binary
            SqlBuffer.StorageType.Boolean,          // Bit
            SqlBuffer.StorageType.String,           // Char
            SqlBuffer.StorageType.DateTime,         // DateTime
            SqlBuffer.StorageType.Decimal,          // Decimal
            SqlBuffer.StorageType.Double,           // Float
            SqlBuffer.StorageType.SqlBinary,        // Image
            SqlBuffer.StorageType.Int32,            // Int
            SqlBuffer.StorageType.Money,            // Money
            SqlBuffer.StorageType.String,           // NChar 
            SqlBuffer.StorageType.String,           // NText 
            SqlBuffer.StorageType.String,           // NVarChar 
            SqlBuffer.StorageType.Single,           // Real
            SqlBuffer.StorageType.SqlGuid,          // UniqueIdentifier
            SqlBuffer.StorageType.DateTime,         // SmallDateTime
            SqlBuffer.StorageType.Int16,            // SmallInt
            SqlBuffer.StorageType.Money,            // SmallMoney
            SqlBuffer.StorageType.String,           // Text
            SqlBuffer.StorageType.SqlBinary,        // Timestamp
            SqlBuffer.StorageType.Byte,             // TinyInt
            SqlBuffer.StorageType.SqlBinary,        // VarBinary
            SqlBuffer.StorageType.String,           // VarChar
            SqlBuffer.StorageType.Empty,            // Variant
            SqlBuffer.StorageType.Empty,            // 24
            SqlBuffer.StorageType.SqlXml,           // Xml
            SqlBuffer.StorageType.Empty,            // 26
            SqlBuffer.StorageType.Empty,            // 27
            SqlBuffer.StorageType.Empty,            // 28
            SqlBuffer.StorageType.Empty,            // Udt
            SqlBuffer.StorageType.Empty,            // Structured
            SqlBuffer.StorageType.Date,             // Date
            SqlBuffer.StorageType.Time,             // Time
            SqlBuffer.StorageType.DateTime2,        // DateTime2
            SqlBuffer.StorageType.DateTimeOffset,   // DateTimeOffset
        };

        internal static void FillCompatibleITypedSettersFromRecord(SmiEventSink_Default sink, ITypedSettersV3 setters, SmiMetaData[] metaData, SqlDataRecord record)
        {
            FillCompatibleITypedSettersFromRecord(sink, setters, metaData, record, null);
        }

        internal static void FillCompatibleITypedSettersFromRecord(SmiEventSink_Default sink, ITypedSettersV3 setters, SmiMetaData[] metaData, SqlDataRecord record, SmiDefaultFieldsProperty useDefaultValues)
        {
            for (int i = 0; i < metaData.Length; ++i)
            {
                if (useDefaultValues != null && useDefaultValues[i])
                {
                    continue;
                }
                if (record.IsDBNull(i))
                {
                    ValueUtilsSmi.SetDBNull_Unchecked(sink, setters, i);
                }
                else
                {
                    switch (metaData[i].SqlDbType)
                    {
                        case SqlDbType.BigInt:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.Int64));
                            SetInt64_Unchecked(sink, setters, i, record.GetInt64(i));
                            break;
                        case SqlDbType.Binary:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlBytes));
                            SetBytes_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        case SqlDbType.Bit:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.Boolean));
                            SetBoolean_Unchecked(sink, setters, i, record.GetBoolean(i));
                            break;
                        case SqlDbType.Char:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlChars));
                            SetChars_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        case SqlDbType.DateTime:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.DateTime));
                            SetDateTime_Checked(sink, setters, i, metaData[i], record.GetDateTime(i));
                            break;
                        case SqlDbType.Decimal:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlDecimal));
                            SetSqlDecimal_Unchecked(sink, setters, i, record.GetSqlDecimal(i));
                            break;
                        case SqlDbType.Float:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.Double));
                            SetDouble_Unchecked(sink, setters, i, record.GetDouble(i));
                            break;
                        case SqlDbType.Image:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlBytes));
                            SetBytes_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        case SqlDbType.Int:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.Int32));
                            SetInt32_Unchecked(sink, setters, i, record.GetInt32(i));
                            break;
                        case SqlDbType.Money:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlMoney));
                            SetSqlMoney_Unchecked(sink, setters, i, metaData[i], record.GetSqlMoney(i));
                            break;
                        case SqlDbType.NChar:
                        case SqlDbType.NText:
                        case SqlDbType.NVarChar:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlChars));
                            SetChars_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        case SqlDbType.Real:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.Single));
                            SetSingle_Unchecked(sink, setters, i, record.GetFloat(i));
                            break;
                        case SqlDbType.UniqueIdentifier:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.Guid));
                            SetGuid_Unchecked(sink, setters, i, record.GetGuid(i));
                            break;
                        case SqlDbType.SmallDateTime:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.DateTime));
                            SetDateTime_Checked(sink, setters, i, metaData[i], record.GetDateTime(i));
                            break;
                        case SqlDbType.SmallInt:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.Int16));
                            SetInt16_Unchecked(sink, setters, i, record.GetInt16(i));
                            break;
                        case SqlDbType.SmallMoney:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlMoney));
                            SetSqlMoney_Checked(sink, setters, i, metaData[i], record.GetSqlMoney(i));
                            break;
                        case SqlDbType.Text:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlChars));
                            SetChars_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        case SqlDbType.Timestamp:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlBytes));
                            SetBytes_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        case SqlDbType.TinyInt:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.Byte));
                            SetByte_Unchecked(sink, setters, i, record.GetByte(i));
                            break;
                        case SqlDbType.VarBinary:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlBytes));
                            SetBytes_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        case SqlDbType.VarChar:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.String));
                            SetChars_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        case SqlDbType.Xml:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlXml));
                            SetSqlXml_Unchecked(sink, setters, i, record.GetSqlXml(i));    // perf improvement?
                            break;
                        case SqlDbType.Variant:
                            object o = record.GetSqlValue(i);
                            ExtendedClrTypeCode typeCode = MetaDataUtilsSmi.DetermineExtendedTypeCode(o);
                            SetCompatibleValue(sink, setters, i, metaData[i], o, typeCode, 0);
                            break;
                        case SqlDbType.Udt:
                            Debug.Assert(CanAccessSetterDirectly(metaData[i], ExtendedClrTypeCode.SqlBytes));
                            SetBytes_FromRecord(sink, setters, i, metaData[i], record, 0);
                            break;
                        default:
                            Debug.Assert(false, "unsupported DbType:" + metaData[i].SqlDbType.ToString());
                            throw ADP.NotSupported();
                    }
                }
            }
        }

        // spool a Stream into a scratch stream from the Smi interface and return it as a SqlStreamChars
        internal static SqlStreamChars CopyIntoNewSmiScratchStreamChars(Stream source, SmiEventSink_Default sink, SmiContext context)
        {
            SqlClientWrapperSmiStreamChars dest = new(sink, context.GetScratchStream(sink));

            int chunkSize;
            if (source.CanSeek && source.Length < MaxByteChunkSize)
            {
                chunkSize = unchecked((int)source.Length);  // unchecked cast is safe due to check on line above
            }
            else
            {
                chunkSize = MaxByteChunkSize;
            }

            byte[] copyBuffer = new byte[chunkSize];
            int bytesRead;
            while (0 != (bytesRead = source.Read(copyBuffer, 0, chunkSize)))
            {
                dest.Write(copyBuffer, 0, bytesRead);
            }
            dest.Flush();

            // SQLBU 494334
            //  Need to re-wind scratch stream to beginning before returning
            dest.Seek(0, SeekOrigin.Begin);

            return dest;
        }
    }
}
