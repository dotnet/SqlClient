// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Tests for the <c>AuthenticationProviderRegistry</c> class.
///
/// Each test exercises an isolated registry instance (constructed via the internal constructor)
/// so there is no shared global state and the tests are safe to run in parallel.
/// </summary>
public class AuthenticationProviderRegistryTest
{
    #region GetProvider

    /// <summary>
    /// GetProvider returns null when no provider has been registered for the specified
    /// authentication method.
    /// </summary>
    [Fact]
    public void GetProvider_ReturnsNull_WhenNoProviderRegistered()
    {
        AuthenticationProviderRegistry registry = new();

        Assert.Null(registry.GetProvider(SqlAuthenticationMethod.SqlPassword));
    }

    /// <summary>
    /// GetProvider returns null for NotSpecified, which is never a valid registration target.
    /// </summary>
    [Fact]
    public void GetProvider_ReturnsNull_ForNotSpecified()
    {
        AuthenticationProviderRegistry registry = new();

        Assert.Null(registry.GetProvider(SqlAuthenticationMethod.NotSpecified));
    }

    /// <summary>
    /// Getting an existing provider works.
    /// </summary>
    [Fact]
    public void GetProvider_ReturnsSameInstance_AfterSetProvider()
    {
        AuthenticationProviderRegistry registry = new();
        DeviceCodeProvider provider = new();

        Assert.True(
            registry.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                provider));

        Assert.Same(
            provider,
            registry.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
    }

    #endregion

    #region SetProvider - Basic

