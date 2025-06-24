// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SqlDataRecord/*' />
    public partial class SqlDataRecord : IDataRecord
    {
#if NET
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        private Type GetFieldTypeFrameworkSpecific(int ordinal) =>
            MetaType.GetMetaTypeFromSqlDbType(GetSqlMetaData(ordinal).SqlDbType, false).ClassType;

        private object GetValueFrameworkSpecific(int ordinal) =>
            ValueUtilsSmi.GetValue200(_recordBuffer, ordinal, GetSmiMetaData(ordinal));
        private object GetSqlValueFrameworkSpecific(int ordinal) =>
            ValueUtilsSmi.GetSqlValue200(_recordBuffer, ordinal, GetSmiMetaData(ordinal));
    
        private SqlBytes GetSqlBytesFrameworkSpecific(int ordinal) =>
            ValueUtilsSmi.GetSqlBytes(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        private SqlXml GetSqlXmlFrameworkSpecific(int ordinal) =>
            ValueUtilsSmi.GetSqlXml(_recordBuffer, ordinal, GetSmiMetaData(ordinal));

        private SqlChars GetSqlCharsFrameworkSpecific(int ordinal) =>
            ValueUtilsSmi.GetSqlChars(_recordBuffer, ordinal, GetSmiMetaData(ordinal));
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
                    metaData.Type
                );
                if (typeCodes[i] == ExtendedClrTypeCode.Invalid)
                {
                    throw ADP.InvalidCast();
                }
            }

            // Now move the data (it'll only throw if someone plays with the values array between
            //      the validation loop and here, or if an invalid UDT was sent).
            for (int i = 0; i < copyLength; i++)
            {
                ValueUtilsSmi.SetCompatibleValueV200(_recordBuffer, i, GetSmiMetaData(i), values[i], typeCodes[i], offset: 0, peekAhead: null);
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
                metaData.Type
            );
            if (typeCode == ExtendedClrTypeCode.Invalid)
            {
                throw ADP.InvalidCast();
            }

            ValueUtilsSmi.SetCompatibleValueV200(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value, typeCode, offset: 0, peekAhead: null);
        }

         private void SetTimeSpanFrameworkSpecific(int ordinal, TimeSpan value) 
            => ValueUtilsSmi.SetTimeSpan(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

         private void SetDateTimeOffsetFrameworkSpecific(int ordinal, DateTimeOffset value) 
            => ValueUtilsSmi.SetDateTimeOffset(_recordBuffer, ordinal, GetSmiMetaData(ordinal), value);

    }
}
