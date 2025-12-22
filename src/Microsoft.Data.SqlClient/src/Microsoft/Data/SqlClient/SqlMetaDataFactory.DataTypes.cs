// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient;

internal sealed partial class SqlMetaDataFactory
{
    private static void LoadDataTypesDataTables(DataSet metaDataCollectionsDataSet)
    {
        DataTable dataTypesDataTable = CreateDataTypesDataTable();

        dataTypesDataTable.BeginLoadData();

        // SQL Server data types are grouped into the rough categories below:
        // 1. Fixed-scale and fixed-precision numeric types: tinyint, smallint, int, bigint, real, money, smallmoney, bit.
        // 2. Variable-precision numeric types: float and decimal / numeric.
        // 3. Fixed-precision date/time types: datetime, smalldatetime, date.
        // 4. Variable-precision date/time types: time, datetime2, datetimeoffset.
        // 5. Long types: xml, text, ntext, image.
        // 6. Fixed-length string and binary types: char, nchar, binary.
        // 7. Variable-length string and binary types: varchar, nvarchar, varbinary.
        // 8. Miscellaneous fixed-length types: uniqueidentifier, sql_variant, timestamp

        AddFixedNumericType(SqlDbType.TinyInt, isBestMatch: true);
        AddFixedNumericType(SqlDbType.SmallInt, isBestMatch: true);
        AddFixedNumericType(SqlDbType.Int, isBestMatch: true);
        AddFixedNumericType(SqlDbType.BigInt, isBestMatch: true);
        AddFixedNumericType(SqlDbType.Real, isBestMatch: true);
        // Money and smallmoney are not the best ways to represent System.Decimal values. SQL Server
        // provides a variable-precision "numeric" type for that purpose.
        AddFixedNumericType(SqlDbType.Money, isBestMatch: false);
        AddFixedNumericType(SqlDbType.SmallMoney, isBestMatch: false);
        AddFixedNumericType(SqlDbType.Bit, isBestMatch: false);

        AddVariablePrecisionNumericType(SqlDbType.Float, columnSize: 53);
        AddVariablePrecisionNumericType(SqlDbType.Decimal, columnSize: 38);
        AddVariablePrecisionNumericType(SqlDbType.Decimal, columnSize: 38,
            aliasType: "numeric");

        AddFixedPrecisionDateTimeType(SqlDbType.DateTime, isBestMatch: true);
        AddFixedPrecisionDateTimeType(SqlDbType.SmallDateTime, isBestMatch: true);
        AddFixedPrecisionDateTimeType(SqlDbType.Date, isBestMatch: false,
            minimumVersion: "10.00.000.0");

        AddVariablePrecisionDateTimeType(SqlDbType.Time, columnSize: 5, isBestMatch: false,
            minimumVersion: "10.00.000.0");
        AddVariablePrecisionDateTimeType(SqlDbType.DateTime2, columnSize: 8,
            minimumVersion: "10.00.000.0");
        AddVariablePrecisionDateTimeType(SqlDbType.DateTimeOffset, columnSize: 10,
            minimumVersion: "10.00.000.0");

        AddLongStringOrBinaryType(SqlDbType.Xml);
        AddLongStringOrBinaryType(SqlDbTypeExtensions.Json, literalPrefix: "'", literalSuffix: "'",
            minimumVersion: "17.00.000.0");
        AddLongStringOrBinaryType(SqlDbType.Text, literalPrefix: "'", literalSuffix: "'");
        AddLongStringOrBinaryType(SqlDbType.NText, literalPrefix: "N'", literalSuffix: "'");
        AddLongStringOrBinaryType(SqlDbType.Image, literalPrefix: "0x");

        AddFixedLengthStringOrBinaryType(SqlDbType.Char, literalPrefix: "'", literalSuffix: "'");
        AddFixedLengthStringOrBinaryType(SqlDbType.NChar, literalPrefix: "N'", literalSuffix: "'");
        AddFixedLengthStringOrBinaryType(SqlDbType.Binary, literalPrefix: "0x");

        AddVariableLengthStringOrBinaryType(SqlDbType.VarChar, literalPrefix: "'", literalSuffix: "'");
        AddVariableLengthStringOrBinaryType(SqlDbType.NVarChar, literalPrefix: "N'", literalSuffix: "'");
        AddVariableLengthStringOrBinaryType(SqlDbType.VarBinary, literalPrefix: "0x");

        AddUniqueIdentifierType();
        AddSqlVariantType();
        AddRowVersionType();

        dataTypesDataTable.EndLoadData();
        dataTypesDataTable.AcceptChanges();

        metaDataCollectionsDataSet.Tables.Add(dataTypesDataTable);

        void AddFixedNumericType(SqlDbType integerDbType, bool isBestMatch)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(integerDbType, isMultiValued: false);
            DataRow typeRow = dataTypesDataTable.NewRow();

            typeRow[DbMetaDataColumnNames.TypeName] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.ProviderDbType] = (int)metaType.SqlDbType;
            typeRow[DbMetaDataColumnNames.CreateFormat] = metaType.TypeName;

