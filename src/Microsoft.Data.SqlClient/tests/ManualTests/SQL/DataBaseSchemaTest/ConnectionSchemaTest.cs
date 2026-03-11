// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class ConnectionSchemaTest
    {
        public static bool CanRunSchemaTests()
        {
            return DataTestUtility.AreConnStringsSetup() &&
                // Tests switch to master database, which is not guaranteed when using AAD auth
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

        private static async Task VerifySchemaTable(string schemaItemName, string[] testColumnNames)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                InitialCatalog = "master"
            };

            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                // Connect to the database then retrieve the schema information
                connection.Open();
                DataTable syncTable = connection.GetSchema(schemaItemName);
                DataTable asyncTable = await connection.GetSchemaAsync(schemaItemName);

                // Get all table columns
                HashSet<string> syncColumnNames = new HashSet<string>();
                HashSet<string> asyncColumnNames = new HashSet<string>();

                foreach (DataColumn column in syncTable.Columns)
                {
                    syncColumnNames.Add(column.ColumnName);
                }
                foreach (DataColumn column in asyncTable.Columns)
                {
                    asyncColumnNames.Add(column.ColumnName);
                }

                Assert.All<string>(testColumnNames, column => Assert.Contains<string>(column, syncColumnNames));
                Assert.All<string>(testColumnNames, column => Assert.Contains<string>(column, asyncColumnNames));
            }
        }
    }
}
