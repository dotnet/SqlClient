// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Owns authentication provider registration and bootstrap in Abstractions so the public
/// registration API can access providers directly, including under NativeAOT.
/// </summary>
/// <remarks>
/// Serializes registry access and gives configuration registrations precedence over application
/// registrations and discovered Azure defaults. Recoverable configuration failures are traced
/// without preventing later configured providers, Azure discovery or application registrations.
/// </remarks>
internal sealed class SqlAuthenticationProviderRegistry
{
    #region Private Fields

    /// <summary>
    /// Synchronizes bootstrap, provider lookup, registration and replacement callbacks.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>Indicates bootstrap is executing, allowing reentrant registry access.</summary>
    private bool _bootstrapInProgress;

    /// <summary>
    /// Indicates that the configured initializer callback is executing.
    /// </summary>
    private bool _invokingConfiguredInitializer;

    /// <summary>Indicates that all enabled bootstrap steps were attempted.</summary>
    private bool _bootstrapCompleted;

    /// <summary>
    /// Preserves the original bootstrap failure for subsequent registry accesses.
    /// </summary>
    private ExceptionDispatchInfo? _bootstrapFailure;

    /// <summary>
    /// The shared registry, available before bootstrap invokes a configured initializer.
    /// </summary>
    private static readonly SqlAuthenticationProviderRegistry Instance = new();

    /// <summary>Provider registrations indexed by the authentication methods they serve.</summary>
    private readonly Dictionary<SqlAuthenticationMethod, ProviderRegistration> _providers = new();

    /// <summary>Registration precedence, ordered from lowest to highest.</summary>
    internal enum RegistrationTier
    {
        /// <summary>
        /// A discovered Azure provider that cannot replace an existing registration.
        /// </summary>
        Default,
        /// <summary>An application registration that can replace a discovered default.</summary>
        Application,
        /// <summary>
        /// A configured provider or initializer registration that application code cannot override.
        /// </summary>
        Config
    }

    private sealed class ProviderRegistration(SqlAuthenticationProvider provider, RegistrationTier tier)
    {
        internal SqlAuthenticationProvider Provider { get; } = provider;
        internal RegistrationTier Tier { get; } = tier;
    }

    /// <summary>Validated authentication settings owned by this registry instance.</summary>
    private SqlAuthenticationConfiguration _configuration = new();

    #endregion

    #region Internal Methods

