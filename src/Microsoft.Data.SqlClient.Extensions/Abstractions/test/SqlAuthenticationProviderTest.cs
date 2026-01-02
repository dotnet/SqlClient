// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

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

    /// <summary>
    /// Construct to confirm preconditions.
    /// </summary>
    public SqlAuthenticationProviderTest()
    {
        // Confirm that the MDS assembly is indeed not present.
        Assert.Throws<FileNotFoundException>(() => Assembly.Load(assemblyName));
    }

    #region Tests

    /// <summary>
    /// Test that GetProvider fails predictably when the MDS assembly can't be
    /// found.
    /// </summary>
    [Theory]
    #pragma warning disable CS0618 // Type or member is obsolete
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryPassword)]
    #pragma warning restore CS0618 // Type or member is obsolete
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryIntegrated)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryMSI)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryDefault)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)]
    public void GetProvider_NoMdsAssembly(SqlAuthenticationMethod method)
    {
        // GetProvider() should return null when the MDS assembly can't be
        // found.
        Assert.Null(SqlAuthenticationProvider.GetProvider(method));
    }

    /// <summary>
    /// Test that SetProvider fails predictably when the MDS assembly can't be
    /// found.
    /// </summary>
    [Theory]
    #pragma warning disable CS0618 // Type or member is obsolete
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryPassword)]
    #pragma warning restore CS0618 // Type or member is obsolete
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryIntegrated)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryMSI)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryDefault)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)]
    public void SetProvider_NoMdsAssembly(SqlAuthenticationMethod method)
    {
        // SetProvider() should return false when the MDS assembly can't be
        // found.
        Assert.False(
            SqlAuthenticationProvider.SetProvider(method, new Provider()));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A dummy provider that supports all authentication methods.
    /// </summary>
    private sealed class Provider : SqlAuthenticationProvider
    {
        /// <inheritDoc/>
        public override bool IsSupported(
            SqlAuthenticationMethod authenticationMethod)
        {
            return true;
        }

        /// <inheritDoc/>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters)
        {
            throw new NotImplementedException();
        }
    }
    
    #endregion
}
