// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlSchemaInfoTest
    {
        #region TestMethods
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(true)]
        [InlineData(false)]
        public static void TestGetSchema(bool openTransaction)
        {
            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                SqlTransaction transaction = null;

                conn.Open();
                try
                {
                    if (openTransaction)
                    {
                        transaction = conn.BeginTransaction();
                    }

                    DataTable dataBases = conn.GetSchema("DATABASES");
                    Assert.True(dataBases.Rows.Count > 0, "At least one database is expected");

                    string firstDatabaseName = dataBases.Rows[0]["database_name"] as string;
                    dataBases = conn.GetSchema("DATABASES", [firstDatabaseName]);

                    Assert.Equal(1, dataBases.Rows.Count);
                    Assert.Equal(firstDatabaseName, dataBases.Rows[0]["database_name"] as string);

                    string nonexistentDatabaseName = DataTestUtility.GenerateRandomCharacters("NonExistentDatabase_");
                    dataBases = conn.GetSchema("DATABASES", [nonexistentDatabaseName]);

                    Assert.Equal(0, dataBases.Rows.Count);
                }
                finally
                {
                    transaction?.Dispose();
                }

                DataTable metaDataCollections = conn.GetSchema(DbMetaDataCollectionNames.MetaDataCollections);
                Assert.True(metaDataCollections != null && metaDataCollections.Rows.Count > 0);

                DataTable metaDataSourceInfo = conn.GetSchema(DbMetaDataCollectionNames.DataSourceInformation);
                Assert.True(metaDataSourceInfo != null && metaDataSourceInfo.Rows.Count > 0);

                //TODO: lots of contention on data types. need to fix locking in other tests to make this more reliable
                DataTable metaDataTypes = conn.GetSchema(DbMetaDataCollectionNames.DataTypes);
                Assert.True(metaDataTypes != null && metaDataTypes.Rows.Count > 0);

                var tinyintRow = metaDataTypes.Rows.OfType<DataRow>().Where(p => (string)p["TypeName"] == "tinyint");
                foreach (var row in tinyintRow)
                {
                    Assert.True((String)row["TypeName"] == "tinyint" && (String)row["DataType"] == "System.Byte" && (bool)row["IsUnsigned"]);
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(true)]
        [InlineData(false)]
        public static async Task TestGetSchemaAsync(bool openTransaction)
        {
            using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                SqlTransaction transaction = null;

                await conn.OpenAsync();
                try
                {
                    if (openTransaction)
                    {
                        transaction = conn.BeginTransaction();
                    }

                    DataTable dataBases = await conn.GetSchemaAsync("DATABASES");
                    Assert.True(dataBases.Rows.Count > 0, "At least one database is expected");

                    string firstDatabaseName = dataBases.Rows[0]["database_name"] as string;
                    dataBases = await conn.GetSchemaAsync("DATABASES", [firstDatabaseName]);

                    Assert.Equal(1, dataBases.Rows.Count);
                    Assert.Equal(firstDatabaseName, dataBases.Rows[0]["database_name"] as string);

                    string nonexistentDatabaseName = DataTestUtility.GenerateRandomCharacters("NonExistentDatabase_");
                    dataBases = await conn.GetSchemaAsync("DATABASES", [nonexistentDatabaseName]);

                    Assert.Equal(0, dataBases.Rows.Count);
                }
                finally
                {
                    transaction?.Dispose();
                }

                DataTable metaDataCollections = await conn.GetSchemaAsync(DbMetaDataCollectionNames.MetaDataCollections);
                Assert.True(metaDataCollections != null && metaDataCollections.Rows.Count > 0);

                DataTable metaDataSourceInfo = await conn.GetSchemaAsync(DbMetaDataCollectionNames.DataSourceInformation);
                Assert.True(metaDataSourceInfo != null && metaDataSourceInfo.Rows.Count > 0);

                DataTable metaDataTypes = await conn.GetSchemaAsync(DbMetaDataCollectionNames.DataTypes);
                Assert.True(metaDataTypes != null && metaDataTypes.Rows.Count > 0);

                var tinyintRow = metaDataTypes.Rows.OfType<DataRow>().Where(p => (string)p["TypeName"] == "tinyint");
                foreach (var row in tinyintRow)
                {
                    Assert.True((String)row["TypeName"] == "tinyint" && (String)row["DataType"] == "System.Byte" && (bool)row["IsUnsigned"]);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void TestCommandBuilder()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommandBuilder commandBuilder = new SqlCommandBuilder())
            using (SqlCommand command = connection.CreateCommand())
            {
                string identifier = "TestIdentifier";
                string quotedIdentifier = commandBuilder.QuoteIdentifier(identifier);
                DataTestUtility.AssertEqualsWithDescription(
                    "[TestIdentifier]", quotedIdentifier,
                    "Unexpected QuotedIdentifier string.");

                string unquotedIdentifier = commandBuilder.UnquoteIdentifier(quotedIdentifier);
                DataTestUtility.AssertEqualsWithDescription(
                    "TestIdentifier", unquotedIdentifier,
                    "Unexpected UnquotedIdentifier string.");

                identifier = "identifier]withclosesquarebracket";
                quotedIdentifier = commandBuilder.QuoteIdentifier(identifier);
                DataTestUtility.AssertEqualsWithDescription(
                    "[identifier]]withclosesquarebracket]", quotedIdentifier,
                    "Unexpected QuotedIdentifier string.");

                unquotedIdentifier = null;
                unquotedIdentifier = commandBuilder.UnquoteIdentifier(quotedIdentifier);
                DataTestUtility.AssertEqualsWithDescription(
                    "identifier]withclosesquarebracket", unquotedIdentifier,
                    "Unexpected UnquotedIdentifier string.");
            }
        }

        // This test validates behavior of SqlInitialCatalogConverter used to present database names in PropertyGrid
        // with the SqlConnectionStringBuilder object presented in the control underneath.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void TestInitialCatalogStandardValues()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                string currentDb = connection.Database;
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connection.ConnectionString);
                PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(builder);
                PropertyDescriptor descriptor = properties["InitialCatalog"];
                DescriptorContext descriptorContext = new DescriptorContext(descriptor, builder);

                DataTestUtility.AssertEqualsWithDescription(
                    "SqlInitialCatalogConverter", descriptor.Converter.GetType().Name,
                    "Unexpected TypeConverter type.");

                Assert.True(descriptor.Converter.GetStandardValuesSupported(descriptorContext));
                Assert.False(descriptor.Converter.GetStandardValuesExclusive());

                // GetStandardValues of this converter calls GetSchema("DATABASES")
                var dbNames = descriptor.Converter.GetStandardValues(descriptorContext);
                HashSet<string> searchSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string name in dbNames)
                {
                    searchSet.Add(name);
                }

                // ensure master and current database exist there
                Assert.True(searchSet.Contains("master"), "Cannot find database: master.");
                Assert.True(searchSet.Contains(currentDb), $"Cannot find database: {currentDb}.");
            }
        }
        #endregion

        #region UtilityMethodsClasses
        // primitive implementation of ITypeDescriptorContext to be used with component model APIs
        private class DescriptorContext : ITypeDescriptorContext
        {
            SqlConnectionStringBuilder _instance;
            PropertyDescriptor _descriptor;

            public DescriptorContext(PropertyDescriptor descriptor, SqlConnectionStringBuilder instance)
            {
                _instance = instance;
                _descriptor = descriptor;
            }

            public object Instance
            {
                get { return _instance; }
            }

            public IContainer Container
            {
                get { return null; }
            }

            public PropertyDescriptor PropertyDescriptor
            {
                get { return _descriptor; }
            }

            public void OnComponentChanged()
            {
                throw new NotImplementedException();
            }

            public bool OnComponentChanging()
            {
                throw new NotImplementedException();
            }

            public object GetService(Type serviceType)
            {
                throw new NotImplementedException();
            }
        }

        private static void DumpDataTable(DataTable dataTable, int rowPrintCount)
        {
            Console.WriteLine("DumpDataTable");
            Console.WriteLine("");

            if (dataTable == null)
            {
                Console.WriteLine("DataTable object is null.");
                return;
            }
            int columnCount = dataTable.Columns.Count;
            int currentColumn;

            int rowCount = dataTable.Rows.Count;
            int currentRow;

            Console.WriteLine("Table \"{0}\" has {1} columns", dataTable.TableName.ToString(), columnCount.ToString());
            Console.WriteLine("Table \"{0}\" has {1} rows. At most the first {2} are dumped.", dataTable.TableName.ToString(), rowCount.ToString(), rowPrintCount.ToString());

            if ((rowPrintCount != 0) && (rowPrintCount < rowCount))
            {
                rowCount = rowPrintCount;
            }

            for (currentColumn = 0; currentColumn < columnCount; currentColumn++)
            {
                DumpDataColumn(dataTable.Columns[currentColumn]);
            }

            for (currentRow = 0; currentRow < rowCount; currentRow++)
            {
                DumpDataRow(dataTable.Rows[currentRow], dataTable);
            }

            return;

        }

        private static void DumpDataRow(DataRow dataRow, DataTable dataTable)
        {
            Console.WriteLine(" ");
            Console.WriteLine("<DumpDataRow>");

            foreach (DataColumn dataColumn in dataTable.Columns)
            {
                Console.WriteLine("{0}.{1} = {2}", dataTable.TableName, dataColumn.ColumnName, dataRow[dataColumn, DataRowVersion.Current].ToString());
            }
            return;
        }

        private static void DumpDataColumn(DataColumn dataColumn)
        {

            Console.WriteLine("Column Name = {0}, Column Type =  {1}", dataColumn.ColumnName, dataColumn.DataType.ToString());
            return;
        }
        #endregion
    }
}
