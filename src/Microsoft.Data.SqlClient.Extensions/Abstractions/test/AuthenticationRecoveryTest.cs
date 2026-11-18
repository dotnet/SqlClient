// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Configuration;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Tier = Microsoft.Data.SqlClient.SqlAuthenticationProviderRegistry.RegistrationTier;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Protects explicit configuration intent and nonblocking callback access.</summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class AuthenticationRecoveryTest : IDisposable
{
    private static Action? s_initialize;

    public void Dispose() => s_initialize = null;

    /// <summary>Failed explicit entries reserve their methods against defaults, not recovery.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailedProviderBlocksDefaultButAllowsApplicationRecovery(bool unsupported)
    {
        var registry = new SqlAuthenticationProviderRegistry();
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.Providers.Add(new ProviderSettings("Active Directory Interactive",
            (unsupported ? typeof(UnsupportedProvider) : typeof(FailingProvider))
                .AssemblyQualifiedName));

        registry.ApplyConfiguration(new SqlAuthenticationConfiguration(section));

        var failures = GetFailures(registry);
        var failure = Assert.IsType<ExceptionDispatchInfo>(
            failures[SqlAuthenticationMethod.ActiveDirectoryInteractive]);
        if (unsupported)
        {
            Assert.IsType<NotSupportedException>(failure.SourceException);
        }
        else
        {
            Assert.IsType<InvalidOperationException>(
                Assert.IsType<TargetInvocationException>(failure.SourceException).InnerException);
        }
        Assert.False(registry.Register(SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new Provider(), Tier.Default));
        Assert.True(registry.Register(SqlAuthenticationMethod.ActiveDirectoryIntegrated,
            new Provider(), Tier.Default));
        Assert.True(registry.Register(SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new Provider()));
        Assert.False(failures.Contains(SqlAuthenticationMethod.ActiveDirectoryInteractive));
    }

    /// <summary>Failed explicit replacements permit deliberate recovery over the old Config tier.</summary>
    [Fact]
    public void FailedConfiguredReplacementCanBeRecoveredByApplication()
    {
        var registry = new SqlAuthenticationProviderRegistry();
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.Providers.Add(new ProviderSettings("Active Directory Interactive",
            typeof(FailingProvider).AssemblyQualifiedName));
        Assert.True(registry.Register(SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new Provider(), Tier.Config));

        registry.ApplyConfiguration(new SqlAuthenticationConfiguration(section));

        Assert.False(registry.Register(SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new Provider(), Tier.Default));
        Assert.True(registry.Register(SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new Provider()));
        Assert.False(GetFailures(registry).Contains(
            SqlAuthenticationMethod.ActiveDirectoryInteractive));
    }

    /// <summary>Public lookup hides a failed explicit replacement without throwing or losing its cause.</summary>
    [Fact]
    public void PublicLookupReturnsNullUntilFailedExplicitMethodIsRecovered()
    {
        const SqlAuthenticationMethod method = SqlAuthenticationMethod.ActiveDirectoryInteractive;
        SqlAuthenticationProvider.GetProvider(method);
        var registry = Assert.IsType<SqlAuthenticationProviderRegistry>(
            typeof(SqlAuthenticationProviderRegistry).GetField(
                "Instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        var providers = Assert.IsAssignableFrom<IDictionary>(
            typeof(SqlAuthenticationProviderRegistry).GetField(
                "_providers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry));
        IDictionary failures = GetFailures(registry);
        FieldInfo configuration = typeof(SqlAuthenticationProviderRegistry).GetField(
            "_configuration", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object? originalConfiguration = configuration.GetValue(registry);
        object? originalProvider = providers[method];
        object? originalFailure = failures[method];
        try
        {
            var old = new Provider();
            Assert.True(registry.Register(method, old, Tier.Config));
            var section = new SqlClientAuthenticationProviderConfigurationSection();
            section.Providers.Add(new ProviderSettings(
                "Active Directory Interactive", typeof(FailingProvider).AssemblyQualifiedName));
            registry.ApplyConfiguration(new SqlAuthenticationConfiguration(section));

            var failure = Assert.IsType<ExceptionDispatchInfo>(failures[method]);
            using var listener = new AuthenticationTraceListener();
            Assert.Null(SqlAuthenticationProvider.GetProvider(method));
            Assert.Same(failure, failures[method]);
            Assert.Contains(listener.Messages, message =>
                message.Contains("no fallback provider") &&
                message.Contains("Test activation failure"));
            var recovery = new Provider();
            Assert.True(SqlAuthenticationProvider.SetProvider(method, recovery));
            Assert.Same(recovery, SqlAuthenticationProvider.GetProvider(method));
        }
        finally
        {
            configuration.SetValue(registry, originalConfiguration);
            providers.Remove(method);
            failures.Remove(method);
            if (originalProvider is not null)
            {
                providers[method] = originalProvider;
            }
            if (originalFailure is not null)
            {
                failures[method] = originalFailure;
            }
        }
    }

    /// <summary>Failed initializers cannot be hidden by defaults for unknown intended methods.</summary>
    [Fact]
    public void FailedInitializerBlocksUnclaimedDefaultsButPreservesExplicitProviders()
    {
        var registry = new SqlAuthenticationProviderRegistry();
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(Initializer).AssemblyQualifiedName;
        s_initialize = () => throw new InvalidOperationException("Test initializer failure");
        section.Providers.Add(new ProviderSettings("Active Directory Integrated",
            typeof(Provider).AssemblyQualifiedName));

        registry.ApplyConfiguration(new SqlAuthenticationConfiguration(section));

        Assert.False(registry.Register(SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new Provider(), Tier.Default));
        Assert.False(registry.Register(SqlAuthenticationMethod.ActiveDirectoryIntegrated,
            new Provider()));
        Assert.True(registry.Register(SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new Provider()));
    }

    /// <summary>Replacement callbacks can synchronously wait for another thread's public lookup.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplacementCallbacksAllowCrossThreadLookup(bool beforeLoad)
    {
        const SqlAuthenticationMethod method = SqlAuthenticationMethod.SqlPassword;
        Task<SqlAuthenticationProvider?>? lookup = null;
        void Lookup()
        {
            lookup = Task.Run(() => SqlAuthenticationProvider.GetProvider(method));
            Assert.True(lookup.Wait(TimeSpan.FromSeconds(5)),
                "A replacement callback blocked a cross-thread lookup.");
        }
        var old = new Provider(beforeUnload: beforeLoad ? null : Lookup);
        var replacement = new Provider(beforeLoad: beforeLoad ? Lookup : null);
        Assert.True(SqlAuthenticationProvider.SetProvider(method, old));
        try
        {
            bool registered = SqlAuthenticationProvider.SetProvider(method, replacement);
            Assert.NotNull(lookup);
            Assert.True(lookup.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(registered);
            Assert.Same(old, lookup.Result);
            Assert.Same(replacement, SqlAuthenticationProvider.GetProvider(method));
        }
        finally
        {
            old.BeforeUnloadAction = null;
            replacement.BeforeLoadAction = null;
            Assert.True(SqlAuthenticationProvider.SetProvider(method, new Provider()));
        }
    }

    /// <summary>Bootstrap permits cross-thread access without waiting for its own callbacks.</summary>
    [Fact]
    public void BootstrapAllowsCrossThreadAccessWithoutRepeatingConfiguration()
    {
        using var switches = new AuthenticationFeatureSwitchesHelper
        {
            EnableAppConfig = true,
            EnableAzureExtensionDiscovery = false
        };
        var registry = new SqlAuthenticationProviderRegistry();
        Task? lookup = null;
        bool completedDuringCallback = false;
        registry.EnsureBootstrapped(_ =>
        {
            lookup = Task.Run(() => registry.EnsureBootstrapped(_ =>
                throw new InvalidOperationException("Bootstrap must not run twice.")));
            completedDuringCallback = lookup.Wait(TimeSpan.FromSeconds(5));
            return new SqlClientAuthenticationProviderConfigurationSection();
        });
        Assert.NotNull(lookup);
        Assert.True(lookup.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(completedDuringCallback,
            "Bootstrap blocked another thread while running a configuration callback.");
    }

    /// <summary>Only the initializer's own thread receives configuration registration precedence.</summary>
    [Fact]
    public void InitializerDoesNotPromoteOtherThreads()
    {
        var registry = new SqlAuthenticationProviderRegistry();
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(Initializer).AssemblyQualifiedName;
        s_initialize = () =>
        {
            Task<bool> registration = Task.Run(() => registry.Register(
                SqlAuthenticationMethod.ActiveDirectoryInteractive, new Provider()));
            Assert.True(registration.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(registration.Result);
            Assert.True(registry.Register(
                SqlAuthenticationMethod.ActiveDirectoryIntegrated, new Provider()));
        };

        registry.ApplyConfiguration(new SqlAuthenticationConfiguration(section));

        Assert.True(registry.Register(SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new Provider()));
        Assert.False(registry.Register(SqlAuthenticationMethod.ActiveDirectoryIntegrated,
            new Provider()));
    }

    /// <summary>A concurrent Config publication wins while another replacement runs its callbacks.</summary>
    [Fact]
    public void ConcurrentReplacementRechecksPrecedenceBeforePublication()
    {
        var registry = new SqlAuthenticationProviderRegistry();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Assert.True(registry.Register(SqlAuthenticationMethod.SqlPassword, new Provider()));
        Task<bool> replacement = Task.Run(() => registry.Register(
            SqlAuthenticationMethod.SqlPassword, new Provider(beforeLoad: () =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            })));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Task<bool> configured = Task.Run(() => registry.Register(
                SqlAuthenticationMethod.SqlPassword, new Provider(), Tier.Config));
            try
            {
                Assert.True(configured.Wait(TimeSpan.FromSeconds(5)),
                    "A replacement callback held the publication lock.");
                Assert.True(configured.Result);
            }
            finally
            {
                release.Set();
                Assert.True(configured.Wait(TimeSpan.FromSeconds(5)));
            }
            Assert.True(replacement.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(replacement.Result);
        }
        finally
        {
            release.Set();
            Assert.True(replacement.Wait(TimeSpan.FromSeconds(5)));
        }
    }

    /// <summary>Reads retained failures without adding a production diagnostics API.</summary>
    /// <param name="registry">The isolated registry whose failures to inspect.</param>
    /// <returns>The method-specific activation failures.</returns>
    private static IDictionary GetFailures(SqlAuthenticationProviderRegistry registry) =>
        Assert.IsAssignableFrom<IDictionary>(typeof(SqlAuthenticationProviderRegistry).GetField(
            "_configurationFailures", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(registry));

    /// <summary>Activates a test-controlled initializer without process-wide configuration.</summary>
    public sealed class Initializer : SqlAuthenticationInitializer
    {
        public override void Initialize() => s_initialize?.Invoke();
    }

    /// <summary>Accepts every method and optionally performs replacement callbacks.</summary>
    public sealed class Provider(
        Action? beforeLoad = null, Action? beforeUnload = null) : SqlAuthenticationProvider
    {
        public Provider() : this(null, null)
        {
        }

        internal Action? BeforeLoadAction = beforeLoad;
        internal Action? BeforeUnloadAction = beforeUnload;
        public override bool IsSupported(SqlAuthenticationMethod method) => true;
        public override void BeforeLoad(SqlAuthenticationMethod method) => BeforeLoadAction?.Invoke();
        public override void BeforeUnload(SqlAuthenticationMethod method) =>
            BeforeUnloadAction?.Invoke();
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }

    /// <summary>Represents an explicitly configured provider that rejects its method.</summary>
    public sealed class UnsupportedProvider : SqlAuthenticationProvider
    {
        public override bool IsSupported(SqlAuthenticationMethod method) => false;
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }

    /// <summary>Represents an explicitly configured provider that fails during construction.</summary>
    public sealed class FailingProvider : SqlAuthenticationProvider
    {
        public FailingProvider() => throw new InvalidOperationException("Test activation failure");
        public override bool IsSupported(SqlAuthenticationMethod method) => true;
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }
}
