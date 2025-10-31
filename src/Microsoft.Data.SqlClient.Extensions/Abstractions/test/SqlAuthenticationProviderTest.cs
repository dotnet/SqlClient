// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

// Tests for the obsolete SqlAuthenticationProvider.GetProvider and SetProvider
// methods.
public class SqlAuthenticationProviderTest
{
    // Choose the MDS assembly name based on the build environment.
    // See the top-level Directory.Build.props for more information.
    #if (APPLY_MDS_ASSEMBLY_NAME_SUFFIX && NET)
    const string assemblyName = "Microsoft.Data.SqlClient.NetCore";
    #elif (APPLY_MDS_ASSEMBLY_NAME_SUFFIX && NETFRAMEWORK)
    const string assemblyName = "Microsoft.Data.SqlClient.NetFx";
    #else
    const string assemblyName = "Microsoft.Data.SqlClient";
    #endif

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
            () => Assembly.Load(assemblyName));

        Assert.Null(
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive));
    }

    // Test that SetProvider fails predictably when the MDS assembly can't be
    // found.
    [Fact]
    public void SetProvider()
    {
        // Confirm that the MDS assembly is indeed not present.
        Assert.Throws<FileNotFoundException>(
            () => Assembly.Load(assemblyName));

        Assert.False(
            SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryIntegrated,
                new Provider()));
    }
}
