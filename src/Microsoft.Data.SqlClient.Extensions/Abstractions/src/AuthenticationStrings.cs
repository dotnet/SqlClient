// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Resources;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Creates localized authentication error messages and exceptions using resources owned by
/// Abstractions, without depending on the driver assembly's resource helpers.
/// </summary>
internal static class AuthenticationStrings
{
    /// <summary>
    /// Loads authentication strings from this assembly and its localized satellite assemblies.
    /// </summary>
    private static readonly ResourceManager s_resources = new(
        "Microsoft.Data.SqlClient.Authentication.Strings", typeof(AuthenticationStrings).Assembly);

    /// <summary>
    /// Retrieves a resource using the current UI culture and formats it using the current culture.
    /// </summary>
    /// <param name="key">The authentication resource key.</param>
    /// <param name="args">Values to substitute into the resource's format placeholders.</param>
    /// <returns>The localized, formatted message.</returns>
    /// <exception cref="MissingManifestResourceException">
    /// The resource key cannot be found, or the resource set cannot be loaded.
    /// </exception>
    internal static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture,
            s_resources.GetString(key, CultureInfo.CurrentUICulture)
                ?? throw new MissingManifestResourceException(key), args);

    /// <summary>
    /// Creates an argument exception for a provider construction failure, preserving its cause.
    /// </summary>
    /// <param name="authentication">The configured authentication method.</param>
    /// <param name="type">The configured provider type name.</param>
    /// <param name="e">The exception that caused construction to fail.</param>
    /// <returns>An exception containing the localized message and original exception.</returns>
    internal static Exception CannotCreateAuthProvider(string authentication, string type, Exception e) =>
        new ArgumentException(Format("SQL_CannotCreateAuthProvider", authentication, type), e);

    /// <summary>
    /// Creates an argument exception for an authentication initializer construction failure,
    /// preserving its cause.
    /// </summary>
    /// <param name="type">The configured initializer type name.</param>
    /// <param name="e">The exception that caused construction to fail.</param>
    /// <returns>An exception containing the localized message and original exception.</returns>
    internal static Exception CannotCreateSqlAuthInitializer(string type, Exception e) =>
        new ArgumentException(Format("SQL_CannotCreateAuthInitializer", type), e);

    /// <summary>
    /// Creates a not-supported exception when a provider rejects its configured authentication method.
    /// </summary>
    /// <param name="authentication">The unsupported authentication method.</param>
    /// <param name="type">The configured provider type name.</param>
    /// <returns>An exception identifying the provider and unsupported method.</returns>
    internal static Exception UnsupportedAuthenticationByProvider(string authentication, string type) =>
        new NotSupportedException(Format("SQL_UnsupportedAuthenticationByProvider", type, authentication));

    /// <summary>
    /// Creates a not-supported exception for an unrecognized authentication method name.
    /// </summary>
    /// <param name="authentication">The unrecognized configured authentication method.</param>
    /// <returns>An exception identifying the unsupported method.</returns>
    internal static Exception UnsupportedAuthentication(string authentication) =>
        new NotSupportedException(Format("SQL_UnsupportedAuthentication", authentication));

    /// <summary>
    /// Creates an invalid-operation exception when the loaded Azure extension cannot support
    /// the requested Web Account Manager (WAM) broker option.
    /// </summary>
    /// <returns>An exception explaining that the Azure extension must be upgraded.</returns>
    internal static Exception UseWamBrokerRequiresAzureExtensionUpgrade() =>
        new InvalidOperationException(Format("SQL_UseWamBrokerRequiresAzureExtensionUpgrade"));
}
