// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SqlDataRecord/*' />
    public partial class SqlDataRecord : IDataRecord
    {
        private readonly SmiRecordBuffer _recordBuffer;
        private readonly SmiExtendedMetaData[] _columnSmiMetaData;
        private readonly SqlMetaData[] _columnMetaData;
        private FieldNameLookup _fieldNameLookup;
        private readonly bool _usesStringStorageForXml = false;

        private static readonly SmiMetaData s_maxNVarCharForXml = new SmiMetaData(
            SqlDbType.NVarChar,
            SmiMetaData.UnlimitedMaxLengthIndicator,
            SmiMetaData.DefaultNVarChar_NoCollation.Precision,
            SmiMetaData.DefaultNVarChar_NoCollation.Scale,
            SmiMetaData.DefaultNVarChar.LocaleId,
            SmiMetaData.DefaultNVarChar.CompareOptions,
            userDefinedType: null
        );

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/FieldCount/*' />
        public virtual int FieldCount => _columnMetaData.Length;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetName/*' />
        public virtual string GetName(int ordinal) => GetSqlMetaData(ordinal).Name;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDataTypeName/*' />
        public virtual string GetDataTypeName(int ordinal)
        {
            SqlMetaData metaData = GetSqlMetaData(ordinal);
            if (metaData.SqlDbType == SqlDbType.Udt)
            {
                return metaData.UdtTypeName;
            }
            else
            {
                return MetaType.GetMetaTypeFromSqlDbType(metaData.SqlDbType, false).TypeName;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetFieldType/*' />
#if NET
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        public virtual Type GetFieldType(int ordinal) => GetFieldTypeFrameworkSpecific(ordinal);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetValue/*' />
        public virtual object GetValue(int ordinal) => GetValueFrameworkSpecific(ordinal);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetValues/*' />
        public virtual int GetValues(object[] values)
        {
            if (values == null)
            {
                throw ADP.ArgumentNull(nameof(values));
            }

            int copyLength = (values.Length < FieldCount) ? values.Length : FieldCount;
            for (int i = 0; i < copyLength; i++)
            {
                values[i] = GetValue(i);
            }

            return copyLength;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetOrdinal/*' />
        public virtual int GetOrdinal(string name)
        {
            if (_fieldNameLookup == null)
            {
                string[] names = new string[FieldCount];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = GetSqlMetaData(i).Name;
                }

                _fieldNameLookup = new FieldNameLookup(names, -1);
            }

            return _fieldNameLookup.GetOrdinal(name);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ItemOrdinal/*' />
        public virtual object this[int ordinal] => GetValue(ordinal);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ItemName/*' />
        public virtual object this[string name] => GetValue(GetOrdinal(name));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetBoolean/*' />
        public virtual bool GetBoolean(int ordinal) => ValueUtilsSmi.GetBoolean(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetByte/*' />
        public virtual byte GetByte(int ordinal) => ValueUtilsSmi.GetByte(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetBytes/*' />
        public virtual long GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) => ValueUtilsSmi.GetBytes(_recordBuffer, ordinal, GetSmiMetaData(ordinal), fieldOffset, buffer, bufferOffset, length, throwOnNull: true);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetChar/*' />
        public virtual char GetChar(int ordinal) => throw ADP.NotSupported();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetChars/*' />
        public virtual long GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) => ValueUtilsSmi.GetChars(_recordBuffer, ordinal, GetSmiMetaData(ordinal), fieldOffset, buffer, bufferOffset, length);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetGuid/*' />
        public virtual Guid GetGuid(int ordinal) => ValueUtilsSmi.GetGuid(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt16/*' />
        public virtual short GetInt16(int ordinal) => ValueUtilsSmi.GetInt16(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt32/*' />
        public virtual int GetInt32(int ordinal) => ValueUtilsSmi.GetInt32(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt64/*' />
        public virtual long GetInt64(int ordinal) => ValueUtilsSmi.GetInt64(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetFloat/*' />
        public virtual float GetFloat(int ordinal) => ValueUtilsSmi.GetSingle(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDouble/*' />
        public virtual double GetDouble(int ordinal) => ValueUtilsSmi.GetDouble(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetString/*' />
        public virtual string GetString(int ordinal)
        {
            SmiMetaData colMeta = GetSmiMetaData(ordinal);
            if (_usesStringStorageForXml && colMeta.SqlDbType == SqlDbType.Xml)
            {
                return ValueUtilsSmi.GetString(_recordBuffer, ordinal, s_maxNVarCharForXml);
            }
            else
            {
                return ValueUtilsSmi.GetString(_recordBuffer, ordinal, colMeta);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDecimal/*' />
        public virtual decimal GetDecimal(int ordinal) => ValueUtilsSmi.GetDecimal(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDateTime/*' />
        public virtual DateTime GetDateTime(int ordinal) => ValueUtilsSmi.GetDateTime(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDateTimeOffset/*' />
        public virtual DateTimeOffset GetDateTimeOffset(int ordinal) => ValueUtilsSmi.GetDateTimeOffset(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetTimeSpan/*' />
        public virtual TimeSpan GetTimeSpan(int ordinal) => ValueUtilsSmi.GetTimeSpan(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/IsDBNull/*' />
        public virtual bool IsDBNull(int ordinal)
        {
            ThrowIfInvalidOrdinal(ordinal);
            return ValueUtilsSmi.IsDBNull(_recordBuffer, ordinal);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlMetaData/*' />
        //  ISqlRecord implementation
        public virtual SqlMetaData GetSqlMetaData(int ordinal)
        {
            ThrowIfInvalidOrdinal(ordinal);
            return _columnMetaData[ordinal];
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlFieldType/*' />
        public virtual Type GetSqlFieldType(int ordinal) => MetaType.GetMetaTypeFromSqlDbType(GetSqlMetaData(ordinal).SqlDbType, false).SqlType;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlValue/*' />
        public virtual object GetSqlValue(int ordinal) => GetSqlValueFrameworkSpecific(ordinal);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlValues/*' />
        public virtual int GetSqlValues(object[] values)
        {
            if (values == null)
            {
                throw ADP.ArgumentNull(nameof(values));
            }

            int copyLength = (values.Length < FieldCount) ? values.Length : FieldCount;
            for (int i = 0; i < copyLength; i++)
            {
                values[i] = GetSqlValue(i);
            }

            return copyLength;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBinary/*' />
        public virtual SqlBinary GetSqlBinary(int ordinal) => ValueUtilsSmi.GetSqlBinary(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBytes/*' />
        public virtual SqlBytes GetSqlBytes(int ordinal) => GetSqlBytesFrameworkSpecific(ordinal);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlXml/*' />
        public virtual SqlXml GetSqlXml(int ordinal) => GetSqlXmlFrameworkSpecific(ordinal);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBoolean/*' />
        public virtual SqlBoolean GetSqlBoolean(int ordinal) => ValueUtilsSmi.GetSqlBoolean(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlByte/*' />
        public virtual SqlByte GetSqlByte(int ordinal) => ValueUtilsSmi.GetSqlByte(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlChars/*' />
        public virtual SqlChars GetSqlChars(int ordinal) => GetSqlCharsFrameworkSpecific(ordinal);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt16/*' />
        public virtual SqlInt16 GetSqlInt16(int ordinal) => ValueUtilsSmi.GetSqlInt16(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt32/*' />
        public virtual SqlInt32 GetSqlInt32(int ordinal) => ValueUtilsSmi.GetSqlInt32(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt64/*' />
        public virtual SqlInt64 GetSqlInt64(int ordinal) => ValueUtilsSmi.GetSqlInt64(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlSingle/*' />
        public virtual SqlSingle GetSqlSingle(int ordinal) => ValueUtilsSmi.GetSqlSingle(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDouble/*' />
        public virtual SqlDouble GetSqlDouble(int ordinal) => ValueUtilsSmi.GetSqlDouble(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlMoney/*' />
        public virtual SqlMoney GetSqlMoney(int ordinal) => ValueUtilsSmi.GetSqlMoney(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDateTime/*' />
        public virtual SqlDateTime GetSqlDateTime(int ordinal) => ValueUtilsSmi.GetSqlDateTime(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDecimal/*' />
        public virtual SqlDecimal GetSqlDecimal(int ordinal) => ValueUtilsSmi.GetSqlDecimal(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlString/*' />
        public virtual SqlString GetSqlString(int ordinal) => ValueUtilsSmi.GetSqlString(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlGuid/*' />
        public virtual SqlGuid GetSqlGuid(int ordinal) => ValueUtilsSmi.GetSqlGuid(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetValues/*' />
        // ISqlUpdateableRecord Implementation
        public virtual int SetValues(params object[] values) => SetValuesFrameworkSpecific(values);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetValue/*' />
        public virtual void SetValue(int ordinal, object value) => SetValueFrameworkSpecific(ordinal, value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetBoolean/*' />
        public virtual void SetBoolean(int ordinal, bool value) => ValueUtilsSmi.SetBoolean(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetByte/*' />
        public virtual void SetByte(int ordinal, byte value) => ValueUtilsSmi.SetByte(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetBytes/*' />
        public virtual void SetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) => ValueUtilsSmi.SetBytes(_recordBuffer, ordinal, GetSmiMetaData(ordinal), fieldOffset, buffer, bufferOffset, length);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetChar/*' />
        public virtual void SetChar(int ordinal, char value) => throw ADP.NotSupported();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetChars/*' />
        public virtual void SetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) => ValueUtilsSmi.SetChars(_recordBuffer, ordinal, GetSmiMetaData(ordinal), fieldOffset, buffer, bufferOffset, length);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt16/*' />
        public virtual void SetInt16(int ordinal, short value) => ValueUtilsSmi.SetInt16(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt32/*' />
        public virtual void SetInt32(int ordinal, int value) => ValueUtilsSmi.SetInt32(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt64/*' />
        public virtual void SetInt64(int ordinal, long value) => ValueUtilsSmi.SetInt64(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetFloat/*' />
        public virtual void SetFloat(int ordinal, float value) => ValueUtilsSmi.SetSingle(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDouble/*' />
        public virtual void SetDouble(int ordinal, double value) => ValueUtilsSmi.SetDouble(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetString/*' />
        public virtual void SetString(int ordinal, string value) => ValueUtilsSmi.SetString(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDecimal/*' />
        public virtual void SetDecimal(int ordinal, decimal value) => ValueUtilsSmi.SetDecimal(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDateTime/*' />
        public virtual void SetDateTime(int ordinal, DateTime value) => ValueUtilsSmi.SetDateTime(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetTimeSpan/*' />
        public virtual void SetTimeSpan(int ordinal, TimeSpan value) => SetTimeSpanFrameworkSpecific(ordinal, value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDateTimeOffset/*' />
        public virtual void SetDateTimeOffset(int ordinal, DateTimeOffset value) => SetDateTimeOffsetFrameworkSpecific(ordinal, value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDBNull/*' />
        public virtual void SetDBNull(int ordinal)
        {
            ThrowIfInvalidOrdinal(ordinal);
            ValueUtilsSmi.SetDBNull(_recordBuffer, ordinal);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetGuid/*' />
        public virtual void SetGuid(int ordinal, Guid value) => ValueUtilsSmi.SetGuid(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBoolean/*' />
        public virtual void SetSqlBoolean(int ordinal, SqlBoolean value) => ValueUtilsSmi.SetSqlBoolean(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlByte/*' />
        public virtual void SetSqlByte(int ordinal, SqlByte value) => ValueUtilsSmi.SetSqlByte(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt16/*' />
        public virtual void SetSqlInt16(int ordinal, SqlInt16 value) => ValueUtilsSmi.SetSqlInt16(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt32/*' />
        public virtual void SetSqlInt32(int ordinal, SqlInt32 value) => ValueUtilsSmi.SetSqlInt32(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt64/*' />
        public virtual void SetSqlInt64(int ordinal, SqlInt64 value) => ValueUtilsSmi.SetSqlInt64(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlSingle/*' />
        public virtual void SetSqlSingle(int ordinal, SqlSingle value) => ValueUtilsSmi.SetSqlSingle(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDouble/*' />
        public virtual void SetSqlDouble(int ordinal, SqlDouble value) => ValueUtilsSmi.SetSqlDouble(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlMoney/*' />
        public virtual void SetSqlMoney(int ordinal, SqlMoney value) => ValueUtilsSmi.SetSqlMoney(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDateTime/*' />
        public virtual void SetSqlDateTime(int ordinal, SqlDateTime value) => ValueUtilsSmi.SetSqlDateTime(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlXml/*' />
        public virtual void SetSqlXml(int ordinal, SqlXml value) => ValueUtilsSmi.SetSqlXml(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDecimal/*' />
        public virtual void SetSqlDecimal(int ordinal, SqlDecimal value) => ValueUtilsSmi.SetSqlDecimal(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlString/*' />
        public virtual void SetSqlString(int ordinal, SqlString value) => ValueUtilsSmi.SetSqlString(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBinary/*' />
        public virtual void SetSqlBinary(int ordinal, SqlBinary value) => ValueUtilsSmi.SetSqlBinary(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlGuid/*' />
        public virtual void SetSqlGuid(int ordinal, SqlGuid value) => ValueUtilsSmi.SetSqlGuid(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlChars/*' />
        public virtual void SetSqlChars(int ordinal, SqlChars value) => ValueUtilsSmi.SetSqlChars(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBytes/*' />
        public virtual void SetSqlBytes(int ordinal, SqlBytes value) => ValueUtilsSmi.SetSqlBytes(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

        //  SqlDataRecord public API
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ctor/*' />
        public SqlDataRecord(params SqlMetaData[] metaData)
        {
            // Initial consistency check
            if (metaData == null)
            {
                throw ADP.ArgumentNull(nameof(metaData));
            }

            _columnMetaData = new SqlMetaData[metaData.Length];
            _columnSmiMetaData = new SmiExtendedMetaData[metaData.Length];

            for (int i = 0; i < _columnSmiMetaData.Length; i++)
            {
                if (metaData[i] == null)
                {
                    throw ADP.ArgumentNull($"{nameof(metaData)}[{i}]");
                }
                _columnMetaData[i] = metaData[i];
                _columnSmiMetaData[i] = MetaDataUtilsSmi.SqlMetaDataToSmiExtendedMetaData(_columnMetaData[i]);
            }

            _recordBuffer = new MemoryRecordBuffer(_columnSmiMetaData);

            #if NETFRAMEWORK
            _usesStringStorageForXml = true;
            #endif
        }

        internal SmiExtendedMetaData GetSmiMetaData(int ordinal)
        {
            ThrowIfInvalidOrdinal(ordinal);
            return _columnSmiMetaData[ordinal];
        }

        internal void ThrowIfInvalidOrdinal(int ordinal)
        {
            if (0 > ordinal || FieldCount <= ordinal)
            {
                throw ADP.IndexOutOfRange(ordinal);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/System.Data.IDataRecord.GetData/*' />
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        IDataReader System.Data.IDataRecord.GetData(int ordinal) => throw ADP.NotSupported();
    }
}
