// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Xunit;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    class DatabaseHelper
    {
        /// <summary>
        /// Insert Customer record into table
        /// </summary>
        internal static void InsertCustomerData(SqlConnection sqlConnection, SqlTransaction transaction, string tableName, Customer customer)
        {
            using SqlCommand sqlCommand = new(
                $"INSERT INTO [{tableName}] (CustomerId, FirstName, LastName) VALUES (@CustomerId, @FirstName, @LastName);",
                connection: sqlConnection,
                transaction: transaction,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled);

            sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
            sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);
            sqlCommand.Parameters.AddWithValue(@"LastName", customer.LastName);
            sqlCommand.ExecuteNonQuery();
        }

#if !NETFRAMEWORK
        /// <summary>
        /// Insert CustomerDateOnly record into table
        /// </summary>
        internal static void InsertCustomerDateOnlyData(SqlConnection sqlConnection, SqlTransaction transaction, string tableName, CustomerDateOnly customer)
        {
            using SqlCommand sqlCommand = new(
                $"INSERT INTO [{tableName}] (CustomerId, FirstName, LastName, DateOfBirth, TimeOfDay) VALUES (@CustomerId, @FirstName, @LastName, @DateOfBirth, @TimeOfDay);",
                connection: sqlConnection,
                transaction: transaction,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled);

            sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
            sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);
            sqlCommand.Parameters.AddWithValue(@"LastName", customer.LastName);
            sqlCommand.Parameters.AddWithValue(@"DateOfBirth", customer.DateOfBirth);
            sqlCommand.Parameters.AddWithValue(@"TimeOfDay", customer.TimeOfDay);
            sqlCommand.ExecuteNonQuery();
        }
#endif

        /// <summary>
        /// Validates that the results are the ones expected.
        /// </summary>
        /// <param name="sqlDataReader"></param>
        public static void ValidateResultSet(SqlDataReader sqlDataReader)
        {
            // Validate the result set
            int rowsFound = 0;

            Assert.True(sqlDataReader.HasRows);
            while (sqlDataReader.Read())
            {
                if (sqlDataReader.FieldCount == 3)
                {
                    Assert.Equal(45, sqlDataReader.GetInt32(0));
                    Assert.Equal(@"Microsoft", sqlDataReader.GetString(1));
                    Assert.Equal(@"Corporation", sqlDataReader.GetString(2));
                }
                else if (sqlDataReader.FieldCount == 1)
                {
                    Assert.True(sqlDataReader.GetString(0) == @"Microsoft" || sqlDataReader.GetString(0) == @"Corporation", "Employee FirstName didn't match.");
                }

                rowsFound++;
            }

            Assert.True(rowsFound == 1, "Incorrect number of rows returned in first execution.");
        }

        public static void VerifyRecordAbsent(SqlConnection sqlConnection, Customer customer, string tableName, SqlTransaction sqlTransaction = null)
        {
            using SqlCommand sqlCommand = new(
                cmdText: $"SELECT * FROM [{tableName}] WHERE CustomerId = @CustomerId and FirstName = @FirstName;",
                connection: sqlConnection,
                transaction: sqlTransaction,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled);
            sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
            sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);

            using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
            Assert.False(sqlDataReader.HasRows);
        }

        public static void VerifyRecordPresent(SqlConnection sqlConnection, Customer customer, string tableName, SqlTransaction sqlTransaction = null)
        {
            using SqlCommand sqlCommand = new(
                cmdText: $"SELECT * FROM [{tableName}] WHERE CustomerId = @CustomerId and FirstName = @FirstName;",
                connection: sqlConnection,
                transaction: sqlTransaction,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled);
            sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
            sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);

            using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
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

        /// <summary>
        /// Verify results of select statement with sync apis.
        /// </summary>
        /// <param name="sqlCommand"></param>
        /// <param name="parameterTypes"></param>
        /// <param name="totalColumnsInSelect"></param>
        public static void VerifyResultsSync(SqlCommand sqlCommand, string[] parameterTypes, int totalColumnsInSelect)
        {
            Assert.True(sqlCommand != null, "FAILED: sqlCommand should not be null.");
            using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
            Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");
            while (sqlDataReader.Read())
            {
                CompareResults(sqlDataReader, parameterTypes, totalColumnsInSelect);
            }
        }

        /// <summary>
        /// Verify results of select statement with async apis.
        /// </summary>
        /// <param name="sqlCommand"></param>
        /// <param name="parameterTypes"></param>
        /// <param name="totalColumnsInSelect"></param>
        public static async Task VerifyResultsAsync(SqlCommand sqlCommand, string[] parameterTypes, int totalColumnsInSelect)
        {
            Assert.True(sqlCommand != null, "FAILED: sqlCommand should not be null.");
            using SqlDataReader sqlDataReader = await sqlCommand.ExecuteReaderAsync();
            Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");
            while (await sqlDataReader.ReadAsync())
            {
                CompareResults(sqlDataReader, parameterTypes, totalColumnsInSelect);
            }
        }

        /// <summary>
        /// Read data using sqlDataReader and compare results.
        /// <summary>
        /// <param name="sqlDataReader"></param>
        /// <param name="parameterTypes"></param>
        /// <param name="totalColumnsInSelect"></param>
        public static void CompareResults(SqlDataReader sqlDataReader, string[] parameterTypes, int totalColumnsInSelect)
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
#if !NETFRAMEWORK
                    case "DateOnly":
                        Assert.True(sqlDataReader.GetFieldValue<DateOnly>(columnsRead) == new DateOnly(2001, 1, 31), "FAILED: read DateOnly value does not match.");
                        break;

                    case "TimeOnly":
                        Assert.True(sqlDataReader.GetFieldValue<TimeOnly>(columnsRead) == new TimeOnly(18, 36, 45), "FAILED: read TimeOnly value does not match.");
                        break;
