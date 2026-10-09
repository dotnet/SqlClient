// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
    /// <summary>The optional Azure extension assembly discovered during bootstrap.</summary>
    private const string azureAssemblyName = "Microsoft.Data.SqlClient.Extensions.Azure";

    /// <summary>The expected Azure extension public key token used by strong-name verification.</summary>
    private static readonly byte[] s_azurePublicKeyToken = [0x23, 0xec, 0x7f, 0xc2, 0xd6, 0xea, 0xa4, 0xa5];

    /// <summary>Synchronizes bootstrap, provider lookup, registration and replacement callbacks.</summary>
    private readonly object _sync = new();
    /// <summary>Indicates that all enabled bootstrap steps completed successfully.</summary>
    private bool _initialized;
    /// <summary>Prevents recursive bootstrap when an initializer accesses the registry.</summary>
    private bool _initializing;
    /// <summary>Assigns configuration precedence to registrations made by the configured initializer.</summary>
    private bool _runningInitializer;
    /// <summary>Preserves the original bootstrap failure for subsequent registry accesses.</summary>
    private ExceptionDispatchInfo? _bootstrapFailure;

    /// <summary>
    /// Runs the enabled version validation, configuration and Azure discovery steps on first access.
    /// </summary>
    /// <remarks>
    /// Reentrant initializer registrations can use the registry while bootstrap is in progress.
    /// A recoverable failure is cached and rethrown internally; GetProvider and SetProvider log it
    /// and return null or false instead of exposing it through the public API.
    /// </remarks>
    private void EnsureInitialized()
    {
        lock (_sync)
        {
            _bootstrapFailure?.Throw();
            if (_initialized || _initializing)
            {
                return;
            }
            _initializing = true;
            try
            {
                if (AuthenticationFeatureSwitches.IsRuntimeVersionValidationSupported)
                {
                    ValidateSqlClientVersion();
                }
                if (AuthenticationFeatureSwitches.IsAppConfigSupported)
                {
                    LoadConfiguration();
                }
                if (AuthenticationFeatureSwitches.IsAzureExtensionDiscoverySupported)
                {
                    LoadAzureExtension();
                }
                _initialized = true;
            }
            catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
            {
                // Keep the original exception, rather than poisoning a static constructor.
                SqlClientEventSource.Log.TryTraceEvent(
                    "Authentication bootstrap failed: {0}", e);
                _bootstrapFailure = ExceptionDispatchInfo.Capture(e);
                throw;
            }
            finally
            {
                _initializing = false;
            }
        }
    }

    /// <summary>
    /// Loads authentication settings from the current app.config section, falling back to the legacy section.
    /// </summary>
    /// <remarks>
    /// Configuration retrieval errors are logged and ignored. Provider and initializer failures
    /// from applying a retrieved section propagate to bootstrap failure handling.
    /// </remarks>
    [RequiresUnreferencedCode("Authentication configuration loads provider types by name.")]
    [RequiresDynamicCode("Authentication configuration constructs providers dynamically.")]
    private void LoadConfiguration()
    {
        ConfigurationSection? configurationSection = null;

        try
        {
            // New configuration section "SqlClientAuthenticationProviders" for Microsoft.Data.SqlClient accepted to avoid conflicts with older one.
            configurationSection = FetchConfigurationSection("SqlClientAuthenticationProviders",
                "Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection");
            if (configurationSection == null)
            {
                // If configuration section is not yet found, try with old Configuration Section name for backwards compatibility
                configurationSection = FetchConfigurationSection("SqlAuthenticationProviders",
                    "Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection");
            }
        }
        catch (ConfigurationErrorsException e)
        {
            // Don't throw an error for invalid config files
            SqlClientEventSource.Log.TryTraceEvent("static SqlAuthenticationProviderRegistry: Unable to load custom SqlAuthenticationProviders or SqlClientAuthenticationProviders. Correct app.config syntax and section handler declarations. ConfigurationManager failed to load due to configuration errors: {0}", e);
        }

        ApplyConfiguration(configurationSection);
    }

    /// <summary>
    /// Discovers the optional Azure extension and registers its provider as the authentication default.
    /// </summary>
    /// <remarks>
    /// Validates the extension's family version and, in signed builds, its public key token.
    /// Defaults do not replace existing registrations. Handled loading or construction failures
    /// are logged and leave the application responsible for supplying a provider.
    /// </remarks>
    [RequiresUnreferencedCode("Azure extension discovery loads types and constructors by name.")]
    [RequiresDynamicCode("Azure extension discovery constructs providers dynamically.")]
    private void LoadAzureExtension()
    {
        // If our Azure extensions package is present, use its authentication provider as our
        // default.
        try
        {
            // Try to load our Azure extension.
#if STRONG_NAME_SIGNING

                // When strong-name signing is enabled, build a fully-qualified AssemblyName
                // that includes the expected public key token.

                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(SqlAuthenticationProviderRegistry) +
                    ": Attempting to load Azure extension assembly={0} with " +
                    "expected public key token={1}",
                    azureAssemblyName,
                    BitConverter.ToString(s_azurePublicKeyToken).Replace("-", ""));

                var qualifiedName = new AssemblyName(azureAssemblyName);
                qualifiedName.SetPublicKeyToken(s_azurePublicKeyToken);

                // The .NET Framework runtime will enforce the token during binding, causing Load()
                // to throw.  This prevents an untrusted assembly from being loaded and having its
                // module initializers run.  This will throw if the public key token doesn't match.
                //
                // The .NET runtime ignores the public key token and will happily load any assembly
                // with the same simple name.
                //
                var assembly = Assembly.Load(qualifiedName);

#if !NETFRAMEWORK
                // For the .NET runtime, we will check the public key token ourselves.
                //
                // Note that a null assembly is handled below.
                if (assembly is not null)
                {
                    byte[]? actualToken = assembly.GetName().GetPublicKeyToken();

                    if (actualToken is null || !actualToken.SequenceEqual(s_azurePublicKeyToken))
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            nameof(SqlAuthenticationProviderRegistry) +
                            ": Azure extension assembly={0} has an " +
                            "unexpected public key token; " +
                            "no default Active Directory provider installed",
                            assembly.GetName());
                        return;
                    }
                }
