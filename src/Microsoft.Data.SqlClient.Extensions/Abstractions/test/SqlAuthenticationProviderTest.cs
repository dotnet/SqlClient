// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

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
    /// Test that SetProvider succeeds even when the MDS assembly can't be
    /// found.  The providers are buffered and will be replayed once the
    /// core SqlClient assembly registers its provider manager via
    /// <see cref="SqlAuthenticationProvider.RegisterProviderManager"/>.
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
        // SetProvider() should return true — the provider is buffered for
        // replay once the core assembly registers via RegisterProviderManager.
        Assert.True(
            SqlAuthenticationProvider.SetProvider(method, new Provider()));
    }

    /// <summary>
    /// Test that providers registered via SetProvider before
    /// RegisterProviderManager is called are buffered and replayed
    /// once the manager registers its callbacks.
    /// </summary>
    [Fact]
    public void SetProvider_BufferedAndReplayed()
    {
        // Arrange: register a provider while no manager callbacks
        // are wired up (MDS assembly is not present in this test
        // project).
        var provider = new Provider();
        var method =
            SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;

        Assert.True(
            SqlAuthenticationProvider.SetProvider(
                method, provider));

        // Before replay, GetProvider returns null (no callback,
        // and the reflection-based Internal.GetProvider also
        // returns null since MDS is absent).
        Assert.Null(
            SqlAuthenticationProvider.GetProvider(method));

        // Act: simulate the core assembly registering its manager
        // callbacks by calling RegisterProviderManager with simple
        // dictionary-backed delegates.
        var store = new Dictionary<SqlAuthenticationMethod,
            SqlAuthenticationProvider>();

        SqlAuthenticationProvider.RegisterProviderManager(
            m => store.TryGetValue(m, out var p) ? p : null,
            (m, p) => { store[m] = p; return true; });

        // Assert: the buffered provider was replayed into the
        // store and is now retrievable via GetProvider.
        Assert.Same(provider,
            SqlAuthenticationProvider.GetProvider(method));
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
