// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationProviderTest
{
    [Fact]
    public void GetProvider()
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        Assert.Null(
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive));
        #pragma warning restore CS0618 // Type or member is obsolete
    }

    private sealed class Provider : SqlAuthenticationProvider
    {
        public override bool IsSupported(
            SqlAuthenticationMethod authenticationMethod)
        {
            return true;
        }

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public void SetProvider()
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        Assert.False(
            SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryIntegrated,
                new Provider()));
        #pragma warning restore CS0618 // Type or member is obsolete
    }
}
