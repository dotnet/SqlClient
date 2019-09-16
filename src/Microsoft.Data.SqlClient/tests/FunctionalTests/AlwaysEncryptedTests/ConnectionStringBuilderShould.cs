// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class ConnectionStringBuilderShould
    {
        public static readonly object[][] SqlConnectionColumnEncryptionSettings =
        {
            new object[] {SqlConnectionColumnEncryptionSetting.Enabled},
            new object[] {SqlConnectionColumnEncryptionSetting.Disabled}
        };

        [Fact]
        public void TestSqlConnectionStringBuilder()
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();
            Assert.Equal(SqlConnectionColumnEncryptionSetting.Disabled, connectionStringBuilder.ColumnEncryptionSetting);
            connectionStringBuilder.DataSource = @"localhost";

            // Create a connection object with the above builder and verify the expected value.
            VerifyColumnEncryptionSetting(connectionStringBuilder, false);
        }

        [Fact]
        public void TestSqlConnectionStringBuilderEnclaveAttestationUrl()
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();
            Assert.Equal(string.Empty, connectionStringBuilder.EnclaveAttestationUrl);
            connectionStringBuilder.DataSource = @"localhost";

            // Create a connection object with the above builder and verify the expected value.
            VerifyEnclaveAttestationUrlSetting(connectionStringBuilder, "");

            SqlConnectionStringBuilder connectionStringBuilder2 = new SqlConnectionStringBuilder();
            connectionStringBuilder2.EnclaveAttestationUrl = "www.foo.com";
            Assert.Equal("www.foo.com", connectionStringBuilder2.EnclaveAttestationUrl);
            connectionStringBuilder2.DataSource = @"localhost";

            // Create a connection object with the above builder and verify the expected value.
            VerifyEnclaveAttestationUrlSetting(connectionStringBuilder2, "www.foo.com");

            connectionStringBuilder2.Clear();
            Assert.Equal(string.Empty, connectionStringBuilder2.EnclaveAttestationUrl);

            Assert.True(string.IsNullOrEmpty(connectionStringBuilder2.DataSource));
        }

        [Fact]
        public void TestSqlConnectionStringBuilderEquivalentTo_EnclaveAttestationUrl()
        {
            string enclaveAttUrl1 = "www.foo.com";
            string enclaveAttUrl2 = "www.foo1.com";
            SqlConnectionStringBuilder connectionStringBuilder1 = new SqlConnectionStringBuilder();
            SqlConnectionStringBuilder connectionStringBuilder2 = new SqlConnectionStringBuilder();

            // Modify the default value and set the same value on the both the builder objects above.
            connectionStringBuilder1.EnclaveAttestationUrl = enclaveAttUrl1;
            connectionStringBuilder2.EnclaveAttestationUrl = enclaveAttUrl1;

            // Use the EquivalentTo function to compare both the builder objects and make sure the result is expected.
            Assert.True(connectionStringBuilder1.EquivalentTo(connectionStringBuilder2));

            connectionStringBuilder2.EnclaveAttestationUrl = enclaveAttUrl2;
            Assert.True(!connectionStringBuilder1.EquivalentTo(connectionStringBuilder2));
        }

        [Theory]
        [MemberData(nameof(SqlConnectionColumnEncryptionSettings))]
        public void TestSqlConnectionStringBuilderColumnEncryptionSetting(SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting)
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();

            // Modify the default value.
            connectionStringBuilder.ColumnEncryptionSetting = sqlConnectionColumnEncryptionSetting;

            // Create a connection object with the above builder and verify the expected value.
            VerifyColumnEncryptionSetting(connectionStringBuilder, sqlConnectionColumnEncryptionSetting == SqlConnectionColumnEncryptionSetting.Enabled);
        }

        [Theory]
        [MemberData(nameof(SqlConnectionColumnEncryptionSettings))]
        public void TestSqlConnectionStringBuilderClear(SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting)
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();

            // Modify the default value.
            connectionStringBuilder.ColumnEncryptionSetting = sqlConnectionColumnEncryptionSetting;
            connectionStringBuilder.DataSource = @"localhost";

            connectionStringBuilder.Clear();

            Assert.Equal(SqlConnectionColumnEncryptionSetting.Disabled, connectionStringBuilder.ColumnEncryptionSetting);
            Assert.True(string.IsNullOrEmpty(connectionStringBuilder.DataSource));
        }

        [Theory]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlConnectionColumnEncryptionSetting.Enabled, true)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlConnectionColumnEncryptionSetting.Disabled, false)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlConnectionColumnEncryptionSetting.Enabled, false)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlConnectionColumnEncryptionSetting.Disabled, true)]
        public void TestSqlConnectionStringBuilderEquivalentTo(
            SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting1,
            SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting2,
            bool isExpectedEquivelance)
        {
            SqlConnectionStringBuilder connectionStringBuilder1 = new SqlConnectionStringBuilder();
            SqlConnectionStringBuilder connectionStringBuilder2 = new SqlConnectionStringBuilder();

            // Modify the default value and set the same value on the both the builder objects above.
            connectionStringBuilder1.ColumnEncryptionSetting = sqlConnectionColumnEncryptionSetting1;
            connectionStringBuilder2.ColumnEncryptionSetting = sqlConnectionColumnEncryptionSetting2;

            // Use the EquivalentTo function to compare both the builder objects and make sure the result is expected.
            if (isExpectedEquivelance)
            {
                Assert.True(connectionStringBuilder1.EquivalentTo(connectionStringBuilder2));
            }
            else
            {
                Assert.False(connectionStringBuilder1.EquivalentTo(connectionStringBuilder2));
            }
        }

        [Theory]
        [MemberData(nameof(SqlConnectionColumnEncryptionSettings))]
        public void TestSqlConnectionStringBuilderContainsKey(SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting)
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();

            // Key is "Column Encryption Setting" with spaces. So lookup for ColumnEncryptionSetting should return false.
            Assert.False(connectionStringBuilder.ContainsKey(@"ColumnEncryptionSetting"));

            // connectionStringBuilder should have the key Column Encryption Setting, even if value is not set.
            Assert.True(connectionStringBuilder.ContainsKey(@"Column Encryption Setting"));

            // set a value and check for the key again, it should exist.
            connectionStringBuilder.ColumnEncryptionSetting = sqlConnectionColumnEncryptionSetting;
            Assert.True(connectionStringBuilder.ContainsKey(@"Column Encryption Setting"));

            //also check attestatin url

            // Key is "Column Encryption Setting" with spaces. So lookup for ColumnEncryptionSetting should return false.
            Assert.False(connectionStringBuilder.ContainsKey(@"EnclaveAttestationUrl"));

            // connectionStringBuilder should have the key Column Encryption Setting, even if value is not set.
            Assert.True(connectionStringBuilder.ContainsKey(@"Enclave Attestation Url"));

            // set a value and check for the key again, it should exist.
            connectionStringBuilder.EnclaveAttestationUrl = "www.foo.com";
            Assert.True(connectionStringBuilder.ContainsKey(@"Enclave Attestation Url"));
        }

        [Theory]
        [MemberData(nameof(SqlConnectionColumnEncryptionSettings))]
        public void TestSqlConnectionStringBuilderTryGetValue(SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting)
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();
            object outputValue;

            // connectionStringBuilder should not have the key ColumnEncryptionSetting. The key is with spaces.
            bool tryGetValueResult = connectionStringBuilder.TryGetValue(@"ColumnEncryptionSetting", out outputValue);
            Assert.False(tryGetValueResult);
            Assert.Null(outputValue);

            // Get the value for the key without setting it.
            tryGetValueResult = connectionStringBuilder.TryGetValue(@"Column Encryption Setting", out outputValue);
            Assert.True(tryGetValueResult);
            Assert.Equal(SqlConnectionColumnEncryptionSetting.Disabled, (SqlConnectionColumnEncryptionSetting)outputValue);

            // set the value for the key without setting it.
            connectionStringBuilder.ColumnEncryptionSetting = sqlConnectionColumnEncryptionSetting;
            tryGetValueResult = connectionStringBuilder.TryGetValue(@"Column Encryption Setting", out outputValue);
            Assert.True(tryGetValueResult);
            Assert.Equal(sqlConnectionColumnEncryptionSetting, (SqlConnectionColumnEncryptionSetting)outputValue);

            // connectionStringBuilder should not have the key ColumnEncryptionSetting. The key is with spaces.
            tryGetValueResult = connectionStringBuilder.TryGetValue(@"EnclaveAttestationUrl", out outputValue);
            Assert.False(tryGetValueResult);
            Assert.Null(outputValue);

            // Get the value for the key without setting it.
            tryGetValueResult = connectionStringBuilder.TryGetValue(@"Enclave Attestation Url", out outputValue);
            Assert.True(tryGetValueResult);
            Assert.Equal(string.Empty, (string)outputValue);

            // set the value for the key without setting it.
            connectionStringBuilder.EnclaveAttestationUrl = "www.foo.com";
            tryGetValueResult = connectionStringBuilder.TryGetValue(@"Enclave Attestation Url", out outputValue);
            Assert.True(tryGetValueResult);
            Assert.Equal("www.foo.com", (string)outputValue);
        }

        [Theory]
        [MemberData(nameof(SqlConnectionColumnEncryptionSettings))]
        public void TestSqlConnectionStringBuilderAdd(SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting)
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();

            // Use the Add function to update the Column Encryption Setting in the dictionary.
            connectionStringBuilder.Add(@"Column Encryption Setting", sqlConnectionColumnEncryptionSetting);

            // Query the property to check if the above add was effective.
            Assert.Equal(sqlConnectionColumnEncryptionSetting, connectionStringBuilder.ColumnEncryptionSetting);

            // Use the Add function to update the Column Encryption Setting in the dictionary.
            string url = "www.foo.com";
            connectionStringBuilder.Add(@"Enclave Attestation Url", url);

            // Query the property to check if the above add was effective.
            Assert.Equal(url, connectionStringBuilder.EnclaveAttestationUrl);
        }

        [Theory]
        [MemberData(nameof(SqlConnectionColumnEncryptionSettings))]
        public void TestSqlConnectionStringBuilderRemove(SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting)
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();

            // Use the Add function to update the Column Encryption Setting in the dictionary.
            connectionStringBuilder.Add(@"Column Encryption Setting", sqlConnectionColumnEncryptionSetting);

            // Query the property to check if the above add was effective.
            Assert.Equal(sqlConnectionColumnEncryptionSetting, connectionStringBuilder.ColumnEncryptionSetting);

            // Use the Remove function to remove the Column Encryption Setting from the dictionary.
            connectionStringBuilder.Remove(@"Column Encryption Setting");

            // Query the property to check if the above add was effective.
            object outputValue;
            connectionStringBuilder.TryGetValue(@"Column Encryption Setting", out outputValue);
            Assert.Equal(SqlConnectionColumnEncryptionSetting.Disabled, outputValue);

            // Use the Add function to update the Column Encryption Setting in the dictionary.
            string url = "www.foo.com";
            connectionStringBuilder.Add(@"Enclave Attestation Url", url);

            // Query the property to check if the above add was effective.
            Assert.Equal(url, connectionStringBuilder.EnclaveAttestationUrl);

            // Use the Remove function to remove the Column Encryption Setting from the dictionary.
            connectionStringBuilder.Remove(@"Enclave Attestation Url");

            // Query the property to check if the above add was effective.
            connectionStringBuilder.TryGetValue(@"Enclave Attestation Url", out outputValue);
            Assert.Equal(string.Empty, outputValue);
        }

        [Theory]
        [MemberData(nameof(SqlConnectionColumnEncryptionSettings))]
        public void TestSqlConnectionStringBuilderShouldSerialize(SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting)
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder();

            // Use the Add function to update the Column Encryption Setting in the dictionary.
            connectionStringBuilder.Add(@"Column Encryption Setting", sqlConnectionColumnEncryptionSetting);

            // Query the ShouldSerialize method to check if the above add was effective.
            Assert.True(connectionStringBuilder.ShouldSerialize(@"Column Encryption Setting"));

            // Use the Remove function to Remove the Column Encryption Setting from the dictionary.
            connectionStringBuilder.Remove(@"Column Encryption Setting");

            // Query the property to check if the above add was effective.
            Assert.False(connectionStringBuilder.ShouldSerialize(@"Column Encryption Setting"));

            // Use the Add function to update the Column Encryption Setting in the dictionary.
            string url = "www.foo.com";
            connectionStringBuilder.Add(@"Enclave Attestation Url", url);

            // Query the ShouldSerialize method to check if the above add was effective.
            Assert.True(connectionStringBuilder.ShouldSerialize(@"Enclave Attestation Url"));

            // Use the Remove function to Remove the Column Encryption Setting from the dictionary.
            connectionStringBuilder.Remove(@"Enclave Attestation Url");

            // Query the property to check if the above add was effective.
            Assert.False(connectionStringBuilder.ShouldSerialize(@"Enclave Attestation Url"));
        }

        /// <summary>
        /// Verify the expected setting value for SqlConnectionColumnEncryptionSetting.
        /// </summary>
        /// <param name="connectionStringBuilder"></param>
        /// <param name="expectedColumnEncryptionSetting"></param>
        private void VerifyEnclaveAttestationUrlSetting(SqlConnectionStringBuilder connectionStringBuilder, string expectedAttestationUrl)
        {
            string connectionString = connectionStringBuilder.ToString();
            using (SqlConnection sqlConnection = new SqlConnection(connectionString))
            {
                string enclaveAttestationUrl = (string)typeof(SqlConnection)
                    .GetProperty(@"EnclaveAttestationUrl", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sqlConnection);

                Assert.Equal(expectedAttestationUrl, enclaveAttestationUrl);
            }
        }

        /// <summary>
        /// Verify the expected setting value for SqlConnectionColumnEncryptionSetting.
        /// </summary>
        /// <param name="connectionStringBuilder"></param>
        /// <param name="expectedColumnEncryptionSetting"></param>
        private void VerifyColumnEncryptionSetting(SqlConnectionStringBuilder connectionStringBuilder, bool expectedEncryptionSetting)
        {
            string connectionString = connectionStringBuilder.ToString();
            using (SqlConnection sqlConnection = new SqlConnection(connectionString))
            {
                bool actualEncryptionSetting = (bool)typeof(SqlConnection)
                    .GetProperty("IsColumnEncryptionSettingEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sqlConnection);

                Assert.Equal(expectedEncryptionSetting, actualEncryptionSetting);
            }
        }
    }
}