#endif
                    default:
                        Assert.Fail("FAILED: unexpected data type.");
                        break;
                }

                columnsRead++;
            }
        }

        /// <summary>
        /// Generates cryptographically random bytes
        /// </summary>
        /// <param name="length">No of cryptographically random bytes to be generated</param>
        /// <returns>A byte array containing cryptographically generated random bytes</returns>
        internal static byte[] GenerateRandomBytes(int length)
        {
            // Generate random bytes cryptographically.
            byte[] randomBytes = new byte[length];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return randomBytes;
        }

        internal static string GenerateUniqueName(string baseName) => string.Concat("AE_", baseName, "_", Guid.NewGuid().ToString().Replace('-', '_'));
    }

    public static class DataHelpers
    {
        public static IEnumerable<object[]> AEConnectionStringProviderWithSchemaType()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, SchemaType.Source };
                yield return new object[] { connStrAE, SchemaType.Mapped };
            }
        }

        public static IEnumerable<object[]> AEConnectionStringProviderWithCommandBehaviorSet1()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, CommandBehavior.SingleResult };
                yield return new object[] { connStrAE, CommandBehavior.SingleRow };
                yield return new object[] { connStrAE, CommandBehavior.CloseConnection };
                yield return new object[] { connStrAE, CommandBehavior.SequentialAccess };
            }
        }

        public static IEnumerable<object[]> AEConnectionStringProviderWithCommandBehaviorSet2()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, CommandBehavior.Default };
                yield return new object[] { connStrAE, CommandBehavior.SequentialAccess };
            }
        }
    }

    public class AEConnectionStringProviderWithBooleanVariable : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, true };
                yield return new object[] { connStrAE, false };
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class AEConnectionStringProvider : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class AEConnectionStringProviderWithIntegers : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, 1 };
                yield return new object[] { connStrAE, 100 };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class AEConnectionStringProviderWithExecutionMethod : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, @"ExecuteReader", 1 };
                yield return new object[] { connStrAE, @"ExecuteReader", 3 };
                yield return new object[] { connStrAE, @"ExecuteNonQuery", 1 };
                yield return new object[] { connStrAE, @"ExecuteNonQuery", 3 };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class AEConnectionStringProviderWithCancellationTime : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                if (DataTestUtility.EnclaveEnabled)
                {
                    yield return new object[] { connStrAE, 0 /*ExecuteReader*/, 2000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 1 /*ExecuteNonQuery*/, 2000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 2 /*ExecuteScalar*/, 2000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 0 /*ExecuteReader*/, 5000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 1 /*ExecuteNonQuery*/, 5000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 2 /*ExecuteScalar*/, 5000 /*CancelAfterMilliseconds*/ };
                }
                else
                {
                    yield return new object[] { connStrAE, 0 /*ExecuteReader*/, 500 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 1 /*ExecuteNonQuery*/, 500 /*CancelAfterMilliseconds*/};
                    yield return new object[] { connStrAE, 2 /*ExecuteScalar*/, 500 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 0 /*ExecuteReader*/, 1000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 1 /*ExecuteNonQuery*/, 1000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 2 /*ExecuteScalar*/, 1000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 0 /*ExecuteReader*/, 2000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 1 /*ExecuteNonQuery*/, 2000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 2 /*ExecuteScalar*/, 2000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 0 /*ExecuteReader*/, 5000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 1 /*ExecuteNonQuery*/, 5000 /*CancelAfterMilliseconds*/ };
                    yield return new object[] { connStrAE, 2 /*ExecuteScalar*/, 5000 /*CancelAfterMilliseconds*/ };
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
