// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.FunctionalTests.DataCommon;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlAuthenticationProviderTest
    {
        [Theory]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryIntegrated)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryPassword)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryMSI)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryDefault)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)]
        public void DefaultAuthenticationProviders(SqlAuthenticationMethod method)
        {
            Assert.IsType<ActiveDirectoryAuthenticationProvider>(SqlAuthenticationProvider.GetProvider(method));
        }

        #if NETFRAMEWORK
        // This test is only valid for .NET Framework

        // Overridden by app.config in this project
        [Theory]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
        public void DefaultAuthenticationProviders_Interactive(SqlAuthenticationMethod method)
        {
            Assert.IsType<DummySqlAuthenticationProvider>(SqlAuthenticationProvider.GetProvider(method));
        }
        
        #endif
    }
}
