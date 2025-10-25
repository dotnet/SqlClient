// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.FunctionalTests.DataCommon;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlAuthenticationProviderTest
    {
        #if NETFRAMEWORK
        // This test is only valid for .NET Framework

        // Overridden by app.config in this project
        [Theory]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
        public void DefaultAuthenticationProviders_Interactive(SqlAuthenticationMethod method)
        {
            Assert.IsType<DummySqlAuthenticationProvider>(SqlAuthenticationProviderManager.GetProvider(method));
        }
        
        #endif
    }
}
