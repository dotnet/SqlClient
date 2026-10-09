// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../doc/SqlAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlAuthenticationProviderConfigurationSection"]/SqlAuthenticationProviderConfigurationSection/*'/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SqlAuthenticationProviderConfigurationSection : ConfigurationSection
    {
        /// <include file='../doc/SqlAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlAuthenticationProviderConfigurationSection"]/Name/*'/>
        public const string Name = "SqlAuthenticationProviders";

        /// <include file='../doc/SqlAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlAuthenticationProviderConfigurationSection"]/Providers/*'/>
        [ConfigurationProperty("providers")]
        public ProviderSettingsCollection Providers => (ProviderSettingsCollection)this["providers"];

        /// <include file='../doc/SqlAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlAuthenticationProviderConfigurationSection"]/InitializerType/*'/>
        [ConfigurationProperty("initializerType")]
        public string InitializerType => this["initializerType"] as string ?? string.Empty;

        /// <include file='../doc/SqlAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlAuthenticationProviderConfigurationSection"]/ApplicationClientId/*'/>
        [ConfigurationProperty("applicationClientId", IsRequired = false)]
        public string ApplicationClientId => this["applicationClientId"] as string ?? string.Empty;

        /// <include file='../doc/SqlAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlAuthenticationProviderConfigurationSection"]/UseWamBroker/*'/>
        [ConfigurationProperty("useWamBroker", IsRequired = false)]
        public string UseWamBroker => this["useWamBroker"] as string ?? string.Empty;
    }

    /// <include file='../doc/SqlAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlClientAuthenticationProviderConfigurationSection"]/SqlClientAuthenticationProviderConfigurationSection/*'/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SqlClientAuthenticationProviderConfigurationSection : SqlAuthenticationProviderConfigurationSection
    {
        /// <include file='../doc/SqlAuthenticationProviderConfigurationSection.xml' path='docs/members[@name="SqlClientAuthenticationProviderConfigurationSection"]/Name/*'/>
        public new const string Name = "SqlClientAuthenticationProviders";
    }
}
