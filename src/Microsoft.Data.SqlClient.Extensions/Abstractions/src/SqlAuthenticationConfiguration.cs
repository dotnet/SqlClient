// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Loads and validates authentication settings without activating initializers or providers.
/// </summary>
internal sealed class SqlAuthenticationConfiguration
{
    // TODO: Localize exception messages in a later PR.

    /// <summary>The client ID override, or null to preserve the Azure provider's default.</summary>
    internal string? ApplicationClientId { get; }

    /// <summary>The broker override, or null to preserve the Azure provider's default.</summary>
    internal bool? UseWamBroker { get; }

    /// <summary>The configured initializer type name, or empty if none was specified.</summary>
    internal string InitializerType { get; } = string.Empty;

    /// <summary>
    /// Provider entries in configuration order, validated and activated by the registry.
    /// </summary>
    internal IReadOnlyList<ProviderSettings> Providers { get; } = Array.Empty<ProviderSettings>();

    /// <summary>
    /// Reads either supported handler's metadata, or creates empty settings when no section is supplied.
    /// </summary>
    /// <param name="section">The selected section, or null for no authentication overrides.</param>
    /// <exception cref="ConfigurationErrorsException">
    /// The schema is invalid.
    /// </exception>
    internal SqlAuthenticationConfiguration(ConfigurationSection? section = null)
    {
        if (section is null)
        {
            return;
        }

        // Both handlers share a metadata contract, including across type-forwarding boundaries.
        string applicationClientId = ReadString(section, "applicationClientId");
        string useWamBrokerValue = ReadString(section, "useWamBroker");
        InitializerType = ReadString(section, "initializerType");
        if (section.ElementInformation.Properties["providers"]?.Value
            is not ProviderSettingsCollection providers)
        {
            throw new ConfigurationErrorsException(
                "The authentication configuration property 'providers' must be a " +
                "ProviderSettingsCollection.");
        }

        // Missing overrides must remain distinct from explicitly configured false or empty values.
        if (!string.IsNullOrEmpty(applicationClientId))
        {
            ApplicationClientId = applicationClientId;
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationConfiguration | Received user-defined Application Client Id");
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationConfiguration | No user-defined Application Client Id found.");
        }

        if (!string.IsNullOrEmpty(useWamBrokerValue))
        {
            if (!bool.TryParse(useWamBrokerValue, out bool useWamBroker))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationConfiguration | Ignoring invalid UseWamBroker; " +
                    "expected 'true' or 'false'.");
            }
            else
            {
                UseWamBroker = useWamBroker;
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationConfiguration | Received user-defined UseWamBroker={0}.",
                    useWamBroker);
            }
        }
        else
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationConfiguration | No user-defined UseWamBroker found.");
        }

        // Snapshot entries, but defer method validation and activation to preserve fail-fast order.
        Providers = providers.Cast<ProviderSettings>()
            .Select(provider => new ProviderSettings(provider.Name, provider.Type)).ToArray();
    }

    /// <summary>Loads the modern section, falling back when it is absent or has an incompatible handler.</summary>
    /// <param name="getSection">
    /// The section reader, or null to use the process-wide ConfigurationManager.
    /// </param>
    /// <returns>Validated settings, including empty settings when neither section exists.</returns>
    /// <remarks>Configuration errors are traced and produce empty settings, matching the driver.</remarks>
    [RequiresUnreferencedCode("Authentication configuration resolves section handlers by name.")]
    [RequiresDynamicCode("Authentication configuration constructs section handlers dynamically.")]
    internal static SqlAuthenticationConfiguration Load(Func<string, object?>? getSection = null)
    {
        try
        {
            ConfigurationSection? section = FetchConfigurationSection("SqlClientAuthenticationProviders",
                typeof(SqlClientAuthenticationProviderConfigurationSection), getSection);
            if (section is null)
            {
                section = FetchConfigurationSection("SqlAuthenticationProviders",
                    typeof(SqlAuthenticationProviderConfigurationSection), getSection);
            }

            if (section is null)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlAuthenticationConfiguration | Neither SqlClientAuthenticationProviders " +
                    "nor SqlAuthenticationProviders configuration section found.");
            }

            return new SqlAuthenticationConfiguration(section);
        }
        catch (ConfigurationErrorsException e)
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlAuthenticationConfiguration: Unable to load custom " +
                "SqlAuthenticationProviders or SqlClientAuthenticationProviders. Correct " +
                "app.config syntax and section handler declarations. ConfigurationManager failed " +
                "to load due to configuration errors: {0}", e);
            return new SqlAuthenticationConfiguration();
        }
    }

    /// <summary>
    /// Reads a string property without treating an incompatible schema as absent.
    /// </summary>
    /// <param name="section">The section containing the property.</param>
    /// <param name="name">The configuration property name.</param>
    /// <returns>The value, or empty if the string property's value is null.</returns>
    /// <exception cref="ConfigurationErrorsException">
    /// The property is missing or incompatible.
    /// </exception>
    internal static string ReadString(ConfigurationSection section, string name)
    {
        PropertyInformation property = section.ElementInformation.Properties[name]
            ?? throw new ConfigurationErrorsException(
                $"The authentication configuration is missing property '{name}'.");
        object? value = property.Value;
        if (property.Type != typeof(string) || (value is not null && value is not string))
        {
            throw new ConfigurationErrorsException(
                $"Authentication configuration property '{name}' must be a string.");
        }
        return value as string ?? string.Empty;
    }

    /// <summary>Fetches a section only when its handler has the expected type.</summary>
    /// <param name="name">The configuration section name.</param>
    /// <param name="type">The expected handler type.</param>
    /// <param name="getSection">
    /// The section reader, or null to use the process-wide ConfigurationManager.
    /// </param>
    /// <returns>The matching section, or null if absent or incompatible.</returns>
    /// <exception cref="ConfigurationErrorsException">
    /// The section cannot be loaded.
    /// </exception>
    [RequiresUnreferencedCode("Configuration section handlers are resolved by name.")]
    [RequiresDynamicCode("Configuration section handlers are constructed dynamically.")]
    internal static ConfigurationSection? FetchConfigurationSection(
        string name, Type type, Func<string, object?>? getSection = null)
    {
        // TODO: Support reading configuration from appsettings.json for .NET runtime applications.
        object? section = (getSection ?? ConfigurationManager.GetSection)(name);
        if (section is null)
        {
            return null;
        }
        if (section is ConfigurationSection configSection &&
            configSection.GetType() == type)
        {
            return configSection;
        }
        SqlClientEventSource.Log.TryTraceEvent(
            "SqlAuthenticationConfiguration | Ignoring section '{0}': expected handler " +
            "'{1}'; found '{2}'.", name, type.FullName, section.GetType().FullName);
        return null;
    }

    /// <summary>Maps only supported authentication method names, ignoring case.</summary>
    /// <param name="authentication">The method name from a configured provider entry.</param>
    /// <returns>The supported Active Directory authentication method.</returns>
    /// <exception cref="NotSupportedException">The configured name is not recognized.</exception>
    internal static SqlAuthenticationMethod AuthenticationEnumFromString(string authentication)
    {
        // Numeric values and unrelated enum members must not bypass the configuration allowlist.
        switch (authentication.ToLowerInvariant())
        {
            case "active directory integrated":
                return SqlAuthenticationMethod.ActiveDirectoryIntegrated;
            case "active directory password":
                #pragma warning disable 0618 // Type or member is obsolete
                return SqlAuthenticationMethod.ActiveDirectoryPassword;
                #pragma warning restore 0618 // Type or member is obsolete
            case "active directory interactive":
                return SqlAuthenticationMethod.ActiveDirectoryInteractive;
            case "active directory service principal":
                return SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;
            case "active directory device code flow":
                return SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
            case "active directory managed identity":
                return SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
            case "active directory msi":
                return SqlAuthenticationMethod.ActiveDirectoryMSI;
            case "active directory default":
                return SqlAuthenticationMethod.ActiveDirectoryDefault;
            case "active directory workload identity":
                return SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;
            default:
                throw new NotSupportedException(
                    $"Configured authentication method '{authentication}' is not supported.");
        }
    }
}
