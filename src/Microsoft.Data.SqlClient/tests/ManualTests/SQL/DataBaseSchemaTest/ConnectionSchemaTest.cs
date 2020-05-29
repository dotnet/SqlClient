// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class ConnectionSchemaTest
    {

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetTablesFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Tables, new string[] { "TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "TABLE_TYPE" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetProceduresFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Procedures, new string[] { "ROUTINE_SCHEMA", "ROUTINE_NAME", "ROUTINE_TYPE" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetProcedureParametersFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.ProcedureParameters, new string[] { "PARAMETER_MODE", "PARAMETER_NAME" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetDatabasesFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Databases, new string[] { "database_name", "dbid", "create_date" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetForeignKeysFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.ForeignKeys, new string[] { "CONSTRAINT_TYPE", "IS_DEFERRABLE", "INITIALLY_DEFERRED" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetIndexesFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Indexes, new string[] { "index_name", "constraint_name" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetIndexColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.IndexColumns, new string[] { "index_name", "KeyType", "column_name" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Columns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetAllColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.AllColumns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT", "IS_FILESTREAM", "IS_SPARSE", "IS_COLUMN_SET" });
        }
        
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetColumnSetColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.ColumnSetColumns, new string[] { "IS_NULLABLE", "COLUMN_DEFAULT", "IS_FILESTREAM", "IS_SPARSE", "IS_COLUMN_SET" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetUsersFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Users, new string[] { "uid", "user_name" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetViewsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.Views, new string[] { "TABLE_NAME", "CHECK_OPTION", "IS_UPDATABLE" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetViewColumnsFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.ViewColumns, new string[] { "VIEW_CATALOG", "VIEW_SCHEMA", "VIEW_NAME" });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetUserDefinedTypesFromSchema()
        {
            VerifySchemaTable(SqlClientMetaDataCollectionNames.UserDefinedTypes, new string[] { "assembly_name", "version_revision", "culture_info" });
        }        

        private static void VerifySchemaTable(string schemaItemName, string[] testColumnNames)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                InitialCatalog = "master"
            };

            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                // Connect to the database then retrieve the schema information.  
                connection.Open();
                DataTable table = connection.GetSchema(schemaItemName);

                // Get all table columns 
                HashSet<string> columnNames = new HashSet<string>();

                foreach (DataColumn column in table.Columns)
                {
                    columnNames.Add(column.ColumnName);
                }

                Assert.All<string>(testColumnNames, column => Assert.Contains<string>(column, columnNames));
            }
        }
    }
}
