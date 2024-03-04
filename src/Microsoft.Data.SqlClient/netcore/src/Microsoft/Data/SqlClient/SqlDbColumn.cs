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
            BaseCatalogName = _metadata.catalogName;
            BaseColumnName = _metadata._baseColumn;
            BaseSchemaName = _metadata.schemaName;
            BaseServerName = _metadata.serverName;
            BaseTableName = _metadata.tableName;
            ColumnName = _metadata._column;
            ColumnOrdinal = _metadata._ordinal;
            ColumnSize = (_metadata._metaType.IsSizeInCharacters && (_metadata._length != 0x7fffffff)) ? (_metadata._length / 2) : _metadata._length;
            IsAutoIncrement = _metadata.IsIdentity;
            IsIdentity = _metadata.IsIdentity;
            IsLong = _metadata._metaType.IsLong;

            if (SqlDbType.Timestamp == _metadata._type)
            {
                IsUnique = true;
            }
            else
            {
                IsUnique = false;
            }

            if (TdsEnums.UNKNOWN_PRECISION_SCALE != _metadata._precision)
            {
                NumericPrecision = _metadata._precision;
            }
            else
            {
                NumericPrecision = _metadata._metaType.Precision;
            }

            IsReadOnly = _metadata.IsReadOnly;

            UdtAssemblyQualifiedName = _metadata.udt?._assemblyQualifiedName;

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
