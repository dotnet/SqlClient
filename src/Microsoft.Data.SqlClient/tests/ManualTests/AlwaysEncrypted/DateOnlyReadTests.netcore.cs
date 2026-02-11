// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This file must be conditionally included because it will not compile on netfx
#if NET

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public sealed class DateOnlyReadTests : IClassFixture<SQLSetupStrategyCertStoreProvider>, IDisposable
    {
        private SQLSetupStrategy fixture;

        private readonly string tableName;

        public DateOnlyReadTests(SQLSetupStrategyCertStoreProvider context)
        {
            fixture = context;
            tableName = fixture.DateOnlyTestTable.Name;
        }

        // tests
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(TestSelectOnEncryptedNonEncryptedColumnsDataDateOnly))]
        public void TestSelectOnEncryptedNonEncryptedColumns(string connString, string selectQuery, int totalColumnsInSelect, string[] types)
        {
            Assert.False(string.IsNullOrWhiteSpace(selectQuery), "FAILED: select query should not be null or empty.");
            Assert.True(totalColumnsInSelect <= 3, "FAILED: totalColumnsInSelect should <= 3.");

            using (SqlConnection sqlConn = new SqlConnection(connString))
            {
                sqlConn.Open();

                Table.DeleteData(tableName, sqlConn);

                // insert 1 row data
                CustomerDateOnly customer = new CustomerDateOnly(
                    45, 
                    "Microsoft", 
                    "Corporation", 
                    new DateOnly(2001, 1, 31), 
                    new TimeOnly(18, 36, 45));

                DatabaseHelper.InsertCustomerDateOnlyData(sqlConn, null, tableName, customer);

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
                    Table.DeleteData(fixture.DateOnlyTestTable.Name, sqlConnection);
                }
            }
        }
    }

    public class TestSelectOnEncryptedNonEncryptedColumnsDataDateOnly : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, @"select CustomerId, FirstName, LastName from  [{0}] ", 3, new string[] { @"int", @"string", @"string" } };
                yield return new object[] { connStrAE, @"select CustomerId, FirstName from  [{0}] ", 2, new string[] { @"int", @"string" } };
                yield return new object[] { connStrAE, @"select LastName from  [{0}] ", 1, new string[] { @"string" }};
                yield return new object[] { connStrAE, @"select DateOfBirth, TimeOfDay from  [{0}] ", 2, new string[] { @"DateOnly", "TimeOnly" } };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

#endif
