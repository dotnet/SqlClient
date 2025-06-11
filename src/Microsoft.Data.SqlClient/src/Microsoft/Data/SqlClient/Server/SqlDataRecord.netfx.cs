// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SqlDataRecord/*' />
    public partial class SqlDataRecord : IDataRecord
    {
        // @TODO: This is always null but is passed around quite a bit.
        private readonly SmiContext _recordContext = null;
     
        private Type GetFieldTypeFrameworkSpecific(int ordinal)
        {
            SqlMetaData md = GetSqlMetaData(ordinal);
            if (md.SqlDbType == SqlDbType.Udt)
            {
                return md.Type;
            }
            else
            {
                return MetaType.GetMetaTypeFromSqlDbType(md.SqlDbType, false).ClassType;
            }
        }
   
        private object GetValueFrameworkSpecific(int ordinal)
        {
            SmiMetaData metaData = GetSmiMetaData(ordinal);
            return ValueUtilsSmi.GetValue200(_eventSink, _recordBuffer, ordinal, metaData, _recordContext);
        }
    
        private object GetSqlValueFrameworkSpecific(int ordinal)
        {
            SmiMetaData metaData = GetSmiMetaData(ordinal);
            return ValueUtilsSmi.GetSqlValue200(_eventSink, _recordBuffer, ordinal, metaData, _recordContext);
        }

        private SqlBytes GetSqlBytesFrameworkSpecific(int ordinal) => ValueUtilsSmi.GetSqlBytes(_eventSink, _recordBuffer, ordinal, GetSmiMetaData(ordinal), _recordContext);
 
        private SqlXml GetSqlXmlFrameworkSpecific(int ordinal) => ValueUtilsSmi.GetSqlXml(_eventSink, _recordBuffer, ordinal, GetSmiMetaData(ordinal), _recordContext);
        
        private SqlChars GetSqlCharsFrameworkSpecific(int ordinal) => ValueUtilsSmi.GetSqlChars(_eventSink, _recordBuffer, ordinal, GetSmiMetaData(ordinal), _recordContext);
 
        private int SetValuesFrameworkSpecific(params object[] values)
        {
            if (values == null)
            {
                throw ADP.ArgumentNull(nameof(values));
            }

            // Allow values array longer than FieldCount, just ignore the extra cells.
            int copyLength = (values.Length > FieldCount) ? FieldCount : values.Length;

            ExtendedClrTypeCode[] typeCodes = new ExtendedClrTypeCode[copyLength];

            // Verify all data values as acceptable before changing current state.
            for (int i = 0; i < copyLength; i++)
            {
                SqlMetaData metaData = GetSqlMetaData(i);
                typeCodes[i] = MetaDataUtilsSmi.DetermineExtendedTypeCodeForUseWithSqlDbType(
                    metaData.SqlDbType,
                    isMultiValued: false,
                    values[i],
                    metaData.Type);
                if (typeCodes[i] == ExtendedClrTypeCode.Invalid)
                {
                    throw ADP.InvalidCast();
                }
            }

            // Now move the data (it'll only throw if someone plays with the values array between
            //      the validation loop and here, or if an invalid UDT was sent).
            for (int i = 0; i < copyLength; i++)
            {
                ValueUtilsSmi.SetCompatibleValueV200(
                    _eventSink,
                    _recordBuffer,
                    ordinal: i,
                    GetSmiMetaData(i),
                    values[i],
                    typeCodes[i],
                    offset: 0,
                    peekAhead: null);
            }

            return copyLength;
        }
      
        private void SetValueFrameworkSpecific(int ordinal, object value)
        {
            SqlMetaData metaData = GetSqlMetaData(ordinal);
            ExtendedClrTypeCode typeCode = MetaDataUtilsSmi.DetermineExtendedTypeCodeForUseWithSqlDbType(
                metaData.SqlDbType,
                isMultiValued: false,
                value,
                metaData.Type);
            if (typeCode == ExtendedClrTypeCode.Invalid)
            {
                throw ADP.InvalidCast();
            }
            
            ValueUtilsSmi.SetCompatibleValueV200(
                _eventSink,
                _recordBuffer,
                ordinal,
                GetSmiMetaData(ordinal),
                value,
                typeCode,
                offset: 0,
                peekAhead: null);
        }
  
        private void SetTimeSpanFrameworkSpecific(int ordinal, TimeSpan value) => 
            ValueUtilsSmi.SetTimeSpan(
                _eventSink,
                _recordBuffer,
                ordinal,
                GetSmiMetaData(ordinal),
                value,
                settersSupport2008DateTime: true);
        
        private void SetDateTimeOffsetFrameworkSpecific(int ordinal, DateTimeOffset value) =>
            ValueUtilsSmi.SetDateTimeOffset(
                _eventSink,
                _recordBuffer,
                ordinal,
                GetSmiMetaData(ordinal),
                value,
                settersSupport2008DateTime: true);
    }
}
