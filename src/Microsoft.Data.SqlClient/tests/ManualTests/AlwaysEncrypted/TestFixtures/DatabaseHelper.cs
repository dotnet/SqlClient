// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    class DatabaseHelper
    {
        /// <summary>
        /// Insert Customer record into table
        /// </summary>
        internal static void InsertCustomerData(SqlConnection sqlConnection, string tableName, Customer customer)
        {
            using (SqlCommand sqlCommand = new SqlCommand(
                $"INSERT INTO [{tableName}] (CustomerId, FirstName, LastName) VALUES (@CustomerId, @FirstName, @LastName);",
                connection: sqlConnection,
                transaction: null,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
            {
                sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
                sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);
                sqlCommand.Parameters.AddWithValue(@"LastName", customer.LastName);

                sqlCommand.ExecuteNonQuery();
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
            RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
            rngCsp.GetBytes(randomBytes);

            return randomBytes;
        }

        internal static string GenerateUniqueName(string baseName) => string.Concat("AE_", baseName, "_", Guid.NewGuid().ToString().Replace('-', '_'));
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

    public class AEConnectionStringProviderWithCommandBehaviorSet1 : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, CommandBehavior.SingleResult };
                yield return new object[] { connStrAE, CommandBehavior.SingleRow };
                yield return new object[] { connStrAE, CommandBehavior.CloseConnection };
                yield return new object[] { connStrAE, CommandBehavior.SequentialAccess };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class AEConnectionStringProviderWithCommandBehaviorSet2 : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, CommandBehavior.Default };
                yield return new object[] { connStrAE, CommandBehavior.SequentialAccess };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class AEConnectionStringProviderWithSchemaType : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, SchemaType.Source };
                yield return new object[] { connStrAE, SchemaType.Mapped };
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
