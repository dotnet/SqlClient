// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient;

internal sealed partial class SqlMetaDataFactory
{
    /// <summary>
    /// Returns <see href="https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/common-schema-collections#datatypes">DataTypes</see>
    /// schema collection.
    /// </summary>
    private sealed class DataTypesCollection : MetaDataCollectionBase
    {
        #pragma warning disable format

        private static readonly TypeMetaData[] s_flat_types = [             
            //                                                                                             IsBestMatch             isFixedPrecisionScale   IsSearchable           MaximumScale             IsLiteralSupported      IsSupported
            //                                                                                                     IsCaseSensitive         IsLong                  IsSearchableWithLike    MinimumScale            LiteralPrefix                                           CreateParameters
            //                              ColumnSize                     (CreateFormat)*         IsAutoIncrementable     IsFixedLength           IsNullable              isUnsigned              IsConcurrencyType       LiteralSuffix              
            new (SqlDbType.SmallInt        ,null                        /*,"smallint"           */,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,null),
            new (SqlDbType.Int             ,null                        /*,"int"                */,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,null),
            new (SqlDbType.Real            ,null                        /*,"real"               */,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,null),
            new (SqlDbType.Float           ,53                          /*,"float({0})"         */,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,["number of bits used to store the mantissa"]),
            new (SqlDbType.Money           ,null                        /*,"money"              */,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,null),
            new (SqlDbType.SmallMoney      ,null                        /*,"smallmoney"         */,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,null),
            new (SqlDbType.Bit             ,null                        /*,"bit"                */,false  ,false  ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,null),
            new (SqlDbType.TinyInt         ,null                        /*,"tinyint"            */,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,null),
            new (SqlDbType.BigInt          ,null                        /*,"bigint"             */,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null   ,null   ,null   ,null                                   ,null),
            new (SqlDbType.Timestamp       ,TdsEnums.TEXT_TIME_STAMP_LEN/*,"timestamp"          */,false  ,false  ,false  ,true   ,false  ,false  ,false  ,true   ,false  ,null   ,-1     ,-1     ,true   ,null   ,"0x"   ,null   ,null                                   ,null),
            new (SqlDbType.Binary          ,8000                        /*,"binary({0})"        */,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null   ,"0x"   ,null   ,null                                   ,["length"]),
            new (SqlDbType.Image           ,int.MaxValue                /*,"image"              */,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,null   ,"0x"   ,null   ,null                                   ,null),
            new (SqlDbType.Text            ,int.MaxValue                /*,"text"               */,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,null   ,-1     ,-1     ,false  ,null   ,"'"    ,"'"    ,null                                   ,null),
            new (SqlDbType.NText           ,int.MaxValue / ADP.CharSize /*,"ntext"              */,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,null   ,-1     ,-1     ,false  ,null   ,"N'"   ,"'"    ,null                                   ,null),
            new (SqlDbType.Decimal         ,38                          /*,"decimal({0}, {1})"  */,true   ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,38     ,0      ,false  ,null   ,null   ,null   ,null                                   ,["precision","scale"]),
            new (SqlDbType.Decimal         ,38                          /*,"numeric({0}, {1})"  */,true   ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,38     ,0      ,false  ,null   ,null   ,null   ,null                                   ,["precision","scale"],
                   alias: "numeric"),                                                                                                                                                                                                                                     
            new (SqlDbType.DateTime        ,null                        /*,"datetime"           */,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null   ,"{ts '","'}"   ,null                                   ,null),
            new (SqlDbType.SmallDateTime   ,null                        /*,"smalldatetime"      */,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null   ,"{ts '","'}"   ,null                                   ,null),
            new (SqlDbType.Variant         ,-1                          /*,"sql_variant"        */,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,false  ,null   ,null   ,null                                   ,null),
            new (SqlDbType.Xml             ,int.MaxValue                /*,"xml"                */,false  ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,false  ,null   ,null   ,caps => caps.XmlDataType               ,null),
            new (SqlDbType.VarChar         ,int.MaxValue                /*,"varchar({0})"       */,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null   ,"'"    ,"'"    ,null                                   ,["max length"]),
            new (SqlDbType.Char            ,int.MaxValue                /*,"char({0})"          */,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null   ,"'"    ,"'"    ,null                                   ,["length"]),
            new (SqlDbType.NChar           ,int.MaxValue / ADP.CharSize /*,"nchar({0})"         */,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null   ,"N'"   ,"'"    ,null                                   ,["length"]),
            new (SqlDbType.NVarChar        ,int.MaxValue / ADP.CharSize /*,"nvarchar({0})"      */,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null   ,"N'"   ,"'"    ,null                                   ,["max length"]),
            new (SqlDbType.VarBinary       ,int.MaxValue / ADP.CharSize /*,"varbinary({0})"     */,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null   ,"0x"   ,null   ,null                                   ,["max length"]),
            new (SqlDbType.UniqueIdentifier,null                        /*,"uniqueidentifier"   */,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null   ,"'"    ,"'"    ,null                                   ,null),
            new (SqlDbType.Date            ,null                        /*,"date"               */,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null   ,"{ts '","'}"   ,caps => caps.ExpandedDateTimeDataTypes ,null),
            new (SqlDbType.Time            ,5                           /*,"time({0})"          */,false  ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,null   ,"{ts '","'}"   ,caps => caps.ExpandedDateTimeDataTypes ,["scale"]),
            new (SqlDbType.DateTime2       ,8                           /*,"datetime2({0})"     */,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,null   ,"{ts '","'}"   ,caps => caps.ExpandedDateTimeDataTypes ,["scale"]),
            new (SqlDbType.DateTimeOffset  ,10                          /*,"datetimeoffset({0})"*/,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,null   ,"{ts '","'}"   ,caps => caps.ExpandedDateTimeDataTypes ,["scale"]),
            new (SqlDbTypeExtensions.Json  ,int.MaxValue                /*,"json"               */,false  ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,false  ,"'"    ,"'"    ,caps => caps.JsonType                  ,null),
            ];
        // * - CreateFormat value, that supposed to be produced