            // A fixed-scale integer always has precision equal to the column size and a scale of 0xFF.
            // If the precision is marked as unknown, then report the column size directly.
            Debug.Assert(metaType.Scale == TdsEnums.UNKNOWN_PRECISION_SCALE);
            typeRow[DbMetaDataColumnNames.ColumnSize] =
                metaType.Precision == TdsEnums.UNKNOWN_PRECISION_SCALE
                    ? metaType.FixedLength
                    : metaType.Precision;

            typeRow[DbMetaDataColumnNames.DataType] = metaType.ClassType.FullName;
            // Of all fixed-scale integer types, only "tinyint", "smallint", "int" and "bigint" are auto-incrementable.
            typeRow[DbMetaDataColumnNames.IsAutoIncrementable] =
                integerDbType is SqlDbType.TinyInt or SqlDbType.SmallInt or SqlDbType.Int or SqlDbType.BigInt;
            typeRow[DbMetaDataColumnNames.IsBestMatch] = isBestMatch;
            typeRow[DbMetaDataColumnNames.IsCaseSensitive] = false;
            typeRow[DbMetaDataColumnNames.IsConcurrencyType] = false;
            typeRow[DbMetaDataColumnNames.IsFixedLength] = true;
            // "real" is an ISO synonym of "float(24)". This means that it's a fixed-scale alias of a dynamic-scale type.
            // "bit" is also not considered fixed precision/scale, since SQL Server packs multiple bit columns into the same byte.
            typeRow[DbMetaDataColumnNames.IsFixedPrecisionScale] = integerDbType is not SqlDbType.Real and not SqlDbType.Bit;
            typeRow[DbMetaDataColumnNames.IsLong] = false;
            typeRow[DbMetaDataColumnNames.IsNullable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchableWithLike] = false;
            // Only "tinyint" is unsigned. "bit" does not have the concept of signed/unsigned.
            if (integerDbType is not SqlDbType.Bit)
            {
                typeRow[DbMetaDataColumnNames.IsUnsigned] = integerDbType is SqlDbType.TinyInt;
            }

