// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.FunctionalTests.DataCommon;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class AADAuthenticationTests
    {
        private SqlConnectionStringBuilder _builder;
        private SqlCredential _credential = null;

        [Theory]
        [InlineData("Test combination of Access Token and IntegratedSecurity", new object[] { "Integrated Security", true })]
        [InlineData("Test combination of Access Token and User Id", new object[] { "UID", "sampleUserId" })]
        [InlineData("Test combination of Access Token and Password", new object[] { "PWD", "samplePassword" })]
        [InlineData("Test combination of Access Token and Credentials", new object[] { "sampleUserId" })]
        public void InvalidCombinationOfAccessToken(string description, object[] Params)
        {
            string _ = description; // Using C# Discards as workaround to the XUnit warning.
            _builder = new SqlConnectionStringBuilder
            {
                ["Data Source"] = "sample.database.windows.net"
            };

            if (Params.Length == 1)
            {
                SecureString password = new SecureString();
                password.MakeReadOnly();
                _credential = new SqlCredential(Params[0] as string, password);
            }
            else
            {
                _builder[Params[0] as string] = Params[1];
            }
            InvalidCombinationCheck(_credential);
        }


        private void InvalidCombinationCheck(SqlCredential credential)
        {
            using (SqlConnection connection = new SqlConnection(_builder.ConnectionString, credential))
            {
                Assert.Throws<InvalidOperationException>(() => connection.AccessToken = "SampleAccessToken");
            }
        }
        
#if NETFRAMEWORK
        // This test is only valid for .NET Framework

        /// <summary>
        /// Tests whether SQL Auth provider is overridden using app.config file.
        /// This use case is only supported for .NET Framework applications, as driver doesn't support reading configuration from appsettings.json file.
        /// In future if need be, appsettings.json support can be added.
        /// </summary>
        [Fact]
        [ActiveIssue("3648")] // Cannot enable on CI/CD pipeline due to limitation of loading app.config file for tests.
        public async Task IsDummySqlAuthenticationProviderSetByDefault()
        {
            var provider = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive);

            Assert.NotNull(provider);
            Assert.Equal(typeof(DummySqlAuthenticationProvider), provider.GetType());

            var token = await provider.AcquireTokenAsync(null);
            Assert.Equal(token.AccessToken, DummySqlAuthenticationProvider.DUMMY_TOKEN_STR);
        }
#endif

        [Fact]
        public void CustomActiveDirectoryProviderTest()
        {
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(static (result) => Task.CompletedTask);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
            Assert.Equal(authProvider, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        }

        [Fact]
        public void CustomActiveDirectoryProviderTest_AppClientId()
        {
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(Guid.NewGuid().ToString());
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
            Assert.Equal(authProvider, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        }

        [Fact]
        public void CustomActiveDirectoryProviderTest_AppClientId_DeviceFlowCallback()
        {
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(static (result) => Task.CompletedTask, Guid.NewGuid().ToString());
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
            Assert.Equal(authProvider, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        }
    }
}
