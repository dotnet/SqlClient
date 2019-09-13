// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class SqlConnectionShould
    {
        #region PositiveColumnEncryptionSettingFromConnectionString
        public static readonly object[][] PositiveColumnEncryptionSettingFromConnectionStringDataObjects =
        {
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column Encryption Setting=enaBled ;", true},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column Encryption Setting= enaBled;", true},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column Encryption Setting=Enabled;", true},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column Encryption Setting=disABLEd   ;", false},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column Encryption Setting=  disaBled;", false},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column Encryption Setting=disaBled;", false},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column Encryption Setting=Disabled;", false},
            new object[] {"Data Source=localhost; Initial Catalog = testdb;", false}
        };

        [Theory]
        [MemberData(nameof(PositiveColumnEncryptionSettingFromConnectionStringDataObjects))]
        public void ProperlyTranslateColumnEncryptionSettingFromConnectionStringViaConstructor(string connectionString, bool expectedColumnEncryptionSetting)
        {
            using (SqlConnection sqlConnection = new SqlConnection(connectionString))
            {
                bool actualColumnEncryptionSetting = (bool)typeof(SqlConnection)
                    .GetProperty("IsColumnEncryptionSettingEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sqlConnection);
                Assert.Equal(expectedColumnEncryptionSetting, actualColumnEncryptionSetting);
            }
        }


        [Theory]
        [MemberData(nameof(PositiveColumnEncryptionSettingFromConnectionStringDataObjects))]
        public void ProperlyTranslateColumnEncryptionSettingFromConnectionStringViaProperty(string connectionString, bool expectedColumnEncryptionSetting)
        {
            using (SqlConnection sqlConnection = new SqlConnection())
            {
                sqlConnection.ConnectionString = connectionString;
                bool actualColumnEncryptionSetting = (bool)typeof(SqlConnection)
                    .GetProperty("IsColumnEncryptionSettingEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sqlConnection);
                Assert.Equal(expectedColumnEncryptionSetting, actualColumnEncryptionSetting);
            }
        }
        #endregion PositiveColumnEncryptionSettingFromConnectionString

        #region NegativeColumnEncryptionSettingFromConnectionString
        public static readonly object[][] NegativeColumnEncryptionSettingFromConnectionStringDataObjects =
        {
            new object[] {"Data Source=localhost; Initial Catalog = testdb; ColumnEncryptionSetting=enaBled ;"},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column EncryptionSetting= enaBled;"},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column EncryptionSetting=disABLEd   ;"},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column EncryptionSetting=  disaBled;"},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; ColumnEncryption Setting=disaBled;"},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column EncryptionSetting=Enabled;"},
            new object[] {"Data Source=localhost; Initial Catalog = testdb; Column EncryptionSetting=Disabled;"}
        };

        [Theory]
        [MemberData(nameof(NegativeColumnEncryptionSettingFromConnectionStringDataObjects))]
        public void NotAllowMalformedColumnEncryptionSettingConnectionStringKeywordInConstructor(string connectionString)
        {
            Assert.Throws<ArgumentException>(() => new SqlConnection(connectionString));
        }

        [Theory]
        [MemberData(nameof(NegativeColumnEncryptionSettingFromConnectionStringDataObjects))]
        public void NotAllowMalformedColumnEncryptionSettingConnectionStringKeywordInPropertySetter(string connectionString)
        {
            Assert.Throws<ArgumentException>(() => new SqlConnection().ConnectionString = connectionString);
        }
        #endregion NegativeColumnEncryptionSettingFromConnectionString

        [Theory]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.ResultSetOnly, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.ResultSetOnly, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.ResultSetOnly, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.ResultSetOnly, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Enabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.ResultSetOnly, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.ResultSetOnly, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.ResultSetOnly, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.ResultSetOnly, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting, SqlCommandColumnEncryptionSetting.Enabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting, SqlCommandColumnEncryptionSetting.Disabled)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting, SqlCommandColumnEncryptionSetting.ResultSetOnly)]
        [InlineData(SqlConnectionColumnEncryptionSetting.Disabled, SqlCommandColumnEncryptionSetting.UseConnectionSetting, SqlCommandColumnEncryptionSetting.UseConnectionSetting)]
        public void TestSqlCommandSetColumnEncryptionSetting(
            SqlConnectionColumnEncryptionSetting sqlConnectionColumnEncryptionSetting,
            SqlCommandColumnEncryptionSetting sqlCommandColumnEncryptionSetting_1,
            SqlCommandColumnEncryptionSetting sqlCommandColumnEncryptionSetting_2
        )
        {
            MethodInfo SetColumnEncryptionSettingMethod = typeof(SqlCommand).GetMethod("SetColumnEncryptionSetting", BindingFlags.Instance | BindingFlags.NonPublic);

            string[] connectionStrings = {
                string.Format(@"Data source = localhost; Column Encryption Setting = {0}", sqlConnectionColumnEncryptionSetting).ToString(),
                string.Format(@"Data source = localhost; Column Encryption Setting = {0}; Enclave Attestation Url=www.foo.com; ", sqlConnectionColumnEncryptionSetting).ToString()
            };

            foreach (var connectionString in connectionStrings)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connectionString))
                {
                    using (SqlCommand sqlCommand = new SqlCommand(@"select 1", sqlConnection, transaction: null, columnEncryptionSetting: sqlCommandColumnEncryptionSetting_1))
                    {
                        // Set the first column encryption setting.
                        SetColumnEncryptionSettingMethod.Invoke(sqlCommand, new object[] { sqlCommandColumnEncryptionSetting_1 });

                        // Simulate setting of the second column encryption setting. If its the same setting, it should succeed.
                        // If its different than the one used before, it should throw an exception.
                        if (sqlCommandColumnEncryptionSetting_1 == sqlCommandColumnEncryptionSetting_2)
                        {
                            SetColumnEncryptionSettingMethod.Invoke(sqlCommand, new object[] { sqlCommandColumnEncryptionSetting_2 });
                            Assert.Equal(sqlCommandColumnEncryptionSetting_1, sqlCommand.ColumnEncryptionSetting);
                        }
                        else
                        {
                            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => SetColumnEncryptionSettingMethod.Invoke(sqlCommand, new object[] { sqlCommandColumnEncryptionSetting_2 }));
                            string expectedMessage = "SqlCommandColumnEncryptionSetting should be identical on all commands (SelectCommand, InsertCommand, UpdateCommand, DeleteCommand) when doing batch updates.";
                            Assert.Equal(expectedMessage, exception.InnerException.Message);
                        }
                    }
                }
            }
        }
    }
}
