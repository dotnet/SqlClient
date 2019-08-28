// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// The configuration section definition for reading app.config.
    /// </summary>
    internal class SqlColumnEncryptionEnclaveProviderConfigurationSection : ConfigurationSection
    {
        /// <summary>
        /// User-defined SqlColumnEncryptionEnclaveProviders.
        /// </summary>
        [ConfigurationProperty("providers")]
        public ProviderSettingsCollection Providers => (ProviderSettingsCollection)base["providers"];

    }

    internal class SqlColumnEncryptionEnclaveProviderConfigurationManager
    {
        private readonly Dictionary<string, SqlColumnEncryptionEnclaveProvider> _enclaveProviders = new Dictionary<string, SqlColumnEncryptionEnclaveProvider>();

        /// <summary>
        /// Constructor.
        /// </summary>
        public SqlColumnEncryptionEnclaveProviderConfigurationManager(SqlColumnEncryptionEnclaveProviderConfigurationSection configSection)
        {
            if (configSection != null && configSection.Providers != null && configSection.Providers.Count > 0)
            {
                foreach (ProviderSettings providerSettings in configSection.Providers)
                {
                    var providerName = providerSettings.Name.ToLowerInvariant();
                    SqlColumnEncryptionEnclaveProvider provider;

                    try
                    {
                        var providerType = Type.GetType(providerSettings.Type, true);
                        provider = (SqlColumnEncryptionEnclaveProvider)Activator.CreateInstance(providerType);
                    }
                    catch (Exception e)
                    {
                        throw SQL.CannotCreateSqlColumnEncryptionEnclaveProvider(providerName, providerSettings.Type, e);
                    }

                    _enclaveProviders[providerName] = provider;
                }
            }
        }

        /// <summary>
        /// Lookup SqlColumnEncryptionEnclaveProvider for a given SqlColumnEncryptionEnclaveProviderName
        /// </summary>
        /// <param name="SqlColumnEncryptionEnclaveProviderName"></param>
        /// <returns>SqlColumnEncryptionEnclaveProvider for a give sqlColumnEncryptionEnclaveProviderName if found, else returns null</returns>
        public SqlColumnEncryptionEnclaveProvider GetSqlColumnEncryptionEnclaveProvider(string SqlColumnEncryptionEnclaveProviderName)
        {
            if (string.IsNullOrEmpty(SqlColumnEncryptionEnclaveProviderName)) throw SQL.SqlColumnEncryptionEnclaveProviderNameCannotBeEmpty();
            SqlColumnEncryptionEnclaveProviderName = SqlColumnEncryptionEnclaveProviderName.ToLowerInvariant();

            SqlColumnEncryptionEnclaveProvider sqlColumnEncryptionEnclaveProvider = null;
            _enclaveProviders.TryGetValue(SqlColumnEncryptionEnclaveProviderName, out sqlColumnEncryptionEnclaveProvider);

            return sqlColumnEncryptionEnclaveProvider;
        }

    }
}
