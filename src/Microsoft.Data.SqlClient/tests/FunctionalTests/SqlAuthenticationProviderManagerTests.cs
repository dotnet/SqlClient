// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.FunctionalTests.DataCommon;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlAuthenticationProviderManagerTests
    {
        // The FunctionalTests project employs a .NET Framework app.config file
        // that configures a dummy authentication provider for
        // ActiveDirectoryInteractive authentication.  Verify that this is
        // respected.
        [ConditionalFact(typeof(TestUtility), nameof(TestUtility.IsNetFramework))]
        public void DefaultAuthenticationProviders_AppConfig()
        {
            // The provider for ActiveDirectoryInteractive should be our dummy
            // provider.
            Assert.IsType<DummySqlAuthenticationProvider>(
                SqlAuthenticationProvider.GetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryInteractive));

            // There should be no provider for other methods.  Spot-check a few.
            Assert.Null(SqlAuthenticationProvider.GetProvider(
                #pragma warning disable CS0618 // Type or member is obsolete
                SqlAuthenticationMethod.ActiveDirectoryPassword));
                #pragma warning restore CS0618 // Type or member is obsolete

            Assert.Null(SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryManagedIdentity));
        }

        // Verify that the dummy provider installed via app.config cannot be replaced.
        [ConditionalFact(typeof(TestUtility), nameof(TestUtility.IsNetFramework))]
        public void DefaultAuthenticationProviders_NoReplace()
        {
            // The provider for ActiveDirectoryInteractive should be our dummy
            // provider.
            Assert.IsType<DummySqlAuthenticationProvider>(
                SqlAuthenticationProvider.GetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryInteractive));

            // Try to add another provider for ActiveDirectoryInteractive.
            bool setResult = SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive,
                new TestProvider());

            // The set should have failed.
            Assert.False(setResult);

            // The dummy provider is still installed.
            Assert.IsType<DummySqlAuthenticationProvider>(
                SqlAuthenticationProvider.GetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryInteractive));
        }

        private class TestProvider : SqlAuthenticationProvider
        {
            public override async Task<SqlAuthenticationToken> AcquireTokenAsync(
                SqlAuthenticationParameters parameters)
            {
                throw new NotImplementedException();
            }

            public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
            {
                return authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive;
            }
        }
    }
}
