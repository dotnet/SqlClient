// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class ConnectionSchemaTest
    {
        public static bool CanRunSchemaTests()
        {
            return DataTestUtility.AreConnStringsSetup() &&
                // Tests switch to master database, which is not guaranteed when using Entra ID auth
                DataTestUtility.TcpConnectionStringDoesNotUseAadAuth;
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetTablesFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.Tables, new string[] { "TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "TABLE_TYPE" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetProceduresFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.Procedures, new string[] { "ROUTINE_SCHEMA", "ROUTINE_NAME", "ROUTINE_TYPE" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetProcedureParametersFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.ProcedureParameters, new string[] { "PARAMETER_MODE", "PARAMETER_NAME" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetDatabasesFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.Databases, new string[] { "database_name", "dbid", "create_date" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetForeignKeysFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.ForeignKeys, new string[] { "CONSTRAINT_TYPE", "IS_DEFERRABLE", "INITIALLY_DEFERRED" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetIndexesFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.Indexes, new string[] { "index_name", "constraint_name" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetIndexColumnsFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.IndexColumns, new string[] { "index_name", "KeyType", "column_name" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetColumnsFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.Columns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetAllColumnsFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.AllColumns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT", "IS_FILESTREAM", "IS_SPARSE", "IS_COLUMN_SET" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetColumnSetColumnsFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.ColumnSetColumns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT", "IS_FILESTREAM", "IS_SPARSE", "IS_COLUMN_SET" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetUsersFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.Users, new string[] { "uid", "user_name" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetViewsFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.Views, new string[] { "TABLE_NAME", "CHECK_OPTION", "IS_UPDATABLE" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetViewColumnsFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.ViewColumns, new string[] { "VIEW_CATALOG", "VIEW_SCHEMA", "VIEW_NAME" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetUserDefinedTypesFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.UserDefinedTypes, new string[] { "assembly_name", "version_revision", "culture_info" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetStructuredTypeMembersFromSchema()
        {
            await VerifySchemaTable(SqlClientMetaDataCollectionNames.StructuredTypeMembers, new string[] { "TYPE_CATALOG", "TYPE_SCHEMA", "TYPE_NAME", "MEMBER_NAME", "ORDINAL_POSITION" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static async Task GetDataTypesFromSchema()
        {
            (DataTable syncSchemaTable, DataTable asyncSchemaTable) = await VerifySchemaTable(DbMetaDataCollectionNames.DataTypes, [
                DbMetaDataColumnNames.TypeName,
                DbMetaDataColumnNames.ProviderDbType,
                DbMetaDataColumnNames.ColumnSize,
                DbMetaDataColumnNames.CreateFormat,
                DbMetaDataColumnNames.CreateParameters,
                DbMetaDataColumnNames.DataType,
                DbMetaDataColumnNames.IsAutoIncrementable,
                DbMetaDataColumnNames.IsBestMatch,
                DbMetaDataColumnNames.IsCaseSensitive,
                DbMetaDataColumnNames.IsFixedLength,
                DbMetaDataColumnNames.IsFixedPrecisionScale,
                DbMetaDataColumnNames.IsLong,
                DbMetaDataColumnNames.IsNullable,
                DbMetaDataColumnNames.IsSearchable,
                DbMetaDataColumnNames.IsSearchableWithLike,
                DbMetaDataColumnNames.IsUnsigned,
                DbMetaDataColumnNames.MaximumScale,
                DbMetaDataColumnNames.MinimumScale,
                DbMetaDataColumnNames.IsConcurrencyType,
                DbMetaDataColumnNames.IsLiteralSupported,
                DbMetaDataColumnNames.LiteralPrefix,
                DbMetaDataColumnNames.LiteralSuffix
            ]);

            VerifyDataTypesTable(syncSchemaTable);
            VerifyDataTypesTable(asyncSchemaTable);
        }

        private static async Task<(DataTable SyncSchema, DataTable AsyncSchema)> VerifySchemaTable(string schemaItemName, string[] testColumnNames)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                InitialCatalog = "master"
            };
            using SqlConnection connection = new(builder.ConnectionString);
            HashSet<string> syncColumnNames = [];
            HashSet<string> asyncColumnNames = [];

            // Connect to the database then retrieve the schema information
            connection.Open();

            DataTable syncTable = connection.GetSchema(schemaItemName);
            DataTable asyncTable = await connection.GetSchemaAsync(schemaItemName);

            // Get all table columns
            foreach (DataColumn column in syncTable.Columns)
            {
                syncColumnNames.Add(column.ColumnName);
            }
            foreach (DataColumn column in asyncTable.Columns)
            {
                asyncColumnNames.Add(column.ColumnName);
            }

            Assert.All(testColumnNames, column => Assert.Contains(column, syncColumnNames));
            Assert.All(testColumnNames, column => Assert.Contains(column, asyncColumnNames));
            return (syncTable, asyncTable);
        }

        private static void VerifyDataTypesTable(DataTable dataTypesTable)
        {
            string[] expectedTypes = [
                "smallint", "int", "real", "float", "money", "smallmoney", "bit", "tinyint", "bigint", "timestamp",
                "binary", "image", "text", "ntext", "decimal", "numeric", "datetime", "smalldatetime", "sql_variant", "xml",
                "varchar", "char", "nchar", "nvarchar", "varbinary", "uniqueidentifier", "date", "time", "datetime2", "datetimeoffset"
            ];
            HashSet<string> actualTypes = [];

            // Get every type name, asserting that it is a unique string.
            foreach (DataRow row in dataTypesTable.Rows)
            {
                string typeName = row[DbMetaDataColumnNames.TypeName] as string;

                Assert.False(string.IsNullOrEmpty(typeName));
                Assert.True(actualTypes.Add(typeName));
            }

            // Every expected type should be present. There will often be additional types present - user-defined table types
            // and CLR types (such as geography and geometry.)
            Assert.All(expectedTypes, type => Assert.Contains(type, actualTypes));

            // The "json" type should only be present when running against a SQL Server version which supports it.
            // SQL Azure reports a version of 12.x but supports JSON, so SqlClient doesn't include it in the list of types.
            Assert.Equal(DataTestUtility.IsJsonSupported && DataTestUtility.IsNotAzureServer(), actualTypes.Contains("json"));
        }
    }
}