            dataTypesDataTable.Rows.Add(typeRow);
        }

        void AddVariablePrecisionNumericType(SqlDbType numericDbType, int columnSize,
            string? aliasType = null)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(numericDbType, isMultiValued: false);
            string typeName = aliasType ?? metaType.TypeName;
            bool variableScale = numericDbType is SqlDbType.Decimal;
            DataRow typeRow = dataTypesDataTable.NewRow();

            typeRow[DbMetaDataColumnNames.TypeName] = typeName;
            typeRow[DbMetaDataColumnNames.ProviderDbType] = (int)metaType.SqlDbType;
            typeRow[DbMetaDataColumnNames.ColumnSize] = columnSize;

            // Both "float" and "decimal" have variable precision, but "decimal" also has variable scale.
            if (variableScale)
            {
                typeRow[DbMetaDataColumnNames.CreateFormat] = $"{typeName}({{0}}, {{1}})";
                typeRow[DbMetaDataColumnNames.CreateParameters] = "precision,scale";
                typeRow[DbMetaDataColumnNames.MinimumScale] = 0;
                // The data type is a fixed number of bytes, which can be distributed between the precision
                // and the scale. Therefore, the maximum scale is equal to the column size.
                typeRow[DbMetaDataColumnNames.MaximumScale] = columnSize;
            }
            else
            {
                typeRow[DbMetaDataColumnNames.CreateFormat] = $"{typeName}({{0}})";
                typeRow[DbMetaDataColumnNames.CreateParameters] = "number of bits used to store the mantissa";
            }

            typeRow[DbMetaDataColumnNames.DataType] = metaType.ClassType.FullName;
            // Only the "decimal" type is auto-incrementable.
            typeRow[DbMetaDataColumnNames.IsAutoIncrementable] = numericDbType is SqlDbType.Decimal;
            typeRow[DbMetaDataColumnNames.IsBestMatch] = true;
            typeRow[DbMetaDataColumnNames.IsCaseSensitive] = false;
            typeRow[DbMetaDataColumnNames.IsConcurrencyType] = false;
            typeRow[DbMetaDataColumnNames.IsFixedLength] = true;
            typeRow[DbMetaDataColumnNames.IsFixedPrecisionScale] = false;
            typeRow[DbMetaDataColumnNames.IsLong] = false;
            typeRow[DbMetaDataColumnNames.IsNullable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchableWithLike] = false;
            typeRow[DbMetaDataColumnNames.IsUnsigned] = false;

            dataTypesDataTable.Rows.Add(typeRow);
        }

        void AddFixedPrecisionDateTimeType(SqlDbType dateTimeDbType, bool isBestMatch,
            string? minimumVersion = null)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(dateTimeDbType, isMultiValued: false);
            DataRow typeRow = dataTypesDataTable.NewRow();

            typeRow[DbMetaDataColumnNames.TypeName] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.ProviderDbType] = (int)metaType.SqlDbType;
            // "date" reports an unknown precision and an unknown scale, even though its precision and scale are fixed.
            // To that end, we report its column length directly.
            typeRow[DbMetaDataColumnNames.ColumnSize] =
                metaType.Precision == TdsEnums.UNKNOWN_PRECISION_SCALE
                    ? metaType.FixedLength
                    : metaType.Precision;
            typeRow[DbMetaDataColumnNames.CreateFormat] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.DataType] = metaType.ClassType.FullName;
            typeRow[DbMetaDataColumnNames.IsAutoIncrementable] = false;
            typeRow[DbMetaDataColumnNames.IsBestMatch] = isBestMatch;
            typeRow[DbMetaDataColumnNames.IsCaseSensitive] = false;
            typeRow[DbMetaDataColumnNames.IsConcurrencyType] = false;
            typeRow[DbMetaDataColumnNames.IsFixedLength] = true;
            typeRow[DbMetaDataColumnNames.IsFixedPrecisionScale] = dateTimeDbType is SqlDbType.Date;
            typeRow[DbMetaDataColumnNames.IsLong] = false;
            typeRow[DbMetaDataColumnNames.IsNullable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchableWithLike] = true;
            typeRow[DbMetaDataColumnNames.LiteralPrefix] = @"{ts '";
            typeRow[DbMetaDataColumnNames.LiteralSuffix] = @"'}";

            if (minimumVersion is not null)
            {
                typeRow[MinimumVersionKey] = minimumVersion;
            }

            dataTypesDataTable.Rows.Add(typeRow);
        }

        void AddVariablePrecisionDateTimeType(SqlDbType dateTimeDbType, int columnSize,
            bool isBestMatch = true,
            string? minimumVersion = null)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(dateTimeDbType, isMultiValued: false);
            DataRow typeRow = dataTypesDataTable.NewRow();

            typeRow[DbMetaDataColumnNames.TypeName] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.ProviderDbType] = (int)metaType.SqlDbType;
            typeRow[DbMetaDataColumnNames.ColumnSize] = columnSize;
            typeRow[DbMetaDataColumnNames.CreateFormat] = $"{metaType.TypeName}({{0}})";
            // The documentation describes these data types as having variable precision, but GetSchema reports that they
            // have variable scale.
            typeRow[DbMetaDataColumnNames.CreateParameters] = "scale";
            typeRow[DbMetaDataColumnNames.DataType] = metaType.ClassType.FullName;
            typeRow[DbMetaDataColumnNames.IsAutoIncrementable] = false;
            typeRow[DbMetaDataColumnNames.IsBestMatch] = isBestMatch;
            typeRow[DbMetaDataColumnNames.IsCaseSensitive] = false;
            typeRow[DbMetaDataColumnNames.IsConcurrencyType] = false;
            typeRow[DbMetaDataColumnNames.IsFixedLength] = false;
            typeRow[DbMetaDataColumnNames.IsFixedPrecisionScale] = false;
            typeRow[DbMetaDataColumnNames.IsLong] = false;
            typeRow[DbMetaDataColumnNames.IsNullable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchableWithLike] = true;
            typeRow[DbMetaDataColumnNames.MinimumScale] = 0;
            typeRow[DbMetaDataColumnNames.MaximumScale] = metaType.Scale;
            typeRow[DbMetaDataColumnNames.LiteralPrefix] = @"{ts '";
            typeRow[DbMetaDataColumnNames.LiteralSuffix] = @"'}";

            if (minimumVersion is not null)
            {
                typeRow[MinimumVersionKey] = minimumVersion;
            }

            dataTypesDataTable.Rows.Add(typeRow);
        }

        void AddLongStringOrBinaryType(SqlDbType longDbType,
            string? literalPrefix = null, string? literalSuffix = null,
            string? minimumVersion = null) =>
            AddStringOrBinaryType(longDbType,
                // The column size is measured in elements, not bytes. For ntext, each element is a 2 byte Unicode character.
                columnSize: longDbType is SqlDbType.NText ? int.MaxValue / ADP.CharSize : int.MaxValue,
                isLong: true, isFixedLength: false,
                isSearchable: false,
                literalPrefix: literalPrefix, literalSuffix: literalSuffix);

        void AddFixedLengthStringOrBinaryType(SqlDbType fixedLengthDbType,
            string? literalPrefix = null, string? literalSuffix = null) =>
            AddStringOrBinaryType(fixedLengthDbType,
                // See the comment on AddLongStringOrBinary regarding column sizes. To add: the "binary" type can be up to 8000 bytes.
                columnSize: fixedLengthDbType switch
                {
                    SqlDbType.Binary => 8000,
                    SqlDbType.NChar => int.MaxValue / ADP.CharSize,
                    _ => int.MaxValue
                },
                isLong: false, isFixedLength: true,
                isSearchable: true,
                literalPrefix: literalPrefix, literalSuffix: literalSuffix);

        void AddVariableLengthStringOrBinaryType(SqlDbType variableLengthDbType,
            string? literalPrefix = null, string? literalSuffix = null) =>
            AddStringOrBinaryType(variableLengthDbType,
                // See the comment on AddLongStringOrBinary regarding column sizes. Unlike the "binary" type, varbinary is reported to
                // have a maximum column size of (2^32-1) / 2 elements, just as nvarchar does.
                columnSize: variableLengthDbType is SqlDbType.NVarChar or SqlDbType.VarBinary
                    ? int.MaxValue / ADP.CharSize
                    : int.MaxValue,
                isLong: false, isFixedLength: false,
                isSearchable: true,
                literalPrefix: literalPrefix, literalSuffix: literalSuffix);

        void AddRowVersionType() =>
            AddStringOrBinaryType(SqlDbType.Timestamp,
            columnSize: TdsEnums.TEXT_TIME_STAMP_LEN, isLong: false, isFixedLength: true,
            isSearchable: true,
            literalPrefix: "0x");

        void AddStringOrBinaryType(SqlDbType sqlDbType, int columnSize, bool isLong,
            bool isFixedLength, bool isSearchable,
            string? literalPrefix = null, string? literalSuffix = null)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(sqlDbType, isMultiValued: false);
            DataRow typeRow = dataTypesDataTable.NewRow();
            bool hasLengthSpecifier = sqlDbType is SqlDbType.Char or SqlDbType.NChar or SqlDbType.Binary
                or SqlDbType.VarChar or SqlDbType.NVarChar or SqlDbType.VarBinary;

            typeRow[DbMetaDataColumnNames.TypeName] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.ProviderDbType] = (int)metaType.SqlDbType;
            typeRow[DbMetaDataColumnNames.ColumnSize] = columnSize;
            // Several string or binary data types do not include the length in their type declaration.
            // Of the data types which do, fixed-length types use the length parameter to decide the full length,
            // while the rest use it to decide the maximum length.
            if (hasLengthSpecifier)
            {
                typeRow[DbMetaDataColumnNames.CreateFormat] = $"{metaType.TypeName}({{0}})";
                typeRow[DbMetaDataColumnNames.CreateParameters] =
                    isFixedLength ? "length" : "max length";
            }
            else
            {
                typeRow[DbMetaDataColumnNames.CreateFormat] = metaType.TypeName;
            }
            typeRow[DbMetaDataColumnNames.DataType] = metaType.ClassType.FullName;
            typeRow[DbMetaDataColumnNames.IsAutoIncrementable] = false;
            // The DataType for XML is string, which is not the best match for an XML type.
            // Similarly, although timestamp/rowversion is represented as a byte array, it's not best
            // represented as such.
            typeRow[DbMetaDataColumnNames.IsBestMatch] = sqlDbType is not SqlDbType.Xml and not SqlDbType.Timestamp and not SqlDbTypeExtensions.Json;
            typeRow[DbMetaDataColumnNames.IsCaseSensitive] = false;
            typeRow[DbMetaDataColumnNames.IsConcurrencyType] = sqlDbType is SqlDbType.Timestamp;
            typeRow[DbMetaDataColumnNames.IsFixedLength] = isFixedLength;
            typeRow[DbMetaDataColumnNames.IsFixedPrecisionScale] = false;
            typeRow[DbMetaDataColumnNames.IsLong] = isLong;
            typeRow[DbMetaDataColumnNames.IsNullable] = sqlDbType is not SqlDbType.Timestamp;
            typeRow[DbMetaDataColumnNames.IsSearchable] = isSearchable;
            // String types are searchable with LIKE; binary types are not. SQL Server considers XML and JSON a binary type for this purpose.
            typeRow[DbMetaDataColumnNames.IsSearchableWithLike] =
                metaType.IsCharType && sqlDbType is not SqlDbType.Xml and not SqlDbTypeExtensions.Json;

            if (literalPrefix is null && literalSuffix is null)
            {
                // If no literal prefix or suffix is specified, then literal support is not available.
                typeRow[DbMetaDataColumnNames.IsLiteralSupported] = false;
            }
            else
            {
                if (literalPrefix is not null)
                {
                    typeRow[DbMetaDataColumnNames.LiteralPrefix] = literalPrefix;
                }
                if (literalSuffix is not null)
                {
                    typeRow[DbMetaDataColumnNames.LiteralSuffix] = literalSuffix;
                }
            }

            dataTypesDataTable.Rows.Add(typeRow);
        }

        void AddSqlVariantType()
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(SqlDbType.Variant, isMultiValued: false);
            DataRow typeRow = dataTypesDataTable.NewRow();

            typeRow[DbMetaDataColumnNames.TypeName] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.ProviderDbType] = (int)metaType.SqlDbType;
            typeRow[DbMetaDataColumnNames.CreateFormat] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.DataType] = metaType.ClassType.FullName;
            typeRow[DbMetaDataColumnNames.IsAutoIncrementable] = false;
            typeRow[DbMetaDataColumnNames.IsBestMatch] = true;
            typeRow[DbMetaDataColumnNames.IsCaseSensitive] = false;
            typeRow[DbMetaDataColumnNames.IsConcurrencyType] = false;
            typeRow[DbMetaDataColumnNames.IsFixedLength] = false;
            typeRow[DbMetaDataColumnNames.IsFixedPrecisionScale] = false;
            typeRow[DbMetaDataColumnNames.IsLong] = false;
            typeRow[DbMetaDataColumnNames.IsNullable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchableWithLike] = false;
            typeRow[DbMetaDataColumnNames.IsLiteralSupported] = false;

            dataTypesDataTable.Rows.Add(typeRow);
        }

        void AddUniqueIdentifierType()
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(SqlDbType.UniqueIdentifier, isMultiValued: false);
            DataRow typeRow = dataTypesDataTable.NewRow();

            typeRow[DbMetaDataColumnNames.TypeName] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.ProviderDbType] = (int)metaType.SqlDbType;
            typeRow[DbMetaDataColumnNames.ColumnSize] = metaType.FixedLength;
            typeRow[DbMetaDataColumnNames.CreateFormat] = metaType.TypeName;
            typeRow[DbMetaDataColumnNames.DataType] = metaType.ClassType.FullName;
            typeRow[DbMetaDataColumnNames.IsAutoIncrementable] = false;
            typeRow[DbMetaDataColumnNames.IsBestMatch] = true;
            typeRow[DbMetaDataColumnNames.IsCaseSensitive] = false;
            typeRow[DbMetaDataColumnNames.IsConcurrencyType] = false;
            typeRow[DbMetaDataColumnNames.IsFixedLength] = true;
            typeRow[DbMetaDataColumnNames.IsFixedPrecisionScale] = false;
            typeRow[DbMetaDataColumnNames.IsLong] = false;
            typeRow[DbMetaDataColumnNames.IsNullable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchable] = true;
            typeRow[DbMetaDataColumnNames.IsSearchableWithLike] = false;
            typeRow[DbMetaDataColumnNames.LiteralPrefix] = "'";
            typeRow[DbMetaDataColumnNames.LiteralSuffix] = "'";

            dataTypesDataTable.Rows.Add(typeRow);
        }
    }

    private static DataTable CreateDataTypesDataTable()
        => new(DbMetaDataCollectionNames.DataTypes)
        {
            Columns =
            {
                new DataColumn(DbMetaDataColumnNames.TypeName, typeof(string)),
                new DataColumn(DbMetaDataColumnNames.ProviderDbType, typeof(int)),
                new DataColumn(DbMetaDataColumnNames.ColumnSize, typeof(long)),
                new DataColumn(DbMetaDataColumnNames.CreateFormat, typeof(string)),
                new DataColumn(DbMetaDataColumnNames.CreateParameters, typeof(string)),
                new DataColumn(DbMetaDataColumnNames.DataType, typeof(string)),
                new DataColumn(DbMetaDataColumnNames.IsAutoIncrementable, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsBestMatch, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsCaseSensitive, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsFixedLength, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsFixedPrecisionScale, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsLong, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsNullable, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsSearchable, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsSearchableWithLike, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.IsUnsigned, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.MaximumScale, typeof(short)),
                new DataColumn(DbMetaDataColumnNames.MinimumScale, typeof(short)),
                new DataColumn(DbMetaDataColumnNames.IsConcurrencyType, typeof(bool)),
                new DataColumn(MaximumVersionKey, typeof(string)),
                new DataColumn(MinimumVersionKey, typeof(string)),
                new DataColumn(DbMetaDataColumnNames.IsLiteralSupported, typeof(bool)),
                new DataColumn(DbMetaDataColumnNames.LiteralPrefix, typeof(string)),
                new DataColumn(DbMetaDataColumnNames.LiteralSuffix, typeof(string))
            }
        };
}
