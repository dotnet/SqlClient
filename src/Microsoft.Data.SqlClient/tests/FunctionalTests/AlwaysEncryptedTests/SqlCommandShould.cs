// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

using static Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests.TestFixtures;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class SqlCommandShould
    {
        /// <summary>
        /// Test the net value of SqlCommand.ColumnEncryptionOverride.
        /// </summary>
        /// <returns></returns>
        [Theory]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Disabled)]
        public void SetSqlCommandColumnEncryptionSettingAppropriately(SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting, SqlCommandColumnEncryptionSetting sqlCommandColumnEncryptionSetting)
        {
            using (SqlConnection sqlConnectionEnclaveEnabled = new SqlConnection(DefaultConnectionString(columnEncryptionSetting: sqlConnectionColumnEncryptionSetting, fEnclaveEnabled: false, enclaveAttestationUrl: "")))
            using (SqlConnection sqlConnectionEnclaveDisabled = new SqlConnection(DefaultConnectionString(columnEncryptionSetting: sqlConnectionColumnEncryptionSetting, fEnclaveEnabled: true, enclaveAttestationUrl: "www.foo.coms")))
            {
                using (SqlCommand sqlCommandEnclaveEnabled = new SqlCommand(@"select * from sys.objects", sqlConnectionEnclaveEnabled, transaction: null, columnEncryptionSetting: sqlCommandColumnEncryptionSetting))
                using (SqlCommand sqlCommandEnclaveDisabled = new SqlCommand(@"select * from sys.objects", sqlConnectionEnclaveDisabled, transaction: null, columnEncryptionSetting: sqlCommandColumnEncryptionSetting))
                {
                    Assert.Equal(sqlCommandColumnEncryptionSetting, sqlCommandEnclaveEnabled.ColumnEncryptionSetting);
                    Assert.Equal(sqlCommandColumnEncryptionSetting, sqlCommandEnclaveDisabled.ColumnEncryptionSetting);
                }
            }
        }

        [Theory]
        [InlineData(SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        public void TestSqlCommandCloneColumnEncryptionSettingSpecified(SqlCommandColumnEncryptionSetting sqlCommandColumnEncryptionSetting)
        {
            using (SqlConnection sqlConnection = new SqlConnection())
            {
                using (SqlCommand sqlCommand1 = new SqlCommand(
                    cmdText: "Data source = localhost; Connect Timeout = 65534;",
                    connection: sqlConnection,
                    transaction: null,
                    columnEncryptionSetting: sqlCommandColumnEncryptionSetting))
                {
                    Assert.Equal(sqlCommandColumnEncryptionSetting, sqlCommand1.ColumnEncryptionSetting);

                    // Clone the above sqlCommand1 and verify the Column Encryption Setting is propogated correctly.
                    using (SqlCommand sqlCommand2 = sqlCommand1.Clone())
                    {
                        Assert.Equal(sqlCommandColumnEncryptionSetting, sqlCommand2.ColumnEncryptionSetting);
                    }
                }
            }
        }

        [Fact]
        public void TestSqlCommandCloneColumnEncryptionSettingUnSpecified()
        {
            using (SqlConnection sqlConnection = new SqlConnection())
            {
                // Use a constructor that does NOT set column encryption setting and make sure in this case, the value is UseConnectionSetting.
                using (SqlCommand sqlCommand1 = new SqlCommand(
                    cmdText: "Data source = localhost; Connect Timeout = 65534;",
                    connection: sqlConnection,
                    transaction: null))
                {
                    Assert.Equal(SqlCommandColumnEncryptionSetting.UseConnectionSetting, sqlCommand1.ColumnEncryptionSetting);

                    // Clone the above sqlCommand1 and verify the Column Encryption Setting is set to the default value correctly.
                    using (SqlCommand sqlCommand2 = sqlCommand1.Clone())
                    {
                        Assert.Equal(SqlCommandColumnEncryptionSetting.UseConnectionSetting, sqlCommand2.ColumnEncryptionSetting);
                    }
                }
            }
        }
    }
}
