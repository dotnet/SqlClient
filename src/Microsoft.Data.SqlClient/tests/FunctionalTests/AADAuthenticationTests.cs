// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
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

        [Fact]
        public void CustomActiveDirectoryProviderTest()
        {
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(CustomDeviceFlowCallback);
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
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(CustomDeviceFlowCallback, Guid.NewGuid().ToString());
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
            Assert.Equal(authProvider, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        }

        private Task CustomDeviceFlowCallback(DeviceCodeResult result)
        {
            Console.WriteLine(result.Message);
            return Task.FromResult(0);
        }
    }
}
