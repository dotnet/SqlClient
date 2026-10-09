// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

namespace Microsoft.Data.SqlClient
{
    /// <summary>Defines the legacy SqlAuthenticationProviders section for authentication configuration in app.config.</summary>
    public class SqlAuthenticationProviderConfigurationSection : ConfigurationSection
    {
        /// <summary>The name of the legacy authentication configuration section.</summary>
        public const string Name = "SqlAuthenticationProviders";

        /// <summary>Gets the user-defined authentication providers.</summary>
        [ConfigurationProperty("providers")]
        public ProviderSettingsCollection Providers => (ProviderSettingsCollection)this["providers"];

        /// <summary>Gets the assembly-qualified initializer type name, or an empty string when unset.</summary>
        [ConfigurationProperty("initializerType")]
        public string InitializerType => this["initializerType"] as string ?? string.Empty;

        /// <summary>Gets the application client ID, or an empty string when unset.</summary>
        [ConfigurationProperty("applicationClientId", IsRequired = false)]
        public string ApplicationClientId => this["applicationClientId"] as string ?? string.Empty;

        /// <summary>Gets the Windows Authentication Manager broker preference, or an empty string when unset.</summary>
        /// <remarks>The value is a string to distinguish an unset preference from an explicit false value.</remarks>
        [ConfigurationProperty("useWamBroker", IsRequired = false)]
        public string UseWamBroker => this["useWamBroker"] as string ?? string.Empty;
    }

    /// <summary>Defines the current SqlClientAuthenticationProviders section for authentication configuration in app.config.</summary>
    public class SqlClientAuthenticationProviderConfigurationSection : SqlAuthenticationProviderConfigurationSection
    {
        /// <summary>The name of the current authentication configuration section.</summary>
        public new const string Name = "SqlClientAuthenticationProviders";
    }
}
