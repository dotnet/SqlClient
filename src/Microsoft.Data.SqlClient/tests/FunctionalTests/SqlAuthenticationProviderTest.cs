// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlAuthenticationProviderTest
    {
        [Theory]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryIntegrated)]
        #pragma warning disable 0618
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryPassword)]
        #pragma warning restore 0618
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
    }
}