        // Type order follows the order from SqlMetaData.xml  
        private static readonly TypeMetaData[] s_types = [
            TypeMetaData.CreateFixedNumericType(SqlDbType.SmallInt, isBestMatch: true),
            TypeMetaData.CreateFixedNumericType(SqlDbType.Int, isBestMatch: true),
            TypeMetaData.CreateFixedNumericType(SqlDbType.Real, isBestMatch: true),
            TypeMetaData.CreateVariablePrecisionNumericType(SqlDbType.Float, columnSize: 53),
            TypeMetaData.CreateFixedNumericType(SqlDbType.Money, isBestMatch: false),
            TypeMetaData.CreateFixedNumericType(SqlDbType.SmallMoney, isBestMatch: false),
            TypeMetaData.CreateFixedNumericType(SqlDbType.Bit, isBestMatch: false),
            TypeMetaData.CreateFixedNumericType(SqlDbType.TinyInt, isBestMatch: true),
            TypeMetaData.CreateFixedNumericType(SqlDbType.BigInt, isBestMatch: true),
            TypeMetaData.CreateRowVersionType(),
            TypeMetaData.CreateFixedLengthStringOrBinaryType(SqlDbType.Binary, literalPrefix: "0x"),
            TypeMetaData.CreateLongStringOrBinaryType(SqlDbType.Image, literalPrefix: "0x"),
            TypeMetaData.CreateLongStringOrBinaryType(SqlDbType.Text, literalPrefix: "'", literalSuffix: "'"),
            TypeMetaData.CreateLongStringOrBinaryType(SqlDbType.NText, literalPrefix: "N'", literalSuffix: "'"),
            TypeMetaData.CreateVariablePrecisionNumericType(SqlDbType.Decimal, columnSize: 38),
            TypeMetaData.CreateVariablePrecisionNumericType(SqlDbType.Decimal, columnSize: 38, aliasType: "numeric"),
            TypeMetaData.CreateFixedPrecisionDateTimeType(SqlDbType.DateTime, isBestMatch: true),
            TypeMetaData.CreateFixedPrecisionDateTimeType(SqlDbType.SmallDateTime, isBestMatch: true),
            TypeMetaData.CreateSqlVariantType(),
            TypeMetaData.CreateLongStringOrBinaryType(SqlDbType.Xml, isSupported: caps => caps.XmlDataType),
            TypeMetaData.CreateVariableLengthStringOrBinaryType(SqlDbType.VarChar, literalPrefix: "'", literalSuffix: "'"),
            TypeMetaData.CreateFixedLengthStringOrBinaryType(SqlDbType.Char, literalPrefix: "'", literalSuffix: "'"),
            TypeMetaData.CreateFixedLengthStringOrBinaryType(SqlDbType.NChar, literalPrefix: "N'", literalSuffix: "'"),
            TypeMetaData.CreateVariableLengthStringOrBinaryType(SqlDbType.NVarChar, literalPrefix: "N'", literalSuffix: "'"),
            TypeMetaData.CreateVariableLengthStringOrBinaryType(SqlDbType.VarBinary, literalPrefix: "0x"),
            TypeMetaData.CreateUniqueIdentifierType(),
            TypeMetaData.CreateFixedPrecisionDateTimeType(SqlDbType.Date, isBestMatch: false, isSupported: caps => caps.ExpandedDateTimeDataTypes),
            TypeMetaData.CreateVariablePrecisionDateTimeType(SqlDbType.Time, columnSize: 5, isBestMatch: false, isSupported: caps => caps.ExpandedDateTimeDataTypes),
            TypeMetaData.CreateVariablePrecisionDateTimeType(SqlDbType.DateTime2, columnSize: 8, isSupported: caps => caps.ExpandedDateTimeDataTypes),
            TypeMetaData.CreateVariablePrecisionDateTimeType(SqlDbType.DateTimeOffset, columnSize: 10, isSupported: caps => caps.ExpandedDateTimeDataTypes),
            TypeMetaData.CreateLongStringOrBinaryType(SqlDbTypeExtensions.Json, literalPrefix: "'", literalSuffix: "'",isSupported: caps => caps.JsonType),
        ];

