// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public sealed class ColumnDecryptErrorTests : IClassFixture<PlatformSpecificTestContext>, IDisposable
    {
        private SQLSetupStrategy fixture;

        private readonly string tableName;

        public ColumnDecryptErrorTests(PlatformSpecificTestContext context)
        {
            fixture = context.Fixture;
            tableName = fixture.ColumnDecryptErrorTestTable.Name;
        }

        // tests
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(TestQueries))]
        public void TestCleanConnectionAfterDecryptFail(string connString, string selectQuery, int totalColumnsInSelect, string[] types)
        {
            Assert.False(string.IsNullOrWhiteSpace(selectQuery), "FAILED: select query should not be null or empty.");
            Assert.True(totalColumnsInSelect <= 3, "FAILED: totalColumnsInSelect should <= 3.");

            using (SqlConnection sqlConn = new SqlConnection(connString))
            {
                sqlConn.Open();

                Table.DeleteData(tableName, sqlConn);

                // insert 1 row data
                Customer customer = new Customer(
                    45,
                    "Microsoft",
                    "Corporation");

                DatabaseHelper.InsertCustomerData(sqlConn, null, tableName, customer);

                using (SqlCommand sqlCommand = new SqlCommand(string.Format(selectQuery, tableName),
                                                            sqlConn, null, SqlCommandColumnEncryptionSetting.Enabled))
                {
                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");

                        while (sqlDataReader.Read())
                        {
                            DatabaseHelper.CompareResults(sqlDataReader, types, totalColumnsInSelect);
                        }
                    }
                }
            }
        }


        public void Dispose()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connStrAE))
                {
                    sqlConnection.Open();
                    Table.DeleteData(fixture.ColumnDecryptErrorTestTable.Name, sqlConnection);
                }
            }
        }
    }

    public class TestQueries : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, @"select CustomerId, FirstName, LastName from  [{0}] ", 3, new string[] { @"int", @"string", @"string" } };
                yield return new object[] { connStrAE, @"select CustomerId, FirstName from  [{0}] ", 2, new string[] { @"int", @"string" } };
                yield return new object[] { connStrAE, @"select LastName from  [{0}] ", 1, new string[] { @"string" } };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

