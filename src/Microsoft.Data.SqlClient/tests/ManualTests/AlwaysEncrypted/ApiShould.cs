// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// Always Encrypted public API Manual tests.
    /// TODO: These tests are marked as Windows only for now but should be run for all platforms once the Master Key is accessible to this app from Azure Key Vault.
    /// </summary>
    [PlatformSpecific(TestPlatforms.Windows)]
    public class ApiShould : IClassFixture<SQLSetupStrategy>, IDisposable
    {
        private SQLSetupStrategy fixture;

        private readonly string tableName;

        public ApiShould(SQLSetupStrategy fixture)
        {
            this.fixture = fixture;
            tableName = fixture.ApiTestTable.Name;
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(true)]
        [InlineData(false)]
        public void TestSqlTransactionCommitRollbackWithTransparentInsert(bool isCommitted)
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();

                Customer customer = new Customer(40, "Microsoft", "Corporation");

                // Start a transaction and either commit or rollback based on the test variation.
                using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
                {
                    InsertCustomerRecord(sqlConnection, sqlTransaction, customer);

                    if (isCommitted)
                    {
                        sqlTransaction.Commit();
                    }
                    else
                    {
                        sqlTransaction.Rollback();
                    }
                }

                // Data should be available on select if committed else, data should not be available.
                if (isCommitted)
                {
                    VerifyRecordPresent(sqlConnection, customer);
                }
                else
                {
                    VerifyRecordAbsent(sqlConnection, customer);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestSqlTransactionRollbackToSavePoint()
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();

                // Start a new transaction, with isolation level as read uncommitted, so we will be able to read the inserted records without committing.
                using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction(System.Data.IsolationLevel.ReadUncommitted))
                {
                    // Insert row no:1 and Save the state of the transaction to a named check point.
                    Customer customer1 = new Customer(50, "Microsoft2", "Corporation2");
                    InsertCustomerRecord(sqlConnection, sqlTransaction, customer1);
                    sqlTransaction.Save(@"checkpoint");

                    // Insert row no:2
                    Customer customer2 = new Customer(60, "Microsoft3", "Corporation3");
                    InsertCustomerRecord(sqlConnection, sqlTransaction, customer2);

                    // Read the data that was just inserted, both Row no:2 and Row no:1 should be available.
                    VerifyRecordPresent(sqlConnection, customer1, sqlTransaction);

                    // Try to read the just inserted record under read-uncommitted mode.
                    VerifyRecordPresent(sqlConnection, customer2, sqlTransaction);

                    // Rollback the transaction to the saved checkpoint, to lose the row no:2.
                    sqlTransaction.Rollback(@"checkpoint");

                    // Row no:2 should not be available.
                    VerifyRecordAbsent(sqlConnection, customer2, sqlTransaction);

                    // Row no:1 should still be available.
                    VerifyRecordPresent(sqlConnection, customer1, sqlTransaction);

                    // Completely rollback the transaction.
                    sqlTransaction.Rollback();

                    // Now even row no:1 should not be available.
                    VerifyRecordAbsent(sqlConnection, customer1, sqlTransaction);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void SqlParameterProperties()
        {
            string tableName = fixture.SqlParameterPropertiesTable.Name;
            const string firstColumnName = @"firstColumn";
            const string secondColumnName = @"secondColumn";
            const string thirdColumnName = @"thirdColumn";
            string inputProcedureName = DataTestUtility.GetUniqueName("InputProc").ToString();
            string outputProcedureName = DataTestUtility.GetUniqueName("OutputProc").ToString();
            const int charColumnSize = 100;
            const int decimalColumnPrecision = 10;
            const int decimalColumnScale = 4;
            const int timeColumnScale = 5;

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                try {
                    sqlConnection.Open();

                    // Create a procedure that gets input parameters that have smaller data types than the actual columns types.
                    // Decimal precision and scale need to match exactly.
                    int charInputParamSize = charColumnSize - 20;
                    int decimalInputParamPrecision = decimalColumnPrecision;
                    int decimalInputParamScale = decimalColumnScale;
                    int timeInputParamScale = timeColumnScale - 1;

                    using (SqlCommand sqlCmd = new SqlCommand(string.Format(
                        @"CREATE PROCEDURE {0} (@p1 nvarchar({5}), @p2 decimal ({6}, {7}), @p3 time ({8})) AS
                            SELECT * FROM [{1}] WHERE {2} = @p1 AND {3} = @p2 AND {4} = @p3",
                        inputProcedureName, tableName, firstColumnName, secondColumnName, thirdColumnName, charInputParamSize, decimalInputParamPrecision, decimalInputParamScale, timeInputParamScale), sqlConnection))
                    {
                        sqlCmd.ExecuteNonQuery();
                    }

                    // Create a procedure that returns output parameters that have larger data type than the actual column types.
                    // Decimal precision and scale need to match exactly.
                    int charOutputParamSize = charColumnSize + 20;
                    int decimalOutputParamPrecision = decimalColumnPrecision;
                    int decimalOutputParamScale = decimalColumnScale;
                    int timeOutputParamScale = timeColumnScale + 1;

                    using (SqlCommand sqlCmd = new SqlCommand(string.Format(
                        @"CREATE PROCEDURE {0} (@p1 nvarchar({5}) OUTPUT, @p2 decimal ({6}, {7}) OUTPUT, @p3 time ({8}) OUTPUT) AS
                            SELECT @p1={2}, @p2={3}, @p3={4} FROM [{1}]",
                        outputProcedureName, tableName, firstColumnName, secondColumnName, thirdColumnName, charOutputParamSize, decimalOutputParamPrecision, decimalOutputParamScale, timeOutputParamScale), sqlConnection))
                    {
                        sqlCmd.ExecuteNonQuery();
                    }

                    // Insert a row.
                    using (SqlCommand sqlCmd = new SqlCommand(
                        cmdText: $"INSERT INTO [{tableName}] VALUES (@p1, @p2, @p3)",
                        connection: sqlConnection, 
                        transaction: null, 
                        columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        SqlParameter param1 = new SqlParameter("@p1", SqlDbType.NVarChar)
                        {
                            Size = charColumnSize,
                            Value = "ColumnValue"
                        };
                        sqlCmd.Parameters.Add(param1);

                        SqlParameter param2 = new SqlParameter("@p2", SqlDbType.Decimal)
                        {
                            Precision = decimalColumnPrecision,
                            Scale = decimalColumnScale,
                            Value = 400.21
                        };
                        sqlCmd.Parameters.Add(param2);

                        SqlParameter param3 = new SqlParameter("@p3", SqlDbType.Time)
                        {
                            Scale = timeColumnScale,
                            Value = TimeSpan.Parse("1:01:01.001")
                        };
                        sqlCmd.Parameters.Add(param3);

                        sqlCmd.ExecuteNonQuery();
                    }

                    // Now execute the procedure with input params and make sure the parameter properties stays as set.
                    using (SqlCommand sqlCmd = new SqlCommand(inputProcedureName, sqlConnection, null, SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        sqlCmd.CommandType = CommandType.StoredProcedure;

                        // Set the actual parameter size to even smaller than the proc size. This is allowed since we assign from these values into the params.
                        // Decimal precision and scale need to match exactly.
                        int charParamSize = charInputParamSize - 20;
                        int decimalParamPrecision = decimalInputParamPrecision;
                        int decimalParamScale = decimalInputParamScale;
                        int timeParamScale = timeInputParamScale - 1;

                        SqlParameter param1 = new SqlParameter("@p1", SqlDbType.NVarChar)
                        {
                            Size = charParamSize,
                            Value = "ColumnValue"
                        };
                        sqlCmd.Parameters.Add(param1);

                        SqlParameter param2 = new SqlParameter("@p2", SqlDbType.Decimal)
                        {
                            Precision = (byte)decimalParamPrecision,
                            Scale = (byte)decimalParamScale,
                            Value = 400.21
                        };
                        sqlCmd.Parameters.Add(param2);

                        SqlParameter param3 = new SqlParameter("@p3", SqlDbType.Time)
                        {
                            Scale = (byte)timeParamScale,
                            Value = TimeSpan.Parse("1:01:01.001")
                        };
                        sqlCmd.Parameters.Add(param3);

                        using (SqlDataReader reader = sqlCmd.ExecuteReader())
                        {
                            Assert.True(reader.Read(), "We should have found one row.");
                            Assert.False(reader.Read(), "We shouldn't have found a second row.");
                        }

                        // Validate that all properties have stayed the same for all parameters.
                        Assert.Equal(SqlDbType.NVarChar, param1.SqlDbType);
                        Assert.Equal(DbType.String, param1.DbType);
                        Assert.Equal(0, param1.Scale);
                        Assert.Equal(0, param1.Precision);
                        Assert.Equal(charParamSize, param1.Size);

                        Assert.Equal(SqlDbType.Decimal, param2.SqlDbType);
                        Assert.Equal(DbType.Decimal, param2.DbType);
                        Assert.Equal(decimalParamScale, param2.Scale);
                        Assert.Equal(decimalParamPrecision, param2.Precision);
                        Assert.Equal(0, param2.Size);

                        Assert.Equal(SqlDbType.Time, param3.SqlDbType);
                        Assert.Equal(DbType.Time, param3.DbType);
                        Assert.Equal(timeParamScale, param3.Scale);
                        Assert.Equal(0, param3.Precision);
                        Assert.Equal(0, param3.Size);
                    }

                    // Now execute the procedure with output params and make sure the parameter properties stays as set.
                    using (SqlCommand sqlCmd = new SqlCommand(outputProcedureName, sqlConnection, null, SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        sqlCmd.CommandType = CommandType.StoredProcedure;

                        // For output params the type needs to be identical with the actual procedure parameter since we will assign in both directions.
                        int charParamSize = charOutputParamSize;
                        int decimalParamPrecision = decimalOutputParamPrecision;
                        int decimalParamScale = decimalOutputParamScale;
                        int timeParamScale = timeOutputParamScale;

                        SqlParameter param1 = new SqlParameter("@p1", SqlDbType.NVarChar)
                        {
                            Direction = ParameterDirection.Output,
                            Size = charParamSize,
                            Value = "DifferentColumnValue"
                        };
                        sqlCmd.Parameters.Add(param1);

                        SqlParameter param2 = new SqlParameter("@p2", SqlDbType.Decimal)
                        {
                            Direction = ParameterDirection.Output,
                            Precision = (byte)decimalParamPrecision,
                            Scale = (byte)decimalParamScale,
                            Value = 4000.21
                        };
                        sqlCmd.Parameters.Add(param2);

                        SqlParameter param3 = new SqlParameter("@p3", SqlDbType.Time)
                        {
                            Direction = ParameterDirection.Output,
                            Scale = (byte)timeParamScale,
                            Value = TimeSpan.Parse("1:01:01.01")
                        };
                        sqlCmd.Parameters.Add(param3);

                        sqlCmd.ExecuteNonQuery();

                        // Validate that all properties have stayed the same for all parameters.
                        Assert.Equal(SqlDbType.NVarChar, param1.SqlDbType);
                        Assert.Equal(DbType.String, param1.DbType);
                        Assert.Equal(0, param1.Scale);
                        Assert.Equal(0, param1.Precision);
                        Assert.Equal(charParamSize, param1.Size);

                        Assert.Equal(SqlDbType.Decimal, param2.SqlDbType);
                        Assert.Equal(DbType.Decimal, param2.DbType);
                        Assert.Equal(decimalParamScale, param2.Scale);
                        Assert.Equal(decimalParamPrecision, param2.Precision);
                        Assert.Equal(0, param2.Size);

                        Assert.Equal(SqlDbType.Time, param3.SqlDbType);
                        Assert.Equal(DbType.Time, param3.DbType);
                        Assert.Equal(timeParamScale, param3.Scale);
                        Assert.Equal(0, param3.Precision);
                        Assert.Equal(0, param3.Size);
                    }
                } finally
                {
                    DropHelperProcedures(new string[] { inputProcedureName, outputProcedureName });
                }

            }
        }

        private void VerifyRecordAbsent(SqlConnection sqlConnection, Customer customer, SqlTransaction sqlTransaction = null)
        {
            using (SqlCommand sqlCommand = new SqlCommand(
                cmdText: $"SELECT * FROM [{tableName}] WHERE CustomerId = @CustomerId and FirstName = @FirstName;",
                connection: sqlConnection,
                transaction: sqlTransaction,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
            {
                sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
                sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);

                using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                {
                    Assert.False(sqlDataReader.HasRows);
                }
            }
        }

        private void VerifyRecordPresent(SqlConnection sqlConnection, Customer customer, SqlTransaction sqlTransaction = null)
        {
            using (SqlCommand sqlCommand = new SqlCommand(
                cmdText: $"SELECT * FROM [{tableName}] WHERE CustomerId = @CustomerId and FirstName = @FirstName;",
                connection: sqlConnection,
                transaction: sqlTransaction,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
            {
                sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
                sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);

                using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                {
                    Assert.True(sqlDataReader.HasRows);

                    while (sqlDataReader.Read())
                    {
                        Assert.True(string.Equals(sqlDataReader.GetDataTypeName(0), @"int", StringComparison.OrdinalIgnoreCase), "unexpected data type");
                        Assert.True(string.Equals(sqlDataReader.GetDataTypeName(1), @"nvarchar", StringComparison.InvariantCultureIgnoreCase), "unexpected data type");
                        Assert.True(string.Equals(sqlDataReader.GetDataTypeName(2), @"nvarchar", StringComparison.InvariantCultureIgnoreCase), "unexpected data type");
                        Assert.Equal(customer.Id, sqlDataReader.GetInt32(0));
                        Assert.Equal(customer.FirstName, sqlDataReader.GetString(1));
                        Assert.Equal(customer.LastName, sqlDataReader.GetString(2));
                    }
                }
            }
        }

        private void InsertCustomerRecord(SqlConnection sqlConnection, SqlTransaction sqlTransaction, Customer customer)
        {
            using (SqlCommand sqlCommand = new SqlCommand(
                $"INSERT INTO [{tableName}] (CustomerId, FirstName, LastName) VALUES (@CustomerId, @FirstName, @LastName);",
                connection: sqlConnection,
                transaction: sqlTransaction,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
            {
                sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
                sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);
                sqlCommand.Parameters.AddWithValue(@"LastName", customer.LastName);

                sqlCommand.ExecuteNonQuery();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestSqlDataAdapterFillDataTable()
        {
            const string DummyParamName = "@dummyParam";
            int numberOfRows = 100;

            IList<object> values = GetValues(dataHint: 71);

            InsertRows(tableName: tableName, numberofRows: numberOfRows, values: values);

            //using (SqlConnection sqlConnection = new SqlConnection(string.Concat(DataTestUtility.TcpConnStr, " Column Encryption Setting = Enabled;")))
            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(DataTestUtility.TcpConnStr, " Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                // Create a command with an encrypted parameter to confirm that parameters work ocrrectly for Fill.
                using (SqlCommand cmd = new SqlCommand(
                    cmdText: $"select * from [{tableName}] where FirstName != {DummyParamName} and CustomerId = @CustomerId",
                    connection: sqlConnection))
                {
                    SqlParameter dummyParam = new SqlParameter(DummyParamName, SqlDbType.NVarChar, 150)
                    {
                        Value = "a"
                    };
                    cmd.Parameters.Add(dummyParam);
                    cmd.Parameters.AddWithValue(@"CustomerId", values[0]);

                    // Fill the data table from the results of select statement.
                    using (SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dataTable = new DataTable();
                        sqlDataAdapter.Fill(dataTable);

                        TestDataAdapterFillResults(dataTable, values);

                        // Try refilling another table with the same adapter to make sure that reusing the command works correctly.
                        dataTable = new DataTable();
                        sqlDataAdapter.Fill(dataTable);
                        TestDataAdapterFillResults(dataTable, values);
                        Assert.Equal(numberOfRows, dataTable.Rows.Count);

                        // Use the Fill overload which fills in a dataset.
                        DataSet dataSet = new DataSet();
                        sqlDataAdapter.Fill(dataSet, tableName);
                        Assert.Single(dataSet.Tables);
                        Assert.Equal(numberOfRows, dataSet.Tables[0].Rows.Count);
                        TestDataAdapterFillResults(dataSet.Tables[0], values);

                        // Use the Fill overload which lets you specify the max number of records to be fetched.
                        dataSet = new DataSet();
                        sqlDataAdapter.Fill(dataSet, 0, 1, tableName);
                        Assert.Single(dataSet.Tables);
                        Assert.Single(dataSet.Tables[0].Rows);
                        TestDataAdapterFillResults(dataSet.Tables[0], values);
                    }
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(SchemaType.Source)]
        [InlineData(SchemaType.Mapped)]
        public void TestSqlDataAdapterFillSchema(SchemaType schemaType)
        {
            IList<object> values = GetValues(dataHint: 44);
            int numberOfRows = 42;

            // Insert a bunch of rows in to the table.
            int rowsAffected = InsertRows(tableName: tableName, numberofRows: numberOfRows, values: values);

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();

                SqlDataAdapter adapter = CreateSqlDataAdapter(sqlConnection);

                DataTable dataTable = new DataTable();
                DataTable dataTable2 = adapter.FillSchema(dataTable, schemaType);
                DataColumnCollection dataColumns = dataTable2.Columns;
                ValidateSchema(dataTable2.Columns);
                ValidateSchema(dataTable.Columns);

                // Test the FillSchema overload that takes in a dataset with SchemaType = Source.
                DataSet dataSet = new DataSet();
                DataTable[] dataSet2 = adapter.FillSchema(dataSet, schemaType);
                Assert.Single(dataSet2);
                ValidateSchema(dataSet2[0].Columns);
                ValidateSchema(dataSet.Tables[0].Columns);
            }
        }

        /// <summary>
        /// Validate the schema obtained SqlDataAdapter.FillSchema
        /// </summary>
        /// <param name="dataColumns"></param>
        private void ValidateSchema(DataColumnCollection dataColumns)
        {
            Assert.Equal(@"CustomerId", dataColumns[0].ColumnName);
            Assert.Equal(@"FirstName", dataColumns[1].ColumnName);
            Assert.Equal(@"LastName", dataColumns[2].ColumnName);

            Assert.Equal(typeof(int), dataColumns[0].DataType);
            Assert.Equal(typeof(string), dataColumns[1].DataType);
            Assert.Equal(typeof(string), dataColumns[2].DataType);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false)]
        [InlineData(true)]
        public void TestExecuteNonQuery(bool isAsync)
        {
            Parallel.For(0, 10, i =>
            {
                IList<object> values = GetValues(dataHint: 45 + i + 1);
                int numberOfRows = 10 + i;

                // Insert a bunch of rows in to the table.
                int rowsAffected = InsertRows(tableName: tableName, numberofRows: numberOfRows, values: values);

                Assert.Equal(numberOfRows, rowsAffected);

                rowsAffected = -1;
                using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
                {
                    sqlConnection.Open();

                    // Update the set of rows that were inserted just now. And verify the rows affected as returned by ExecuteNonQuery.
                    using (SqlCommand sqlCommand = new SqlCommand(
                        cmdText: $"UPDATE [{tableName}] SET FirstName = @FirstName WHERE CustomerId = @CustomerId",
                        connection: sqlConnection, 
                        transaction: null, 
                        columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        sqlCommand.Parameters.AddWithValue(@"FirstName", string.Format(@"Microsoft{0}", i + 100));
                        sqlCommand.Parameters.AddWithValue(@"CustomerId", values[0]);

                        if (isAsync)
                        {
                            Task<int> executeTask = VerifyExecuteNonQueryAsync(sqlCommand);
                            rowsAffected = executeTask.Result;
                        }
                        else
                        {
                            rowsAffected = sqlCommand.ExecuteNonQuery();
                        }

                        Assert.Equal(numberOfRows, rowsAffected);
                    }
                }
            });
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false)]
        [InlineData(true)]
        public void TestExecuteScalar(bool isAsync)
        {
            Parallel.For(0, 10, i =>
            {
                IList<object> values = GetValues(dataHint: 42);
                int numberOfRows = 10 + i;

                // Insert a bunch of rows in to the table.
                int rowsAffected = InsertRows(tableName: tableName, numberofRows: numberOfRows, values: values);

                using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
                {
                    sqlConnection.Open();

                    // Do a select * from the table and check on the first column of the first row for the expected value.
                    using (SqlCommand sqlCommand = new SqlCommand
                    (
                        cmdText: $"select CustomerId, FirstName, LastName from [{tableName}] where CustomerId = @CustomerId",
                        connection: sqlConnection,
                        transaction: null,
                        columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        sqlCommand.Parameters.AddWithValue(@"CustomerId", values[0]);
                        int customerId = -1;

                        if (isAsync)
                        {
                            Task<object> result = VerifyExecuteScalarAsync(sqlCommand);
                            customerId = (int)result.Result;
                        }
                        else
                        {
                            customerId = (int)sqlCommand.ExecuteScalar();
                        }

                        Assert.Equal((int)values[0], customerId);
                    }
                }
            });
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(1)]
        [InlineData(100)]
        public void TestSqlDataAdapterBatchUpdate(int numberofRows)
        {

            DataTable dataTable = CreateDataTable(tableName: tableName, numberofRows: numberofRows);

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();

                // Create a SqlDataAdapter.
                SqlDataAdapter adapter = CreateSqlDataAdapter(sqlConnection);

                // Execute the update.
                int rowsAffected = adapter.Update(dataTable);

                Assert.Equal(numberofRows, rowsAffected);

                if (numberofRows == 100)
                {
                    // Delete a row, add two new ones and update the table again to make sure reusing the commands is working properly.
                    int lastRowId = dataTable.Rows.Count;
                    lastRowId++;

                    dataTable.Rows.RemoveAt(1);

                    DataRow row = dataTable.NewRow();
                    row["CustomerId"] = 45 + lastRowId;
                    row["FirstName"] = string.Format(@"Microsoft{0}", lastRowId);
                    row["LastName"] = string.Format(@"Corporation{0}", lastRowId);
                    dataTable.Rows.Add(row);

                    lastRowId++;

                    row = dataTable.NewRow();
                    row["CustomerId"] = 45 + lastRowId;
                    row["FirstName"] = string.Format(@"Microsoft{0}", lastRowId);
                    row["LastName"] = string.Format(@"Corporation{0}", lastRowId);
                    dataTable.Rows.Add(row);

                    rowsAffected = adapter.Update(dataTable);
                }
            }
        }

        private SqlDataAdapter CreateSqlDataAdapter(SqlConnection sqlConnection)
        {
            // Create a SqlDataAdapter.
            SqlDataAdapter adapter = new SqlDataAdapter(string.Empty, sqlConnection)
            {

                // Set the SELECT command.
                SelectCommand = new SqlCommand
            (
                cmdText: $"SELECT CustomerId, FirstName, LastName  FROM [{tableName}]",
                connection: sqlConnection,
                transaction: null,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled
            ),

                // Set the UPDATE command and parameters.
                UpdateCommand = new SqlCommand
            (
                cmdText: $"UPDATE [{tableName}] SET FirstName=@FirstName WHERE CustomerId=@CustomerId",
                connection: sqlConnection,
                transaction: null,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled
            )
            };
            adapter.UpdateCommand.Parameters.Add("@FirstName", SqlDbType.NVarChar, 50, "FirstName");
            adapter.UpdateCommand.Parameters.Add("@CustomerId", SqlDbType.Int, 4, "CustomerId");
            adapter.UpdateCommand.UpdatedRowSource = UpdateRowSource.None;

            // Set the INSERT command and parameter.
            adapter.InsertCommand = new SqlCommand
            (
                cmdText: $"INSERT INTO [{tableName}] (FirstName, LastName) VALUES (@FirstName, @LastName);",
                connection: sqlConnection,
                transaction: null,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled
            );
            adapter.InsertCommand.Parameters.Add("@FirstName", SqlDbType.NVarChar, 50, "FirstName");
            adapter.InsertCommand.Parameters.Add("@LastName", SqlDbType.NVarChar, 50, "LastName");
            adapter.InsertCommand.UpdatedRowSource = UpdateRowSource.None;

            // Set the DELETE command and parameter.
            adapter.DeleteCommand = new SqlCommand(
                cmdText: $"DELETE FROM [{tableName}] WHERE CustomerId=@CustomerId",
                connection: sqlConnection,
                transaction: null,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled
            );
            adapter.DeleteCommand.Parameters.Add("@CustomerId", SqlDbType.Int, 4, "CustomerId");
            adapter.DeleteCommand.UpdatedRowSource = UpdateRowSource.None;

            // Set the batch size.
            adapter.UpdateBatchSize = 10;

            return adapter;
        }

        /// <summary>
        /// Create a data table.
        /// </summary>
        /// <returns></returns>
        private DataTable CreateDataTable(string tableName, int numberofRows)
        {
            // Create a new DataTable.
            DataTable table = new DataTable(tableName);

            // Declare variables for DataColumn and DataRow objects.
            DataColumn column;
            DataRow row;

            column = new DataColumn
            {
                DataType = System.Type.GetType("System.Int32"),
                ColumnName = "CustomerId",
                ReadOnly = false,
                Unique = false
            };
            table.Columns.Add(column);

            // Create second column.
            column = new DataColumn
            {
                DataType = System.Type.GetType("System.String"),
                ColumnName = "FirstName",
                ReadOnly = false,
                Unique = false
            };
            table.Columns.Add(column);

            // Create third column.
            column = new DataColumn
            {
                DataType = System.Type.GetType("System.String"),
                ColumnName = "LastName",
                ReadOnly = false,
                Unique = false
            };
            table.Columns.Add(column);

            // Create three new DataRow objects and add  
            // them to the DataTable 
            for (int i = 0; i < numberofRows; i++)
            {
                row = table.NewRow();
                row["CustomerId"] = 45 + i + 1;
                row["FirstName"] = string.Format(@"Microsoft{0}", i);
                row["LastName"] = string.Format(@"Corporation{0}", i);
                table.Rows.Add(row);
            }

            return table;
        }

        /// <summary>
        /// Insert rows in to the table.
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="numberofRows"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        private int InsertRows(string tableName, int numberofRows, IList<object> values)
        {
            int rowsAffected = 0;

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();

                // Temporarily inserting one row at a time, since server can't support multiple rows insert yet.
                for (int i = 0; i < numberofRows; i++)
                {
                    using (SqlCommand sqlCommand = new SqlCommand("", sqlConnection, transaction: null, columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
                    {
                        sqlCommand.CommandText = $"INSERT INTO [{tableName}] VALUES (@CustomerId, @FirstName, @LastName)";
                        sqlCommand.Parameters.AddWithValue(@"CustomerId", values[0]);
                        sqlCommand.Parameters.AddWithValue(@"FirstName", values[1]);
                        sqlCommand.Parameters.AddWithValue(@"LastName", values[2]);

                        rowsAffected += sqlCommand.ExecuteNonQuery();
                    }
                }
            }

            return rowsAffected;
        }


        /// <summary>
        /// Drops the specified procedures.
        /// </summary>
        /// <param name="procNames"></param>
        private void DropHelperProcedures(string[] procNames)
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                foreach (string name in procNames)
                {
                    using (SqlCommand cmd = new SqlCommand(string.Format("IF EXISTS (SELECT * FROM sys.procedures WHERE name = '{0}') \n DROP PROCEDURE {0}", name), sqlConnection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Asynchronously invoke ExecuteNonQuery using await.
        /// </summary>
        /// <param name="sqlCommand"></param>
        /// <returns></returns>
        private async Task<int> VerifyExecuteNonQueryAsync(SqlCommand sqlCommand)
        {
            int rowsAffected = await sqlCommand.ExecuteNonQueryAsync();
            return rowsAffected;
        }

        /// <summary>
        /// Asynchronously invoke ExecuteScalar using await.
        /// </summary>
        /// <param name="sqlCommand"></param>
        /// <returns></returns>
        private async Task<object> VerifyExecuteScalarAsync(SqlCommand sqlCommand)
        {
            object result = await sqlCommand.ExecuteScalarAsync();
            return result;
        }

        /// <summary>
        /// Populate a list of Values for the predefined set of default columns.
        /// </summary>
        /// <param name="dataHint"></param>
        /// <returns></returns>
        private IList<object> GetValues(int dataHint)
        {
            IList<object> values = new List<object>(3)
            {
                dataHint,
                string.Format("Microsoft{0}", dataHint),
                string.Format("Corporation{0}", dataHint)
            };
            return values;
        }

        /// <summary>
        /// Test the results of DataAdapter.Fill command.
        /// </summary>
        /// <param name="dataTable"></param>
        /// <param name="values"></param>
        private void TestDataAdapterFillResults(DataTable dataTable, IList<object> values)
        {
            foreach (DataRow row in dataTable.Rows)
            {
                Assert.Equal(values[0], row.ItemArray[0]);
                Assert.Equal(values[1], row.ItemArray[1]);
                Assert.Equal(values[2], row.ItemArray[2]);
            }
        }

        public void Dispose()
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                Table.DeleteData(fixture.ApiTestTable.Name, sqlConnection);
            }
        }
    }

    struct Customer
    {
        public Customer(int id, string firstName, string lastName)
        {
            Id = id;
            FirstName = firstName;
            LastName = lastName;
        }

        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