    /// <summary>
    /// Gets the registered provider for an authentication method after ensuring bootstrap.
    /// </summary>
    /// <param name="authenticationMethod">The authentication method to look up.</param>
    /// <returns>
    /// The provider, or null if none is registered, including after a recoverable bootstrap failure.
    /// </returns>
    /// <remarks>
    /// Recoverable failures, including cached bootstrap failures, emit actionable diagnostic
    /// traces.
    /// </remarks>
    internal static SqlAuthenticationProvider? GetProvider(
        SqlAuthenticationMethod authenticationMethod)
    {
        // Keep lookup synchronized with bootstrap and replacement callbacks.
        lock (Instance._sync)
        {
            try
            {
                Instance.EnsureBootstrapped();
            }
            catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProvider.GetProvider bootstrap failed for {0}; checking " +
                    "registered providers. Check " +
                    "app.config provider and initializer type names, public parameterless " +
                    "constructors, initializer implementations and supported authentication " +
                    "methods. Upgrade all SqlClient family packages together to the same " +
                    "version. Bootstrap failures are cached; register an application provider " +
                    "with SetProvider, or correct the cause and restart the application. Error: {1}",
                    authenticationMethod, e);
            }
            return Instance._providers.TryGetValue(authenticationMethod, out ProviderRegistration? value)
                ? value.Provider
                : null;
        }
    }

    /// <summary>
    /// Registers an authentication provider with application or configured-initializer precedence.
    /// </summary>
    /// <param name="authenticationMethod">Authentication method.</param>
    /// <param name="provider">Authentication provider.</param>
    /// <returns>
    /// True if registration succeeds; false on recoverable argument or callback
    /// errors, or if an existing registration has higher precedence.
    /// </returns>
    /// <remarks>
    /// Recoverable failures and precedence refusals emit diagnostic traces. Registrations made
    /// by a configured initializer receive configuration precedence rather than application
    /// precedence.
    /// Registration errors during a configured initializer stop that initializer. Configuration
    /// application traces the failure and continues with the remaining configured providers.
    /// After bootstrap, application-provider support validation runs outside the registry lock.
    /// </remarks>
    internal static bool SetProvider(
        SqlAuthenticationMethod authenticationMethod,
        SqlAuthenticationProvider provider)
    {
        try
        {
            // Establish configuration precedence before registering an application provider.
            try
            {
                Instance.EnsureBootstrapped();
            }
            catch (Exception e)
            when (!Instance._invokingConfiguredInitializer &&
                  ExceptionHelpers.IsRecoverableException(e))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProvider.SetProvider bootstrap failed for {0}; continuing " +
                    "with application registration. Bootstrap will not be retried; existing " +
                    "configuration registrations retain precedence. Error: {1}",
                    authenticationMethod, e);
            }
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }
            // Outside bootstrap, support validation must allow cross-thread registry lookups.
            return Instance.Register(authenticationMethod, provider);
        }
        catch (Exception e)
        when (!Instance._invokingConfiguredInitializer &&
              ExceptionHelpers.IsRecoverableException(e))
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProvider.SetProvider failed for {0}; returning false. Pass a " +
                "non-null provider that supports this method and fix any throwing IsSupported, " +
                "BeforeLoad or BeforeUnload callbacks. Error: {1}",
                authenticationMethod, e);
            return false;
        }
    }

    /// <summary>
    /// Discovers and registers Azure defaults using the supplied assembly loader.
    /// </summary>
    /// <param name="loadAssembly">
    /// The assembly loader; production uses <see cref="LoadAzureExtensionAssembly"/> to validate
    /// identity in signed builds.
    /// </param>
    /// <remarks>
    /// Recoverable discovery failures are traced without failing registry bootstrap; fatal runtime
    /// errors propagate.
    /// </remarks>
    [RequiresUnreferencedCode("Azure extension discovery loads types and constructors by name.")]
    [RequiresDynamicCode("Azure extension discovery constructs providers dynamically.")]
    internal void LoadAzureExtension(Func<Assembly?> loadAssembly)
    {
        try
        {
            Assembly? assembly = loadAssembly();
            if (assembly is null)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(SqlAuthenticationProviderRegistry) +
                    ": Azure extension assembly={0} not found; no default Active Directory " +
                    "provider installed",
                    AzureExtensionLoader.AssemblyName);
                return;
            }

            SqlClientEventSource.Log.TryTraceEvent(
                nameof(SqlAuthenticationProviderRegistry) +
                ": Azure extension assembly={0} found; attempting to set as default provider for " +
                "all Active Directory authentication methods",
                assembly.GetName());

            // Resolve by name to keep the Azure extension optional, without a static dependency.
            const string className =
                "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider";
            Type? type = assembly.GetType(className);

            if (type is null)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(SqlAuthenticationProviderRegistry) +
                    ": Azure extension does not contain class={0}; no default Active Directory " +
                    "provider installed",
                    className);

                return;
            }

            // Require the modern options contract rather than probing older constructors or types.
            const string optionsTypeName =
                "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProviderOptions";
            Type optionsType = assembly.GetType(optionsTypeName)
                ?? throw new TypeLoadException(
                    $"Azure authentication discovery requires '{optionsTypeName}'. Upgrade the " +
                    "'Microsoft.Data.SqlClient.Extensions.Azure' package to version 7.0.2 or " +
                    "later with the options-based provider contract, and keep SqlClient family " +
                    "package versions compatible.");

            SqlAuthenticationProvider? instance = CreateAzureAuthenticationProvider(
                type,
                optionsType,
                _configuration.ApplicationClientId,
                _configuration.UseWamBroker);

            if (instance is null)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(SqlAuthenticationProviderRegistry) +
                    ": Failed to instantiate Azure extension class={0}; no default Active " +
                    "Directory provider installed",
                    className);

                return;
            }

            // Share one provider across methods, filling only unregistered methods at Default tier.
            Register(SqlAuthenticationMethod.ActiveDirectoryIntegrated, instance, RegistrationTier.Default);
            #pragma warning disable 0618 // Type or member is obsolete
            Register(SqlAuthenticationMethod.ActiveDirectoryPassword, instance, RegistrationTier.Default);
            #pragma warning restore 0618 // Type or member is obsolete
            Register(SqlAuthenticationMethod.ActiveDirectoryInteractive, instance, RegistrationTier.Default);
            Register(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, instance, RegistrationTier.Default);
            Register(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, instance, RegistrationTier.Default);
            Register(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, instance, RegistrationTier.Default);
            Register(SqlAuthenticationMethod.ActiveDirectoryMSI, instance, RegistrationTier.Default);
            Register(SqlAuthenticationMethod.ActiveDirectoryDefault, instance, RegistrationTier.Default);
            Register(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity, instance, RegistrationTier.Default);

            SqlClientEventSource.Log.TryTraceEvent(
                nameof(SqlAuthenticationProviderRegistry) +
                ": Attempted to register Azure extension class={0} as the default for Active " +
                "Directory authentication methods; existing registrations were preserved",
                className);
        }
        catch (Exception ex) when (ExceptionHelpers.IsRecoverableException(ex))
        {
            // Optional discovery failures must not poison bootstrap or block explicit registration.
            SqlClientEventSource.Log.TryTraceEvent(
                nameof(SqlAuthenticationProviderRegistry) +
                ": Azure extension assembly={0} not found or not usable; default registration " +
                "did not complete. Error: {1}",
                AzureExtensionLoader.AssemblyName,
                ex);
        }
    }

    /// <summary>
    /// Reflectively constructs an Azure authentication provider using the configured overrides.
    /// </summary>
    /// <param name="providerType">The Azure authentication provider type to instantiate.</param>
    /// <param name="optionsType">The required Azure options type.</param>
    /// <param name="applicationClientId">
    /// The application client ID override, or null to retain the default.
    /// </param>
    /// <param name="useWamBroker">The WAM broker override, or null to retain the default.</param>
    /// <returns>
    /// The constructed provider, or null if reflective construction does not produce an
    /// authentication provider.
    /// </returns>
    /// <remarks>
    /// Always uses the options constructor, preserving option defaults when no overrides are
    /// configured.
    /// Incompatible Azure contracts are logged during discovery without installing a default
    /// provider.
    /// </remarks>
    /// <exception cref="MissingMethodException">
    /// The extension lacks the required options constructor.
    /// </exception>
    /// <exception cref="MissingMemberException">
    /// The options type lacks a configured property.
    /// </exception>
    [RequiresUnreferencedCode("Azure provider constructors and properties are discovered by name.")]
    [RequiresDynamicCode("Azure providers are constructed dynamically.")]
    internal static SqlAuthenticationProvider? CreateAzureAuthenticationProvider(
        Type providerType,
        Type optionsType,
        string? applicationClientId,
        bool? useWamBroker)
    {
        // Require the options constructor even when there are no configured overrides.
        ConstructorInfo optionsCtor = providerType.GetConstructor([optionsType])
            ?? throw new MissingMethodException(
                providerType.FullName, $".ctor({optionsType.FullName})");
        // Start with the extension's defaults and change only explicitly configured options.
        object? options = Activator.CreateInstance(optionsType);

        if (applicationClientId is not null)
        {
            PropertyInfo applicationClientIdProperty =
                optionsType.GetProperty("ApplicationClientId")
                ?? throw new MissingMemberException(optionsType.FullName, "ApplicationClientId");
            applicationClientIdProperty.SetValue(options, applicationClientId);
        }

        if (useWamBroker is bool useWam)
        {
            PropertyInfo useWamBrokerProperty = optionsType.GetProperty("UseWamBroker")
                ?? throw new MissingMemberException(optionsType.FullName, "UseWamBroker");
            useWamBrokerProperty.SetValue(options, useWam);
        }

        return optionsCtor.Invoke([options]) as SqlAuthenticationProvider;
    }

    #endregion

    #region Bootstrap, Configuration and Registration Methods

    /// <summary>
    /// Runs the enabled configuration and Azure discovery steps on first access.
    /// </summary>
    /// <param name="getSection">
    /// The section reader, or null to use the process-wide ConfigurationManager.
    /// </param>
    /// <param name="loadAzureAssembly">
    /// The Azure assembly loader, or null to use the production loader with identity validation.
    /// </param>
    /// <remarks>
    /// Reentrant initializer registrations can use the registry while bootstrap is in progress.
    /// Recoverable configuration failures are traced without preventing Azure discovery.
    /// Completed bootstrap does not retry failed configuration or optional discovery.
    /// Unexpected recoverable bootstrap failures are cached and rethrown internally.
    /// </remarks>
    internal void EnsureBootstrapped(
        Func<string, object?>? getSection = null,
        Func<Assembly?>? loadAzureAssembly = null)
    {
        lock (_sync)
        {
            // Cached failures take precedence over in-progress or completed bootstrap state.
            _bootstrapFailure?.Throw();
            // Initializers can reenter on this thread; do not recursively bootstrap the registry.
            if (_bootstrapCompleted || _bootstrapInProgress)
            {
                return;
            }
            _bootstrapInProgress = true;
            try
            {
                // Capability guards let trimming/AOT remove these reflective bootstrap paths.
                // Load configuration first so Azure discovery receives its option overrides.
                if (AuthenticationFeatureSwitches.IsAppConfigSupported)
                {
                    try
                    {
                        ApplyConfiguration(SqlAuthenticationConfiguration.Load(getSection));
                    }
                    catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            "Authentication configuration could not be loaded or applied; " +
                            "continuing with Azure discovery if enabled. Correct app.config " +
                            "and restart the application to retry configuration. Error: {0}", e);
                    }
                }
                if (AuthenticationFeatureSwitches.IsAzureExtensionDiscoverySupported)
                {
                    LoadAzureExtension(loadAzureAssembly ?? LoadAzureExtensionAssembly);
                }
                _bootstrapCompleted = true;
            }
            catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
            {
                // Cache partial-bootstrap failures without losing their original stack.
                SqlClientEventSource.Log.TryTraceEvent(
                    "Authentication bootstrap failed: {0}", e);
                _bootstrapFailure = ExceptionDispatchInfo.Capture(e);
                throw;
            }
            finally
            {
                _bootstrapInProgress = false;
            }
        }
    }

    /// <summary>Loads the Azure extension with the same identity checks as attestation.</summary>
    /// <returns>The trusted Azure assembly, or null if it is unavailable.</returns>
    [RequiresUnreferencedCode("Azure extension discovery loads an assembly by name.")]
    private static Assembly? LoadAzureExtensionAssembly() =>
        AzureExtensionLoader.Load(nameof(SqlAuthenticationProviderRegistry));

    /// <summary>
    /// Applies Azure options, invokes a configured initializer and registers configured providers.
    /// </summary>
    /// <param name="configuration">
    /// The validated settings to retain for Azure discovery and provider activation.
    /// </param>
    /// <remarks>
    /// Initializer and provider registrations receive configuration precedence. Recoverable
    /// initializer or provider failures are traced, and remaining entries are still attempted.
    /// Earlier registrations and callback effects are not rolled back. Fatal errors propagate.
    /// </remarks>
    [RequiresUnreferencedCode("Configured initializer and provider types are loaded by name.")]
    [RequiresDynamicCode("Configured initializer and providers are constructed dynamically.")]
    internal void ApplyConfiguration(SqlAuthenticationConfiguration configuration)
    {
        // Retain the settings before callbacks run so reentrant discovery sees their overrides.
        _configuration = configuration;
        string initializerTypeName = configuration.InitializerType;
        IReadOnlyList<ProviderSettings> providers = configuration.Providers;

        // Run the initializer first so explicit configured provider entries take final precedence.
        if (!string.IsNullOrEmpty(initializerTypeName))
        {
            try
            {
                var initializerType = Type.GetType(initializerTypeName, throwOnError: true)!;
                var initializer = Activator.CreateInstance(initializerType)
                    as SqlAuthenticationInitializer
                    ?? throw AuthenticationStrings.InvalidAuthenticationInitializerInstance();

                // Only registrations made during this callback inherit Config precedence.
                _invokingConfiguredInitializer = true;
                try
                {
                    initializer.Initialize();
                }
                finally
                {
                    _invokingConfiguredInitializer = false;
                }
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProviderRegistry | Created user-defined " +
                    "SqlAuthenticationInitializer.");
            }
            catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProviderRegistry | Could not create or run configured " +
                    "authentication initializer '{0}'; continuing with configured providers. " +
                    "Check the type name, public parameterless constructor and initializer " +
                    "implementation. Error: {1}", initializerTypeName, e);
            }
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProviderRegistry | No user-defined " +
                "SqlAuthenticationInitializer found.");
        }

        // These entries remain configuration-owned after the initializer callback has returned.
        if (providers.Count > 0)
        {
            foreach (ProviderSettings providerSettings in providers)
            {
                try
                {
                    SqlAuthenticationMethod authentication =
                        SqlAuthenticationConfiguration.AuthenticationEnumFromString(
                            providerSettings.Name);
                    var providerType = Type.GetType(providerSettings.Type, throwOnError: true)!;
                    var provider = Activator.CreateInstance(providerType) as SqlAuthenticationProvider
                        ?? throw AuthenticationStrings.InvalidAuthenticationProviderInstance();
                    if (!provider.IsSupported(authentication))
                    {
                        throw AuthenticationStrings.UnsupportedAuthenticationByProvider(
                            providerSettings.Name, providerSettings.Type);
                    }

                    if (!Register(authentication, provider, RegistrationTier.Config))
                    {
                        throw AuthenticationStrings.CannotRegisterAuthProvider(
                            providerSettings.Name, providerSettings.Type);
                    }
                    SqlClientEventSource.Log.TryTraceEvent(
                        "SqlAuthenticationProviderRegistry | Added user-defined auth provider: {0} " +
                        "for authentication {1}.",
                        providerSettings.Type, authentication);
                }
                catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        "SqlAuthenticationProviderRegistry | Could not apply configured " +
                        "authentication provider '{0}' for method '{1}'; skipping this entry. " +
                        "Check the type name, public parameterless constructor, supported " +
                        "method and registration callbacks. Error: {2}",
                        providerSettings.Type, providerSettings.Name, e);
                }
            }
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProviderRegistry | No user-defined auth providers.");
        }
    }

    /// <summary>
    /// Validates and publishes a provider registration according to its precedence.
    /// </summary>
    /// <param name="authenticationMethod">The authentication method to register.</param>
    /// <param name="provider">The provider that must support the method.</param>
    /// <param name="tier">
    /// The registration precedence; defaults to application precedence, promoted to configuration
    /// precedence while invoking the configured initializer.
    /// </param>
    /// <returns>True if registered; false if precedence prevents replacement.</returns>
    /// <remarks>
    /// Validates support before acquiring the registry lock; bootstrap may already hold that lock.
    /// Precedence checks, replacement callbacks and publication remain synchronized. Replacement
    /// invokes
    /// the old provider's BeforeUnload and the new provider's BeforeLoad before updating the
    /// registration; first registrations do not invoke these callbacks. Reentrant changes to the
    /// same method restart precedence checks and callbacks against the current registration.
    /// Callback failures do not publish the replacement.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// The provider does not support the authentication method.
    /// </exception>
    internal bool Register(
        SqlAuthenticationMethod authenticationMethod,
        SqlAuthenticationProvider provider,
        RegistrationTier tier = RegistrationTier.Application)
    {
        // Initializers use the public API, but their registrations belong to configuration.
        // Leave explicitly supplied Default and Config tiers unchanged.
        if (tier == RegistrationTier.Application && _invokingConfiguredInitializer)
        {
            tier = RegistrationTier.Config;
        }

        // After bootstrap, validate outside the lock to permit cross-thread registry lookups.
        if (!provider.IsSupported(authenticationMethod))
        {
            throw AuthenticationStrings.UnsupportedAuthenticationByProvider(
                authenticationMethod.ToString(), provider.GetType().FullName!);
        }
        lock (_sync)
        {
            while (true)
            {
                if (_providers.TryGetValue(authenticationMethod, out ProviderRegistration? registration))
                {
                    // Equal Application/Config tiers may replace; defaults never replace a registration.
                    if (registration.Tier > tier ||
                        (tier == RegistrationTier.Default && registration.Tier == tier))
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            "Authentication registration refused for {0}: existing tier {1} takes " +
                            "precedence over {2}. To change a configuration-owned provider, update " +
                            "app.config or its initializer rather than overriding it with an application " +
                            "registration.",
                            authenticationMethod, registration.Tier, tier);

                        return false;
                    }

                    registration.Provider.BeforeUnload(authenticationMethod);
                    if (!ReferenceEquals(_providers[authenticationMethod], registration))
                    {
                        continue;
                    }

                    provider.BeforeLoad(authenticationMethod);
                    // Entry identity also detects re-registration of the same provider instance.
                    if (!ReferenceEquals(_providers[authenticationMethod], registration))
                    {
                        continue;
                    }
                }
                // Publish provider and precedence together only after replacement callbacks succeed.
                _providers[authenticationMethod] = new ProviderRegistration(provider, tier);
                SqlClientEventSource.Log.TryTraceEvent(
                    "Authentication provider registered for {0}: provider {1}, tier {2}.",
                    authenticationMethod, provider.GetType().FullName, tier);
                return true;
            }
        }
    }

    #endregion
}
