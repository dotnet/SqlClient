// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SqlDataRecord/*'/>
public class SqlDataRecord : System.Data.IDataRecord
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ctor/*'/>
    public SqlDataRecord(params Microsoft.Data.SqlClient.Server.SqlMetaData[] metaData) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/FieldCount/*'/>
    public virtual int FieldCount { get { throw null; } }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ItemOrdinal/*'/>
    public virtual object this[int ordinal] { get { throw null; } }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ItemName/*'/>
    public virtual object this[string name] { get { throw null; } }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetBoolean/*'/>
    public virtual bool GetBoolean(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetByte/*'/>
    public virtual byte GetByte(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetBytes/*'/>
    public virtual long GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetChar/*'/>
    public virtual char GetChar(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetChars/*'/>
    public virtual long GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetData/*'/>
    System.Data.IDataReader System.Data.IDataRecord.GetData(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDataTypeName/*'/>
    public virtual string GetDataTypeName(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDateTime/*'/>
    public virtual System.DateTime GetDateTime(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDateTimeOffset/*'/>
    public virtual System.DateTimeOffset GetDateTimeOffset(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDecimal/*'/>
    public virtual decimal GetDecimal(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDouble/*'/>
    public virtual double GetDouble(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetFieldType/*'/>
    #if NET
    [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
    #endif
    public virtual System.Type GetFieldType(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetFloat/*'/>
    public virtual float GetFloat(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetGuid/*'/>
    public virtual System.Guid GetGuid(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt16/*'/>
    public virtual short GetInt16(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt32/*'/>
    public virtual int GetInt32(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt64/*'/>
    public virtual long GetInt64(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetName/*'/>
    public virtual string GetName(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetOrdinal/*'/>
    public virtual int GetOrdinal(string name) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBinary/*'/>
    public virtual System.Data.SqlTypes.SqlBinary GetSqlBinary(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBoolean/*'/>
    public virtual System.Data.SqlTypes.SqlBoolean GetSqlBoolean(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlByte/*'/>
    public virtual System.Data.SqlTypes.SqlByte GetSqlByte(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBytes/*'/>
    public virtual System.Data.SqlTypes.SqlBytes GetSqlBytes(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlChars/*'/>
    public virtual System.Data.SqlTypes.SqlChars GetSqlChars(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDateTime/*'/>
    public virtual System.Data.SqlTypes.SqlDateTime GetSqlDateTime(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDecimal/*'/>
    public virtual System.Data.SqlTypes.SqlDecimal GetSqlDecimal(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDouble/*'/>
    public virtual System.Data.SqlTypes.SqlDouble GetSqlDouble(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlFieldType/*'/>
    public virtual System.Type GetSqlFieldType(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlGuid/*'/>
    public virtual System.Data.SqlTypes.SqlGuid GetSqlGuid(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt16/*'/>
    public virtual System.Data.SqlTypes.SqlInt16 GetSqlInt16(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt32/*'/>
    public virtual System.Data.SqlTypes.SqlInt32 GetSqlInt32(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt64/*'/>
    public virtual System.Data.SqlTypes.SqlInt64 GetSqlInt64(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlMetaData/*'/>
    public virtual Microsoft.Data.SqlClient.Server.SqlMetaData GetSqlMetaData(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlMoney/*'/>
    public virtual System.Data.SqlTypes.SqlMoney GetSqlMoney(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlSingle/*'/>
    public virtual System.Data.SqlTypes.SqlSingle GetSqlSingle(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlString/*'/>
    public virtual System.Data.SqlTypes.SqlString GetSqlString(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlValue/*'/>
    public virtual object GetSqlValue(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlValues/*'/>
    public virtual int GetSqlValues(object[] values) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlXml/*'/>
    public virtual System.Data.SqlTypes.SqlXml GetSqlXml(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetString/*'/>
    public virtual string GetString(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetTimeSpan/*'/>
    public virtual System.TimeSpan GetTimeSpan(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetValue/*'/>
    public virtual object GetValue(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetValues/*'/>
    public virtual int GetValues(object[] values) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/IsDBNull/*'/>
    public virtual bool IsDBNull(int ordinal) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetBoolean/*'/>
    public virtual void SetBoolean(int ordinal, bool value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetByte/*'/>
    public virtual void SetByte(int ordinal, byte value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetBytes/*'/>
    public virtual void SetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetChar/*'/>
    public virtual void SetChar(int ordinal, char value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetChars/*'/>
    public virtual void SetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDateTime/*'/>
    public virtual void SetDateTime(int ordinal, System.DateTime value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDateTimeOffset/*'/>
    public virtual void SetDateTimeOffset(int ordinal, System.DateTimeOffset value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDBNull/*'/>
    public virtual void SetDBNull(int ordinal) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDecimal/*'/>
    public virtual void SetDecimal(int ordinal, decimal value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDouble/*'/>
    public virtual void SetDouble(int ordinal, double value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetFloat/*'/>
    public virtual void SetFloat(int ordinal, float value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetGuid/*'/>
    public virtual void SetGuid(int ordinal, System.Guid value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt16/*'/>
    public virtual void SetInt16(int ordinal, short value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt32/*'/>
    public virtual void SetInt32(int ordinal, int value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt64/*'/>
    public virtual void SetInt64(int ordinal, long value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBinary/*'/>
    public virtual void SetSqlBinary(int ordinal, System.Data.SqlTypes.SqlBinary value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBoolean/*'/>
    public virtual void SetSqlBoolean(int ordinal, System.Data.SqlTypes.SqlBoolean value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlByte/*'/>
    public virtual void SetSqlByte(int ordinal, System.Data.SqlTypes.SqlByte value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBytes/*'/>
    public virtual void SetSqlBytes(int ordinal, System.Data.SqlTypes.SqlBytes value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlChars/*'/>
    public virtual void SetSqlChars(int ordinal, System.Data.SqlTypes.SqlChars value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDateTime/*'/>
    public virtual void SetSqlDateTime(int ordinal, System.Data.SqlTypes.SqlDateTime value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDecimal/*'/>
    public virtual void SetSqlDecimal(int ordinal, System.Data.SqlTypes.SqlDecimal value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDouble/*'/>
    public virtual void SetSqlDouble(int ordinal, System.Data.SqlTypes.SqlDouble value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlGuid/*'/>
    public virtual void SetSqlGuid(int ordinal, System.Data.SqlTypes.SqlGuid value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt16/*'/>
    public virtual void SetSqlInt16(int ordinal, System.Data.SqlTypes.SqlInt16 value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt32/*'/>
    public virtual void SetSqlInt32(int ordinal, System.Data.SqlTypes.SqlInt32 value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt64/*'/>
    public virtual void SetSqlInt64(int ordinal, System.Data.SqlTypes.SqlInt64 value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlMoney/*'/>
    public virtual void SetSqlMoney(int ordinal, System.Data.SqlTypes.SqlMoney value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlSingle/*'/>
    public virtual void SetSqlSingle(int ordinal, System.Data.SqlTypes.SqlSingle value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlString/*'/>
    public virtual void SetSqlString(int ordinal, System.Data.SqlTypes.SqlString value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlXml/*'/>
    public virtual void SetSqlXml(int ordinal, System.Data.SqlTypes.SqlXml value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetString/*'/>
    public virtual void SetString(int ordinal, string value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetTimeSpan/*'/>
    public virtual void SetTimeSpan(int ordinal, System.TimeSpan value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetValue/*'/>
    public virtual void SetValue(int ordinal, object value) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetValues/*'/>
    public virtual int SetValues(params object[] values) { throw null; }
}

/// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SqlMetaData/*' />
public sealed class SqlMetaData
{
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbType/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypePrecisionScale/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, byte precision, byte scale) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypePrecisionScaleUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, byte precision, byte scale, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLength/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthPrecisionScaleLocaleCompareOptionsUserDefinedType/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, byte precision, byte scale, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions, System.Type userDefinedType) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthPrecisionScaleLocaleCompareOptionsUserDefinedTypeUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, byte precision, byte scale, long localeId, System.Data.SqlTypes.SqlCompareOptions compareOptions, System.Type userDefinedType, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthLocaleCompareOptions/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthLocaleCompareOptionsUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeDatabaseOwningSchemaObjectName/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, string database, string owningSchema, string objectName) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeDatabaseOwningSchemaObjectNameUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, string database, string owningSchema, string objectName, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedType/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedTypeServerTypeName/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType, string serverTypeName) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedTypeServerTypeNameUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
    public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType, string serverTypeName, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/CompareOptions/*' />
    public System.Data.SqlTypes.SqlCompareOptions CompareOptions { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/DbType/*' />
    public System.Data.DbType DbType { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/IsUniqueKey/*' />
    public bool IsUniqueKey { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/LocaleId/*' />
    public long LocaleId { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Max/*' />
    public static long Max { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/MaxLength/*' />
    public long MaxLength { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Name/*' />
    public string Name { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Precision/*' />
    public byte Precision { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Scale/*' />
    public byte Scale { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SortOrder/*' />
    public Microsoft.Data.SqlClient.SortOrder SortOrder { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SortOrdinal/*' />
    public int SortOrdinal { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SqlDbType/*' />
    public System.Data.SqlDbType SqlDbType { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Type/*' />
    public System.Type Type { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/TypeName/*' />
    public string TypeName { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/UseServerDefault/*' />
    public bool UseServerDefault { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionDatabase/*' />
    public string XmlSchemaCollectionDatabase { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionName/*' />
    public string XmlSchemaCollectionName { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionOwningSchema/*' />
    public string XmlSchemaCollectionOwningSchema { get { throw null; } }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue1/*' />
    public bool Adjust(bool value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue2/*' />
    public byte Adjust(byte value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue3/*' />
    public byte[] Adjust(byte[] value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue4/*' />
    public char Adjust(char value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue5/*' />
    public char[] Adjust(char[] value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue6/*' />
    public System.Data.SqlTypes.SqlBinary Adjust(System.Data.SqlTypes.SqlBinary value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue7/*' />
    public System.Data.SqlTypes.SqlBoolean Adjust(System.Data.SqlTypes.SqlBoolean value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue8/*' />
    public System.Data.SqlTypes.SqlByte Adjust(System.Data.SqlTypes.SqlByte value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue9/*' />
    public System.Data.SqlTypes.SqlBytes Adjust(System.Data.SqlTypes.SqlBytes value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue10/*' />
    public System.Data.SqlTypes.SqlChars Adjust(System.Data.SqlTypes.SqlChars value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue11/*' />
    public System.Data.SqlTypes.SqlDateTime Adjust(System.Data.SqlTypes.SqlDateTime value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue12/*' />
    public System.Data.SqlTypes.SqlDecimal Adjust(System.Data.SqlTypes.SqlDecimal value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue13/*' />
    public System.Data.SqlTypes.SqlDouble Adjust(System.Data.SqlTypes.SqlDouble value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue14/*' />
    public System.Data.SqlTypes.SqlGuid Adjust(System.Data.SqlTypes.SqlGuid value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue15/*' />
    public System.Data.SqlTypes.SqlInt16 Adjust(System.Data.SqlTypes.SqlInt16 value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue16/*' />
    public System.Data.SqlTypes.SqlInt32 Adjust(System.Data.SqlTypes.SqlInt32 value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue17/*' />
    public System.Data.SqlTypes.SqlInt64 Adjust(System.Data.SqlTypes.SqlInt64 value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue18/*' />
    public System.Data.SqlTypes.SqlMoney Adjust(System.Data.SqlTypes.SqlMoney value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue19/*' />
    public System.Data.SqlTypes.SqlSingle Adjust(System.Data.SqlTypes.SqlSingle value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue20/*' />
    public System.Data.SqlTypes.SqlString Adjust(System.Data.SqlTypes.SqlString value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue21/*' />
    public System.Data.SqlTypes.SqlXml Adjust(System.Data.SqlTypes.SqlXml value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue22/*' />
    public System.DateTime Adjust(System.DateTime value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue23/*' />
    public System.DateTimeOffset Adjust(System.DateTimeOffset value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue24/*' />
    public decimal Adjust(decimal value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue25/*' />
    public double Adjust(double value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue26/*' />
    public System.Guid Adjust(System.Guid value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue27/*' />
    public short Adjust(short value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue28/*' />
    public int Adjust(int value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue29/*' />
    public long Adjust(long value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue30/*' />
    public object Adjust(object value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue31/*' />
    public float Adjust(float value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue32/*' />
    public string Adjust(string value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue33/*' />
    public System.TimeSpan Adjust(System.TimeSpan value) { throw null; }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/InferFromValue/*' />
    public static Microsoft.Data.SqlClient.Server.SqlMetaData InferFromValue(object value, string name) { throw null; }
}