    /// <summary>
    /// SetProvider throws NullReferenceException when a null provider is passed (current behavior,
    /// not a validated argument).
    /// </summary>
    [Fact]
    public void SetProvider_ThrowsOnNullProvider()
    {
        AuthenticationProviderRegistry registry = new();

        Assert.Throws<NullReferenceException>(() =>
            registry.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                null!));
    }

    /// <summary>
    /// SetProvider throws NotSupportedException when the provider does not support the specified
    /// authentication method, and the message names both the provider type and the method.
    /// </summary>
    [Fact]
    public void SetProvider_ThrowsOnUnsupportedMethod()
    {
        AuthenticationProviderRegistry registry = new();

        // DeviceCodeProvider only supports DeviceCodeFlow.
        DeviceCodeProvider provider = new();

        NotSupportedException ex = Assert.Throws<NotSupportedException>(() =>
            registry.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive,
                provider));

        Assert.Contains(nameof(DeviceCodeProvider), ex.Message);
        Assert.Contains(
            SqlAuthenticationMethod.ActiveDirectoryInteractive.ToString(),
            ex.Message);
    }

    /// <summary>
    /// SetProvider replaces a previously registered provider for the same authentication method.
    /// </summary>
    [Fact]
    public void SetProvider_ReplacesExistingProvider()
    {
        AuthenticationProviderRegistry registry = new();
        DeviceCodeProvider provider1 = new();
        DeviceCodeProvider provider2 = new();

        Assert.True(
            registry.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                provider1));

        Assert.Same(
            provider1,
            registry.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

        // Replace with provider2.
        Assert.True(
            registry.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                provider2));

        Assert.Same(
            provider2,
            registry.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
    }

    /// <summary>
    /// Distinct providers registered for distinct methods are keyed independently: each method
    /// returns its own provider, and a method that was never registered returns null. This guards
    /// against mis-keying or cross-method bleed in the backing store.
    /// </summary>
    [Fact]
    public void SetProvider_DistinctProvidersPerMethod_AreKeyedIndependently()
    {
        AuthenticationProviderRegistry registry = new();

        AllMethodsProvider integrated = new();
        AllMethodsProvider interactive = new();
        AllMethodsProvider deviceCode = new();

        Assert.True(registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated, integrated));
        Assert.True(registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, interactive));
        Assert.True(registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, deviceCode));

        // Each method returns its own provider -- no cross-talk.
        Assert.Same(integrated, registry.GetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated));
        Assert.Same(interactive, registry.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive));
        Assert.Same(deviceCode, registry.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

        // A method that was never registered returns null.
        Assert.Null(registry.GetProvider(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal));
    }

    #endregion


    #region SetProvider - Lifecycle callbacks

    /// <summary>
    /// The first registration for a method invokes BeforeLoad (immediately before the provider is
    /// added to the registry) but not BeforeUnload (there is no prior provider to unload).
    /// </summary>
    [Fact]
    public void SetProvider_FirstRegistration_InvokesBeforeLoad_NotBeforeUnload()
    {
        AuthenticationProviderRegistry registry = new();
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;

        RecordingProvider provider = new();

        Assert.True(registry.SetProvider(method, provider));
        Assert.Same(provider, registry.GetProvider(method));

        Assert.Equal([method], provider.BeforeLoadCalls);
        Assert.Empty(provider.BeforeUnloadCalls);
    }

    /// <summary>
    /// An exception thrown by BeforeLoad during the first registration (the add path, with no prior
    /// provider to override) is swallowed; the provider is still registered and SetProvider
    /// succeeds.
    /// </summary>
    [Fact]
    public void SetProvider_FirstRegistration_BeforeLoadThrows_IsSwallowed_AndRegistrationSucceeds()
    {
        AuthenticationProviderRegistry registry = new();
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;

        RecordingProvider provider = new(throwFromCallbacks: true);

        // No prior provider exists, so this takes the add path; BeforeLoad throws but is swallowed.
        Assert.True(registry.SetProvider(method, provider));
        Assert.Same(provider, registry.GetProvider(method));

        // The throwing BeforeLoad was actually invoked (before it threw); BeforeUnload was not
        // (there was nothing to unload).
        Assert.Equal([method], provider.BeforeLoadCalls);
        Assert.Empty(provider.BeforeUnloadCalls);
    }

    /// <summary>
    /// Replacing an existing provider invokes BeforeUnload on the old provider and BeforeLoad on
    /// the new provider, each for the affected method.
    /// </summary>
    [Fact]
    public void SetProvider_Replace_InvokesBeforeUnloadOnOld_AndBeforeLoadOnNew()
    {
        AuthenticationProviderRegistry registry = new();
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;

        RecordingProvider oldProvider = new();
        RecordingProvider newProvider = new();

        Assert.True(registry.SetProvider(method, oldProvider));

        // We see BeforeLoad called on oldProvider.
        Assert.Equal([method], oldProvider.BeforeLoadCalls);
        Assert.Empty(oldProvider.BeforeUnloadCalls);

        Assert.True(registry.SetProvider(method, newProvider));

        // We see BeforeUnload called on oldProvider, and its BeforeLoad calls are not repeated.
        Assert.Equal([method], oldProvider.BeforeLoadCalls);
        Assert.Equal([method], oldProvider.BeforeUnloadCalls);

        // We see BeforeLoad called on newProvider.
        Assert.Equal([method], newProvider.BeforeLoadCalls);
        Assert.Empty(newProvider.BeforeUnloadCalls);

        Assert.Same(newProvider, registry.GetProvider(method));
    }

    /// <summary>
    /// An exception thrown by the old provider's BeforeUnload callback is swallowed; the new
    /// provider is still registered and SetProvider succeeds.
    /// </summary>
    [Fact]
    public void SetProvider_Replace_BeforeUnloadThrows_IsSwallowed_AndRegistrationSucceeds()
    {
        AuthenticationProviderRegistry registry = new();
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;

        RecordingProvider oldProvider = new(throwFromCallbacks: true);
        RecordingProvider newProvider = new();

        Assert.True(registry.SetProvider(method, oldProvider));

        // We see BeforeLoad called on oldProvider.
        Assert.Equal([method], oldProvider.BeforeLoadCalls);
        Assert.Empty(oldProvider.BeforeUnloadCalls);

        // oldProvider.BeforeUnload throws, but the override still succeeds.
        Assert.True(registry.SetProvider(method, newProvider));
        Assert.Same(newProvider, registry.GetProvider(method));

        // The throwing BeforeUnload was actually invoked (before it threw).
        Assert.Equal([method], oldProvider.BeforeLoadCalls);
        Assert.Equal([method], oldProvider.BeforeUnloadCalls);

        // The new provider's BeforeLoad still ran.
        Assert.Equal([method], newProvider.BeforeLoadCalls);
        Assert.Empty(newProvider.BeforeUnloadCalls);
    }

    /// <summary>
    /// An exception thrown by the new provider's BeforeLoad callback is swallowed; the new provider
    /// is still registered and SetProvider succeeds.
    /// </summary>
    [Fact]
    public void SetProvider_Replace_BeforeLoadThrows_IsSwallowed_AndRegistrationSucceeds()
    {
        AuthenticationProviderRegistry registry = new();
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;

        RecordingProvider oldProvider = new();
        RecordingProvider newProvider = new(throwFromCallbacks: true);

        Assert.True(registry.SetProvider(method, oldProvider));

        // We see BeforeLoad called on oldProvider.
        Assert.Equal([method], oldProvider.BeforeLoadCalls);
        Assert.Empty(oldProvider.BeforeUnloadCalls);

        // newProvider.BeforeLoad throws, but the override still succeeds.
        Assert.True(registry.SetProvider(method, newProvider));
        Assert.Same(newProvider, registry.GetProvider(method));

        // The old provider's BeforeUnload still ran.
        Assert.Equal([method], oldProvider.BeforeLoadCalls);
        Assert.Equal([method], oldProvider.BeforeUnloadCalls);

        // The throwing BeforeLoad was actually invoked (before it threw).
        Assert.Equal([method], newProvider.BeforeLoadCalls);
        Assert.Empty(newProvider.BeforeUnloadCalls);
    }

    #endregion

    #region SetPermanentProvider

    /// <summary>
    /// SetPermanentProvider registers the provider, and GetProvider then returns
    /// that same instance.
    /// </summary>
    [Fact]
    public void SetPermanentProvider_ThenGetProvider_ReturnsSameInstance()
    {
        AuthenticationProviderRegistry registry = new();
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;

        AllMethodsProvider provider = new();
        registry.SetPermanentProvider(method, provider);

        Assert.Same(provider, registry.GetProvider(method));
    }

    /// <summary>
    /// A permanently registered provider takes precedence: a subsequent user
    /// SetProvider call for the same method returns false and does not replace
    /// it.
    /// </summary>
    [Fact]
    public void SetPermanentProvider_TakesPrecedence_OverUserSetProvider()
    {
        AuthenticationProviderRegistry registry = new();
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;

        AllMethodsProvider permanent = new();
        registry.SetPermanentProvider(method, permanent);

        Assert.Same(permanent, registry.GetProvider(method));

        // A user attempt to override the permanent provider fails.
        AllMethodsProvider userProvider = new();
        Assert.False(registry.SetProvider(method, userProvider));

        // The permanent provider is still in place.
        Assert.Same(permanent, registry.GetProvider(method));
    }

    /// <summary>
    /// SetPermanentProvider is last-in-wins: a later call for the same method
    /// unconditionally replaces the previously registered permanent provider,
    /// and the replacement remains non-overridable by SetProvider.
    /// </summary>
    [Fact]
    public void SetPermanentProvider_LastInWins()
    {
        AuthenticationProviderRegistry registry = new();
        const SqlAuthenticationMethod method =
            SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;

        AllMethodsProvider first = new();
        AllMethodsProvider second = new();

        registry.SetPermanentProvider(method, first);
        registry.SetPermanentProvider(method, second);

        // The second permanent registration replaced the first.
        Assert.Same(second, registry.GetProvider(method));

        // The replacement is still permanent: a user SetProvider is refused.
        Assert.False(registry.SetProvider(method, new AllMethodsProvider()));
        Assert.Same(second, registry.GetProvider(method));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A dummy provider that supports all authentication methods.
    /// </summary>
    private sealed class AllMethodsProvider : SqlAuthenticationProvider
    {
        /// <inheritDoc/>
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => true;

        /// <inheritDoc/>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// A dummy provider that only supports ActiveDirectoryDeviceCodeFlow.
    /// </summary>
    private sealed class DeviceCodeProvider : SqlAuthenticationProvider
    {
        /// <inheritDoc/>
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
            => authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;

        /// <inheritDoc/>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters)
            => Task.FromResult(
                new SqlAuthenticationToken(
                    "SampleAccessToken", DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    /// <summary>
    /// A provider that supports all methods and records every BeforeLoad and
    /// BeforeUnload invocation. When constructed with <c>throwFromCallbacks</c>,
    /// each callback throws after recording, so tests can verify the registry
    /// both invokes the callback and isolates its failure.
    /// </summary>
    private sealed class RecordingProvider : SqlAuthenticationProvider
    {
        private readonly bool _throwFromCallbacks;

        public RecordingProvider(bool throwFromCallbacks = false)
            => _throwFromCallbacks = throwFromCallbacks;

        public List<SqlAuthenticationMethod> BeforeLoadCalls { get; } = new();

        public List<SqlAuthenticationMethod> BeforeUnloadCalls { get; } = new();

        /// <inheritDoc/>
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => true;

        /// <inheritDoc/>
        public override void BeforeLoad(SqlAuthenticationMethod authenticationMethod)
        {
            BeforeLoadCalls.Add(authenticationMethod);
            if (_throwFromCallbacks)
            {
                throw new InvalidOperationException("BeforeLoad failed.");
            }
        }

        /// <inheritDoc/>
        public override void BeforeUnload(SqlAuthenticationMethod authenticationMethod)
        {
            BeforeUnloadCalls.Add(authenticationMethod);
            if (_throwFromCallbacks)
            {
                throw new InvalidOperationException("BeforeUnload failed.");
            }
        }

        /// <inheritDoc/>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters)
            => throw new NotImplementedException();
    }

    #endregion
}
