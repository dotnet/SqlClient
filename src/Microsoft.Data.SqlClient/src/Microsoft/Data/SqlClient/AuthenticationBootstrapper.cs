// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.IO;
using System.Reflection;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Seeds an <see cref="AuthenticationProviderRegistry"/> with the authentication providers
    /// discovered from application configuration and from the optional Azure extension assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Production code uses a lazily-created singleton that seeds the shared
    /// <see cref="AuthenticationProviderRegistry.Instance"/>.  Because the singleton is only
    /// created when a federated/Active Directory connection first authenticates, the (reflection
    /// based) config and Azure-extension discovery is deferred until it is actually needed.
    /// </para>
    /// <para>
    /// Call <see cref="Bootstrap"/> to force that one-time initialization.  It accesses the lazy
    /// singleton, whose factory runs exactly once in a thread-safe manner.  Constructing a
    /// bootstrapper directly does not run that factory, so it has no effect on the shared registry.
    /// </para>
    /// <para>
    /// Tests can instead construct an isolated bootstrapper (which seeds a fresh registry) to
    /// inspect discovered providers without mutating global state.
    /// </para>
    /// </remarks>
    internal sealed class AuthenticationBootstrapper
    {
        // The production singleton. Its factory seeds the shared AuthenticationProviderRegistry.
        // Instance, and runs exactly once, only when Value is first accessed (via Bootstrap()).
        // Constructing a bootstrapper directly does not touch this field, so it has no effect on
        // the shared registry - keeping isolated-registry callers (e.g. tests) free of global state.
        private static readonly Lazy<AuthenticationBootstrapper> s_instance =
            new(static () => new AuthenticationBootstrapper(AuthenticationProviderRegistry.Instance));

        // The registry this bootstrapper seeds. Production uses the shared singleton registry;
        // tests can inject an isolated registry to avoid mutating global state.
        private readonly AuthenticationProviderRegistry _registry;

        // Our logging instance.
        private readonly SqlClientLogger _sqlAuthLogger = new();

        // Application client ID read from the app.config configuration section, or null if none
        // was configured.
        private string? _applicationClientId = null;

        // Optional override for ActiveDirectoryAuthenticationProviderOptions.UseWamBroker read from
        // the app.config <SqlClientAuthenticationProviders useWamBroker="..."/> attribute.
        // null means the app did not configure the value, in which case we leave the provider's
        // default behavior (WAM is implied by the SqlClient first-party app id and off otherwise)
        // untouched.
        private bool? _useWamBroker = null;

        /// <summary>
        /// Creates a bootstrapper that seeds the supplied registry, running config-driven and
        /// Azure extension provider discovery.
        /// </summary>
        internal AuthenticationBootstrapper(AuthenticationProviderRegistry registry)
        {
            _registry = registry;

            // Config-driven auth providers, initializers, and application client ID all use
            // reflection (Type.GetType / Activator.CreateInstance) and are incompatible with AOT.
            // Only read the config section and load the Azure extension when reflection-based
            // discovery is enabled.
            if (LocalAppContextSwitches.EnableReflectionBasedAuthenticationProviderDiscovery)
            {
                LoadConfiguration();
                LoadAzureExtensionProvider();
            }
            else
            {
                _sqlAuthLogger.LogInfo(
                    nameof(AuthenticationBootstrapper),
                    "Ctor",
                    "Reflection-based provider discovery is disabled; skipping app.config " +
                    "authentication provider configuration.");
            }
        }

        /// <summary>
        /// Forces the one-time initialization that seeds the shared authentication provider
        /// registry.  Accessing the lazy singleton's value runs its factory exactly once, in a
        /// thread-safe manner; subsequent calls are a cheap no-op.
        /// </summary>
        internal static void Bootstrap()
        {
            _ = s_instance.Value;
        }

        /// <summary>
        /// Gets the application client ID read from the app.config configuration section,
        /// or <see langword="null"/> if none was configured.
        /// </summary>
        internal string? ApplicationClientId => _applicationClientId;

        /// <summary>
        /// Reads the app.config configuration section and registers config-driven initializers and
        /// authentication providers.  Uses reflection (Type.GetType / Activator.CreateInstance) and
        /// is not compatible with NativeAOT trimming.
        /// </summary>
        #if NET
        [RequiresUnreferencedCode(
            "Config-driven auth providers and initializers use Type.GetType and Activator.CreateInstance. " +
            "For AOT applications, register providers explicitly via SetProvider().")]
        [RequiresDynamicCode(
            "Config-driven auth providers and initializers use Activator.CreateInstance. " +
            "For AOT applications, register providers explicitly via SetProvider().")]
        #endif
        private void LoadConfiguration()
        {
            SqlClientEventSource.Log.TryTraceEvent("AuthenticationBootstrapper | Loading authentication provider configuration from app.config.");

            SqlAuthenticationProviderConfigurationSection? configSection = null;

            try
            {
                // New configuration section "SqlClientAuthenticationProviders" for Microsoft.Data.SqlClient accepted to avoid conflicts with older one.
                configSection = FetchConfigurationSection<SqlClientAuthenticationProviderConfigurationSection>(SqlClientAuthenticationProviderConfigurationSection.Name);
                if (configSection == null)
                {
                    // If configuration section is not yet found, try with old Configuration Section name for backwards compatibility
                    configSection = FetchConfigurationSection<SqlAuthenticationProviderConfigurationSection>(SqlAuthenticationProviderConfigurationSection.Name);
                }
            }
            catch (ConfigurationErrorsException e)
            {
                // Don't throw an error for invalid config files
                SqlClientEventSource.Log.TryTraceEvent("static AuthenticationBootstrapper: Unable to load custom SqlAuthenticationProviders or SqlClientAuthenticationProviders. ConfigurationManager failed to load due to configuration errors: {0}", e);
            }

            var methodName = "Ctor";

            if (configSection == null)
            {
                _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, "Neither SqlClientAuthenticationProviders nor SqlAuthenticationProviders configuration section found.");
                return;
            }

            if (!string.IsNullOrEmpty(configSection.ApplicationClientId))
            {
                _applicationClientId = configSection.ApplicationClientId;
                _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, "Received user-defined Application Client Id");
            }
            else
            {
                _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, "No user-defined Application Client Id found.");
            }

            if (!string.IsNullOrEmpty(configSection.UseWamBroker))
            {
                if (bool.TryParse(configSection.UseWamBroker, out bool useWamBroker))
                {
                    _useWamBroker = useWamBroker;
                    _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, $"Received user-defined UseWamBroker={useWamBroker}.");
                }
                else
                {
                    _sqlAuthLogger.LogError(nameof(AuthenticationBootstrapper), methodName, $"Ignoring user-defined UseWamBroker='{configSection.UseWamBroker}': not a valid boolean.");
                }
            }
            else
            {
                _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, "No user-defined UseWamBroker found.");
            }

            // Create user-defined auth initializer, if any.
            if (!string.IsNullOrEmpty(configSection.InitializerType))
            {
                try
                {
                    var initializerType = Type.GetType(configSection.InitializerType, true);
                    if (initializerType is not null)
                    {
                        var initializer = (SqlAuthenticationInitializer?)Activator.CreateInstance(initializerType);
                        if (initializer is not null)
                        {
                            initializer.Initialize();
                        }
                    }
                }
                catch (Exception e)
                {
                    throw SQL.CannotCreateSqlAuthInitializer(configSection.InitializerType, e);
                }
                _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, "Created user-defined SqlAuthenticationInitializer.");
            }
            else
            {
                _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, "No user-defined SqlAuthenticationInitializer found.");
            }

            // add user-defined providers, if any.
            if (configSection.Providers != null && configSection.Providers.Count > 0)
            {
                foreach (ProviderSettings providerSettings in configSection.Providers)
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
                        throw SQL.CannotCreateAuthProvider(authentication.ToString(), providerSettings.Type, e);
                    }
                    if (provider is null)
                    {
                        continue;
                    }
                    if (!provider.IsSupported(authentication))
                    {
                        throw SQL.UnsupportedAuthenticationByProvider(authentication.ToString(), providerSettings.Type);
                    }

                    // Register as a permanent (application-specified) provider so it cannot be
                    // overridden by the Azure extension default or by a later SetProvider call.
                    _registry.SetPermanentProvider(authentication, provider);
                    _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, string.Format("Added user-defined auth provider: {0} for authentication {1}.", providerSettings?.Type, authentication));
                }
            }
            else
            {
                _sqlAuthLogger.LogInfo(nameof(AuthenticationBootstrapper), methodName, "No user-defined auth providers.");
            }
        }

        /// <summary>
        /// Attempts to load the Azure extension authentication provider via
        /// reflection. This method uses Assembly.Load and Activator.CreateInstance
        /// and is not compatible with NativeAOT trimming.
        /// </summary>
        #if NET
        [RequiresUnreferencedCode(
            "Azure extension provider discovery uses Assembly.Load and Activator.CreateInstance. " +
            "For AOT applications, register providers explicitly via SetProvider().")]
        [RequiresDynamicCode(
            "Azure extension provider discovery uses Activator.CreateInstance. " +
            "For AOT applications, register providers explicitly via SetProvider().")]
        #endif
        private void LoadAzureExtensionProvider()
        {
            // The name of our Azure extension assembly.
            const string azureAssemblyName = "Microsoft.Data.SqlClient.Extensions.Azure";

            try
            {
                // Try to load our Azure extension.
                #if STRONG_NAME_SIGNING

                // When strong-name signing is enabled, build a fully-qualified AssemblyName
                // that includes the expected public key token.

                // The public key token of our Azure extension assembly, used to avoid loading
                // imposter assemblies.
                byte[] azurePublicKeyToken = [ 0x23, 0xec, 0x7f, 0xc2, 0xd6, 0xea, 0xa4, 0xa5 ];

                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(AuthenticationBootstrapper) +
                    $": Attempting to load Azure extension assembly={azureAssemblyName} with " +
                    "expected public key token=" +
                    BitConverter.ToString(azurePublicKeyToken).Replace("-", ""));

                var qualifiedName = new AssemblyName(azureAssemblyName);
                qualifiedName.SetPublicKeyToken(azurePublicKeyToken);

                // The .NET Framework runtime will enforce the token during binding, causing Load()
                // to throw.  This prevents an untrusted assembly from being loaded and having its
                // module initializers run.  This will throw if the public key token doesn't match.
                //
                // The .NET runtime ignores the public key token and will happily load any assembly
                // with the same simple name.
                //
                var assembly = Assembly.Load(qualifiedName);

                #if NET
                // For the .NET runtime, we will check the public key token ourselves.
                //
                // Note that a null assembly is handled below.
                if (assembly is not null)
                {
                    byte[]? actualToken = assembly.GetName().GetPublicKeyToken();

                    if (actualToken is null || !actualToken.AsSpan().SequenceEqual(azurePublicKeyToken))
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            nameof(AuthenticationBootstrapper) +
                            $": Azure extension assembly={assembly.GetName()} has an " +
                            "unexpected public key token; " +
                            "no default Active Directory provider installed");
                        return;
                    }
                }
                #endif

                #else

                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(AuthenticationBootstrapper) +
                    $": Attempting to load Azure extension assembly={azureAssemblyName} without " +
                    "strong name verification; ensure this assembly is from a trusted source");

                var assembly = Assembly.Load(azureAssemblyName);

                #endif

                if (assembly is null)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        nameof(AuthenticationBootstrapper) +
                        $": Azure extension assembly={azureAssemblyName} not found; " +
                        "no default Active Directory provider installed");
                    return;
                }

                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(AuthenticationBootstrapper) +
                    $": Azure extension assembly={assembly.GetName()} found; " +
                    "attempting to set as default provider for all Active " +
                    "Directory authentication methods");

                // Look for the authentication provider class.
                const string className = "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider";
                Type? type = assembly.GetType(className);

                if (type is null)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        nameof(AuthenticationBootstrapper) +
                        $": Azure extension does not contain class={className}; " +
                        "no default Active Directory provider installed");

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
                    _applicationClientId,
                    _useWamBroker);

                if (instance is null)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        nameof(AuthenticationBootstrapper) +
                        $": Failed to instantiate Azure extension class={className}; " +
                        "no default Active Directory provider installed");

                    return;
                }

                // We successfully instantiated the provider, so set it as the
                // default for all Active Directory authentication methods.
                //
                // Note that SetProvider() will refuse to clobber an application
                // specified provider, so these defaults will only be applied
                // for methods that do not already have a provider.
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated, instance);
                #pragma warning disable 0618 // Type or member is obsolete
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, instance);
                #pragma warning restore 0618 // Type or member is obsolete
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, instance);
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, instance);
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, instance);
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, instance);
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryMSI, instance);
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, instance);
                _registry.SetProvider(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity, instance);

                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(AuthenticationBootstrapper) +
                    $": Azure extension class={className} installed as " +
                    "provider for all Active Directory authentication methods");
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
                    nameof(AuthenticationBootstrapper) +
                    $": Azure extension assembly={azureAssemblyName} not found or " +
                    "not usable; no default provider installed; " +
                    $"{ex.GetType().Name}: {ex.Message}");
            }
            // Any other exceptions are fatal.
        }

        // Reflectively constructs the Azure extension's ActiveDirectoryAuthenticationProvider,
        // selecting the constructor that matches what the app configured. Extracted from the
        // static initializer so it can be unit-tested with stub provider/options shapes.
        //
        // Returns null when no compatible constructor is available (e.g. a custom assembly
        // that lacks both the (Options) and (string) ctors).
        //
        // Throws InvalidOperationException when useWamBroker is configured but the Azure
        // extension is too old to expose ActiveDirectoryAuthenticationProviderOptions; that
        // signals user-actionable misconfiguration and intentionally escapes the static ctor's
        // catch-when filter so it surfaces as a TypeInitializationException.
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
                    throw SQL.UseWamBrokerRequiresAzureExtensionUpgrade();
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
        /// Fetches provided configuration section from app.config file.
        /// Does not support reading from appsettings.json yet.
        /// </summary>
        private static T? FetchConfigurationSection<T>(string name) where T : class
        {
            Type t = typeof(T);

            // TODO: Support reading configuration from appsettings.json for .NET runtime applications.
            object section = ConfigurationManager.GetSection(name);
            if (section != null)
            {
                if (section is ConfigurationSection configSection && configSection.GetType() == t)
                {
                    return (T)section;
                }
                else
                {
                    SqlClientEventSource.Log.TraceEvent("Found a custom {0} configuration but it is not of type {1}.", name, t.FullName);
                }
            }
            return default;
        }

        private static SqlAuthenticationMethod AuthenticationEnumFromString(string authentication)
        {
            switch (authentication.ToLowerInvariant())
            {
                case "active directory integrated":
                    return SqlAuthenticationMethod.ActiveDirectoryIntegrated;
                #pragma warning disable 0618 // Type or member is obsolete
                case "active directory password":
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
                    throw SQL.UnsupportedAuthentication(authentication);
            }
        }
    }

    /// <summary>
    /// The configuration section definition for reading app.config.
    /// </summary>
    internal class SqlAuthenticationProviderConfigurationSection : ConfigurationSection
    {
        public const string Name = "SqlAuthenticationProviders";

        /// <summary>
        /// User-defined auth providers.
        /// </summary>
        [ConfigurationProperty("providers")]
        public ProviderSettingsCollection Providers => (ProviderSettingsCollection)this["providers"];

        /// <summary>
        /// User-defined initializer.
        /// </summary>
        [ConfigurationProperty("initializerType")]
        public string InitializerType => this["initializerType"] as string ?? string.Empty;

        /// <summary>
        /// Application Client Id
        /// </summary>
        [ConfigurationProperty("applicationClientId", IsRequired = false)]
        public string ApplicationClientId => this["applicationClientId"] as string ?? string.Empty;

        /// <summary>
        /// Forwarded to <c>ActiveDirectoryAuthenticationProviderOptions.UseWamBroker</c>
        /// when the Azure extension's default provider is auto-installed. Stored as a string so
        /// that an unset attribute can be distinguished from <c>useWamBroker="false"</c>; the
        /// runtime parses it with <see cref="bool.TryParse(string, out bool)"/>.
        /// </summary>
        [ConfigurationProperty("useWamBroker", IsRequired = false)]
        public string UseWamBroker => this["useWamBroker"] as string ?? string.Empty;
    }

    /// <summary>
    /// The configuration section definition for reading app.config.
    /// </summary>
    internal class SqlClientAuthenticationProviderConfigurationSection : SqlAuthenticationProviderConfigurationSection
    {
        public new const string Name = "SqlClientAuthenticationProviders";
    }

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/SqlAuthenticationInitializer/*'/>
    public abstract class SqlAuthenticationInitializer
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/SqlAuthenticationInitializer/*'/>
        public abstract void Initialize();
    }
}
