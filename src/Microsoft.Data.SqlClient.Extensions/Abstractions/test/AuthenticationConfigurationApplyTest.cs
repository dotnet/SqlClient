// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;
using Tier = Microsoft.Data.SqlClient.SqlAuthenticationProviderRegistry.RegistrationTier;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Verifies configured activation, precedence and fail-fast application on isolated registries.
/// </summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class AuthenticationConfigurationApplyTest : IDisposable
{
    /// <summary>
    /// Exposes the current test's context to reflectively activated fixtures whose parameterless
    /// constructors cannot receive it. References the same object as <see cref="_context"/>.
    /// Activation must remain synchronous on the test thread; disposal clears this reference.
    /// </summary>
    [ThreadStatic]
    private static Context? s_context;

    /// <summary>
    /// Owns fresh state for each xUnit test instance. The constructor publishes this same object
    /// through <see cref="s_context"/> so activated fixtures can access it without the test instance.
    /// </summary>
    private readonly Context _context = new();

    public AuthenticationConfigurationApplyTest() => s_context = _context;

    public void Dispose() => s_context = null;

    private static Context Current => s_context
        ?? throw new InvalidOperationException("No configuration test context is active.");

    /// <summary>
    /// Missing configuration does not install providers or reserve configuration precedence.
    /// </summary>
    [Fact]
    public void ApplyConfigurationAllowsMissingSection()
    {
        // Empty parsed settings must not activate anything or reserve a configuration-owned slot.
        Apply(null);

        Assert.Empty(_context.Events);
        Assert.True(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new FirstProvider()));
    }

    /// <summary>
    /// Both handlers run the initializer first and let explicit entries replace its providers.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyConfigurationOrdersInitializerAndProviders(bool modern)
    {
        // The explicit provider must replace the provider registered by Initialize, not vice versa.
        var section = CreateSection(modern);
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(Initializer).AssemblyQualifiedName;
        AddProvider(section, "Active Directory Integrated", typeof(SecondProvider));

        Apply(section);

        // Callback order proves which provider won; lower tiers must not replace that result.
        Assert.Equal(new[]
        {
            "construct:initializer", "initialize", "construct:first",
            "construct:second", "unload:first", "load:second"
        }, _context.Events);
        Assert.False(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new ThirdProvider()));
        Assert.True(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new ThirdProvider(), Tier.Config));
        Assert.Equal(new[] { "unload:second", "load:third" },
            _context.Events.Skip(_context.Events.Count - 2));
    }

    /// <summary>
    /// Initializer callback registrations inherit Config precedence without explicit entries.
    /// </summary>
    [Fact]
    public void ApplyConfigurationPromotesInitializerRegistrations()
    {
        // No explicit entry exists, so only the callback-context promotion can confer Config tier.
        var section = CreateSection();
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(Initializer).AssemblyQualifiedName;

        Apply(section);

        // Both application registrations and Azure defaults must preserve the initializer's choice.
        Assert.False(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new SecondProvider()));
        Assert.False(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new SecondProvider(), Tier.Default));
    }

    /// <summary>
    /// Registrations in an initializer constructor are not promoted before Initialize begins.
    /// </summary>
    [Fact]
    public void ApplyConfigurationDoesNotPromoteInitializerConstructorRegistrations()
    {
        // Construction precedes the Initialize callback and must not inherit its precedence.
        var section = CreateSection();
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(ConstructorRegisteringInitializer).AssemblyQualifiedName;

        Apply(section);

        Assert.True(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new SecondProvider()));
        Assert.Equal(new[] { "construct:first", "construct:second", "unload:first", "load:second" },
            _context.Events);
    }

    /// <summary>
    /// Initializer failures stop provider activation and restore ordinary registration precedence.
    /// </summary>
    [Theory]
    [InlineData("construct:initializer")]
    [InlineData("initialize")]
    public void ApplyConfigurationStopsAfterInitializerFailure(string failureAt)
    {
        // Seed a Config registration to detect a callback-context flag left set after failure.
        var existing = CreateSection();
        AddProvider(existing, "Active Directory Integrated", typeof(FirstProvider));
        Apply(existing);
        _context.Events.Clear();
        _context.FailureAt = failureAt;
        var section = CreateSection();
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(Initializer).AssemblyQualifiedName;
        AddProvider(section, "Active Directory Interactive", typeof(SecondProvider));

        var failure = Assert.Throws<ArgumentException>(() =>
            Apply(section));

        // Later providers stay unconstructed and subsequent callers retain Application tier.
        Assert.Same(_context.Failure, failure.GetBaseException());
        Assert.Contains(typeof(Initializer).AssemblyQualifiedName!, failure.Message);
        Assert.DoesNotContain("construct:second", _context.Events);
        Assert.False(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new ThirdProvider()));
    }

    /// <summary>
    /// Earlier providers survive later failures, and entries after the failure are not applied.
    /// </summary>
    [Theory]
    [InlineData("construct:second")]
    [InlineData("support:second")]
    public void ApplyConfigurationPreservesPartialState(string failureAt)
    {
        // A failing middle entry exposes both retained earlier state and skipped later work.
        _context.FailureAt = failureAt;
        var section = CreateSection();
        AddProvider(section, "Active Directory Integrated", typeof(FirstProvider));
        AddProvider(section, "Active Directory Interactive", typeof(SecondProvider));
        AddProvider(section, "Active Directory Default", typeof(ThirdProvider));

        Exception failure = Assert.ThrowsAny<Exception>(() =>
            Apply(section));

        // Construction failures are wrapped; support failures preserve their own exception.
        Assert.Same(_context.Failure, failure.GetBaseException());
        if (failureAt == "construct:second")
        {
            Assert.IsType<ArgumentException>(failure);
            Assert.Contains(typeof(SecondProvider).AssemblyQualifiedName!, failure.Message);
            Assert.Contains("Active Directory Interactive", failure.Message);
        }
        else
        {
            Assert.Same(_context.Failure, failure);
        }
        Assert.Equal(new[] { "construct:first", "construct:second" }, _context.Events);
        // Only the first method should have acquired configuration precedence.
        Assert.False(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new ThirdProvider()));
        Assert.True(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryInteractive, new ThirdProvider()));
        Assert.True(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryDefault, new ThirdProvider()));
    }

    /// <summary>
    /// Unknown methods fail before constructing that entry or any later provider.
    /// </summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("SqlPassword")]
    [InlineData("1")]
    public void ApplyConfigurationRejectsUnknownMethodsBeforeActivation(string method)
    {
        // Put the invalid name first to distinguish validation from provider-constructor failure.
        var section = CreateSection();
        AddProvider(section, method, typeof(FirstProvider));
        AddProvider(section, "Active Directory Default", typeof(SecondProvider));

        var failure = Assert.Throws<NotSupportedException>(() =>
            Apply(section));

        Assert.Contains(method, failure.Message);
        Assert.Empty(_context.Events);
    }

    /// <summary>
    /// A provider rejecting its configured method aborts before later entries are constructed.
    /// </summary>
    [Fact]
    public void ApplyConfigurationRejectsUnsupportedProvider()
    {
        // A recognized method still requires the activated provider to explicitly support it.
        var section = CreateSection();
        AddProvider(section, "Active Directory Integrated", typeof(UnsupportedProvider));
        AddProvider(section, "Active Directory Default", typeof(SecondProvider));

        var failure = Assert.Throws<NotSupportedException>(() =>
            Apply(section));

        // The rejected entry must not reserve its method or allow later provider construction.
        Assert.Contains(typeof(UnsupportedProvider).AssemblyQualifiedName!, failure.Message);
        Assert.Contains("Active Directory Integrated", failure.Message);
        Assert.Equal(new[] { "construct:unsupported" }, _context.Events);
        Assert.True(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new FirstProvider()));
    }

    /// <summary>
    /// Failed replacement callbacks preserve the old provider and stop subsequent entries.
    /// </summary>
    [Theory]
    [InlineData("unload:first")]
    [InlineData("load:second")]
    public void ApplyConfigurationStopsAfterCallbackFailure(string failureAt)
    {
        // Replacing an existing configured provider reaches both unload and load failure paths.
        var existing = CreateSection();
        AddProvider(existing, "Active Directory Integrated", typeof(FirstProvider));
        Apply(existing);
        _context.Events.Clear();
        _context.FailureAt = failureAt;
        var section = CreateSection();
        AddProvider(section, "Active Directory Integrated", typeof(SecondProvider));
        AddProvider(section, "Active Directory Default", typeof(ThirdProvider));

        Assert.Same(_context.Failure, Assert.Throws<InvalidOperationException>(() =>
            Apply(section)));

        Assert.Equal(failureAt == "unload:first"
            ? new[] { "construct:second", "unload:first" }
            : new[] { "construct:second", "unload:first", "load:second" }, _context.Events);
        _context.FailureAt = null;
        // A successful replacement must still unload the original, proving failed publication.
        Assert.True(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryIntegrated, new ThirdProvider(), Tier.Config));
        Assert.Equal(new[] { "unload:first", "load:third" },
            _context.Events.Skip(_context.Events.Count - 2));
        Assert.True(_context.Registry.Register(
            SqlAuthenticationMethod.ActiveDirectoryDefault, new ThirdProvider()));
    }

    /// <summary>
    /// Missing types or parameterless constructors produce actionable activation errors.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ApplyConfigurationReportsTypeAndConstructorFailures(
        bool initializer, bool missingType)
    {
        // Keep resolution errors distinct from activation errors for each configured role.
        string typeName = missingType ? "Missing.AuthenticationType"
            : (initializer ? typeof(ParameterizedInitializer) : typeof(ParameterizedProvider))
                .AssemblyQualifiedName!;
        var section = CreateSection();
        if (initializer)
        {
            section.ElementInformation.Properties["initializerType"].Value = typeName;
        }
        else
        {
            section.Providers.Add(new ProviderSettings("Active Directory Integrated", typeName));
        }
        AddProvider(section, "Active Directory Default", typeof(ThirdProvider));

        var failure = Assert.Throws<ArgumentException>(() =>
            Apply(section));

        // Diagnostics must retain the configured name and the specific reflection failure.
        Assert.Contains(typeName, failure.Message);
        if (missingType)
        {
            Assert.IsType<TypeLoadException>(failure.InnerException);
        }
        else
        {
            Assert.IsType<MissingMethodException>(failure.InnerException);
        }
        Assert.Empty(_context.Events);
    }

    /// <summary>
    /// Configured option overrides flow from this registry into the discovered Azure provider.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "true")]
    [InlineData(null, "false")]
    [InlineData("configured-client", null)]
    [InlineData("configured-client", "")]
    [InlineData("configured-client", "true")]
    [InlineData("configured-client", "false")]
    [InlineData("configured-client", " TRUE ")]
    public void ApplyConfigurationPassesOptionsToAzureDiscovery(string? clientId, string? broker)
    {
        // Exercise absent overrides separately from explicit false and mixed-case true values.
        var section = CreateSection();
        if (clientId is not null)
        {
            section.ElementInformation.Properties["applicationClientId"].Value = clientId;
        }
        if (broker is not null)
        {
            section.ElementInformation.Properties["useWamBroker"].Value = broker;
        }
        ActiveDirectoryAuthenticationProviderOptions? actual = null;
        ThrowingProvider.OnConstructed = options => actual = options;
        try
        {
            Apply(section);
            _context.Registry.LoadAzureExtension(() => typeof(ThrowingProvider).Assembly);

            // Discovery must use this registry's settings, not the shared singleton's values.
            Assert.NotNull(actual);
            Assert.Equal(string.IsNullOrEmpty(clientId) ? null : clientId,
                actual.ApplicationClientId);
            Assert.Equal(!string.IsNullOrEmpty(broker) && bool.Parse(broker), actual.UseWamBroker);
        }
        finally
        {
            // The simulated Azure provider is shared with other serialized discovery tests.
            ThrowingProvider.OnConstructed = null;
        }
    }

    /// <summary>
    /// Parses the section before asking the isolated registry to activate its entries.
    /// </summary>
    /// <param name="section">The handler under test, or null for absent configuration.</param>
    private void Apply(ConfigurationSection? section) =>
        _context.Registry.ApplyConfiguration(new SqlAuthenticationConfiguration(section));

    /// <summary>
    /// Creates a fresh handler without accessing the process-wide configuration system.
    /// </summary>
    /// <param name="modern">Whether to use the preferred rather than legacy handler.</param>
    /// <returns>The empty configuration section.</returns>
    private static SqlAuthenticationProviderConfigurationSection CreateSection(
        bool modern = true) =>
        modern ? new SqlClientAuthenticationProviderConfigurationSection()
            : new SqlAuthenticationProviderConfigurationSection();

    /// <summary>Adds a constructible provider entry using its assembly-qualified name.</summary>
    /// <param name="section">The configuration under test.</param>
    /// <param name="method">The configured authentication-method name.</param>
    /// <param name="type">The configured provider type.</param>
    private static void AddProvider(
        SqlAuthenticationProviderConfigurationSection section, string method, Type type) =>
        section.Providers.Add(new ProviderSettings(method, type.AssemblyQualifiedName));

    /// <summary>
    /// Holds per-test state shared with types activated by the configuration loader.
    /// </summary>
    private sealed class Context
    {
        internal readonly SqlAuthenticationProviderRegistry Registry = new();
        internal readonly List<string> Events = new();
        internal readonly Exception Failure =
            new InvalidOperationException("Test activation failure");
        internal string? FailureAt;

        /// <summary>
        /// Records activation/callback order and raises the selected test failure.
        /// </summary>
        /// <param name="action">The activation or callback being executed.</param>
        internal void Record(string action)
        {
            Events.Add(action);
            FailIfRequested(action);
        }

        /// <summary>
        /// Fails a selected operation without imposing a support-check count contract.
        /// </summary>
        /// <param name="action">The operation being executed.</param>
        internal void FailIfRequested(string action)
        {
            if (FailureAt == action)
            {
                throw Failure;
            }
        }
    }

    /// <summary>
    /// Registers through the same instance method used by public initializer registrations.
    /// </summary>
    public sealed class Initializer : SqlAuthenticationInitializer
    {
        public Initializer() => Current.Record("construct:initializer");

        public override void Initialize()
        {
            Current.Record("initialize");
            Assert.True(Current.Registry.Register(
                SqlAuthenticationMethod.ActiveDirectoryIntegrated, new FirstProvider()));
        }
    }

    /// <summary>
    /// Distinguishes initializer construction from the narrower Initialize callback context.
    /// </summary>
    public sealed class ConstructorRegisteringInitializer : SqlAuthenticationInitializer
    {
        public ConstructorRegisteringInitializer() =>
            Assert.True(Current.Registry.Register(
                SqlAuthenticationMethod.ActiveDirectoryIntegrated, new FirstProvider()));

        public override void Initialize() { }
    }

    /// <summary>
    /// Records construction and replacement callbacks without acquiring real tokens.
    /// </summary>
    public abstract class Provider : SqlAuthenticationProvider
    {
        private readonly string _name;

        protected Provider(string name)
        {
            _name = name;
            Current.Record("construct:" + name);
        }

        public override bool IsSupported(SqlAuthenticationMethod method)
        {
            Current.FailIfRequested("support:" + _name);
            return true;
        }

        public override void BeforeLoad(SqlAuthenticationMethod method) =>
            Current.Record("load:" + _name);

        public override void BeforeUnload(SqlAuthenticationMethod method) =>
            Current.Record("unload:" + _name);

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }

    /// <summary>Supplies the first configured or initializer-registered provider.</summary>
    public sealed class FirstProvider() : Provider("first");

    /// <summary>Supplies a replacement or later failing configured provider.</summary>
    public sealed class SecondProvider() : Provider("second");

    /// <summary>Detects whether entries after a failure were incorrectly activated.</summary>
    public sealed class ThirdProvider() : Provider("third");

    /// <summary>Rejects the configured method before registration can publish it.</summary>
    public sealed class UnsupportedProvider() : Provider("unsupported")
    {
        public override bool IsSupported(SqlAuthenticationMethod method) => false;
    }

    /// <summary>Lacks the parameterless constructor required for configured providers.</summary>
    public sealed class ParameterizedProvider : Provider
    {
        public ParameterizedProvider(int value) : base("parameterized") => _ = value;
    }

    /// <summary>Lacks the parameterless constructor required for configured initializers.</summary>
    public sealed class ParameterizedInitializer : SqlAuthenticationInitializer
    {
        public ParameterizedInitializer(int value) => _ = value;

        public override void Initialize() { }
    }
}
