// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Authentication provider manager.
    /// </summary>
    internal sealed class SqlAuthenticationProviderManager
    {
        private const string ActiveDirectoryPassword = "active directory password";
        private const string ActiveDirectoryIntegrated = "active directory integrated";
        private const string ActiveDirectoryInteractive = "active directory interactive";
        private const string ActiveDirectoryServicePrincipal = "active directory service principal";
        private const string ActiveDirectoryDeviceCodeFlow = "active directory device code flow";
        private const string ActiveDirectoryManagedIdentity = "active directory managed identity";
        private const string ActiveDirectoryMSI = "active directory msi";
        private const string ActiveDirectoryDefault = "active directory default";
        private const string ActiveDirectoryWorkloadIdentity = "active directory workload identity";

        static SqlAuthenticationProviderManager()
        {
            SqlAuthenticationProviderConfigurationSection configurationSection = null;

            try
            {
                // New configuration section "SqlClientAuthenticationProviders" for Microsoft.Data.SqlClient accepted to avoid conflicts with older one.
                configurationSection = FetchConfigurationSection<SqlClientAuthenticationProviderConfigurationSection>(SqlClientAuthenticationProviderConfigurationSection.Name);
                if (configurationSection == null)
                {
                    // If configuration section is not yet found, try with old Configuration Section name for backwards compatibility
                    configurationSection = FetchConfigurationSection<SqlAuthenticationProviderConfigurationSection>(SqlAuthenticationProviderConfigurationSection.Name);
                }
            }
            catch (ConfigurationErrorsException e)
            {
                // Don't throw an error for invalid config files
                SqlClientEventSource.Log.TryTraceEvent("static SqlAuthenticationProviderManager: Unable to load custom SqlAuthenticationProviders or SqlClientAuthenticationProviders. ConfigurationManager failed to load due to configuration errors: {0}", e);
            }

            Instance = new SqlAuthenticationProviderManager(configurationSection);
            SetDefaultAuthProviders(Instance);
        }

        /// <summary>
        /// Sets default supported Active Directory Authentication providers by the driver 
        /// on the SqlAuthenticationProviderManager instance.
        /// </summary>
        private static void SetDefaultAuthProviders(SqlAuthenticationProviderManager instance)
        {
            if (instance != null)
            {
                var activeDirectoryAuthProvider = new ActiveDirectoryAuthenticationProvider(instance._applicationClientId);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated, activeDirectoryAuthProvider);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, activeDirectoryAuthProvider);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, activeDirectoryAuthProvider);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, activeDirectoryAuthProvider);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, activeDirectoryAuthProvider);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, activeDirectoryAuthProvider);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryMSI, activeDirectoryAuthProvider);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, activeDirectoryAuthProvider);
                instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity, activeDirectoryAuthProvider);
            }
        }

        public static readonly SqlAuthenticationProviderManager Instance;

        private readonly SqlAuthenticationInitializer _initializer;
        private readonly IReadOnlyCollection<SqlAuthenticationMethod> _authenticationsWithAppSpecifiedProvider;
        private readonly ConcurrentDictionary<SqlAuthenticationMethod, SqlAuthenticationProvider> _providers;
        private readonly SqlClientLogger _sqlAuthLogger = new SqlClientLogger();
        private readonly string _applicationClientId = ActiveDirectoryAuthentication.AdoClientId;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SqlAuthenticationProviderManager(SqlAuthenticationProviderConfigurationSection configSection = null)
        {
            var methodName = "Ctor";
            _providers = new ConcurrentDictionary<SqlAuthenticationMethod, SqlAuthenticationProvider>();
            var authenticationsWithAppSpecifiedProvider = new HashSet<SqlAuthenticationMethod>();
            _authenticationsWithAppSpecifiedProvider = authenticationsWithAppSpecifiedProvider;

            if (configSection == null)
            {
                _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), methodName, "Neither SqlClientAuthenticationProviders nor SqlAuthenticationProviders configuration section found.");
                return;
            }

            if (!string.IsNullOrEmpty(configSection.ApplicationClientId))
            {
                _applicationClientId = configSection.ApplicationClientId;
                _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), methodName, "Received user-defined Application Client Id");
            }
            else
            {
                _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), methodName, "No user-defined Application Client Id found.");
            }

            // Create user-defined auth initializer, if any.
            if (!string.IsNullOrEmpty(configSection.InitializerType))
            {
                try
                {
                    var initializerType = Type.GetType(configSection.InitializerType, true);
                    _initializer = (SqlAuthenticationInitializer)Activator.CreateInstance(initializerType);
                    _initializer.Initialize();
                }
                catch (Exception e)
                {
                    throw SQL.CannotCreateSqlAuthInitializer(configSection.InitializerType, e);
                }
                _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), methodName, "Created user-defined SqlAuthenticationInitializer.");
            }
            else
            {
                _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), methodName, "No user-defined SqlAuthenticationInitializer found.");
            }

            // add user-defined providers, if any.
            if (configSection.Providers != null && configSection.Providers.Count > 0)
            {
                foreach (ProviderSettings providerSettings in configSection.Providers)
                {
                    SqlAuthenticationMethod authentication = AuthenticationEnumFromString(providerSettings.Name);
                    SqlAuthenticationProvider provider;
                    try
                    {
                        var providerType = Type.GetType(providerSettings.Type, true);
                        provider = (SqlAuthenticationProvider)Activator.CreateInstance(providerType);
                    }
                    catch (Exception e)
                    {
                        throw SQL.CannotCreateAuthProvider(authentication.ToString(), providerSettings.Type, e);
                    }
                    if (!provider.IsSupported(authentication))
                    {
                        throw SQL.UnsupportedAuthenticationByProvider(authentication.ToString(), providerSettings.Type);
                    }

                    _providers[authentication] = provider;
                    authenticationsWithAppSpecifiedProvider.Add(authentication);
                    _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), methodName, string.Format("Added user-defined auth provider: {0} for authentication {1}.", providerSettings?.Type, authentication));
                }
            }
            else
            {
                _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), methodName, "No user-defined auth providers.");
            }
        }

        /// <summary>
        /// Get an authentication provider by method.
        /// </summary>
        /// <param name="authenticationMethod">Authentication method.</param>
        /// <returns>Authentication provider or null if not found.</returns>
        public SqlAuthenticationProvider GetProvider(SqlAuthenticationMethod authenticationMethod)
        {
            SqlAuthenticationProvider value;
            return _providers.TryGetValue(authenticationMethod, out value) ? value : null;
        }

        /// <summary>
        /// Set an authentication provider by method.
        /// </summary>
        /// <param name="authenticationMethod">Authentication method.</param>
        /// <param name="provider">Authentication provider.</param>
        /// <returns>True if succeeded, false otherwise, e.g., the existing provider disallows overriding.</returns>
        public bool SetProvider(SqlAuthenticationMethod authenticationMethod, SqlAuthenticationProvider provider)
        {
            if (!provider.IsSupported(authenticationMethod))
            {
                throw SQL.UnsupportedAuthenticationByProvider(authenticationMethod.ToString(), provider.GetType().Name);
            }
            var methodName = "SetProvider";
            if (_authenticationsWithAppSpecifiedProvider.Count > 0)
            {
                foreach (SqlAuthenticationMethod candidateMethod in _authenticationsWithAppSpecifiedProvider)
                {
                    if (candidateMethod == authenticationMethod)
                    {
                        _sqlAuthLogger.LogError(nameof(SqlAuthenticationProviderManager), methodName, $"Failed to add provider {GetProviderType(provider)} because a user-defined provider with type {GetProviderType(_providers[authenticationMethod])} already existed for authentication {authenticationMethod}.");
                        return false; // return here to avoid replacing user-defined provider
                    }
                }
            }
            _providers.AddOrUpdate(authenticationMethod, provider, (key, oldProvider) =>
            {
                if (oldProvider != null)
                {
                    oldProvider.BeforeUnload(authenticationMethod);
                }
                if (provider != null)
                {
                    provider.BeforeLoad(authenticationMethod);
                }
                _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), methodName, $"Added auth provider {GetProviderType(provider)}, overriding existed provider {GetProviderType(oldProvider)} for authentication {authenticationMethod}.");
                return provider;
            });
            return true;
        }

        /// <summary>
        /// Fetches provided configuration section from app.config file.
        /// Does not support reading from appsettings.json yet.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        private static T FetchConfigurationSection<T>(string name)
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
                case ActiveDirectoryIntegrated:
                    return SqlAuthenticationMethod.ActiveDirectoryIntegrated;
                case ActiveDirectoryPassword:
                    return SqlAuthenticationMethod.ActiveDirectoryPassword;
                case ActiveDirectoryInteractive:
                    return SqlAuthenticationMethod.ActiveDirectoryInteractive;
                case ActiveDirectoryServicePrincipal:
                    return SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;
                case ActiveDirectoryDeviceCodeFlow:
                    return SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
                case ActiveDirectoryManagedIdentity:
                    return SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
                case ActiveDirectoryMSI:
                    return SqlAuthenticationMethod.ActiveDirectoryMSI;
                case ActiveDirectoryDefault:
                    return SqlAuthenticationMethod.ActiveDirectoryDefault;
                case ActiveDirectoryWorkloadIdentity:
                    return SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;
                default:
                    throw SQL.UnsupportedAuthentication(authentication);
            }
        }

        private static string GetProviderType(SqlAuthenticationProvider provider)
        {
            if (provider == null)
                return "null";
            return provider.GetType().FullName;
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
        public string InitializerType => this["initializerType"] as string;

        /// <summary>
        /// Application Client Id
        /// </summary>
        [ConfigurationProperty("applicationClientId", IsRequired = false)]
        public string ApplicationClientId => this["applicationClientId"] as string;
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
