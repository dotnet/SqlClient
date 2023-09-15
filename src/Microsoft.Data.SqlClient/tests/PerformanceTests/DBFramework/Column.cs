// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Text;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class Column
    {
        public string Name;
        public DataType Type;
        public object Value;

        public Column(DataType type, string prefix = null, object value = null)
        {
            Type = type;
            Name = (prefix ?? "c_") + type.Name;
            Value = value ?? Type.DefaultValue;
        }

        public string QueryString
        {
            get => new StringBuilder(Name).Append(' ').Append(Type.ToString()).ToString();
        }

        public DataColumn AsDataColumn() => new(Name);
    }
}