#endif

#else

            SqlClientEventSource.Log.TryTraceEvent(
                nameof(SqlAuthenticationProviderRegistry) +
                ": Attempting to load Azure extension assembly={0} without " +
                "strong name verification; ensure this assembly is from a trusted source",
                azureAssemblyName);

            var assembly = Assembly.Load(azureAssemblyName);

#endif

            if (assembly is null)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(SqlAuthenticationProviderRegistry) +
                    ": Azure extension assembly={0} not found; " +
                    "no default Active Directory provider installed",
                    azureAssemblyName);
                return;
            }

            ValidateFamilyVersion(assembly);
            SqlClientEventSource.Log.TryTraceEvent(
                nameof(SqlAuthenticationProviderRegistry) +
                ": Azure extension assembly={0} found; " +
                "attempting to set as default provider for all Active " +
                "Directory authentication methods",
                assembly.GetName());

            // Look for the authentication provider class.
            const string className = "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider";
            Type? type = assembly.GetType(className);

            if (type is null)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(SqlAuthenticationProviderRegistry) +
                    ": Azure extension does not contain class={0}; " +
                    "no default Active Directory provider installed",
                    className);

                return;
            }

            // Try to instantiate it.  Behavior depends on what the app
            // configured in <SqlClientAuthenticationProviders>:
            //  * Neither applicationClientId nor useWamBroker -> use the
            //    parameterless constructor (defaults to the SqlClient
            //    first-party app id and enables WAM brokering on Windows).
            //  * applicationClientId only -> prefer the
            //    (ActiveDirectoryAuthenticationProviderOptions) constructor
            //    when the Azure extension exposes it; otherwise fall back
            //    to the legacy (string applicationClientId) constructor so
            //    older Azure extension versions keep working.
            //  * useWamBroker (with or without applicationClientId) ->
            //    requires the (Options) constructor because there is no
            //    positional analog. If the Azure extension is too old to
            //    expose Options, throw to surface the misconfiguration.
            const string optionsTypeName = "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProviderOptions";
            Type? optionsType = assembly.GetType(optionsTypeName);

            SqlAuthenticationProvider? instance = CreateAzureAuthenticationProvider(
                type,
                optionsType,
                Instance._applicationClientId,
                Instance._useWamBroker);

            if (instance is null)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(SqlAuthenticationProviderRegistry) +
                    ": Failed to instantiate Azure extension class={0}; " +
                    "no default Active Directory provider installed",
                    className);

                return;
            }

            // We successfully instantiated the provider, so set it as the
            // default for all Active Directory authentication methods.
            //
            // Note that SetProvider() will refuse to clobber an application
            // specified provider, so these defaults will only be applied
            // for methods that do not already have a provider.
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
                ": Azure extension class={0} installed as " +
                "provider for all Active Directory authentication methods",
                className);
        }
        // All of these exceptions mean we couldn't find or instantiate the
        // Azure extension's authentication provider, in which case we
        // simply have no default and the app must provide one if they
        // attempt to use Active Directory authentication.
        catch (Exception ex)
        when (ex is
                  AmbiguousMatchException or
                  ArgumentException or
                  BadImageFormatException or
                  FileLoadException or
                  FileNotFoundException or
                  MemberAccessException or
                  MethodAccessException or
                  MissingMethodException or
                  NotSupportedException or
                  TargetInvocationException or
                  TypeInitializationException or
                  TypeLoadException)
        {
            SqlClientEventSource.Log.TryTraceEvent(
                nameof(SqlAuthenticationProviderRegistry) +
                ": Azure extension assembly={0} not found or " +
                "not usable; no default provider installed; " +
                "{1}: {2}",
                azureAssemblyName,
                ex.GetType().Name,
                ex.Message);
        }
        // Any other exceptions are fatal.
    }

    /// <summary>The shared registry, available before bootstrap invokes a configured initializer.</summary>
    private static readonly SqlAuthenticationProviderRegistry Instance = new();

    /// <summary>Registration precedence, ordered from lowest to highest.</summary>
    private enum RegistrationTier
    {
        /// <summary>A discovered Azure provider that cannot replace an existing registration.</summary>
        Default,
        /// <summary>An application registration that can replace a discovered default.</summary>
        User,
        /// <summary>A configured provider or initializer registration that application code cannot override.</summary>
        Config
    }

    /// <summary>Providers indexed by the authentication methods they serve.</summary>
    private readonly Dictionary<SqlAuthenticationMethod, SqlAuthenticationProvider> _providers = new();
    /// <summary>The precedence assigned to each registered authentication method.</summary>
    private readonly Dictionary<SqlAuthenticationMethod, RegistrationTier> _tiers = new();
    /// <summary>The optional application client ID read from authentication configuration.</summary>
    private string? _applicationClientId;

    /// <summary>The optional Web Account Manager (WAM) broker override read from app.config.</summary>
    /// <remarks>
    /// Null preserves the Azure provider's default broker behavior rather than explicitly enabling
    /// or disabling it.
    /// </remarks>
    private bool? _useWamBroker;

    /// <summary>
    /// Applies Azure options, invokes a configured initializer and registers configured providers.
    /// </summary>
    /// <param name="configSection">The authentication configuration section, or null if none was found.</param>
    /// <remarks>Initializer and provider registrations receive configuration precedence.</remarks>
    /// <exception cref="ArgumentException">A configured provider or initializer could not be created or initialized.</exception>
    /// <exception cref="NotSupportedException">A configured method is unrecognized or rejected by its provider.</exception>
    [RequiresUnreferencedCode("Configured initializer and provider types are loaded by name.")]
    [RequiresDynamicCode("Configured initializer and providers are constructed dynamically.")]
    private void ApplyConfiguration(ConfigurationSection? configSection)
    {
        if (configSection == null)
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", "Neither SqlClientAuthenticationProviders nor SqlAuthenticationProviders configuration section found.");
            return;
        }

        string applicationClientId = ReadString(configSection, "applicationClientId");
        string useWamBrokerValue = ReadString(configSection, "useWamBroker");
        string initializerTypeName = ReadString(configSection, "initializerType");
        var providers = configSection.ElementInformation.Properties["providers"].Value
            as ProviderSettingsCollection;

        if (!string.IsNullOrEmpty(applicationClientId))
        {
            _applicationClientId = applicationClientId;
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", "Received user-defined Application Client Id");
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", "No user-defined Application Client Id found.");
        }

        if (!string.IsNullOrEmpty(useWamBrokerValue))
        {
            if (bool.TryParse(useWamBrokerValue, out bool useWamBroker))
            {
                _useWamBroker = useWamBroker;
                SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", $"Received user-defined UseWamBroker={useWamBroker}.");
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", $"Ignoring user-defined UseWamBroker='{useWamBrokerValue}': not a valid boolean.");
            }
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", "No user-defined UseWamBroker found.");
        }

        // Create user-defined auth initializer, if any.
        if (!string.IsNullOrEmpty(initializerTypeName))
        {
            try
            {
                var initializerType = Type.GetType(initializerTypeName, true);
                if (initializerType is not null)
                {
                    var initializer = (SqlAuthenticationInitializer?)Activator.CreateInstance(initializerType);
                    if (initializer is not null)
                    {
                        _runningInitializer = true;
                        try
                        {
                            initializer.Initialize();
                        }
                        finally
                        {
                            _runningInitializer = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw AuthenticationStrings.CannotCreateSqlAuthInitializer(initializerTypeName, e);
            }
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", "Created user-defined SqlAuthenticationInitializer.");
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", "No user-defined SqlAuthenticationInitializer found.");
        }

        // add user-defined providers, if any.
        if (providers != null && providers.Count > 0)
        {
            foreach (ProviderSettings providerSettings in providers)
            {
                SqlAuthenticationMethod authentication = AuthenticationEnumFromString(providerSettings.Name);
                SqlAuthenticationProvider? provider;
                try
                {
                    var providerType = Type.GetType(providerSettings.Type, true);
                    if (providerType is null)
                    {
                        continue;
                    }
                    provider = (SqlAuthenticationProvider?)Activator.CreateInstance(providerType);
                }
                catch (Exception e)
                {
                    throw AuthenticationStrings.CannotCreateAuthProvider(authentication.ToString(), providerSettings.Type, e);
                }
                if (provider is null)
                {
                    continue;
                }
                if (!provider.IsSupported(authentication))
                {
                    throw AuthenticationStrings.UnsupportedAuthenticationByProvider(authentication.ToString(), providerSettings.Type);
                }

                Register(authentication, provider, RegistrationTier.Config);
                SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", string.Format("Added user-defined auth provider: {0} for authentication {1}.", providerSettings?.Type, authentication));
            }
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProviderRegistry | {0}", "No user-defined auth providers.");
        }
    }

    /// <summary>Reads a string-valued property from a configuration section.</summary>
    /// <param name="section">The section containing the property.</param>
    /// <param name="name">The configuration property name.</param>
    /// <returns>The string value, or an empty string if the property's value is null or not a string.</returns>
    private static string ReadString(ConfigurationSection section, string name) =>
        section.ElementInformation.Properties[name].Value as string ?? string.Empty;

    /// <summary>Gets the registered provider for an authentication method after ensuring bootstrap.</summary>
    /// <param name="authenticationMethod">The authentication method to look up.</param>
    /// <returns>The provider, or null if none is registered or bootstrap has a recoverable failure.</returns>
    /// <remarks>Recoverable failures, including cached bootstrap failures, emit actionable diagnostic traces.</remarks>
    internal static SqlAuthenticationProvider? GetProvider(SqlAuthenticationMethod authenticationMethod)
    {
        lock (Instance._sync)
        {
            try
            {
                Instance.EnsureInitialized();
                return Instance._providers.TryGetValue(authenticationMethod, out SqlAuthenticationProvider? value) ? value : null;
            }
            catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProvider.GetProvider failed for {0}; returning null. " +
                    "Check app.config provider and initializer type names, public parameterless constructors, " +
                    "initializer implementations and supported authentication methods. Upgrade all SqlClient family packages together " +
                    "to the same version. Bootstrap failures are cached; correct the cause and restart the application. Error: {1}",
                    authenticationMethod, e);
                return null;
            }
        }
    }

    /// <summary>Reflectively constructs an Azure authentication provider using the configured overrides.</summary>
    /// <param name="providerType">The Azure authentication provider type to instantiate.</param>
    /// <param name="optionsType">The Azure options type, or null if the extension does not expose it.</param>
    /// <param name="applicationClientId">The application client ID override, or null to retain the default.</param>
    /// <param name="useWamBroker">The WAM broker override, or null to retain the default.</param>
    /// <returns>
    /// The constructed provider, or null if a client-ID-only override has no compatible constructor
    /// or reflective construction does not produce an authentication provider.
    /// </returns>
    /// <remarks>
    /// Uses the parameterless constructor when neither override is set. A client-ID-only override
    /// prefers the options constructor and falls back to the legacy string constructor.
    /// A broker override requires the options constructor; during bootstrap, incompatibility is
    /// cached and logged while public GetProvider/SetProvider return null/false.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A broker override was requested but the extension lacks the required options constructor.</exception>
    [RequiresUnreferencedCode("Azure provider constructors and properties are discovered by name.")]
    [RequiresDynamicCode("Azure providers are constructed dynamically.")]
    internal static SqlAuthenticationProvider? CreateAzureAuthenticationProvider(
        Type providerType,
        Type? optionsType,
        string? applicationClientId,
        bool? useWamBroker)
    {
        if (applicationClientId is null && useWamBroker is null)
        {
            return Activator.CreateInstance(providerType) as SqlAuthenticationProvider;
        }

        ConstructorInfo? optionsCtor = optionsType is null
            ? null
            : providerType.GetConstructor([optionsType]);

        if (useWamBroker is bool useWam)
        {
            if (optionsType is null || optionsCtor is null)
            {
                throw AuthenticationStrings.UseWamBrokerRequiresAzureExtensionUpgrade();
            }

            var options = Activator.CreateInstance(optionsType);
            if (options is null)
            {
                return null;
            }

            if (applicationClientId is not null)
            {
                optionsType.GetProperty("ApplicationClientId")
                    ?.SetValue(options, applicationClientId);
            }
            optionsType.GetProperty("UseWamBroker")
                ?.SetValue(options, useWam);

            return optionsCtor.Invoke([options]) as SqlAuthenticationProvider;
        }

        // applicationClientId-only: prefer Options when the extension exposes it,
        // otherwise fall back to the legacy (string) ctor for backward compatibility
        // with older Azure extension versions.
        if (optionsType is not null && optionsCtor is not null)
        {
            var options = Activator.CreateInstance(optionsType);
            if (options is null)
            {
                return null;
            }
            optionsType.GetProperty("ApplicationClientId")
                ?.SetValue(options, applicationClientId);
            return optionsCtor.Invoke([options]) as SqlAuthenticationProvider;
        }

        ConstructorInfo? legacyCtor = providerType.GetConstructor([typeof(string)]);
        if (legacyCtor is not null)
        {
            return legacyCtor.Invoke([applicationClientId]) as SqlAuthenticationProvider;
        }

        return null;
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
    /// by a configured initializer receive configuration precedence rather than application precedence.
    /// </remarks>
    internal static bool SetProvider(SqlAuthenticationMethod authenticationMethod, SqlAuthenticationProvider provider)
    {
        lock (Instance._sync)
        {
            try
            {
                Instance.EnsureInitialized();
                if (provider is null)
                {
                    throw new ArgumentNullException(nameof(provider));
                }
                return Instance.Register(authenticationMethod, provider,
                    Instance._runningInitializer ? RegistrationTier.Config : RegistrationTier.User);
            }
            catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationProvider.SetProvider failed for {0}; returning false. " +
                    "Pass a non-null provider that supports this method and fix any throwing IsSupported, " +
                    "BeforeLoad or BeforeUnload callbacks. For bootstrap errors, check app.config provider and " +
                    "initializer type names, public parameterless constructors and initializer implementations, " +
                    "and upgrade all SqlClient family " +
                    "packages together to the same version. Bootstrap failures are cached; correct the cause and " +
                    "restart the application. Error: {1}",
                    authenticationMethod, e);
                return false;
            }
        }
    }

    /// <summary>Validates and publishes a provider registration according to its precedence.</summary>
    /// <param name="authenticationMethod">The authentication method to register.</param>
    /// <param name="provider">The provider that must support the method.</param>
    /// <param name="tier">The precedence assigned to this registration.</param>
    /// <returns>True if registered; false if precedence prevents replacement.</returns>
    /// <remarks>
    /// Called while holding the registry lock. Replacement invokes the old provider's BeforeUnload
    /// and the new provider's BeforeLoad before updating the registration; first registrations do
    /// not invoke these callbacks. Callback failures propagate without publishing the replacement.
    /// </remarks>
    /// <exception cref="NotSupportedException">The provider does not support the authentication method.</exception>
    private bool Register(SqlAuthenticationMethod authenticationMethod,
        SqlAuthenticationProvider provider, RegistrationTier tier)
    {
        if (!provider.IsSupported(authenticationMethod))
        {
            throw AuthenticationStrings.UnsupportedAuthenticationByProvider(authenticationMethod.ToString(), provider.GetType().Name);
        }
        if (_tiers.TryGetValue(authenticationMethod, out RegistrationTier oldTier) &&
            (oldTier > tier || (tier == RegistrationTier.Default && oldTier == tier)))
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "Authentication registration refused for {0}: existing tier {1} takes precedence over {2}. " +
                "To change a configuration-owned provider, update app.config or its initializer rather than " +
                "overriding it with an application registration.",
                authenticationMethod, oldTier, tier);

            // The app has already specified a Provider for this
            // authentication method, so we won't override it.
            return false;
        }
        if (_providers.TryGetValue(authenticationMethod, out SqlAuthenticationProvider? oldProvider))
        {
            oldProvider.BeforeUnload(authenticationMethod);
            provider.BeforeLoad(authenticationMethod);
        }
        _providers[authenticationMethod] = provider;
        _tiers[authenticationMethod] = tier;
        return true;
    }

    /// <summary>
    /// Fetches an app.config section only if its handler has the expected full type name.
    /// </summary>
    /// <param name="name">The configuration section name.</param>
    /// <param name="typeName">The expected section handler's full type name.</param>
    /// <returns>The matching section, or null if it is absent or has a different handler type.</returns>
    /// <remarks>Does not read appsettings.json. Handler mismatches are logged.</remarks>
    /// <exception cref="ConfigurationErrorsException">The configuration system cannot load the section.</exception>
    [RequiresUnreferencedCode("Configuration section handlers are resolved by name.")]
    [RequiresDynamicCode("Configuration section handlers are constructed dynamically.")]
    private static ConfigurationSection? FetchConfigurationSection(string name, string typeName)
    {
        // TODO: Support reading configuration from appsettings.json for .NET runtime applications.
        object section = ConfigurationManager.GetSection(name);
        if (section != null)
        {
            if (section is ConfigurationSection configSection && configSection.GetType().FullName == typeName)
            {
                return configSection;
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("Found a custom {0} configuration but it is not of type {1}.", name, typeName);
            }
        }
        return default;
    }

    /// <summary>Loads SqlClient and validates its exact family version against Abstractions.</summary>
    /// <remarks>A missing SqlClient assembly is logged and does not prevent registration.</remarks>
    /// <exception cref="InvalidOperationException">The family versions differ or cannot be determined.</exception>
    [RequiresUnreferencedCode("The SqlClient assembly is loaded by name for version validation.")]
    [RequiresDynamicCode("The SqlClient assembly is loaded dynamically.")]
    private static void ValidateSqlClientVersion()
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.Load("Microsoft.Data.SqlClient");
        }
        catch (FileNotFoundException e)
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlClient version validation skipped: assembly not installed. {0}", e.Message);
            return;
        }
        ValidateFamilyVersion(assembly);
    }

    /// <summary>Enforces exact family-version agreement between an assembly and Abstractions.</summary>
    /// <param name="assembly">The loaded SqlClient family assembly to validate.</param>
    /// <exception cref="InvalidOperationException">The family versions differ or cannot be determined.</exception>
    internal static void ValidateFamilyVersion(Assembly assembly)
    {
        string expected = GetFamilyVersion(typeof(SqlAuthenticationProvider).Assembly);
        string actual = GetFamilyVersion(assembly);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"SqlClient family version mismatch: {assembly.GetName().Name} is {actual}, " +
                $"but Microsoft.Data.SqlClient.Extensions.Abstractions is {expected}. " +
                "Upgrade all SqlClient family packages together.");
        }
    }

    /// <summary>Reads an assembly's informational version without build metadata.</summary>
    /// <param name="assembly">The assembly whose family version is required.</param>
    /// <returns>The version including any prerelease label, but excluding the build metadata suffix.</returns>
    /// <exception cref="InvalidOperationException">The assembly has no informational version attribute.</exception>
    private static string GetFamilyVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
            ?? throw new InvalidOperationException(
                $"Cannot determine the exact SqlClient family version of {assembly.GetName().Name}.");

    /// <summary>Maps a configured authentication method name to its enum value, ignoring case.</summary>
    /// <param name="authentication">The authentication method name from a configured provider entry.</param>
    /// <returns>The corresponding supported Active Directory authentication method.</returns>
    /// <exception cref="NotSupportedException">The configured name is not recognized.</exception>
    private static SqlAuthenticationMethod AuthenticationEnumFromString(string authentication)
    {
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
                throw AuthenticationStrings.UnsupportedAuthentication(authentication);
        }
    }

}