        #pragma warning restore format

        internal DataTypesCollection()
            : base(DbMetaDataCollectionNames.DataTypes, 0)
        {
        }

        public async override ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable? accumulator = null)
        {
            if (ADP.IsEmptyArray(context.RestrictionValues) == false)
            {
                throw ADP.TooManyRestrictions(DbMetaDataCollectionNames.DataTypes);
            }

            DataTable result = accumulator ?? new(DbMetaDataCollectionNames.DataTypes)
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
                    new DataColumn(DbMetaDataColumnNames.IsLiteralSupported, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.LiteralPrefix, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.LiteralSuffix, typeof(string))
                }
            };

            // 1. Load built-in types
            result.BeginLoadData();
            foreach(TypeMetaData t in s_types)
            {
                if (t.IsSupported == null || t.IsSupported(context.Caps))
                {
                    DataRow row = result.NewRow();
                    row[DbMetaDataColumnNames.TypeName] = t.TypeName;
                    row[DbMetaDataColumnNames.ProviderDbType] = t.ProviderDbType;
                    if (t.ColumnSize > -1)
                    {
                        row[DbMetaDataColumnNames.ColumnSize] = t.ColumnSize;
                    }

                    row[DbMetaDataColumnNames.CreateFormat] = t.CreateFormat;
                    row[DbMetaDataColumnNames.CreateParameters] = t.CreateParameters;
                    row[DbMetaDataColumnNames.DataType] = t.DataType;
                    row[DbMetaDataColumnNames.IsAutoIncrementable] = t.IsAutoIncrementable;
                    row[DbMetaDataColumnNames.IsBestMatch] = t.IsBestMatch;
                    row[DbMetaDataColumnNames.IsCaseSensitive] = t.IsCaseSensitive;
                    row[DbMetaDataColumnNames.IsFixedLength] = t.IsFixedLength;
                    row[DbMetaDataColumnNames.IsFixedPrecisionScale] = t.IsFixedPrecisionScale;
                    row[DbMetaDataColumnNames.IsLong] = t.IsLong;
                    row[DbMetaDataColumnNames.IsNullable] = t.IsNullable;
                    row[DbMetaDataColumnNames.IsSearchable] = t.IsSearchable;
                    row[DbMetaDataColumnNames.IsSearchableWithLike] = t.IsSearchableWithLike;
                    if (t.IsUnsigned != null)
                    {
                        row[DbMetaDataColumnNames.IsUnsigned] = t.IsUnsigned;
                    }

                    if (t.MaximumScale > -1)
                    {
                        row[DbMetaDataColumnNames.MaximumScale] = t.MaximumScale;
                    }

                    if (t.MinimumScale > -1)
                    {
                        row[DbMetaDataColumnNames.MinimumScale] = t.MinimumScale;
                    }

                    row[DbMetaDataColumnNames.IsConcurrencyType] = t.IsConcurrencyType;
                    if (t.IsLiteralSupported != null)
                    {
                        row[DbMetaDataColumnNames.IsLiteralSupported] = t.IsLiteralSupported;
                    }

                    row[DbMetaDataColumnNames.LiteralPrefix] = t.LiteralPrefix;
                    row[DbMetaDataColumnNames.LiteralSuffix] = t.LiteralSuffix;
                    result.Rows.Add(row);
                }
            }
            result.EndLoadData();
            result.AcceptChanges();

            // 2. Add UDTs from the server if supported
            MetaDataCollectionBase? udtCollection = FindMetaDataCollection("_UDTs", context);
            if (udtCollection != null && context.Caps.UserDefinedTypes)
            {
                const string GetEngineEditionSqlCommand = "SELECT SERVERPROPERTY('EngineEdition');";
                using SqlCommand command = ((SqlConnection)context.Connection).CreateCommand();

                command.CommandText = GetEngineEditionSqlCommand;
                int engineEdition = (int)(context.IsAsync ? await command.ExecuteScalarAsync(context.CancellationToken).ConfigureAwait(false) : command.ExecuteScalar());
                // Azure SQL Edge (9) throws an exception when querying sys.assemblies
                // Azure Synapse Analytics (6) and Azure Synapse serverless SQL pool (11)
                // do not support ASSEMBLYPROPERTY
                if (!s_assemblyPropertyUnsupportedEngines.Contains(engineEdition))
                {
                    await udtCollection.GetMetadata(context, result);
                }
            }

            // 3. Add TVPs from the server if supported
            MetaDataCollectionBase? tvpCollection = FindMetaDataCollection("_TVPs", context);
            if (tvpCollection != null)
            {
                await tvpCollection.GetMetadata(context, result);
            }

            return result;
        }
    }

    private sealed class TypeMetaData
    {
        public string TypeName{ get; init; }
        public int ProviderDbType{ get; init; }
        public int ColumnSize{ get; init; }
        public string CreateFormat{ get; init; }
        public string? CreateParameters{ get; init; }
        public string DataType{ get; init; }
        public bool IsAutoIncrementable{ get; init; }
        public bool IsBestMatch{ get; init; }
        public bool IsCaseSensitive{ get; init; }
        public bool IsFixedLength{ get; init; }
        public bool IsFixedPrecisionScale{ get; init; }
        public bool IsLong{ get; init; }
        public bool IsNullable{ get; init; }
        public bool IsSearchable{ get; init; }
        public bool IsSearchableWithLike{ get; init; }
        public bool? IsUnsigned{ get; init; }
        public short MaximumScale{ get; init; }
        public short MinimumScale{ get; init; }
        public bool IsConcurrencyType{ get; init; }
        public Predicate<ConnectionCapabilities>? IsSupported { get; init; }
        public bool? IsLiteralSupported{ get; init; }
        public string? LiteralPrefix{ get; init; }
        public string? LiteralSuffix{ get; init; }

        /// <summary>
        /// Fills main values from the <see cref="MetaType"/> specified by <paramref name="dbType"/> and sets reasonable default values for the rest of the properties.
        /// </summary>
        /// <param name="dbType"></param>
        private TypeMetaData(SqlDbType dbType)
        {
            MetaType _metaType = MetaType.GetMetaTypeFromSqlDbType(dbType, isMultiValued: false);
            TypeName = _metaType.TypeName;
            ProviderDbType = (int)_metaType.SqlDbType;
            DataType = _metaType.ClassType.FullName!;
            ColumnSize = (_metaType.Precision == TdsEnums.UNKNOWN_PRECISION_SCALE
                                            ? _metaType.FixedLength
                                            : _metaType.Precision);
            CreateFormat = _metaType.TypeName;
            CreateParameters = null;

            IsAutoIncrementable = false;
            IsBestMatch = true;
            IsCaseSensitive = false;
            IsFixedLength = true;
            IsFixedPrecisionScale = false;
            IsLong = false;
            IsNullable = true;
            IsSearchable = true;
            IsSearchableWithLike = false;
            IsUnsigned = null;
            MaximumScale = -1;
            MinimumScale = -1;
            IsConcurrencyType = false;
            IsLiteralSupported = null;
            LiteralPrefix = null;
            LiteralSuffix = null;
        }

        public TypeMetaData(SqlDbType dbType, int? columnSize,
            bool isAutoIncrementable, bool isBestMatch, bool isCaseSensitive, bool isFixedLength,
            bool isFixedPrecisionScale, bool isLong, bool isNullable, bool isSearchable, bool isSearchableWithLike,
            bool? isUnsigned, short maximumScale, short minimumScale, bool isConcurrencyType,
            bool? isLiteralSupported, string? literalPrefix, string? literalSuffix,
            Predicate<ConnectionCapabilities>? isSupported, string[]? createParameters, string? alias = null)
        {
            // Shared type properties
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(dbType, isMultiValued: false);
            TypeName = alias ?? metaType.TypeName;
            ProviderDbType = (int)metaType.SqlDbType;
            DataType = metaType.ClassType.FullName!;
            ColumnSize = columnSize ?? (metaType.Precision == TdsEnums.UNKNOWN_PRECISION_SCALE
                                            ? metaType.FixedLength
                                            : metaType.Precision);
            if (ADP.IsEmptyArray(createParameters))
            {
                CreateFormat = metaType.TypeName;
                CreateParameters = null;
            }
            else
            {
                StringBuilder sbFormat = new StringBuilder(alias ?? metaType.TypeName);
                StringBuilder sbParams = new StringBuilder();
                sbFormat.Append('(');
                for (int i = 0; i < createParameters!.Length; i++)
                {
                    if (i > 0)
                    {
                        sbFormat.Append(',');
                        sbParams.Append(',');
                    }
                    sbFormat.AppendFormat("{{{0}}}", i);
                    sbParams.Append(createParameters[i]);
                }
                sbFormat.Append(')');
                CreateFormat = sbFormat.ToString();
                CreateParameters = sbParams.ToString();
            }

            // Schema specific type properties
            IsAutoIncrementable = isAutoIncrementable;
            IsBestMatch = isBestMatch;
            IsCaseSensitive = isCaseSensitive;
            IsFixedLength = isFixedLength;
            IsFixedPrecisionScale = isFixedPrecisionScale;
            IsLong = isLong;
            IsNullable = isNullable;
            IsSearchable = isSearchable;
            IsSearchableWithLike = isSearchableWithLike;
            IsUnsigned = isUnsigned;
            MaximumScale = maximumScale;
            MinimumScale = minimumScale;
            IsConcurrencyType = isConcurrencyType;
            IsSupported = isSupported;
            IsLiteralSupported = isLiteralSupported;
            LiteralPrefix = literalPrefix;
            LiteralSuffix = literalSuffix;
        }

        internal static TypeMetaData CreateFixedNumericType(SqlDbType integerDbType, bool isBestMatch) =>
            new TypeMetaData(integerDbType)
            {
                // Of all fixed-scale integer types, only "tinyint", "smallint", "int" and "bigint" are auto-incrementable.
                IsAutoIncrementable = integerDbType is SqlDbType.TinyInt or SqlDbType.SmallInt or SqlDbType.Int or SqlDbType.BigInt,
                IsBestMatch = isBestMatch,
                // "real" is an ISO synonym of "float(24)". This means that it's a fixed-scale alias of a dynamic-scale type.
                // "bit" is also not considered fixed precision/scale, since SQL Server packs multiple bit columns into the same byte.
                IsFixedPrecisionScale = integerDbType is not SqlDbType.Real and not SqlDbType.Bit,
                // Only "tinyint" is unsigned. "bit" does not have the concept of signed/unsigned.
                IsUnsigned = integerDbType is SqlDbType.Bit ? null : integerDbType is SqlDbType.TinyInt,
            };

        internal static TypeMetaData CreateVariablePrecisionNumericType(SqlDbType numericDbType, int columnSize,
                                                                            string? aliasType = null)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(numericDbType, isMultiValued: false);
            bool variableScale = numericDbType is SqlDbType.Decimal;
            string typeName = aliasType ?? metaType.TypeName;

            return new TypeMetaData(numericDbType)
            {
                TypeName = typeName,
                ColumnSize = columnSize,
                CreateFormat = variableScale ? $"{typeName}({{0}}, {{1}})" : $"{typeName}({{0}})",
                CreateParameters = variableScale ? "precision,scale" : "number of bits used to store the mantissa",
                // Both "float" and "decimal" have variable precision, but "decimal" also has variable scale.
                MinimumScale = (short)(variableScale ? 0 : -1),
                // The data type is a fixed number of bytes, which can be distributed between the precision
                // and the scale. Therefore, the maximum scale is equal to the column size.
                MaximumScale = (short)(variableScale ? columnSize : -1),
                IsAutoIncrementable = numericDbType is SqlDbType.Decimal,
                IsUnsigned = false
            };
        }

        internal static TypeMetaData CreateFixedPrecisionDateTimeType(SqlDbType dateTimeDbType, bool isBestMatch,
            Predicate<ConnectionCapabilities>? isSupported = null) => new TypeMetaData(dateTimeDbType)
            {
                IsBestMatch = isBestMatch,
                IsFixedPrecisionScale = dateTimeDbType is SqlDbType.Date,
                IsSearchableWithLike = true,
                LiteralPrefix = @"{ts '",
                LiteralSuffix = @"'}",
                IsSupported = isSupported
            };

        internal static TypeMetaData CreateVariablePrecisionDateTimeType(SqlDbType dateTimeDbType, int columnSize,
            bool isBestMatch = true,
            Predicate<ConnectionCapabilities>? isSupported = null)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(dateTimeDbType, isMultiValued: false);
            return new TypeMetaData(dateTimeDbType)
            {
                ColumnSize = columnSize,
                CreateFormat = $"{metaType.TypeName}({{0}})",
                // The documentation describes these data types as having variable precision, but GetSchema reports that they
                // have variable scale.
                CreateParameters = "scale",
                IsBestMatch = isBestMatch,
                IsFixedLength = false,
                IsSearchableWithLike = true,
                MinimumScale = 0,
                MaximumScale = metaType.Scale,
                LiteralPrefix = @"{ts '",
                LiteralSuffix = @"'}",
                IsSupported = isSupported
            };
        }

        internal static TypeMetaData CreateStringOrBinaryType(SqlDbType sqlDbType, int columnSize, bool isLong,
            bool isFixedLength, bool isSearchable,
            string? literalPrefix = null, string? literalSuffix = null,
            Predicate<ConnectionCapabilities>? isSupported = null)
        {
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(sqlDbType, isMultiValued: false);
            bool hasLengthSpecifier = sqlDbType is SqlDbType.Char or SqlDbType.NChar or SqlDbType.Binary
                or SqlDbType.VarChar or SqlDbType.NVarChar or SqlDbType.VarBinary;
            return new TypeMetaData(sqlDbType)
            {
                ColumnSize = columnSize,
                CreateFormat = hasLengthSpecifier ? $"{metaType.TypeName}({{0}})" : metaType.TypeName,
                // Several string or binary data types do not include the length in their type declaration.
                // Of the data types which do, fixed-length types use the length parameter to decide the full length,
                // while the rest use it to decide the maximum length.
                CreateParameters = hasLengthSpecifier ? (isFixedLength ? "length" : "max length") : null,
                // The DataType for XML is string, which is not the best match for an XML type.
                // Similarly, although timestamp/rowversion is represented as a byte array, it's not best
                // represented as such.
                IsBestMatch = sqlDbType is not SqlDbType.Xml and not SqlDbType.Timestamp and not SqlDbTypeExtensions.Json,
                IsConcurrencyType = sqlDbType is SqlDbType.Timestamp,
                IsFixedLength = isFixedLength,
                IsLong = isLong,
                IsNullable = sqlDbType is not SqlDbType.Timestamp,
                IsSearchable = isSearchable,
                // String types are searchable with LIKE; binary types are not. SQL Server considers XML and JSON a binary type for this purpose.
                IsSearchableWithLike = metaType.IsCharType && sqlDbType is not SqlDbType.Xml and not SqlDbTypeExtensions.Json,
                // If no literal prefix or suffix is specified, then literal support is not available.
                IsLiteralSupported = literalPrefix is not null || literalSuffix is not null,
                LiteralPrefix = literalPrefix,
                LiteralSuffix = literalSuffix,
                IsSupported = isSupported,
            };
        }

        internal static TypeMetaData CreateLongStringOrBinaryType(SqlDbType longDbType,
            string? literalPrefix = null, string? literalSuffix = null,
            Predicate<ConnectionCapabilities>? isSupported = null) =>
            CreateStringOrBinaryType(longDbType,
                // The column size is measured in elements, not bytes. For ntext, each element is a 2 byte Unicode character.
                columnSize: longDbType is SqlDbType.NText ? int.MaxValue / ADP.CharSize : int.MaxValue,
                isLong: true, isFixedLength: false,
                isSearchable: false,
                literalPrefix: literalPrefix, literalSuffix: literalSuffix,
                isSupported: isSupported);

        internal static TypeMetaData CreateFixedLengthStringOrBinaryType(SqlDbType fixedLengthDbType,
            string? literalPrefix = null, string? literalSuffix = null) =>
            CreateStringOrBinaryType(fixedLengthDbType,
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

        internal static TypeMetaData CreateVariableLengthStringOrBinaryType(SqlDbType variableLengthDbType,
            string? literalPrefix = null, string? literalSuffix = null) =>
            CreateStringOrBinaryType(variableLengthDbType,
                // See the comment on AddLongStringOrBinary regarding column sizes. Unlike the "binary" type, varbinary is reported to
                // have a maximum column size of (2^32-1) / 2 elements, just as nvarchar does.
                columnSize: variableLengthDbType is SqlDbType.NVarChar or SqlDbType.VarBinary
                    ? int.MaxValue / ADP.CharSize
                    : int.MaxValue,
                isLong: false, isFixedLength: false,
                isSearchable: true,
                literalPrefix: literalPrefix, literalSuffix: literalSuffix);

        internal static TypeMetaData CreateRowVersionType() =>
            CreateStringOrBinaryType(SqlDbType.Timestamp,
                columnSize: TdsEnums.TEXT_TIME_STAMP_LEN, isLong: false, isFixedLength: true,
                isSearchable: true,
                literalPrefix: "0x");

        internal static TypeMetaData CreateSqlVariantType() => new TypeMetaData(SqlDbType.Variant)
        {
            IsFixedLength = false,
            IsLiteralSupported = false
        };

        internal static TypeMetaData CreateUniqueIdentifierType() => new TypeMetaData(SqlDbType.UniqueIdentifier)
        {
            LiteralPrefix = "'",
            LiteralSuffix = "'",
        };
    }
}
