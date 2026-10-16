// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Registration no longer depends on reflective access to a SqlClient assembly.</summary>
public class SqlAuthenticationProviderTest
{
    #region Test Setup

    /// <summary>
    /// Construct to confirm preconditions.
    /// </summary>
    public SqlAuthenticationProviderTest()
    {
        // Confirm that the MDS assembly is indeed not present.
        Assert.Throws<FileNotFoundException>(
            () => Assembly.Load("Microsoft.Data.SqlClient"));
    }

    #endregion

    #region Tests

    /// <summary>
    /// Without SqlClient, a method starts without a provider and supports direct registration and lookup.
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
    public void GetAndSetProvider_NoMdsAssembly(SqlAuthenticationMethod method)
    {
        Assert.Null(SqlAuthenticationProvider.GetProvider(method));

        var provider = new Provider();
        Assert.True(SqlAuthenticationProvider.SetProvider(method, provider));
        Assert.Same(provider, SqlAuthenticationProvider.GetProvider(method));
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
