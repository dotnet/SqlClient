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
        public async Task IsDummySqlAuthenticationProviderSetByDefault()
        {
            var provider = SqlAuthenticationProviderManager.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive);

            Assert.NotNull(provider);
            Assert.Equal(typeof(DummySqlAuthenticationProvider), provider.GetType());

            var token = await provider.AcquireTokenAsync(null);
            Assert.Equal(token.AccessToken, DummySqlAuthenticationProvider.DUMMY_TOKEN_STR);
        }
#endif

        // Verify that we can get and set providers via both the Abstractions
        // package and Manager class interchangeably.
        //
        // This tests the dynamic assembly loading code in the Abstractions
        // package.
        [Fact]
        public void Abstractions_And_Manager_GetSetProvider_Equivalent()
        {
            // Set via Manager, get via both.
            DummySqlAuthenticationProvider provider1 = new();

            Assert.True(
                SqlAuthenticationProviderManager.SetProvider(
                    // GOTCHA: On .NET Framework, the dummy provider is already
                    // registered as the default provider for Interactive, so we
                    // use DeviceCodeFlow instead.
                    SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                    provider1));

            Assert.Same(
                provider1,
                SqlAuthenticationProviderManager.GetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

            Assert.Same(
                provider1,
                #pragma warning disable CS0618 // Type or member is obsolete
                SqlAuthenticationProvider.GetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
                #pragma warning restore CS0618 // Type or member is obsolete

            // Set via Abstractions, get via both.
            DummySqlAuthenticationProvider provider2 = new();

            Assert.True(
                #pragma warning disable CS0618 // Type or member is obsolete
                SqlAuthenticationProvider.SetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                    provider2));
                #pragma warning restore CS0618 // Type or member is obsolete

            Assert.Same(
                provider2,
                SqlAuthenticationProviderManager.GetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
            
            Assert.Same(
                provider2,
                #pragma warning disable CS0618 // Type or member is obsolete
                SqlAuthenticationProvider.GetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
                #pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        public void CustomActiveDirectoryProviderTest()
        {
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(static (result) => Task.CompletedTask);
            SqlAuthenticationProviderManager.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
            Assert.Same(authProvider, SqlAuthenticationProviderManager.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        }

        [Fact]
        public void CustomActiveDirectoryProviderTest_AppClientId()
        {
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(Guid.NewGuid().ToString());
            SqlAuthenticationProviderManager.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
            Assert.Same(authProvider, SqlAuthenticationProviderManager.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        }

        [Fact]
        public void CustomActiveDirectoryProviderTest_AppClientId_DeviceFlowCallback()
        {
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(static (result) => Task.CompletedTask, Guid.NewGuid().ToString());
            SqlAuthenticationProviderManager.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
            Assert.Same(authProvider, SqlAuthenticationProviderManager.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        }
    }
}
