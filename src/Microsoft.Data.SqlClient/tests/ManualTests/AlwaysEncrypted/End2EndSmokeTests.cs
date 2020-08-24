// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class End2EndSmokeTests : IClassFixture<PlatformSpecificTestContext>, IDisposable
    {
        private SQLSetupStrategy fixture;

        private readonly string tableName;

        public End2EndSmokeTests(PlatformSpecificTestContext context)
        {
            fixture = context.Fixture;
            tableName = fixture.End2EndSmokeTable.Name;
        }

        // tests
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(TestSelectOnEncryptedNonEncryptedColumnsData))]
        public void TestSelectOnEncryptedNonEncryptedColumns(string connString, string selectQuery, int totalColumnsInSelect, string[] types)
        {
            Assert.False(string.IsNullOrWhiteSpace(selectQuery), "FAILED: select query should not be null or empty.");
            Assert.True(totalColumnsInSelect <= 3, "FAILED: totalColumnsInSelect should <= 3.");

            using (SqlConnection sqlConn = new SqlConnection(connString))
            {
                sqlConn.Open();

                Table.DeleteData(tableName, sqlConn);

                // insert 1 row data
                Customer customer = new Customer(45, "Microsoft", "Corporation");

                DatabaseHelper.InsertCustomerData(sqlConn, tableName, customer);

                using (SqlCommand sqlCommand = new SqlCommand(string.Format(selectQuery, tableName),
                                                            sqlConn, null, SqlCommandColumnEncryptionSetting.Enabled))
                {
                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");

                        while (sqlDataReader.Read())
                        {
                            CompareResults(sqlDataReader, types, totalColumnsInSelect);
                        }
                    }
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(TestSelectOnEncryptedNonEncryptedColumnsWithEncryptedParametersData))]
        public void TestSelectOnEncryptedNonEncryptedColumnsWithEncryptedParameters(string connString,
                                                                                    bool sync,
                                                                                    string selectQuery,
                                                                                    int totalColumnsInSelect,
                                                                                    string[] types,
                                                                                    int numberofParameters,
                                                                                    object[] values)
        {
            Assert.False(string.IsNullOrWhiteSpace(selectQuery), "FAILED: select query should not be null or empty.");
            Assert.True(totalColumnsInSelect <= 3, "FAILED: totalColumnsInSelect should <= 3.");

            using (SqlConnection sqlConn = new SqlConnection(connString))
            {
                sqlConn.Open();

                Table.DeleteData(tableName, sqlConn);

                // insert 1 row data
                Customer customer = new Customer(45, "Microsoft", "Corporation");

                DatabaseHelper.InsertCustomerData(sqlConn, tableName, customer);

                using (SqlCommand sqlCommand = new SqlCommand(string.Format(selectQuery, tableName),
                                                            sqlConn, null, SqlCommandColumnEncryptionSetting.Enabled))
                {
                    Assert.True(numberofParameters <= 3, "FAILED: No:of parameters should be <= 3.");

                    int parameterIndex = 0;

                    while (parameterIndex < numberofParameters * 3)
                    {
                        object value = null;
                        string parameterName = (string)values[parameterIndex];
                        Assert.False(string.IsNullOrWhiteSpace(parameterName), "FAILED: parameterName should not be null or empty.");

                        switch ((string)values[parameterIndex + 1])
                        {
                            case "string":
                                value = (string)values[parameterIndex + 2];
                                break;

                            case "int":
                                value = (int)values[parameterIndex + 2];
                                break;

                            default:
                                Assert.True(false, "FAILED: No other data type is supported.");
                                break;
                        }

                        sqlCommand.Parameters.AddWithValue(parameterName, value);
                        parameterIndex += 3;
                    }

                    if (sync)
                    {
                        VerifyResultsSync(sqlCommand, types, totalColumnsInSelect);
                    }
                    else
                    {
                        Task verifyTask = VerifyResultsAsync(sqlCommand, types, totalColumnsInSelect);
                        verifyTask.Wait();
                    }
                }
            }
        }

        /// <summary>
        /// Verify results of select statement with sync apis.
        /// </summary>
        /// <param name="sqlCommand"></param>
        /// <param name="parameterTypes"></param>
        /// <param name="totalColumnsInSelect"></param>
        private void VerifyResultsSync(SqlCommand sqlCommand, string[] parameterTypes, int totalColumnsInSelect)
        {
            Assert.True(sqlCommand != null, "FAILED: sqlCommand should not be null.");
            using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
            {
                Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");
                while (sqlDataReader.Read())
                {
                    CompareResults(sqlDataReader, parameterTypes, totalColumnsInSelect);
                }
            }
        }

        /// <summary>
        /// Verify results of select statement with async apis.
        /// </summary>
        /// <param name="sqlCommand"></param>
        /// <param name="parameterTypes"></param>
        /// <param name="totalColumnsInSelect"></param>
        private async Task VerifyResultsAsync(SqlCommand sqlCommand, string[] parameterTypes, int totalColumnsInSelect)
        {
            Assert.True(sqlCommand != null, "FAILED: sqlCommand should not be null.");
            using (SqlDataReader sqlDataReader = await sqlCommand.ExecuteReaderAsync())
            {
                Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");
                while (await sqlDataReader.ReadAsync())
                {
                    CompareResults(sqlDataReader, parameterTypes, totalColumnsInSelect);
                }
            }
        }

        /// <summary>
        /// Read data using sqlDataReader and compare results.
        /// <summary>
        /// <param name="sqlDataReader"></param>
        /// <param name="parameterTypes"></param>
        /// <param name="totalColumnsInSelect"></param>
        private void CompareResults(SqlDataReader sqlDataReader, string[] parameterTypes, int totalColumnsInSelect)
        {
            int columnsRead = 0;

            while (columnsRead < totalColumnsInSelect)
            {
                switch (parameterTypes[columnsRead])
                {
                    case "string":
                        Assert.True((string.Equals(sqlDataReader.GetString(columnsRead), @"Microsoft", StringComparison.Ordinal))
                            || (string.Equals(sqlDataReader.GetString(columnsRead), @"Corporation", StringComparison.Ordinal)),
                            "FAILED: read string value isn't expected.");
                        break;

                    case "int":
                        Assert.True(sqlDataReader.GetInt32(columnsRead) == 45, "FAILED: read int value does not match.");
                        break;

                    default:
                        Assert.True(false, "FAILED: unexpected data type.");
                        break;
                }

                columnsRead++;
            }
        }

        public void Dispose()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connStrAE))
                {
                    sqlConnection.Open();
                    Table.DeleteData(fixture.End2EndSmokeTable.Name, sqlConnection);
                }
            }
        }
    }

    public class TestSelectOnEncryptedNonEncryptedColumnsData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, @"select CustomerId, FirstName, LastName from  [{0}] ", 3, new string[] { @"int", @"string", @"string" } };
                yield return new object[] { connStrAE, @"select CustomerId, FirstName from  [{0}] ", 2, new string[] { @"int", @"string" } };
                yield return new object[] { connStrAE, @"select LastName from  [{0}] ", 1, new string[] { @"string" }};
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class TestSelectOnEncryptedNonEncryptedColumnsWithEncryptedParametersData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { 
                    connStrAE, 
                    true, /*sync*/
                    @"select CustomerId, FirstName, LastName from [{0}] where CustomerId = @CustomerId and FirstName = @FirstName",
                    3, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/ 
                                   @"string" /*datatype of third column in select statement*/},
                    2, /*no:of input parameters*/
                    new object[] { @"CustomerId", /*input parameter name*/
                                   @"int", /*input parameter data type*/
                                   45, /*input parameter value*/
                                   @"FirstName", /*input parameter name*/
                                   @"string", /*input parameter data type*/
                                   @"Microsoft" /*input parameter value*/}
                };

                yield return new object[] { 
                    connStrAE,
                    true, /*sync*/
                    @"select CustomerId, FirstName, LastName from [{0}] where CustomerId = @CustomerId",
                    3, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/ 
                                   @"string" /*datatype of third column in select statement*/},
                    1, /*no:of input parameters*/
                    new object[] { @"CustomerId", /*input parameter name*/
                                   @"int", /*input parameter data type*/
                                   45, /*input parameter value*/}
                };

                yield return new object[] { 
                    connStrAE,
                    true, /*sync*/
                    @"select CustomerId, FirstName, LastName from [{0}] where FirstName = @FirstName",
                    3, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/ 
                                   @"string" /*datatype of third column in select statement*/},
                    1, /*no:of input parameters*/
                    new object[] { @"FirstName", /*input parameter name*/
                                   @"string", /*input parameter data type*/
                                   @"Microsoft" /*input parameter value*/}
                };

                yield return new object[] {
                    connStrAE,
                    true, /*sync*/
                    @"select CustomerId, FirstName, LastName from [{0}] where LastName = @LastName",
                    3, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/ 
                                   @"string" /*datatype of third column in select statement*/},
                    1, /*no:of input parameters*/
                    new object[] { @"LastName", /*input parameter name*/
                                   @"string", /*input parameter data type*/
                                   @"Corporation" /*input parameter value*/}
                };

                yield return new object[] {
                    connStrAE,
                    true, /*sync*/
                    @"select CustomerId, FirstName from [{0}] where CustomerId = @CustomerId and FirstName = @FirstName",
                    2, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/},
                    2, /*no:of input parameters*/
                    new object[] { @"CustomerId", /*input parameter name*/
                                   @"int", /*input parameter data type*/
                                   45, /*input parameter value*/
                                   @"FirstName", /*input parameter name*/
                                   @"string", /*input parameter data type*/
                                   @"Microsoft" /*input parameter value*/}
                };

                yield return new object[] {
                    connStrAE,
                    false, /*sync*/
                    @"select CustomerId, FirstName, LastName from [{0}] where CustomerId = @CustomerId and FirstName = @FirstName",
                    3, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/ 
                                   @"string" /*datatype of third column in select statement*/},
                    2, /*no:of input parameters*/
                    new object[] { @"CustomerId", /*input parameter name*/
                                   @"int", /*input parameter data type*/
                                   45, /*input parameter value*/
                                   @"FirstName", /*input parameter name*/
                                   @"string", /*input parameter data type*/
                                   @"Microsoft" /*input parameter value*/}
                };

                yield return new object[] {
                    connStrAE,
                    false, /*sync*/
                    @"select CustomerId, FirstName, LastName from [{0}] where CustomerId = @CustomerId",
                    3, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/ 
                                   @"string" /*datatype of third column in select statement*/},
                    1, /*no:of input parameters*/
                    new object[] { @"CustomerId", /*input parameter name*/
                                   @"int", /*input parameter data type*/
                                   45, /*input parameter value*/}
                };

                yield return new object[] {
                    connStrAE,
                    false, /*sync*/
                    @"select CustomerId, FirstName, LastName from [{0}] where FirstName = @FirstName",
                    3, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/ 
                                   @"string" /*datatype of third column in select statement*/},
                    1, /*no:of input parameters*/
                    new object[] { @"FirstName", /*input parameter name*/
                                   @"string", /*input parameter data type*/
                                   @"Microsoft" /*input parameter value*/}
                };

                yield return new object[] {
                    connStrAE,
                    false, /*sync*/
                    @"select CustomerId, FirstName, LastName from [{0}] where LastName = @LastName",
                    3, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/ 
                                   @"string" /*datatype of third column in select statement*/},
                    1, /*no:of input parameters*/
                    new object[] { @"LastName", /*input parameter name*/
                                   @"string", /*input parameter data type*/
                                   @"Corporation" /*input parameter value*/}
                };

                yield return new object[] {
                    connStrAE,
                    false, /*sync*/
                    @"select CustomerId, FirstName from [{0}] where CustomerId = @CustomerId and FirstName = @FirstName",
                    2, /*total number of columns in select statement*/
                    new string[] { @"int", /*unencrypted datatype of first column in select statement*/ 
                                   @"string", /*unencrypted datatype of second column in select statement*/},
                    2, /*no:of input parameters*/
                    new object[] { @"CustomerId", /*input parameter name*/
                                   @"int", /*input parameter data type*/
                                   45, /*input parameter value*/
                                   @"FirstName", /*input parameter name*/
                                   @"string", /*input parameter data type*/
                                   @"Microsoft" /*input parameter value*/}
                };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
