// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Defines the legacy SqlAuthenticationProviders section for configuring authentication in app.config.
    /// </summary>
    public class SqlAuthenticationProviderConfigurationSection : ConfigurationSection
    {
        /// <summary>The name of the legacy authentication configuration section.</summary>
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
    /// Defines the SqlClientAuthenticationProviders section for configuring authentication in app.config.
    /// </summary>
    public class SqlClientAuthenticationProviderConfigurationSection : SqlAuthenticationProviderConfigurationSection
    {
        /// <summary>The name of the current authentication configuration section.</summary>
        public new const string Name = "SqlClientAuthenticationProviders";
    }

}
