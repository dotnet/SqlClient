// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    // These names and properties are app.config contracts. Keep the handlers in SqlClient
    // even though Abstractions now owns authentication bootstrap and reads the values untyped.
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

}
