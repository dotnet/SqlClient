// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// Authentication provider manager.
    /// </summary>
    internal class SqlAuthenticationProviderManager
    {
        private const string ActiveDirectoryPassword = "active directory password";
        private const string ActiveDirectoryIntegrated = "active directory integrated";
        private const string ActiveDirectoryInteractive = "active directory interactive";

        static SqlAuthenticationProviderManager()
        {
            var activeDirectoryAuthNativeProvider = new ActiveDirectoryNativeAuthenticationProvider();
            SqlAuthenticationProviderConfigurationSection configurationSection;
            try
            {
                configurationSection = (SqlAuthenticationProviderConfigurationSection)ConfigurationManager.GetSection(SqlAuthenticationProviderConfigurationSection.Name);
            }
            catch (ConfigurationErrorsException e)
            {
                throw SQL.CannotGetAuthProviderConfig(e);
            }
            Instance = new SqlAuthenticationProviderManager(configurationSection);
            Instance.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, activeDirectoryAuthNativeProvider);
        }
        public static readonly SqlAuthenticationProviderManager Instance;

        private readonly string _typeName;
        private readonly SqlAuthenticationInitializer _initializer;
        private readonly IReadOnlyCollection<SqlAuthenticationMethod> _authenticationsWithAppSpecifiedProvider;
        private readonly ConcurrentDictionary<SqlAuthenticationMethod, SqlAuthenticationProvider> _providers;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SqlAuthenticationProviderManager(SqlAuthenticationProviderConfigurationSection configSection)
        {
            _typeName = GetType().Name;
            _providers = new ConcurrentDictionary<SqlAuthenticationMethod, SqlAuthenticationProvider>();
            var authenticationsWithAppSpecifiedProvider = new HashSet<SqlAuthenticationMethod>();
            _authenticationsWithAppSpecifiedProvider = authenticationsWithAppSpecifiedProvider;

            if (configSection == null)
            {
                return;
            }

            // Create user-defined auth initializer, if any.
            //
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
            }

            // add user-defined providers, if any.
            //
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
                        throw SQL.UnsupportedAuthenticationByProvider(authentication.ToString(), providerSettings.Type);

                    _providers[authentication] = provider;
                    authenticationsWithAppSpecifiedProvider.Add(authentication);
                }
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
                throw SQL.UnsupportedAuthenticationByProvider(authenticationMethod.ToString(), provider.GetType().Name);

            if (_authenticationsWithAppSpecifiedProvider.Contains(authenticationMethod))
            {
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
                return provider;
            });
            return true;
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
        public ProviderSettingsCollection Providers => (ProviderSettingsCollection)base["providers"];

        /// <summary>
        /// User-defined initializer.
        /// </summary>
        [ConfigurationProperty("initializerType")]
        public string InitializerType => base["initializerType"] as string;
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/SqlAuthenticationInitializer/*'/>
    public abstract class SqlAuthenticationInitializer
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/SqlAuthenticationInitializer/*'/>
        public abstract void Initialize();
    }
}
