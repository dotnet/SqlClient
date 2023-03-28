// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Authentication provider manager.
    /// </summary>
    internal partial class SqlAuthenticationProviderManager
    {
        private const string ActiveDirectoryPassword = "active directory password";
        private const string ActiveDirectoryIntegrated = "active directory integrated";
        private const string ActiveDirectoryInteractive = "active directory interactive";
        private const string ActiveDirectoryServicePrincipal = "active directory service principal";
        private const string ActiveDirectoryDeviceCodeFlow = "active directory device code flow";
        private const string ActiveDirectoryManagedIdentity = "active directory managed identity";
        private const string ActiveDirectoryMSI = "active directory msi";
        private const string ActiveDirectoryDefault = "active directory default";

        private readonly IReadOnlyCollection<SqlAuthenticationMethod> _authenticationsWithAppSpecifiedProvider;
        private readonly ConcurrentDictionary<SqlAuthenticationMethod, SqlAuthenticationProvider> _providers;
        private readonly SqlClientLogger _sqlAuthLogger = new SqlClientLogger();
        private readonly string _applicationClientId = ActiveDirectoryAuthentication.AdoClientId;

        public static readonly SqlAuthenticationProviderManager Instance;

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
            }
        }
        /// <summary>
        /// Constructor.
        /// </summary>
        public SqlAuthenticationProviderManager()
        {
            _providers = new ConcurrentDictionary<SqlAuthenticationMethod, SqlAuthenticationProvider>();
            _authenticationsWithAppSpecifiedProvider = new HashSet<SqlAuthenticationMethod>();
            _sqlAuthLogger.LogInfo(nameof(SqlAuthenticationProviderManager), "Ctor", "No SqlAuthProviders configuration section found.");
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
                        break;
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

        private static string GetProviderType(SqlAuthenticationProvider provider)
        {
            if (provider == null)
                return "null";
            return provider.GetType().FullName;
        }
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/SqlAuthenticationInitializer/*'/>
    public abstract class SqlAuthenticationInitializer
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/SqlAuthenticationInitializer/*'/>
        public abstract void Initialize();
    }
}
