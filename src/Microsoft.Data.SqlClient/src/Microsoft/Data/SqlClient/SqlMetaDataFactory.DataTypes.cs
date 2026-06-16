// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Connection;

#nullable enable

namespace Microsoft.Data.SqlClient;

internal sealed partial class SqlMetaDataFactory
{
    private sealed class DataTypesCollection : MetaDataCollectionBase
    {
        #pragma warning disable format
        private static readonly TypeMetaData[] s_types_flat = [
            // Type order follows the order from SqlMetaData.xml               
            //                                                                                            IsBestMatch             isFixedPrecisionScale   IsSearchable           MaximumScale             MinimumVersion                  LiteralPrefix
            //                      ProviderDbType                                                                IsCaseSensitive         IsLong                  IsSearchableWithLike    MinimumScale                    MaximumVersion          LiteralSuffix
            //   TypeName                ColumnSize CreateFormat          DataType                IsAutoIncrementable     IsFixedLength           IsNullable              isUnsigned              IsConcurrencyType               IsLiteralSupported      CreateParameters
            new ("smallint"        ,16  ,5         ,"smallint"           ,"System.Int16"         ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new ("int"             ,8   ,10        ,"int"                ,"System.Int32"         ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new ("real"            ,13  ,7         ,"real"               ,"System.Single"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new ("float"           ,6   ,53        ,"float({0})"         ,"System.Double"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,"number of bits used to store the mantissa"),
            new ("money"           ,9   ,19        ,"money"              ,"System.Decimal"       ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new ("smallmoney"      ,17  ,10        ,"smallmoney"         ,"System.Decimal"       ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new ("bit"             ,2   ,1         ,"bit"                ,"System.Boolean"       ,false  ,false  ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new ("tinyint"         ,20  ,3         ,"tinyint"            ,"System.Byte"          ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new ("bigint"          ,0   ,19        ,"bigint"             ,"System.Int64"         ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new ("timestamp"       ,19  ,8         ,"timestamp"          ,"System.Byte[]"        ,false  ,false  ,false  ,true   ,false  ,false  ,false  ,true   ,false  ,null   ,-1     ,-1     ,true   ,null           ,null   ,null   ,"0x"   ,null   ,""),
            new ("binary"          ,1   ,8000      ,"binary({0})"        ,"System.Byte[]"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,"length"),
            new ("image"           ,7   ,2147483647,"image"              ,"System.Byte[]"        ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,""),
            new ("text"            ,18  ,2147483647,"text"               ,"System.String"        ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,""),
            new ("ntext"           ,11  ,1073741823,"ntext"              ,"System.String"        ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,""),
            new ("decimal"         ,5   ,38        ,"decimal({0}, {1})"  ,"System.Decimal"       ,true   ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,38     ,0      ,false  ,null           ,null   ,null   ,null   ,null   ,"precision,scale"),
            new ("numeric"         ,5   ,38        ,"numeric({0}, {1})"  ,"System.Decimal"       ,true   ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,38     ,0      ,false  ,null           ,null   ,null   ,null   ,null   ,"precision,scale"),
            new ("datetime"        ,4   ,23        ,"datetime"           ,"System.DateTime"      ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"{ts '","'}"   ,""),
            new ("smalldatetime"   ,15  ,16        ,"smalldatetime"      ,"System.DateTime"      ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"{ts '","'}"   ,""),
            new ("sql_variant"     ,23  ,-1        ,"sql_variant"        ,"System.Object"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,false  ,null   ,null   ,""),
            new ("xml"             ,25  ,2147483647,"xml"                ,"System.String"        ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,false  ,null   ,null   ,""),
            new ("varchar"         ,22  ,2147483647,"varchar({0})"       ,"System.String"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,"max length"),
            new ("char"            ,3   ,2147483647,"char({0})"          ,"System.String"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,"length"),
            new ("nchar"           ,10  ,1073741823,"nchar({0})"         ,"System.String"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,"length"),
            new ("nvarchar"        ,12  ,1073741823,"nvarchar({0})"      ,"System.String"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,"max length"),
            new ("varbinary"       ,21  ,1073741823,"varbinary({0})"     ,"System.Byte[]"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,"max length"),
            new ("uniqueidentifier",14  ,16        ,"uniqueidentifier"   ,"System.Guid"          ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,""),
            new ("date"            ,31  ,3         ,"date"               ,"System.DateTime"      ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,""),
            new ("time"            ,32  ,5         ,"time({0})"          ,"System.TimeSpan"      ,false  ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
            new ("datetime2"       ,33  ,8         ,"datetime2({0})"     ,"System.DateTime"      ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
            new ("datetimeoffset"  ,34  ,10        ,"datetimeoffset({0})","System.DateTimeOffset",false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
            new ("json"            ,35  ,2147483647,"json"               ,"System.String"        ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,"17.00.000.0"  ,null   ,false  ,"'"    ,"'"    ,""),
            ];

        private static readonly TypeMetaData[] s_types = [
            // Type order follows the order from SqlMetaData.xml               
            //                                                                       IsBestMatch             isFixedPrecisionScale   IsSearchable           MaximumScale             MinimumVersion                  LiteralPrefix
            //                                                                               IsCaseSensitive         IsLong                  IsSearchableWithLike    MinimumScale                    MaximumVersion          LiteralSuffix
            //                              ColumnSize CreateFormat          IsAutoIncrementable     IsFixedLength           IsNullable              isUnsigned              IsConcurrencyType               IsLiteralSupported      CreateParameters
            new (SqlDbType.SmallInt        ,5         ,"smallint"           ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new (SqlDbType.Int             ,10        ,"int"                ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new (SqlDbType.Real            ,7         ,"real"               ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new (SqlDbType.Float           ,53        ,"float({0})"         ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,"number of bits used to store the mantissa"),
            new (SqlDbType.Money           ,19        ,"money"              ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new (SqlDbType.SmallMoney      ,10        ,"smallmoney"         ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new (SqlDbType.Bit             ,1         ,"bit"                ,false  ,false  ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new (SqlDbType.TinyInt         ,3         ,"tinyint"            ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new (SqlDbType.BigInt          ,19        ,"bigint"             ,true   ,true   ,false  ,true   ,true   ,false  ,true   ,true   ,false  ,false  ,-1     ,-1     ,false  ,null           ,null   ,null   ,null   ,null   ,""),
            new (SqlDbType.Timestamp       ,8         ,"timestamp"          ,false  ,false  ,false  ,true   ,false  ,false  ,false  ,true   ,false  ,null   ,-1     ,-1     ,true   ,null           ,null   ,null   ,"0x"   ,null   ,""),
            new (SqlDbType.Binary          ,8000      ,"binary({0})"        ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,"length"),
            new (SqlDbType.Image           ,2147483647,"image"              ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,""),
            new (SqlDbType.Text            ,2147483647,"text"               ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,""),
            new (SqlDbType.NText           ,1073741823,"ntext"              ,false  ,true   ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,""),
            new (SqlDbType.Decimal         ,38        ,"decimal({0}, {1})"  ,true   ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,38     ,0      ,false  ,null           ,null   ,null   ,null   ,null   ,"precision,scale"),
            new (SqlDbType.Decimal         ,38        ,"numeric({0}, {1})"  ,true   ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,false  ,38     ,0      ,false  ,null           ,null   ,null   ,null   ,null   ,"precision,scale",
                   alias: "numeric"),
            new (SqlDbType.DateTime        ,23        ,"datetime"           ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"{ts '","'}"   ,""),
            new (SqlDbType.SmallDateTime   ,16        ,"smalldatetime"      ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"{ts '","'}"   ,""),
            new (SqlDbType.Variant         ,-1        ,"sql_variant"        ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,false  ,null   ,null   ,""),
            new (SqlDbType.Xml             ,2147483647,"xml"                ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,false  ,null   ,null   ,""),
            new (SqlDbType.VarChar         ,2147483647,"varchar({0})"       ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,"max length"),
            new (SqlDbType.Char            ,2147483647,"char({0})"          ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,"length"),
            new (SqlDbType.NChar           ,1073741823,"nchar({0})"         ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,"length"),
            new (SqlDbType.NVarChar        ,1073741823,"nvarchar({0})"      ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"N'"   ,"'"    ,"max length"),
            new (SqlDbType.VarBinary       ,1073741823,"varbinary({0})"     ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"0x"   ,null   ,"max length"),
            new (SqlDbType.UniqueIdentifier,16        ,"uniqueidentifier"   ,false  ,true   ,false  ,true   ,false  ,false  ,true   ,true   ,false  ,null   ,-1     ,-1     ,false  ,null           ,null   ,null   ,"'"    ,"'"    ,""),
            new (SqlDbType.Date            ,3         ,"date"               ,false  ,false  ,false  ,true   ,true   ,false  ,true   ,true   ,true   ,null   ,-1     ,-1     ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,""),
            new (SqlDbType.Time            ,5         ,"time({0})"          ,false  ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
            new (SqlDbType.DateTime2       ,8         ,"datetime2({0})"     ,false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
            new (SqlDbType.DateTimeOffset  ,10        ,"datetimeoffset({0})",false  ,true   ,false  ,false  ,false  ,false  ,true   ,true   ,true   ,null   ,7      ,0      ,false  ,"10.00.000.0"  ,null   ,null   ,"{ts '","'}"   ,"scale"),
            new (SqlDbTypeExtensions.Json  ,35        ,"json"               ,false  ,false  ,false  ,false  ,false  ,true   ,true   ,false  ,false  ,null   ,-1     ,-1     ,false  ,"17.00.000.0"  ,null   ,false  ,"'"    ,"'"    ,""),
            ];

        #pragma warning restore format

        internal DataTypesCollection()
            : base(DbMetaDataCollectionNames.DataTypes, 0, 0)
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
                    //new DataColumn(MaximumVersionKey, typeof(string)),
                    //new DataColumn(MinimumVersionKey, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.IsLiteralSupported, typeof(bool)),
                    new DataColumn(DbMetaDataColumnNames.LiteralPrefix, typeof(string)),
                    new DataColumn(DbMetaDataColumnNames.LiteralSuffix, typeof(string))
                }
            };

            // 1. Load built-in types
            result.BeginLoadData();
            foreach(TypeMetaData t in s_types)
            {
                // TODO : There is a problem, that json datatype is not returned on Azure, because it's minimumVersion="17.0.0000.0", but Azure SQL version is always 12.0.2000.8
                // || ((context.Connection as SqlConnection)?.InnerConnection as SqlConnectionInternal)?.IsAzureSqlConnection ?? false)
                if ((t.MinimumVersion == null || string.Compare(context.ServerVersion, t.MinimumVersion, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    (t.MaximumVersion == null || string.Compare(context.ServerVersion, t.MaximumVersion, StringComparison.OrdinalIgnoreCase) <= 0))
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
            MetaDataCollectionBase? udtCollection = FindMetaDataCollection("_UDTs", context.ServerVersion);
            if (udtCollection != null)
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
            MetaDataCollectionBase? tvpCollection = FindMetaDataCollection("_TVPs", context.ServerVersion);
            if (tvpCollection != null)
            {
                await tvpCollection.GetMetadata(context, result);
            }

            return result;
        }
    }

    private sealed class TypeMetaData
    {
        public readonly string TypeName;
        public readonly int ProviderDbType;
        public readonly long ColumnSize;
        public readonly string CreateFormat;
        public readonly string? CreateParameters;
        public readonly string DataType;
        public readonly bool IsAutoIncrementable;
        public readonly bool IsBestMatch;
        public readonly bool IsCaseSensitive;
        public readonly bool IsFixedLength;
        public readonly bool IsFixedPrecisionScale;
        public readonly bool IsLong;
        public readonly bool IsNullable;
        public readonly bool IsSearchable;
        public readonly bool IsSearchableWithLike;
        public readonly bool? IsUnsigned;
        public readonly short MaximumScale;
        public readonly short MinimumScale;
        public readonly bool IsConcurrencyType;
        public readonly string? MaximumVersion;
        public readonly string? MinimumVersion;
        public readonly bool? IsLiteralSupported;
        public readonly string? LiteralPrefix;
        public readonly string? LiteralSuffix;

        public TypeMetaData(string typeName, int providerDbType, long columnSize, string createFormat,
            string dataType, bool isAutoIncrementable, bool isBestMatch, bool isCaseSensitive, bool isFixedLength,
            bool isFixedPrecisionScale, bool isLong, bool isNullable, bool isSearchable, bool isSearchableWithLike,
            bool? isUnsigned, short maximumScale, short minimumScale, bool isConcurrencyType,
            string? minimumVersion, string? maximumVersion, bool? isLiteralSupported, string? literalPrefix, string? literalSuffix, string createParameters)
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
            MaximumVersion = maximumVersion;
            MinimumVersion = minimumVersion;
            IsLiteralSupported = isLiteralSupported;
            LiteralPrefix = literalPrefix;
            LiteralSuffix = literalSuffix;
        }

        public TypeMetaData(SqlDbType dbType, long columnSize, string createFormat,
            bool isAutoIncrementable, bool isBestMatch, bool isCaseSensitive, bool isFixedLength,
            bool isFixedPrecisionScale, bool isLong, bool isNullable, bool isSearchable, bool isSearchableWithLike,
            bool? isUnsigned, short maximumScale, short minimumScale, bool isConcurrencyType,
            string? minimumVersion, string? maximumVersion, bool? isLiteralSupported, string? literalPrefix, string? literalSuffix,
            string createParameters, string? alias = null)
        {
            // Shared type properties
            MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(dbType, isMultiValued: false);
            TypeName = alias ?? metaType.TypeName;
            ProviderDbType = (int)metaType.SqlDbType;
            DataType = metaType.ClassType.FullName!;

            // Schema specific type properties
            ColumnSize = columnSize;
            CreateParameters = createParameters;
            CreateFormat = createFormat;
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
            MaximumVersion = maximumVersion;
            MinimumVersion = minimumVersion;
            IsLiteralSupported = isLiteralSupported;
            LiteralPrefix = literalPrefix;
            LiteralSuffix = literalSuffix;
        }
    }
}
