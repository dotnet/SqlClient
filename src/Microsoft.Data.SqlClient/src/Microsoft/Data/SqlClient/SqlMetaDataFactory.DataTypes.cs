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

        private static readonly TypeMetaData[] s_types = [
            // Type order follows the order from SqlMetaData.xml               
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

        public TypeMetaData(string typeName, int providerDbType, int columnSize, string createFormat,
            string dataType, bool isAutoIncrementable, bool isBestMatch, bool isCaseSensitive, bool isFixedLength,
            bool isFixedPrecisionScale, bool isLong, bool isNullable, bool isSearchable, bool isSearchableWithLike,
            bool? isUnsigned, short maximumScale, short minimumScale, bool isConcurrencyType,
            bool? isLiteralSupported, string? literalPrefix, string? literalSuffix,
            Predicate<ConnectionCapabilities>? isSupported, string createParameters)
        {
            TypeName = typeName;
            ProviderDbType = providerDbType;
            ColumnSize = columnSize;
            CreateFormat = createFormat;
            CreateParameters = createParameters;
            DataType = dataType;
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
    }
}
