// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Validates errors and callback compatibility of the direct provider registry.</summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class AuthenticationRegistryTest
{
    /// <summary>Application registration remains usable after failure without overriding successful configuration registrations.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedBootstrapFailureAllowsApplicationRegistration(bool configuredProvider)
    {
        var registry = Assert.IsType<SqlAuthenticationProviderRegistry>(
            typeof(SqlAuthenticationProviderRegistry).GetField(
                "Instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        FieldInfo failureField = typeof(SqlAuthenticationProviderRegistry).GetField(
            "_bootstrapFailure", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var providers = Assert.IsAssignableFrom<IDictionary>(
            typeof(SqlAuthenticationProviderRegistry).GetField(
                "_providers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry));
        const SqlAuthenticationMethod method = SqlAuthenticationMethod.ActiveDirectoryDefault;
        object? originalFailure = failureField.GetValue(registry);
        object? originalProvider = providers[method];
        var failure = ExceptionDispatchInfo.Capture(new ArgumentException(
            "Test configured initializer failure", new InvalidOperationException("Test activation failure")));
        using var listener = new AuthenticationTraceListener();
        providers.Remove(method);
        failureField.SetValue(registry, failure);
        try
        {
            Assert.Null(SqlAuthenticationProvider.GetProvider(method));
            var provider = new Provider(true);
            var configured = new Provider(true);
            if (configuredProvider)
            {
                Assert.True(registry.Register(
                    method, configured, SqlAuthenticationProviderRegistry.RegistrationTier.Config));
            }

            Assert.Equal(!configuredProvider, SqlAuthenticationProvider.SetProvider(method, provider));
            SqlAuthenticationProvider expected = configuredProvider ? configured : provider;
            Assert.Same(expected, SqlAuthenticationProvider.GetProvider(method));
            Assert.Same(failure, failureField.GetValue(registry));
            Assert.False(SqlAuthenticationProvider.SetProvider(method, new Provider(false)));
            Assert.Same(expected, SqlAuthenticationProvider.GetProvider(method));
            Assert.Contains(listener.Messages, message =>
                message.Contains("Test configured initializer failure") &&
                message.Contains("application registration"));
        }
        finally
        {
            failureField.SetValue(registry, originalFailure);
            if (originalProvider is null)
            {
                providers.Remove(method);
            }
            else
            {
                providers[method] = originalProvider;
            }
        }
    }

    /// <summary>An installed legacy Azure contract reports the required upgrade rather than a missing package.</summary>
    [Fact]
    public void LegacyAzureContractIsTracedWithUpgradeGuidance()
    {
        using var listener = new AuthenticationTraceListener();
        var registry = new SqlAuthenticationProviderRegistry();

        registry.LoadAzureExtension(() => new LegacyAzureAssembly());

        Assert.Contains(listener.Messages, message =>
            message.Contains("ActiveDirectoryAuthenticationProviderOptions") &&
            message.Contains("Upgrade") && message.Contains("1.1.0"));
        Assert.True(registry.Register(SqlAuthenticationMethod.ActiveDirectoryDefault, new Provider(true)));
    }

    /// <summary>Discovery reports attempts, not installation, when existing defaults reject it.</summary>
    [Fact]
    public void AzureDiscoveryTraceDoesNotClaimRejectedRegistrationsSucceeded()
    {
        // Populate every default slot, then observe a second discovery where all attempts lose.
        var registry = new SqlAuthenticationProviderRegistry();
        using (var firstListener = new AuthenticationTraceListener())
        {
            registry.LoadAzureExtension(() => typeof(ThrowingProvider).Assembly);
            Assert.Contains(
                "Authentication provider registered for ActiveDirectoryIntegrated: provider " +
                "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider, tier Default.",
                firstListener.Messages);
        }

        using var listener = new AuthenticationTraceListener();
        registry.LoadAzureExtension(() => typeof(ThrowingProvider).Assembly);

        // Rejections may be traced, but none may be reported as successful installation.
        Assert.Contains(listener.Messages, message =>
            message.Contains("existing tier Default takes precedence over Default"));
        Assert.DoesNotContain(listener.Messages, message =>
            message.StartsWith("Authentication provider registered for ", StringComparison.Ordinal));
        Assert.Contains(
            "SqlAuthenticationProviderRegistry: Attempted to register Azure extension " +
            "class=Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider as the default " +
            "for Active Directory authentication methods; existing registrations were preserved",
            listener.Messages);
    }

    /// <summary>Deferred trace formatting preserves configuration diagnostics and broker values.</summary>
    [Theory]
    [InlineData("true", "Received user-defined UseWamBroker=True.")]
    [InlineData("false", "Received user-defined UseWamBroker=False.")]
    [InlineData("", "No user-defined UseWamBroker found.")]
    public void ConfigurationTraceMessagesPreserveText(string brokerValue, string brokerMessage)
    {
        using var listener = new AuthenticationTraceListener();
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["useWamBroker"].Value = brokerValue;

        // Avoid changing process-wide configuration or registering providers in the shared registry.
        new SqlAuthenticationProviderRegistry().ApplyConfiguration(
            new SqlAuthenticationConfiguration(section));

        // Values are traced during parsing; activation diagnostics remain the registry's concern.
        Assert.Contains("SqlAuthenticationConfiguration | " + brokerMessage, listener.Messages);
        Assert.Contains(
            "SqlAuthenticationConfiguration | No user-defined Application Client Id found.",
            listener.Messages);
        Assert.Contains(
            "SqlAuthenticationProviderRegistry | No user-defined SqlAuthenticationInitializer found.",
            listener.Messages);
        Assert.Contains(
            "SqlAuthenticationProviderRegistry | No user-defined auth providers.",
            listener.Messages);
    }

    /// <summary>Registration errors inside configured initializers must abort configuration.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InitializerRegistrationFailuresAreNotSwallowed(bool nullProvider)
    {
        // Complete shared bootstrap before simulating the narrower configured-initializer context.
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.NotSpecified);
        var registry = Assert.IsType<SqlAuthenticationProviderRegistry>(
            typeof(SqlAuthenticationProviderRegistry).GetField(
                "Instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        FieldInfo invoking = typeof(SqlAuthenticationProviderRegistry).GetField(
            "_invokingConfiguredInitializer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        bool original = Assert.IsType<bool>(invoking.GetValue(registry));

        // Simulate callback context on the shared registry, restoring it even if an assertion fails.
        invoking.SetValue(registry, true);
        try
        {
            Exception? failure = Record.Exception(() => SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.NotSpecified, nullProvider ? null! : new Provider(false)));
            if (nullProvider)
            {
                Assert.IsType<ArgumentNullException>(failure);
            }
            else
            {
                Assert.IsType<NotSupportedException>(failure);
            }
        }
        finally
        {
            invoking.SetValue(registry, original);
        }
    }

    /// <summary>Unsupported registrations preserve the false result and explain how to fix the provider.</summary>
    [Fact]
    public void UnsupportedProviderReturnsFalseAndLogs()
    {
        // Outside initialization, a support rejection must return false and emit a trace.
        using var listener = new AuthenticationTraceListener();
        Assert.False(SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.NotSpecified, new Provider(false)));
        Assert.Contains(listener.Messages, message =>
            message.Contains("SetProvider") && message.Contains("NotSpecified") &&
            message.Contains("returning false") && message.Contains("supports"));
    }

    /// <summary>Null registrations preserve the false result and explain the required argument.</summary>
    [Fact]
    public void NullProviderReturnsFalseAndLogs()
    {
        // Null is a caller error, but outside initialization registration remains non-throwing.
        using var listener = new AuthenticationTraceListener();
        Assert.False(SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.NotSpecified, null!));
        Assert.Contains(listener.Messages, message =>
            message.Contains("SetProvider") && message.Contains("returning false") &&
            message.Contains("non-null"));
    }

    /// <summary>Provider callback failures are logged without replacing the existing registration.</summary>
    [Theory]
    [InlineData("IsSupported")]
    [InlineData("BeforeLoad")]
    [InlineData("BeforeUnload")]
    public void CallbackFailureReturnsFalseAndPreservesProvider(string callback)
    {
        // Place the failure on the old or replacement provider according to callback ownership.
        using var listener = new AuthenticationTraceListener();
        var first = new ThrowingProvider(callback == "BeforeUnload" ? callback : null);
        var replacement = new ThrowingProvider(callback == "BeforeUnload" ? null : callback);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, first));

        // Publication must wait until support validation and both replacement callbacks succeed.
        Assert.False(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, replacement));

        Assert.Same(first, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.NotSpecified));
        Assert.Contains(listener.Messages, message =>
            message.Contains("SetProvider") && message.Contains(callback) &&
            message.Contains("returning false") && message.Contains("callbacks"));
        first.Throw = false;
        // A later valid replacement must succeed; the failed attempt must not claim registration.
        Assert.Single(listener.Messages, message =>
            message.StartsWith(
                "Authentication provider registered for NotSpecified:", StringComparison.Ordinal));
        var valid = new Provider(true);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, valid));
        Assert.Same(valid, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.NotSpecified));
    }

    /// <summary>The registry preserves initial insertion and replacement callback behavior.</summary>
    [Fact]
    public void ReplacementInvokesCallbacks()
    {
        // First insertion has no replacement callbacks; replacing it unloads old and loads new.
        using var listener = new AuthenticationTraceListener();
        var first = new Provider(true);
        var second = new Provider(true);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, first));
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, second));
        Assert.Equal(1, first.Unloads);
        Assert.Equal(1, second.Loads);
        Assert.Same(second, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.SqlPassword));
        Assert.Contains(
            "Authentication provider registered for SqlPassword: provider " +
            typeof(Provider).FullName + ", tier Application.",
            listener.Messages);
    }

    /// <summary>Fatal failures in later aggregate entries propagate from every registration callback without replacing the provider.</summary>
    [Theory]
    [InlineData("IsSupported")]
    [InlineData("BeforeLoad")]
    [InlineData("BeforeUnload")]
    public void AggregateCallbackFailurePropagatesAndPreservesProvider(string callback)
    {
        // Put a fatal error after an ordinary aggregate entry to defeat first-inner-only checks.
        var failure = new AggregateException(
            new InvalidOperationException("Ordinary failure"), new OutOfMemoryException("Test fatal failure"));
        var first = new ThrowingProvider(callback == "BeforeUnload" ? callback : null, failure);
        var replacement = new ThrowingProvider(callback == "BeforeUnload" ? null : callback, failure);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, first));

        try
        {
            Assert.Same(failure, Assert.Throws<AggregateException>(() =>
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, replacement)));
            Assert.Same(first, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.NotSpecified));
        }
        finally
        {
            // Restore a harmless shared provider even when an assertion fails.
            first.Throw = false;
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.NotSpecified, new Provider(true)));
        }
    }

    /// <summary>Application-provider support checks can wait for another thread to look up the existing provider.</summary>
    [Fact]
    public void SupportValidationAllowsCrossThreadLookup()
    {
        // IsSupported waits for another thread's lookup, exposing any lock held around validation.
        var first = new Provider(true);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, first));
        var replacement = new LookupProvider();

        bool registered = SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, replacement);

        // The worker must see the old provider before the replacement is published.
        Assert.NotNull(replacement.Lookup);
        Assert.True(replacement.Lookup.Wait(TimeSpan.FromSeconds(10)), "Cross-thread lookup did not complete.");
        Assert.True(registered, "Support validation blocked a cross-thread registry lookup.");
        Assert.Same(first, replacement.Lookup.Result);
        Assert.Equal(1, replacement.SupportChecks);
        Assert.Same(replacement, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.SqlPassword));
    }

    /// <summary>Nested providers are unloaded before a reentrant outer replacement is published.</summary>
    [Theory]
    [InlineData("BeforeLoad")]
    [InlineData("BeforeUnload")]
    public void ReentrantReplacementUnloadsNestedProvider(string callback)
    {
        var nested = new Provider(true);
        bool nestedRegistrationAttempted = false;
        bool nestedRegistrationSucceeded = false;
        void RegisterNested(SqlAuthenticationMethod method)
        {
            if (!nestedRegistrationAttempted)
            {
                // Set the guard before reentering callbacks so the retry can converge.
                nestedRegistrationAttempted = true;
                nestedRegistrationSucceeded = SqlAuthenticationProvider.SetProvider(method, nested);
            }
        }

        var first = new Provider(true, beforeUnload: callback == "BeforeUnload" ? RegisterNested : null);
        var replacement = new Provider(true, beforeLoad: callback == "BeforeLoad" ? RegisterNested : null);
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, first));

        try
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, replacement));

            Assert.True(nestedRegistrationAttempted);
            Assert.True(nestedRegistrationSucceeded);
            Assert.Same(replacement, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.SqlPassword));
            Assert.Equal(1, nested.Loads);
            // The nested provider must be cleaned up even though the outer registration wins.
            Assert.Equal(1, nested.Unloads);
        }
        finally
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, new Provider(true)));
        }
    }

    /// <summary>Re-registering the same provider instance still requires replacement callbacks to retry.</summary>
    [Fact]
    public void ReentrantReplacementDetectsSameProviderReregistration()
    {
        var first = new Provider(true);
        bool registeredAgain = false;
        var replacement = new Provider(true, beforeLoad: method =>
        {
            if (!registeredAgain)
            {
                registeredAgain = true;
                Assert.True(SqlAuthenticationProvider.SetProvider(method, first));
            }
        });
        // Force the populated-slot path, then exclude its setup callback from the measured loads.
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, new Provider(true)));
        Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, first));
        first.Loads = 0;

        try
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, replacement));

            Assert.True(registeredAgain);
            Assert.Equal(1, first.Loads);
            // The outer attempt, nested registration and retry each unload the original provider.
            Assert.Equal(3, first.Unloads);
            Assert.Same(replacement, SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.SqlPassword));
        }
        finally
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.SqlPassword, new Provider(true)));
        }
    }

    /// <summary>Nested configuration registrations retain precedence over an outer application replacement.</summary>
    [Theory]
    [InlineData("BeforeLoad")]
    [InlineData("BeforeUnload")]
    public void ReentrantReplacementPreservesHigherPrecedence(string callback)
    {
        // Keep Config-tier registrations out of the process-wide registry used by other tests.
        var registry = new SqlAuthenticationProviderRegistry();
        var nested = new Provider(true);
        bool registeredNested = false;
        void RegisterNested(SqlAuthenticationMethod method)
        {
            if (!registeredNested)
            {
                registeredNested = true;
                Assert.True(registry.Register(
                    method, nested, SqlAuthenticationProviderRegistry.RegistrationTier.Config));
            }
        }

        var first = new Provider(true, beforeUnload: callback == "BeforeUnload" ? RegisterNested : null);
        var replacement = new Provider(true, beforeLoad: callback == "BeforeLoad" ? RegisterNested : null);
        Assert.True(registry.Register(SqlAuthenticationMethod.SqlPassword, first));

        // Rechecking precedence must leave the nested Config provider registered and loaded.
        Assert.False(registry.Register(SqlAuthenticationMethod.SqlPassword, replacement));
        Assert.True(registeredNested);
        Assert.Equal(1, nested.Loads);
        Assert.Equal(0, nested.Unloads);
        Assert.False(registry.Register(SqlAuthenticationMethod.SqlPassword, new Provider(true)));
    }

    private sealed class Provider(
        bool supported,
        Action<SqlAuthenticationMethod>? beforeLoad = null,
        Action<SqlAuthenticationMethod>? beforeUnload = null) : SqlAuthenticationProvider
    {
        internal int Loads;
        internal int Unloads;
        public override bool IsSupported(SqlAuthenticationMethod method) => supported;
        public override void BeforeLoad(SqlAuthenticationMethod method)
        {
            Loads++;
            beforeLoad?.Invoke(method);
        }
        public override void BeforeUnload(SqlAuthenticationMethod method)
        {
            Unloads++;
            beforeUnload?.Invoke(method);
        }
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingProvider(string? callback, Exception? failure = null) : SqlAuthenticationProvider
    {
        internal bool Throw { get; set; } = true;

        public override bool IsSupported(SqlAuthenticationMethod method)
        {
            ThrowIfRequested(nameof(IsSupported));
            return true;
        }

        public override void BeforeLoad(SqlAuthenticationMethod method) =>
            ThrowIfRequested(nameof(BeforeLoad));

        public override void BeforeUnload(SqlAuthenticationMethod method) =>
            ThrowIfRequested(nameof(BeforeUnload));

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
            throw new NotSupportedException();

        private void ThrowIfRequested(string name)
        {
            if (Throw && callback == name)
            {
                throw failure ?? new InvalidOperationException("Test callback failure: " + name);
            }
        }
    }

    private sealed class LookupProvider : SqlAuthenticationProvider
    {
        internal Task<SqlAuthenticationProvider?>? Lookup { get; private set; }
        internal int SupportChecks { get; private set; }

        public override bool IsSupported(SqlAuthenticationMethod method)
        {
            SupportChecks++;
            Lookup = Task.Run(() => SqlAuthenticationProvider.GetProvider(method));
            return Lookup.Wait(TimeSpan.FromSeconds(10));
        }

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
            throw new NotSupportedException();
    }

    private sealed class LegacyAzureAssembly : Assembly
    {
        public override AssemblyName GetName() =>
            new("Microsoft.Data.SqlClient.Extensions.Azure") { Version = new Version(1, 0, 0, 0) };

        public override Type? GetType(string name) =>
            name == "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider"
                ? typeof(ThrowingProvider)
                : null;
    }

    private sealed class AuthenticationTraceListener : EventListener
    {
        internal ConcurrentQueue<string> Messages { get; } = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)2);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId == 3 && eventData.Payload?[0] is string message)
            {
                Messages.Enqueue(message);
            }
        }
    }
}
