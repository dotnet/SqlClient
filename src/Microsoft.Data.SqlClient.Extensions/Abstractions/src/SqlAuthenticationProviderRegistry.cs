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
/// registrations and discovered Azure defaults. Recoverable bootstrap failures are cached and
/// logged while public registry operations preserve their null/false failure results.
/// </remarks>
internal sealed class SqlAuthenticationProviderRegistry
{
    // TODO: Localize exception messages in a later PR.

    #region Private Fields

    /// <summary>The optional Azure extension assembly discovered during bootstrap.</summary>
    private const string azureAssemblyName = "Microsoft.Data.SqlClient.Extensions.Azure";

    /// <summary>
    /// The expected Azure extension public key token used by strong-name verification.
    /// </summary>
    private static readonly byte[] s_azurePublicKeyToken = [0x23, 0xec, 0x7f, 0xc2, 0xd6, 0xea, 0xa4, 0xa5];

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

    /// <summary>Indicates that all enabled bootstrap steps completed successfully.</summary>
    private bool _bootstrapCompleted;

    /// <summary>
    /// Preserves the original bootstrap failure for subsequent registry accesses.
    /// </summary>
    private ExceptionDispatchInfo? _bootstrapFailure;

    /// <summary>
    /// The shared registry, available before bootstrap invokes a configured initializer.
    /// </summary>
    private static readonly SqlAuthenticationProviderRegistry Instance = new();

    /// <summary>Providers indexed by the authentication methods they serve.</summary>
    private readonly Dictionary<SqlAuthenticationMethod, SqlAuthenticationProvider> _providers = new();

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

    /// <summary>The precedence assigned to each registered authentication method.</summary>
    private readonly Dictionary<SqlAuthenticationMethod, RegistrationTier> _tiers = new();

    /// <summary>
    /// The optional application client ID read from authentication configuration.
    /// </summary>
    private string? _applicationClientId;

    /// <summary>
    /// The optional Web Account Manager (WAM) broker override read from app.config.
    /// </summary>
    /// <remarks>
    /// Null preserves the Azure provider's default broker behavior rather than explicitly enabling
    /// or disabling it.
    /// </remarks>
    private bool? _useWamBroker;

    #endregion

    #region Internal Methods

