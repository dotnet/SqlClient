// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../doc/SqlClientAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlClientAuthenticationProviderConfigurationSection"]/SqlClientAuthenticationProviderConfigurationSection/*'/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SqlClientAuthenticationProviderConfigurationSection : ConfigurationSection
    {
        /// <include file='../doc/SqlClientAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlClientAuthenticationProviderConfigurationSection"]/Name/*'/>
        public const string Name = "SqlClientAuthenticationProviders";

        /// <include file='../doc/SqlClientAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlClientAuthenticationProviderConfigurationSection"]/Providers/*'/>
        [ConfigurationProperty("providers")]
        public ProviderSettingsCollection Providers => (ProviderSettingsCollection)this["providers"];

        /// <include file='../doc/SqlClientAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlClientAuthenticationProviderConfigurationSection"]/InitializerType/*'/>
        [ConfigurationProperty("initializerType")]
        public string InitializerType => this["initializerType"] as string ?? string.Empty;

        /// <include file='../doc/SqlClientAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlClientAuthenticationProviderConfigurationSection"]/ApplicationClientId/*'/>
        [ConfigurationProperty("applicationClientId", IsRequired = false)]
        public string ApplicationClientId => this["applicationClientId"] as string ?? string.Empty;

        /// <include file='../doc/SqlClientAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlClientAuthenticationProviderConfigurationSection"]/UseWamBroker/*'/>
        [ConfigurationProperty("useWamBroker", IsRequired = false)]
        public string UseWamBroker => this["useWamBroker"] as string ?? string.Empty;
    }
}
