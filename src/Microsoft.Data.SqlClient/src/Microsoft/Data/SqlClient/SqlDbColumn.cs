// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Globalization;

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
            BaseColumnName = _metadata.baseColumn;
            BaseSchemaName = _metadata.schemaName;
            BaseServerName = _metadata.serverName;
            BaseTableName = _metadata.tableName;
            ColumnName = _metadata.column;
            ColumnOrdinal = _metadata.ordinal;
            ColumnSize = (_metadata.metaType.IsSizeInCharacters && (_metadata.length != 0x7fffffff)) ? (_metadata.length / 2) : _metadata.length;
            IsAutoIncrement = _metadata.IsIdentity;
            IsIdentity = _metadata.IsIdentity;
            IsLong = _metadata.metaType.IsLong;

            if (SqlDbType.Timestamp == _metadata.type)
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
                NumericPrecision = _metadata.metaType.Precision;
            }

            IsReadOnly = _metadata.IsReadOnly;

            UdtAssemblyQualifiedName = _metadata.udt?.AssemblyQualifiedName;

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

        /// <summary>
        /// The name of the property exposing a vector column's base type.
        /// </summary>
        internal const string VectorBaseTypePropertyName = "VectorBaseType";

        /// <summary>
        /// The name of the property exposing a vector column's number of dimensions.
        /// </summary>
        internal const string VectorDimensionsPropertyName = "VectorDimensions";

        /// <summary>
        /// Exposes the properties of a vector column which have no corresponding
        /// <see cref="DbColumn"/> property, in addition to the standard properties.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A vector column's base type and number of dimensions are both carried by
        /// <see cref="DbColumn"/> properties whose meaning for vectors is not
        /// self-evident: the base type is reported as the numeric scale, and the number
        /// of dimensions has to be derived from the column size. They are surfaced here
        /// under their own names so that callers do not have to know that encoding.
        /// </para>
        /// <para>
        /// Both properties are <see langword="null"/> for columns which are not vectors,
        /// including a vector column which the server returned as <c>varchar</c> because
        /// the connection did not negotiate support for its base type.
        /// </para>
        /// </remarks>
        public override object this[string property] =>
            property switch
            {
                VectorBaseTypePropertyName => VectorBaseType,
                VectorDimensionsPropertyName => VectorDimensions,
                _ => base[property],
            };

        /// <summary>
        /// The name of a vector column's base type, such as <c>float32</c> or
        /// <c>float16</c>, or <see langword="null"/> if the column is not a vector.
        /// </summary>
        private string VectorBaseType =>
            _metadata.type != SqlDbTypeExtensions.Vector
                ? null
                : (MetaType.SqlVectorElementType)_metadata.scale switch
                {
                    MetaType.SqlVectorElementType.Float32 => "float32",
                    // An unrecognised base type is reported rather than throwing, because
                    // reading metadata should not fail on a column the caller may ignore.
                    _ => _metadata.scale.ToString(CultureInfo.InvariantCulture),
                };

        /// <summary>
        /// The number of dimensions in a vector column, or <see langword="null"/> if the
        /// column is not a vector or has an unrecognised base type.
        /// </summary>
        private int? VectorDimensions
        {
            get
            {
                if (_metadata.type != SqlDbTypeExtensions.Vector)
                {
                    return null;
                }

                try
                {
                    return MetaType.GetVectorElementCount(_metadata.length, _metadata.scale);
                }
                catch (NotSupportedException)
                {
                    // The base type is not one this version of the driver knows the
                    // element size of, so the dimension count cannot be derived.
                    return null;
                }
            }
        }
    }
}
