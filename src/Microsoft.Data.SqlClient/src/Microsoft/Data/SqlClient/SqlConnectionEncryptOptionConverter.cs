// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.ComponentModel;
using System.Globalization;
using System.Drawing;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOptionConverter.xml' path='docs/members[@name="SqlConnectionEncryptOptionConverter"]/SqlConnectionEncryptOptionConverter/*'/>
    public class SqlConnectionEncryptOptionConverter : TypeConverter
    {
        // Overrides the CanConvertFrom method of TypeConverter.
        // The ITypeDescriptorContext interface provides the context for the
        // conversion. Typically, this interface is used at design time to 
        // provide information about the design-time container.
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOptionConverter.xml' path='docs/members[@name="SqlConnectionEncryptOptionConverter"]/SqlConnectionEncryptOptionConverter/CanConvertFrom/*'/>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }
        // Overrides the ConvertFrom method of TypeConverter.
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOptionConverter.xml' path='docs/members[@name="SqlConnectionEncryptOptionConverter"]/SqlConnectionEncryptOptionConverter/ConvertFrom/*'/>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
            {
                return SqlConnectionEncryptOption.Parse(value.ToString());
            }
            throw new Exception("Value to convert must be of string type!");
        }
        // Overrides the ConvertTo method of TypeConverter.
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOptionConverter.xml' path='docs/members[@name="SqlConnectionEncryptOptionConverter"]/SqlConnectionEncryptOptionConverter/ConvertTo/*'/>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
