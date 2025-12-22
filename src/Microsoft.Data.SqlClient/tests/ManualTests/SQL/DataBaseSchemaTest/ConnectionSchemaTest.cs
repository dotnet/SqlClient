// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
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
        public static void GetTablesFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Tables, new string[] { "TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "TABLE_TYPE" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetProceduresFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Procedures, new string[] { "ROUTINE_SCHEMA", "ROUTINE_NAME", "ROUTINE_TYPE" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetProcedureParametersFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.ProcedureParameters, new string[] { "PARAMETER_MODE", "PARAMETER_NAME" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetDatabasesFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Databases, new string[] { "database_name", "dbid", "create_date" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetForeignKeysFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.ForeignKeys, new string[] { "CONSTRAINT_TYPE", "IS_DEFERRABLE", "INITIALLY_DEFERRED" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetIndexesFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Indexes, new string[] { "index_name", "constraint_name" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetIndexColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.IndexColumns, new string[] { "index_name", "KeyType", "column_name" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Columns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetAllColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.AllColumns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT", "IS_FILESTREAM", "IS_SPARSE", "IS_COLUMN_SET" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetColumnSetColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.ColumnSetColumns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT", "IS_FILESTREAM", "IS_SPARSE", "IS_COLUMN_SET" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetUsersFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Users, new string[] { "uid", "user_name" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetViewsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Views, new string[] { "TABLE_NAME", "CHECK_OPTION", "IS_UPDATABLE" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetViewColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.ViewColumns, new string[] { "VIEW_CATALOG", "VIEW_SCHEMA", "VIEW_NAME" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetUserDefinedTypesFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.UserDefinedTypes, new string[] { "assembly_name", "version_revision", "culture_info" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetStructuredTypeMembersFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.StructuredTypeMembers, new string[] { "TYPE_CATALOG", "TYPE_SCHEMA", "TYPE_NAME", "MEMBER_NAME", "ORDINAL_POSITION" });
        }

        [ConditionalFact(nameof(CanRunSchemaTests))]
        public static void GetDataTypesFromSchema()
        {
            DataTable schemaTable = VerifySchemaTable(DbMetaDataCollectionNames.DataTypes, [
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

            VerifyDataTypesTable(schemaTable);
        }

        private static DataTable GetSchemaTable(string schemaItemName)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                InitialCatalog = "master"
            };

            using SqlConnection connection = new(builder.ConnectionString);
            // Connect to the database then retrieve the schema information
            connection.Open();

            return connection.GetSchema(schemaItemName);
        }

        private static DataTable VerifySchemaTable(string schemaItemName, string[] testColumnNames)
        {
            HashSet<string> columnNames = [];
            DataTable schemaTable = GetSchemaTable(schemaItemName);

            // Get all table columns
            foreach (DataColumn column in schemaTable.Columns)
            {
                columnNames.Add(column.ColumnName);
            }

            Assert.All(testColumnNames, column => Assert.Contains(column, columnNames));
            return schemaTable;
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
