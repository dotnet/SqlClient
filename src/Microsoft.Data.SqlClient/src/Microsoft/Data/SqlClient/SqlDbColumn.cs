// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Parser.Tokens;

namespace Microsoft.Data.SqlClient
{
    internal class SqlDbColumn : DbColumn
    {
        private readonly TdsColumnMetadata _metadata;

        internal SqlDbColumn(TdsColumnMetadata md)
        {
            _metadata = md;
            Populate();
        }

        private void Populate()
        {
            AllowDBNull = _metadata.IsNullable;
            BaseCatalogName = _metadata.CatalogName;
            BaseColumnName = _metadata.baseColumn;
            BaseSchemaName = _metadata.SchemaName;
            BaseServerName = _metadata.ServerName;
            BaseTableName = _metadata.TableName;
            ColumnName = _metadata.column;
            ColumnOrdinal = _metadata.Ordinal;
            ColumnSize = (_metadata.MetaType.IsSizeInCharacters && (_metadata.length != 0x7fffffff)) ? (_metadata.length / 2) : _metadata.length;
            IsAutoIncrement = _metadata.IsIdentity;
            IsIdentity = _metadata.IsIdentity;
            IsLong = _metadata.MetaType.IsLong;

            if (SqlDbType.Timestamp == _metadata.DbType)
            {
                IsUnique = true;
            }
            else
            {
                IsUnique = false;
            }

            if (TdsEnums.UNKNOWN_PRECISION_SCALE != _metadata.precision)
            {
                NumericPrecision = _metadata.precision;
            }
            else
            {
                NumericPrecision = _metadata.MetaType.Precision;
            }

            IsReadOnly = _metadata.IsReadOnly;

            UdtAssemblyQualifiedName = _metadata.UdtTypeInfo?.AssemblyQualifiedName;

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
