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
            var activeDirectoryAuthNativeProvider = new ActiveDirectoryNativeAuthenticationProvider();
            SqlAuthenticationProviderConfigurationSection configurationSection = null;

            try
            {
                configurationSection = (SqlAuthenticationProviderConfigurationSection)ConfigurationManager.GetSection(SqlAuthenticationProviderConfigurationSection.Name);
            }
            catch (ConfigurationErrorsException)
            {
                // Don't throw an error for invalid config files
            }

            Instance = new SqlAuthenticationProviderManager(configurationSection);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, activeDirectoryAuthNativeProvider);
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
                _sqlAuthLogger.LogInfo(_typeName, methodName, "No SqlAuthProviders configuration section found.");
                return;
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

        private static SqlAuthenticationMethod AuthenticationEnumFromString(string authentication)
        {
            switch (authentication.ToLowerInvariant())
            {
                case ActiveDirectoryPassword:
                    return SqlAuthenticationMethod.ActiveDirectoryPassword;
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
            public ProviderSettingsCollection Providers => (ProviderSettingsCollection)base["providers"];

            /// <summary>
            /// User-defined initializer.
            /// </summary>
            [ConfigurationProperty("initializerType")]
            public string InitializerType => base["initializerType"] as string;
        }
    }
}