    /// <summary>
    /// Gets the registered provider for an authentication method after ensuring bootstrap.
    /// </summary>
    /// <param name="authenticationMethod">The authentication method to look up.</param>
    /// <returns>
    /// The provider, or null if none is registered or bootstrap has a recoverable failure.
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
                return
                    Instance._providers.TryGetValue(authenticationMethod, out SqlAuthenticationProvider? value)
                    ? value
                    : null;
            }
            catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProvider.GetProvider failed for {0}; returning null. Check " +
                    "app.config provider and initializer type names, public parameterless " +
                    "constructors, initializer implementations and supported authentication " +
                    "methods. Upgrade all SqlClient family packages together to the same " +
                    "version. Bootstrap failures are cached; correct the cause and restart the " +
                    "application. Error: {1}",
                    authenticationMethod, e);
                return null;
            }
        }
    }

    /// <summary>
    /// Registers an authentication provider with application or configured-initializer precedence.
    /// </summary>
    /// <param name="authenticationMethod">Authentication method.</param>
    /// <param name="provider">Authentication provider.</param>
    /// <returns>
    /// True if registration succeeds; false on recoverable initialization, argument or callback
    /// errors, or if an existing registration has higher precedence.
    /// </returns>
    /// <remarks>
    /// Recoverable failures and precedence refusals emit diagnostic traces. Registrations made
    /// by a configured initializer receive configuration precedence rather than application
    /// precedence.
    /// Registration errors during a configured initializer propagate so bootstrap cannot succeed
    /// after a failed configuration-owned registration.
    /// After bootstrap, application-provider support validation runs outside the registry lock.
    /// </remarks>
    internal static bool SetProvider(
        SqlAuthenticationMethod authenticationMethod,
        SqlAuthenticationProvider provider)
    {
        try
        {
            // Establish configuration precedence before registering an application provider.
            Instance.EnsureBootstrapped();
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
                "BeforeLoad or BeforeUnload callbacks. For bootstrap errors, check app.config " +
                "provider and initializer type names, public parameterless constructors and " +
                "initializer implementations, and upgrade all SqlClient family packages together " +
                "to the same version. Bootstrap failures are cached; correct the cause and " +
                "restart the application. Error: {1}",
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
                    azureAssemblyName);
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
            Type optionsType = assembly.GetType(optionsTypeName, throwOnError: true)!;

            SqlAuthenticationProvider? instance = CreateAzureAuthenticationProvider(
                type,
                optionsType,
                _applicationClientId,
                _useWamBroker);

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
                "did not complete; {1}: {2}",
                azureAssemblyName,
                ex.GetType().Name,
                ex.Message);
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
    /// <remarks>
    /// Reentrant initializer registrations can use the registry while bootstrap is in progress.
    /// A recoverable failure is cached and rethrown internally; GetProvider and SetProvider log it
    /// and return null or false instead of exposing it through the public API.
    /// </remarks>
    internal void EnsureBootstrapped(Func<string, object?>? getSection = null)
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
                    LoadConfiguration(getSection);
                }
                if (AuthenticationFeatureSwitches.IsAzureExtensionDiscoverySupported)
                {
                    LoadAzureExtension(LoadAzureExtensionAssembly);
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

    /// <summary>
    /// Loads authentication settings from the current app.config section, falling back to the
    /// legacy section.
    /// </summary>
    /// <param name="getSection">
    /// The section reader, or null to use the process-wide ConfigurationManager.
    /// </param>
    /// <remarks>
    /// Configuration retrieval and application errors propagate to bootstrap failure handling.
    /// Only an absent current section permits fallback to the legacy section.
    /// </remarks>
    [RequiresUnreferencedCode("Authentication configuration loads provider types by name.")]
    [RequiresDynamicCode("Authentication configuration constructs providers dynamically.")]
    internal void LoadConfiguration(Func<string, object?>? getSection = null)
    {
        ConfigurationSection? configurationSection = null;

        try
        {
            // Prefer the SqlClient-specific section so it can coexist with the legacy provider.
            configurationSection = FetchConfigurationSection("SqlClientAuthenticationProviders",
                "Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection",
                getSection);
            if (configurationSection == null)
            {
                // A malformed current section must not be hidden by falling back to the legacy one.
                configurationSection = FetchConfigurationSection("SqlAuthenticationProviders",
                    "Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection",
                    getSection);
            }
        }
        catch (ConfigurationErrorsException e) when (ExceptionHelpers.IsRecoverableException(e))
        {
            // Preserve actionable retrieval diagnostics, but do not continue with missing settings.
            SqlClientEventSource.Log.TryTraceEvent(
                "static SqlAuthenticationProviderRegistry: Unable to load custom " +
                "SqlAuthenticationProviders or SqlClientAuthenticationProviders. Correct " +
                "app.config syntax and section handler declarations. ConfigurationManager failed " +
                "to load due to configuration errors: {0}",
                e);
            throw;
        }

        ApplyConfiguration(configurationSection);
    }

    /// <summary>Loads the Azure extension, validating its identity in signed builds.</summary>
    /// <returns>The trusted Azure assembly, or null if its public key token is invalid.</returns>
    [RequiresUnreferencedCode("Azure extension discovery loads an assembly by name.")]
    private static Assembly? LoadAzureExtensionAssembly()
    {
        #if STRONG_NAME_SIGNING
        SqlClientEventSource.Log.TryTraceEvent(
            nameof(SqlAuthenticationProviderRegistry) +
            ": Attempting to load Azure extension assembly={0} with expected public key token={1}",
            azureAssemblyName,
            BitConverter.ToString(s_azurePublicKeyToken).Replace("-", ""));

        var qualifiedName = new AssemblyName(azureAssemblyName);
        qualifiedName.SetPublicKeyToken(s_azurePublicKeyToken);

        // .NET Framework enforces the token during binding; .NET requires the explicit check below.
        var assembly = Assembly.Load(qualifiedName);

        #if !NETFRAMEWORK
        if (assembly is not null)
        {
            byte[]? actualToken = assembly.GetName().GetPublicKeyToken();
            if (actualToken is null || !actualToken.SequenceEqual(s_azurePublicKeyToken))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(SqlAuthenticationProviderRegistry) +
                    ": Azure extension assembly={0} has an unexpected public key token; no " +
                    "default Active Directory provider installed",
                    assembly.GetName());
                return null;
            }
        }
        #endif
        #else
        SqlClientEventSource.Log.TryTraceEvent(
            nameof(SqlAuthenticationProviderRegistry) +
            ": Attempting to load Azure extension assembly={0} without strong name verification; " +
            "ensure this assembly is from a trusted source",
            azureAssemblyName);
        var assembly = Assembly.Load(azureAssemblyName);
        #endif
        return assembly;
    }

    /// <summary>
    /// Applies Azure options, invokes a configured initializer and registers configured providers.
    /// </summary>
    /// <param name="configSection">
    /// The authentication configuration section, or null if none was found.
    /// </param>
    /// <remarks>
    /// Initializer and provider registrations receive configuration precedence. An invalid
    /// initializer or provider aborts bootstrap; earlier registrations and callback effects
    /// are not rolled back.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// A configured provider or initializer could not be created or initialized.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// A configured method is unrecognized or rejected by its provider.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A configured provider registration was refused.
    /// </exception>
    /// <exception cref="ConfigurationErrorsException">
    /// The authentication schema or an explicitly configured broker value is invalid.
    /// </exception>
    [RequiresUnreferencedCode("Configured initializer and provider types are loaded by name.")]
    [RequiresDynamicCode("Configured initializer and providers are constructed dynamically.")]
    internal void ApplyConfiguration(ConfigurationSection? configSection)
    {
        if (configSection == null)
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProviderRegistry | Neither SqlClientAuthenticationProviders " +
                "nor SqlAuthenticationProviders configuration section found.");
            return;
        }

        // Read through metadata to support both authentication configuration section handlers.
        string applicationClientId = ReadString(configSection, "applicationClientId");
        string useWamBrokerValue = ReadString(configSection, "useWamBroker");
        string initializerTypeName = ReadString(configSection, "initializerType");
        if (configSection.ElementInformation.Properties["providers"]?.Value
            is not ProviderSettingsCollection providers)
        {
            throw new ConfigurationErrorsException(
                "The authentication configuration property 'providers' must be a " +
                "ProviderSettingsCollection.");
        }

        // Leave missing overrides unset so discovery preserves the Azure provider's defaults.
        if (!string.IsNullOrEmpty(applicationClientId))
        {
            _applicationClientId = applicationClientId;
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProviderRegistry | Received user-defined Application Client Id");
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProviderRegistry | No user-defined Application Client Id found.");
        }

        if (!string.IsNullOrEmpty(useWamBrokerValue))
        {
            if (bool.TryParse(useWamBrokerValue, out bool useWamBroker))
            {
                _useWamBroker = useWamBroker;
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProviderRegistry | Received user-defined UseWamBroker={0}.",
                    useWamBroker);
            }
            else
            {
                throw new ConfigurationErrorsException(
                    $"Invalid authentication configuration value '{useWamBrokerValue}' for " +
                    "'useWamBroker'; expected 'true' or 'false'.");
            }
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProviderRegistry | No user-defined UseWamBroker found.");
        }

        // Run the initializer first so explicit configured provider entries take final precedence.
        if (!string.IsNullOrEmpty(initializerTypeName))
        {
            try
            {
                var initializerType = Type.GetType(initializerTypeName, throwOnError: true)!;
                var initializer = Activator.CreateInstance(initializerType)
                    as SqlAuthenticationInitializer
                    ?? throw new InvalidOperationException(
                        "The configured initializer must create a non-null " +
                        "SqlAuthenticationInitializer instance.");

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
            }
            catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
            {
                throw new ArgumentException(
                    "Could not create or run configured authentication initializer " +
                    $"'{initializerTypeName}'.", e);
            }
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProviderRegistry | Created user-defined " +
                "SqlAuthenticationInitializer.");
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
                SqlAuthenticationMethod authentication = AuthenticationEnumFromString(providerSettings.Name);
                SqlAuthenticationProvider provider;
                try
                {
                    var providerType = Type.GetType(providerSettings.Type, throwOnError: true)!;
                    provider = Activator.CreateInstance(providerType) as SqlAuthenticationProvider
                        ?? throw new InvalidOperationException(
                            "The configured provider must create a non-null " +
                            "SqlAuthenticationProvider instance.");
                }
                catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
                {
                    throw new ArgumentException(
                        "Could not create configured authentication provider " +
                        $"'{providerSettings.Type}' for method '{providerSettings.Name}'.", e);
                }
                if (!provider.IsSupported(authentication))
                {
                    throw new NotSupportedException(
                        $"Configured authentication provider '{providerSettings.Type}' does not " +
                        $"support method '{providerSettings.Name}'.");
                }

                if (!Register(authentication, provider, RegistrationTier.Config))
                {
                    throw new InvalidOperationException(
                        $"Configured authentication provider '{providerSettings.Type}' could not " +
                        $"be registered for method '{providerSettings.Name}'.");
                }
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProviderRegistry | Added user-defined auth provider: {0} " +
                    "for authentication {1}.",
                    providerSettings?.Type, authentication);
            }
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationProviderRegistry | No user-defined auth providers.");
        }
    }

    /// <summary>Reads a string-valued property from a configuration section.</summary>
    /// <param name="section">The section containing the property.</param>
    /// <param name="name">The configuration property name.</param>
    /// <returns>
    /// The string value, or an empty string if the property's value is null.
    /// </returns>
    /// <exception cref="ConfigurationErrorsException">
    /// The property is missing from the section schema or has a non-string value.
    /// </exception>
    internal static string ReadString(ConfigurationSection section, string name)
    {
        PropertyInformation property = section.ElementInformation.Properties[name]
            ?? throw new ConfigurationErrorsException(
                $"The authentication configuration is missing property '{name}'.");
        object? value = property.Value;
        if (property.Type != typeof(string) || (value is not null && value is not string))
        {
            throw new ConfigurationErrorsException(
                $"Authentication configuration property '{name}' must be a string.");
        }
        return value as string ?? string.Empty;
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
    /// registration;
    /// first registrations do not invoke these callbacks. Callback failures do not publish the
    /// replacement.
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
            throw new NotSupportedException(
                $"Authentication provider '{provider.GetType().FullName}' does not support " +
                $"method '{authenticationMethod}'.");
        }
        lock (_sync)
        {
            // Equal Application/Config tiers may replace; defaults never replace a registration.
            if (_tiers.TryGetValue(authenticationMethod, out RegistrationTier oldTier) &&
                (oldTier > tier || (tier == RegistrationTier.Default && oldTier == tier)))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "Authentication registration refused for {0}: existing tier {1} takes " +
                    "precedence over {2}. To change a configuration-owned provider, update " +
                    "app.config or its initializer rather than overriding it with an application " +
                    "registration.",
                    authenticationMethod, oldTier, tier);

                return false;
            }
            if (_providers.TryGetValue(authenticationMethod, out SqlAuthenticationProvider? oldProvider))
            {
                // Keep callback order; publish nothing if either callback fails.
                oldProvider.BeforeUnload(authenticationMethod);
                provider.BeforeLoad(authenticationMethod);
            }
            // Publish provider and precedence together only after replacement callbacks succeed.
            _providers[authenticationMethod] = provider;
            _tiers[authenticationMethod] = tier;
            SqlClientEventSource.Log.TryTraceEvent(
                "Authentication provider registered for {0}: provider {1}, tier {2}.",
                authenticationMethod, provider.GetType().FullName, tier);
            return true;
        }
    }

    /// <summary>
    /// Fetches an app.config section only if its handler has the expected full type name.
    /// </summary>
    /// <param name="name">The configuration section name.</param>
    /// <param name="typeName">The expected section handler's full type name.</param>
    /// <param name="getSection">
    /// The section reader, or null to use the process-wide ConfigurationManager.
    /// </param>
    /// <returns>
    /// The matching section, or null if it is absent.
    /// </returns>
    /// <remarks>
    /// Does not read appsettings.json. An unexpected handler fails configuration.
    /// </remarks>
    /// <exception cref="ConfigurationErrorsException">
    /// The configuration system cannot load the section, or its handler is incompatible.
    /// </exception>
    [RequiresUnreferencedCode("Configuration section handlers are resolved by name.")]
    [RequiresDynamicCode("Configuration section handlers are constructed dynamically.")]
    internal static ConfigurationSection? FetchConfigurationSection(
        string name, string typeName, Func<string, object?>? getSection = null)
    {
        // TODO: Support reading configuration from appsettings.json for .NET runtime applications.
        object? section = (getSection ?? ConfigurationManager.GetSection)(name);
        if (section != null)
        {
            // Match the handler contract by full type name, independent of its defining assembly.
            if (section is ConfigurationSection configSection && configSection.GetType().FullName == typeName)
            {
                return configSection;
            }
            else
            {
                throw new ConfigurationErrorsException(
                    $"Authentication configuration section '{name}' must use handler " +
                    $"'{typeName}'; found '{section.GetType().FullName}'.");
            }
        }
        return default;
    }

    /// <summary>
    /// Maps a configured authentication method name to its enum value, ignoring case.
    /// </summary>
    /// <param name="authentication">
    /// The authentication method name from a configured provider entry.
    /// </param>
    /// <returns>The corresponding supported Active Directory authentication method.</returns>
    /// <exception cref="NotSupportedException">The configured name is not recognized.</exception>
    internal static SqlAuthenticationMethod AuthenticationEnumFromString(string authentication)
    {
        // Keep an explicit allowlist; numeric values and unrelated enum members are not valid here.
        switch (authentication.ToLowerInvariant())
        {
            case "active directory integrated":
                return SqlAuthenticationMethod.ActiveDirectoryIntegrated;
            case "active directory password":
                #pragma warning disable 0618 // Type or member is obsolete
                return SqlAuthenticationMethod.ActiveDirectoryPassword;
                #pragma warning restore 0618 // Type or member is obsolete
            case "active directory interactive":
                return SqlAuthenticationMethod.ActiveDirectoryInteractive;
            case "active directory service principal":
                return SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;
            case "active directory device code flow":
                return SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
            case "active directory managed identity":
                return SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
            case "active directory msi":
                return SqlAuthenticationMethod.ActiveDirectoryMSI;
            case "active directory default":
                return SqlAuthenticationMethod.ActiveDirectoryDefault;
            case "active directory workload identity":
                return SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;
            default:
                throw new NotSupportedException(
                    $"Configured authentication method '{authentication}' is not supported.");
        }
    }

    #endregion
}
