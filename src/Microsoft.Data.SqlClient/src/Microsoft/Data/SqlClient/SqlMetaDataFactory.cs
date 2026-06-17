// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Connection;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlMetaDataFactory
    {    
        #pragma warning disable format
        private readonly static MetaDataCollectionBase[] s_metaDataCollection = [
            new MetaDataCollection(),  // GetSchemaCore(...) expects MetaDataCollection to be first element.
            new DataSourceInformationCollection(),
            new DataTypesCollection(),
            new RestrictionsCollection(),
            new ReservedWordsCollection(),
            new SqlCommandCollection("Users",                  1, 1, 
                    [new SupportedQuery(null,          null, "select uid, name as user_name, createdate, updatedate from sysusers where (name = @Name or (@Name is null))")],
                    [new Restriction(1, "User_Name", "@Name")]),
            new SqlCommandCollection("Databases",              1, 1,
                    [new SupportedQuery(null, "09.99.999.9", "select name as database_name, dbid, crdate as create_date from master..sysdatabases where (name = @Name or (@Name is null))"),
                     new SupportedQuery("10.00.000.0", null, "IF OBJECT_ID('master..sysdatabases') IS NULL EXEC sp_executesql N'select name as database_name, dbid, crdate as create_date from sysdatabases where (name = @Name or (@Name is null))',N'@Name NVARCHAR(128)',@Name=@Name ELSE EXEC sp_executesql N'select name as database_name, dbid, crdate as create_date from master..sysdatabases where (name = @Name or (@Name is null))',N'@Name NVARCHAR(128)',@Name=@Name")],
                    [new Restriction(1, "Name", "@Name")]),
            new SqlCommandCollection("Tables",                 4, 3,
                    [new SupportedQuery(null,          null, "select TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE from INFORMATION_SCHEMA.TABLES where (TABLE_CATALOG = @Catalog or (@Catalog is null)) and (TABLE_SCHEMA = @Owner or (@Owner is null)) and (TABLE_NAME = @Name or (@Name is null)) and (TABLE_TYPE = @TableType or (@TableType is null))")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Name"),
                     new Restriction(4, "TableType", "@TableType")]),
            new SqlCommandCollection("Columns",                4, 4,
                    [new SupportedQuery(null, "09.99.999.9", "select TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, ORDINAL_POSITION, COLUMN_DEFAULT, IS_NULLABLE, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, CHARACTER_OCTET_LENGTH, NUMERIC_PRECISION, NUMERIC_PRECISION_RADIX, NUMERIC_SCALE, DATETIME_PRECISION, CHARACTER_SET_CATALOG, CHARACTER_SET_SCHEMA, CHARACTER_SET_NAME, COLLATION_CATALOG from INFORMATION_SCHEMA.COLUMNS where (TABLE_CATALOG = @Catalog or (@Catalog is null)) and (TABLE_SCHEMA = @Owner or (@Owner is null)) and (TABLE_NAME = @Table or (@Table is null)) and (COLUMN_NAME = @Column or (@Column is null)) order by TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME"),
                     new SupportedQuery("10.00.000.0", null, "EXEC sys.sp_columns_managed @Catalog, @Owner, @Table, @Column, 0")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Column", "@Column")]),
            new SqlCommandCollection("AllColumns",             4, 4,
                    [new SupportedQuery("10.00.000.0", null, "EXEC sys.sp_columns_managed @Catalog, @Owner, @Table, @Column, 1")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Column", "@Column")]),
            new SqlCommandCollection("ColumnSetColumns",       3, 3,
                    [new SupportedQuery("10.00.000.0", null, "EXEC sys.sp_columns_managed @Catalog, @Owner, @Table, null, 2")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table")]),
            new SqlCommandCollection("StructuredTypeMembers",  4, 4,
                    [new SupportedQuery("10.00.000.0", null, "SELECT DB_NAME() AS TYPE_CATALOG, sc.name AS TYPE_SCHEMA, tt.name AS TYPE_NAME, c.name AS MEMBER_NAME, ColumnProperty(c.object_id, c.name, 'ordinal') AS ORDINAL_POSITION, convert(nvarchar(4000), object_definition(c.default_object_id)) AS MEMBER_DEFAULT, convert(varchar(3), CASE c.is_nullable WHEN 1 THEN 'YES' ELSE 'NO' END) AS IS_NULLABLE, type_name(c.system_type_id) AS DATA_TYPE, ColumnProperty(c.object_id, c.name, 'charmaxlen') AS CHARACTER_MAXIMUM_LENGTH, ColumnProperty(c.object_id, c.name, 'octetmaxlen') AS CHARACTER_OCTET_LENGTH, convert(tinyint, CASE WHEN c.system_type_id IN /* int/decimal/numeric/real/float/money */ (48, 52, 56, 59, 60, 62, 106, 108, 122, 127) THEN c.precision END) AS NUMERIC_PRECISION, convert(smallint, CASE WHEN c.system_type_id IN /* int/money/decimal/numeric */ (48, 52, 56, 60, 106, 108, 122, 127) THEN 10 WHEN c.system_type_id IN /* real/float */ (59, 62) THEN 2 END) AS NUMERIC_PRECISION_RADIX, convert(int, CASE WHEN c.system_type_id IN /* datetime/smalldatetime */ (58, 61) THEN NULL ELSE odbcscale(c.system_type_id, c.scale) END) AS NUMERIC_SCALE, convert(smallint, CASE WHEN c.system_type_id IN /* datetime/smalldatetime */ (58, 61) THEN 3 END) AS DATETIME_PRECISION, convert(sysname, null) AS CHARACTER_SET_CATALOG, convert(sysname, null) AS CHARACTER_SET_SCHEMA, convert(sysname, CASE WHEN c.system_type_id IN /* char/varchar/text */ (35, 167, 175) THEN CollationProperty(c.collation_name, 'sqlcharsetname') WHEN c.system_type_id IN /* nchar/nvarchar/ntext */ (99, 231, 239) THEN N'UNICODE' END) AS CHARACTER_SET_NAME, convert(sysname, null) AS COLLATION_CATALOG FROM sys.table_types tt join sys.objects o on o.object_id = tt.type_table_object_id JOIN sys.schemas sc on sc.schema_id = tt.schema_id JOIN sys.columns c ON c.object_id = o.object_id LEFT JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE o.type IN ('TT') AND (DB_NAME() = @Catalog or (@Catalog is null)) and (sc.name = @Owner or (@Owner is null)) and (tt.name = @Type or (@Type is null)) and (c.name = @Member or (@Member is null)) order by sc.name, tt.name, c.name")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Type", "@Type"),
                     new Restriction(4, "Member", "@Member")]),
            new SqlCommandCollection("Views",                  3, 3,
                    [new SupportedQuery("08.00.000.0", null, "select TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, CHECK_OPTION, IS_UPDATABLE from INFORMATION_SCHEMA.VIEWS where (TABLE_CATALOG = @Catalog or (@Catalog is null)) and (TABLE_SCHEMA = @Owner or (@Owner is null)) and (TABLE_NAME = @Table or (@Table is null)) order by TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table")]),
            new SqlCommandCollection("ViewColumns",            4, 4,
                    [new SupportedQuery("08.00.000.0", null, "select VIEW_CATALOG, VIEW_SCHEMA, VIEW_NAME, TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME from INFORMATION_SCHEMA.VIEW_COLUMN_USAGE where (VIEW_CATALOG = @Catalog or (@Catalog is null)) and (VIEW_SCHEMA = @Owner or (@Owner is null)) and (VIEW_NAME = @Table or (@Table is null)) and (COLUMN_NAME = @Column or (@Column is null)) order by VIEW_CATALOG, VIEW_SCHEMA, VIEW_NAME")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Column", "@Column")]),
            new SqlCommandCollection("ProcedureParameters",    4, 1,
                    [new SupportedQuery("08.00.0000", null,  "select SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME, ORDINAL_POSITION, PARAMETER_MODE, IS_RESULT, AS_LOCATOR, PARAMETER_NAME, CASE WHEN DATA_TYPE IS NULL THEN USER_DEFINED_TYPE_NAME WHEN DATA_TYPE = 'table type' THEN USER_DEFINED_TYPE_NAME ELSE DATA_TYPE END as DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, CHARACTER_OCTET_LENGTH, COLLATION_CATALOG, COLLATION_SCHEMA, COLLATION_NAME, CHARACTER_SET_CATALOG, CHARACTER_SET_SCHEMA, CHARACTER_SET_NAME, NUMERIC_PRECISION, NUMERIC_PRECISION_RADIX, NUMERIC_SCALE, DATETIME_PRECISION, INTERVAL_TYPE, INTERVAL_PRECISION from INFORMATION_SCHEMA.PARAMETERS where (SPECIFIC_CATALOG = @Catalog or (@Catalog is null)) and (SPECIFIC_SCHEMA = @Owner or (@Owner is null)) and (SPECIFIC_NAME = @Name or (@Name is null)) and (PARAMETER_NAME = @Parameter or (@Parameter is null)) order by SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME, PARAMETER_NAME")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Name", "@Name"),
                     new Restriction(4, "Parameter", "@Parameter")]),
            new SqlCommandCollection("Procedures",             4, 3,
                    [new SupportedQuery("08.00.0000", null, "select SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME, ROUTINE_CATALOG, ROUTINE_SCHEMA, ROUTINE_NAME, ROUTINE_TYPE, CREATED, LAST_ALTERED from INFORMATION_SCHEMA.ROUTINES where (SPECIFIC_CATALOG = @Catalog or (@Catalog is null)) and (SPECIFIC_SCHEMA = @Owner or (@Owner is null)) and (SPECIFIC_NAME = @Name or (@Name is null)) and (ROUTINE_TYPE = @Type or (@Type is null)) order by SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Name", "@Name"),
                     new Restriction(4, "Type", "@Type")]),
            new SqlCommandCollection("IndexColumns",           5, 4,
                    [new SupportedQuery(null, "09.99.9999", "select distinct db_Name() as constraint_catalog, constraint_schema = user_name(o.uid), constraint_name = x.name, table_catalog  = db_name(), table_schema = user_name(o.uid), table_name = o.name, column_name = c.name, ordinal_position = convert(int, xk.keyno), KeyType  = c.xtype, index_name = x.name from sysobjects o, sysindexes x, syscolumns c, sysindexkeys xk where o.type in ('U') and x.id = o.id  and o.id = c.id and o.id = xk.id and x.indid = xk.indid and c.colid = xk.colid and xk.keyno < = x.keycnt and permissions(o.id, c.name) <> 0  and (db_name() = @Catalog or (@Catalog is null)) and (user_name()= @Owner or (@Owner is null)) and (o.name = @Table or (@Table is null)) and (x.name = @ConstraintName or (@ConstraintName is null)) and (c.name = @Column or (@Column is null)) order by table_name, index_name"),
                     new SupportedQuery("10.00.0000", null, "EXEC sys.sp_indexcolumns_managed @Catalog, @Owner, @Table, @ConstraintName, @Column")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "ConstraintName", "@ConstraintName"),
                     new Restriction(5, "Column", "@Column")]),
            new SqlCommandCollection("Indexes",                4, 3,
                    [new SupportedQuery(null, "09.99.9999", "select distinct db_Name() as constraint_catalog, constraint_schema = user_name(o.uid), constraint_name = x.name, table_catalog  = db_name(), table_schema = user_name(o.uid), table_name = o.name, index_name = x.name from sysobjects o, sysindexes x, sysindexkeys xk where o.type in ('U') and x.id = o.id  and o.id = xk.id and x.indid = xk.indid and xk.keyno < = x.keycnt and (db_name() = @Catalog or (@Catalog is null)) and (user_name()= @Owner or (@Owner is null)) and (o.name = @Table or (@Table is null)) and (x.name = @Name or (@Name is null)) order by table_name, index_name"),
                     new SupportedQuery("10.00.0000", null, "EXEC sys.sp_indexes_managed @Catalog, @Owner, @Table, @Name")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Name", "@Name")]),
            new SqlCommandCollection("UserDefinedTypes",       2, 1,
                    [new SupportedQuery("09.00.0000", null, "select assemblies.name as assembly_name, types.assembly_class as udt_name, ASSEMBLYPROPERTY(assemblies.name, 'VersionMajor') as version_major, ASSEMBLYPROPERTY(assemblies.name, 'VersionMinor') as version_minor, ASSEMBLYPROPERTY(assemblies.name, 'VersionBuild') as version_build, ASSEMBLYPROPERTY(assemblies.name, 'VersionRevision') as version_revision, ASSEMBLYPROPERTY(assemblies.name, 'CultureInfo') as culture_info, ASSEMBLYPROPERTY(assemblies.name, 'PublicKey') as public_key, is_fixed_length, max_length, Create_Date, Permission_set_desc from sys.assemblies as assemblies  join sys.assembly_types as types on assemblies.assembly_id = types.assembly_id where (assemblies.name = @AssemblyName or (@AssemblyName is null)) and (types.assembly_class = @UDTName or (@UDTName is null))")],
                    [new Restriction(1, "assembly_name", "@AssemblyName"),
                     new Restriction(2, "udt_name", "@UDTName")]),
            new SqlCommandCollection("ForeignKeys",            4, 3,
                    [new SupportedQuery(null,         null, "select CONSTRAINT_CATALOG, CONSTRAINT_SCHEMA, CONSTRAINT_NAME, TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, CONSTRAINT_TYPE, IS_DEFERRABLE, INITIALLY_DEFERRED from INFORMATION_SCHEMA.TABLE_CONSTRAINTS where (CONSTRAINT_CATALOG = @Catalog or (@Catalog is null)) and (CONSTRAINT_SCHEMA = @Owner or (@Owner is null)) and (TABLE_NAME = @Table or (@Table is null)) and (CONSTRAINT_NAME = @Name or (@Name is null)) and CONSTRAINT_TYPE = 'FOREIGN KEY' order by CONSTRAINT_CATALOG, CONSTRAINT_SCHEMA, CONSTRAINT_NAME")],
                    [new Restriction(1, "Catalog", "@Catalog"),
                     new Restriction(2, "Owner", "@Owner"),
                     new Restriction(3, "Table", "@Table"),
                     new Restriction(4, "Name", "@Name")]),
            new SqlCommandCollection("_TVPs",                  0, 0,
                    [new SupportedQuery("10.00.0000", null, "select name TypeName, 30 ProviderDbType, max_length ColumnSize, null CreateFormat, null CreateParameters, null DataType, null IsAutoincrementable, null IsBestMatch, null IsCaseSensitive, null IsFixedLength, null IsFixedPrecisionScale, null IsLong, is_nullable IsNullable, 0 IsSearchable, null IsSearchableWithLike, null IsUnsigned, null MaximumScale, null MinimumScale, null IsConcurrencyType, 0 IsLiteralSupported, null LiteralPrefix, null LiteralSuffix, null NativeDataType from sys.types  where is_table_type = 1")],
                    []),
            new SqlCommandCollection("_UDTs",                  0, 0,
                    [new SupportedQuery("09.00.0000", null, "select types.assembly_class COLLATE database_default + ', ' + assemblies.name + ', Version=' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'VersionMajor')) + '.' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'VersionMinor')) + '.' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'VersionBuild')) + '.' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'VersionRevision')) + ISNULL(', Culture=' + CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'CultureInfo')),'') + ISNULL(', PublicKeyToken=' + LOWER(REPLACE(CONVERT(VARCHAR(200),ASSEMBLYPROPERTY(assemblies.name, 'PublicKey'),1),'0x','')),'') TypeName, 29 ProviderDbType, max_length ColumnSize, null CreateFormat, null CreateParameters, null DataType, null IsAutoincrementable, null IsBestMatch, null IsCaseSensitive, is_fixed_length IsFixedLength, null IsFixedPrecisionScale, null IsLong, is_nullable IsNullable, 1 IsSearchable, null IsSearchableWithLike, null IsUnsigned, null MaximumScale, null MinimumScale, null IsConcurrencyType, 0 IsLiteralSupported, null LiteralPrefix, null LiteralSuffix, null NativeDataType from sys.assemblies as assemblies  join sys.assembly_types as types on assemblies.assembly_id = types.assembly_id")],
                    []),
        ];
        #pragma warning restore format


        // Well-known column names
        private const string MaximumVersionKey = "MaximumVersion";
        private const string MinimumVersionKey = "MinimumVersion";
        private const string RestrictionDefaultKey = "RestrictionDefault";
        private const string RestrictionNumberKey = "RestrictionNumber";
        private const string RestrictionNameKey = "RestrictionName";
        private const string ParameterNameKey = "ParameterName";

        private static readonly HashSet<int> s_assemblyPropertyUnsupportedEngines = new() { 6, 9, 11 };

        private readonly string _serverVersion;

        public SqlMetaDataFactory(string serverVersion)
        {
            ADP.CheckArgumentNull(serverVersion, nameof(serverVersion));

            _serverVersion = serverVersion;
        }

        public DataTable GetSchema(DbConnection connection, string collectionName, string[] restrictions) =>
            GetSchemaCore(connection, collectionName, restrictions, isAsync: false, default).Result;

        public async Task<DataTable> GetSchemaAsync(DbConnection connection, string collectionName, string[] restrictions, CancellationToken cancellationToken) =>
            await GetSchemaCore(connection, collectionName, restrictions, isAsync: true, cancellationToken).ConfigureAwait(false);

        public async ValueTask<DataTable> GetSchemaCore(DbConnection connection, string collectionName, string[] restrictions, bool isAsync, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MetaDataCollection? metadataRoot = s_metaDataCollection[0] as MetaDataCollection;
            // We expect first element of s_metaDataCollection to be an instance of MetaDataCollection
            Debug.Assert(metadataRoot != null);
            DataTable schema = await metadataRoot!.GetMetadata(collectionName, new MetaDataContext(_serverVersion, restrictions, connection, isAsync, cancellationToken));

            return schema;
        }

        internal interface ISupported
        {
            string? MinimumVersion { get; }
            string? MaximumVersion { get; }
        }

        internal sealed class MetaDataContext
        {
            public string ServerVersion { get; init; }
            public string[] RestrictionValues { get; init; }
            public DbConnection Connection { get; init; }
            public bool IsAsync { get; init; }
            public CancellationToken CancellationToken { get; init; }

            internal MetaDataContext(string serverVersion, string[] restrictions, DbConnection connection, bool isAsync, CancellationToken cancellationToken)
            {
                ServerVersion = serverVersion;
                RestrictionValues = restrictions;
                Connection = connection;
                IsAsync = isAsync;
                CancellationToken = cancellationToken;
            }
        }

        internal sealed class Restriction
        {
            public string RestrictionName { get; init; }
            public string ParameterName { get; init; }
            public int RestrictionNumber { get; init; }

            internal Restriction(int restrictionNumber, string restrictionName, string parameterName)
            {
                RestrictionName = restrictionName;
                ParameterName = parameterName;
                RestrictionNumber = restrictionNumber;
            }
        }

        internal abstract class MetaDataCollectionBase
        {
            public string CollectionName { get; init; }
            public int NumberOfRestrictions { get; init; }
            public int NumberOfIdentifierParts { get; init; }

            internal MetaDataCollectionBase(string collectionName, int numberOfRestrictions, int numberOfIdentifierParts)
            {
                CollectionName = collectionName;
                NumberOfRestrictions = numberOfRestrictions;
                NumberOfIdentifierParts = numberOfIdentifierParts;
            }

            public abstract ValueTask<DataTable> GetMetadata(MetaDataContext context, DataTable? accumulator = null);

            public virtual bool SupportedByCurrentVersion(MetaDataContext context) => true;

            protected MetaDataCollectionBase? FindMetaDataCollection(string collectionName, MetaDataContext context)
            {
                bool versionFailure = false;
                bool haveExactMatch = false;
                bool haveMultipleInexactMatches = false;
                string? exactCollectionName = null;
                MetaDataCollectionBase? requestedCollection = null;

                foreach (MetaDataCollectionBase metaData in s_metaDataCollection)
                {
                    if (string.Equals(metaData.CollectionName, collectionName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!SupportedByCurrentVersion(context))
                        {
                            versionFailure = true;
                        }
                        else if (collectionName == metaData.CollectionName)
                        {
                            if (haveExactMatch)
                            {
                                throw ADP.CollectionNameIsNotUnique(collectionName);
                            }
                            requestedCollection = metaData;
                            exactCollectionName = metaData.CollectionName;
                            haveExactMatch = true;
                        }
                        else if (!haveExactMatch)
                        {
                            // have an inexact match - ok only if it is the only one
                            if (exactCollectionName is not null)
                            {
                                // can't fail here because we may still find an exact match
                                haveMultipleInexactMatches = true;
                            }
                            requestedCollection = metaData;
                            exactCollectionName = metaData.CollectionName;
                        }
                    }
                }

                if (requestedCollection is null)
                {
                    if (!versionFailure)
                    {
                        throw ADP.UndefinedCollection(collectionName);
                    }
                    else
                    {
                        //throw ADP.UnsupportedVersion(collectionName);
                    }
                }

                if (!haveExactMatch && haveMultipleInexactMatches)
                {
                    throw ADP.AmbiguousCollectionName(collectionName);
                }

                return requestedCollection;
            }
        }
    }

    internal static class SqlMetaDataFactoryExtensions
    {
        internal static bool SupportedByCurrentVersion(this SqlMetaDataFactory.ISupported item, SqlMetaDataFactory.MetaDataContext context)
        {
            bool isAzure = ADP.IsAzureSqlServerEndpoint(context.Connection.DataSource);
            // Azure SQL always returns v12.00.XXXX (TDS returns 12.00.9114, SERVERPROPERTY('ProductVersion') returns 12.0.2000.8, SERVERPROPERTY('ResourceVersion') returns 17.00.9114),
            // but in fact it has latest stable version. For Azure SQL only item where MaximumVersion=null should be valid.
            if (isAzure)
            {
                return item.MaximumVersion == null;
            }

            return (item.MinimumVersion == null || string.Compare(context.ServerVersion, item.MinimumVersion, StringComparison.OrdinalIgnoreCase) >= 0) &&
                   (item.MaximumVersion == null || string.Compare(context.ServerVersion, item.MaximumVersion, StringComparison.OrdinalIgnoreCase) <= 0);
        }
    }
}
