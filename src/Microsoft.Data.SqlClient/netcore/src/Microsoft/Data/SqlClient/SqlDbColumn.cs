// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal class SqlDbColumn : DbColumn
    {
        private readonly _SqlMetaData _metadata;

        internal SqlDbColumn(_SqlMetaData md)
        {
            _metadata = md;
            Populate();
        }

        private void Populate()
        {
            AllowDBNull = _metadata.IsNullable;
            BaseCatalogName = _metadata.CatalogName;
            BaseColumnName = _metadata.BaseColumn;
            BaseSchemaName = _metadata.SchemaName;
            BaseServerName = _metadata.ServerName;
            BaseTableName = _metadata.TableName;
            ColumnName = _metadata.Column;
            ColumnOrdinal = _metadata.Ordinal;
            ColumnSize = (_metadata.MetaType.IsSizeInCharacters && (_metadata.Length != 0x7fffffff)) ? (_metadata.Length / 2) : _metadata.Length;
            IsAutoIncrement = _metadata.IsIdentity;
            IsIdentity = _metadata.IsIdentity;
            IsLong = _metadata.MetaType.IsLong;

            if (SqlDbType.Timestamp == _metadata.Type)
            {
                IsUnique = true;
            }
            else
            {
                IsUnique = false;
            }

            if (TdsEnums.UNKNOWN_PRECISION_SCALE != _metadata.Precision)
            {
                NumericPrecision = _metadata.Precision;
            }
            else
            {
                NumericPrecision = _metadata.MetaType.Precision;
            }

            IsReadOnly = _metadata.IsReadOnly;

            UdtAssemblyQualifiedName = _metadata.Udt?.AssemblyQualifiedName;

        }

        internal bool? SqlIsAliased
        {
            set
            {
                IsAliased = value;
            }
        }

        internal bool? SqlIsKey
        {
            set
            {
                IsKey = value;
            }
        }

        internal bool? SqlIsHidden
        {
            set
            {
                IsHidden = value;
            }
        }

        internal bool? SqlIsExpression
        {
            set
            {
                IsExpression = value;
            }
        }

        internal Type SqlDataType
        {
            set
            {
                DataType = value;
            }
        }

        internal string SqlDataTypeName
        {
            set
            {
                DataTypeName = value;
            }
        }

        internal int? SqlNumericScale
        {
            set
            {
                NumericScale = value;
            }
        }

    }
}
