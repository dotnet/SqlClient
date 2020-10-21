// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    internal partial class SqlAuthenticationProviderManager
    {
        private readonly SqlAuthenticationInitializer _initializer;

        static SqlAuthenticationProviderManager()
        {
            var azureManagedIdentityAuthenticationProvider = new AzureManagedIdentityAuthenticationProvider();
            SqlAuthenticationProviderConfigurationSection configurationSection = null;

            try
            {
                // New configuration section "SqlClientAuthenticationProviders" for Microsoft.Data.SqlClient accepted to avoid conflicts with older one.
                configurationSection = FetchConfigurationSection<SqlClientAuthenticationProviderConfigurationSection>(SqlClientAuthenticationProviderConfigurationSection.Name);
                if (null == configurationSection)
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
            var activeDirectoryAuthProvider = new ActiveDirectoryAuthenticationProvider(Instance._applicationClientId);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated, activeDirectoryAuthProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, activeDirectoryAuthProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, activeDirectoryAuthProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, activeDirectoryAuthProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, activeDirectoryAuthProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, azureManagedIdentityAuthenticationProvider);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryMSI, azureManagedIdentityAuthenticationProvider);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public SqlAuthenticationProviderManager(SqlAuthenticationProviderConfigurationSection configSection = null)
        {
            var methodName = "Ctor";
            _typeName = GetType().Name;
            _providers = new ConcurrentDictionary<SqlAuthenticationMethod, SqlAuthenticationProvider>();
            var authenticationsWithAppSpecifiedProvider = new HashSet<SqlAuthenticationMethod>();
            _authenticationsWithAppSpecifiedProvider = authenticationsWithAppSpecifiedProvider;

            if (configSection == null)
            {
                _sqlAuthLogger.LogInfo(_typeName, methodName, "Neither SqlClientAuthenticationProviders nor SqlAuthenticationProviders configuration section found.");
                return;
            }

            if (!string.IsNullOrEmpty(configSection.ApplicationClientId))
            {
                _applicationClientId = configSection.ApplicationClientId;
                _sqlAuthLogger.LogInfo(_typeName, methodName, "Received user-defined Application Client Id");
            }
            else
            {
                _sqlAuthLogger.LogInfo(_typeName, methodName, "No user-defined Application Client Id found.");
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
                _sqlAuthLogger.LogInfo(_typeName, methodName, "Created user-defined SqlAuthenticationInitializer.");
            }
            else
            {
                _sqlAuthLogger.LogInfo(_typeName, methodName, "No user-defined SqlAuthenticationInitializer found.");
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
                    _sqlAuthLogger.LogInfo(_typeName, methodName, string.Format("Added user-defined auth provider: {0} for authentication {1}.", providerSettings?.Type, authentication));
                }
            }
            else
            {
                _sqlAuthLogger.LogInfo(_typeName, methodName, "No user-defined auth providers.");
            }
        }

        private static T FetchConfigurationSection<T>(string name)
        {
            Type t = typeof(T);
            object section = ConfigurationManager.GetSection(name);
            if (null != section)
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
                default:
                    throw SQL.UnsupportedAuthentication(authentication);
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
    }
}
