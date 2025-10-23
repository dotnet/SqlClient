// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

// Tests for the obsolete SqlAuthenticationProvider.GetProvider and SetProvider
// methods.
public class SqlAuthenticationProviderTest
{
    // A dummy provider that supports all authentication methods.
    private sealed class Provider : SqlAuthenticationProvider
    {
        public override bool IsSupported(
            SqlAuthenticationMethod authenticationMethod)
        {
            return true;
        }

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters)
        {
            throw new NotImplementedException();
        }
    }

    // Test that GetProvider fails predictably when the MDS assembly can't be
    // found.
    [Fact]
    public void GetProvider()
    {
        // Confirm that the MDS assembly is indeed not present.
        Assert.Throws<FileNotFoundException>(
            () => Assembly.Load("Microsoft.Data.SqlClient"));

#pragma warning disable CS0618 // Type or member is obsolete
        Assert.Null(
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive));
#pragma warning restore CS0618 // Type or member is obsolete
    }

    // Test that SetProvider fails predictably when the MDS assembly can't be
    // found.
    [Fact]
    public void SetProvider()
    {
        // Confirm that the MDS assembly is indeed not present.
        Assert.Throws<FileNotFoundException>(
            () => Assembly.Load("Microsoft.Data.SqlClient"));

#pragma warning disable CS0618 // Type or member is obsolete
        Assert.False(
            SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryIntegrated,
                new Provider()));
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
