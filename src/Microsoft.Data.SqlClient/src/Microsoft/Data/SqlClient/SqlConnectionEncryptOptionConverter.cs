// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal class SqlConnectionEncryptOptionConverter : TypeConverter
    {
        // Overrides the CanConvertFrom method of TypeConverter.
        // The ITypeDescriptorContext interface provides the context for the
        // conversion. Typically, this interface is used at design time to 
        // provide information about the design-time container.
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }

        // Overrides the CanConvertTo method of TypeConverter.
        public override bool CanConvertTo(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            return base.CanConvertTo(context, sourceType);
        }

        // Overrides the ConvertFrom method of TypeConverter.
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
            {
                return SqlConnectionEncryptOption.Parse(value.ToString());
            }
            throw ADP.ConvertFailed(value.GetType(), typeof(SqlConnectionEncryptOption), null);
        }

        // Overrides the ConvertTo method of TypeConverter.
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                return base.ConvertTo(context, culture, value, destinationType);
            }
            throw ADP.ConvertFailed(value.GetType(), typeof(SqlConnectionEncryptOption), null);
        }
    }
}
