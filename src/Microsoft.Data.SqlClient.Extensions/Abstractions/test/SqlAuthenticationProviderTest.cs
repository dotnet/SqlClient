// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Tests for the public <see cref="SqlAuthenticationProvider"/> static API, which delegates to
/// the shared <c>AuthenticationProviderRegistry.Instance</c> within the Abstractions assembly.
/// Registry behavior in isolation is covered by <c>AuthenticationProviderRegistryTest</c>.
/// </summary>
public class SqlAuthenticationProviderTest
{
    #region Test Setup

    /// <summary>
    /// Construct to confirm preconditions.
    /// </summary>
    public SqlAuthenticationProviderTest()
    {
        // Confirm that the MDS assembly is indeed not present.  This proves the
        // registry operates purely within the Abstractions assembly.
        Assert.Throws<FileNotFoundException>(
            () => Assembly.Load("Microsoft.Data.SqlClient"));
    }

    #endregion

    #region Tests

    /// <summary>
    /// The public static <see cref="SqlAuthenticationProvider"/> API delegates to the shared
    /// <see cref="AuthenticationProviderRegistry.Instance"/>, so reads and writes through the
    /// public API and the shared registry instance observe the same backing store.
    /// </summary>
    [Fact]
    public void PublicApi_DelegatesToSharedInstance()
    {
        // Use a method that no other test registers on the shared instance, so this cannot
        // interfere with other tests running in the same class.
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;

        DeviceCodeProvider provider = new();

        Assert.True(SqlAuthenticationProvider.SetProvider(method, provider));

        Assert.Same(provider, SqlAuthenticationProvider.GetProvider(method));

        // The public API and the shared registry instance agree.
        Assert.Same(provider, AuthenticationProviderRegistry.Instance.GetProvider(method));

        // Replacing via the internal API is reflected through both the public API and the shared
        // registry instance, confirming they observe the same backing store.
        DeviceCodeProvider replacement = new();

        Assert.True(AuthenticationProviderRegistry.Instance.SetProvider(method, replacement));

        Assert.Same(replacement, SqlAuthenticationProvider.GetProvider(method));
        Assert.Same(replacement, AuthenticationProviderRegistry.Instance.GetProvider(method));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A dummy provider that only supports ActiveDirectoryDeviceCodeFlow.
    /// </summary>
    private sealed class DeviceCodeProvider : SqlAuthenticationProvider
    {
        /// <inheritDoc/>
        public override bool IsSupported(
            SqlAuthenticationMethod authenticationMethod)
        {
            return authenticationMethod ==
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
        }

        /// <inheritDoc/>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters)
        {
            return Task.FromResult(
                new SqlAuthenticationToken(
                    "SampleAccessToken", DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }

    #endregion
}
