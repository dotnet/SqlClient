// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Resources;
using System.Text;
using Microsoft.Data.SqlClient.Internal;

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
    /// Recoverable lookup or formatting failures are logged and produce a fallback containing
    /// the resource key and argument values.
    /// </summary>
    /// <param name="key">The authentication resource key.</param>
    /// <param name="args">Values to substitute into the resource's format placeholders.</param>
    /// <returns>The localized message, or a generic diagnostic message if formatting fails.</returns>
    internal static string Format(string key, params object[] args)
    {
        try
        {
            string? format = s_resources.GetString(key, CultureInfo.CurrentUICulture);
            if (format is not null)
            {
                return string.Format(CultureInfo.CurrentCulture, format, args);
            }

            SqlClientEventSource.Log.TryTraceEvent(
                "AuthenticationStrings.Format | Resource '{0}' was not found. Check the authentication resource keys and deployed resource assemblies; using a fallback message.",
                key);
        }
        catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "AuthenticationStrings.Format | Resource '{0}' could not be formatted ({1}). Check the deployed resource assemblies, format placeholders and argument values; using a fallback message.",
                key, e.GetType().FullName);
        }

        args ??= Array.Empty<object>();
        var builder = new StringBuilder();
        builder.Append(key ?? "<null>").Append(": [");
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(FormatArgument(args[i]));
        }
        return builder.Append(']').ToString();
    }

    /// <summary>
    /// Formats an argument using the current culture. Recoverable formatting failures are logged
    /// and replaced with a type-name placeholder; fatal runtime errors propagate.
    /// </summary>
    /// <param name="argument">The value to format, or null.</param>
    /// <returns>
    /// The formatted value, "&lt;null&gt;" for null, or "&lt;unformattable: type-name&gt;" if the
    /// value cannot be formatted.
    /// </returns>
    internal static string FormatArgument(object? argument)
    {
        if (argument is null)
        {
            return "<null>";
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}", argument);
        }
        catch (Exception e) when (ExceptionHelpers.IsRecoverableException(e))
        {
            string typeName = argument.GetType().FullName ?? argument.GetType().Name;
            SqlClientEventSource.Log.TryTraceEvent(
                "AuthenticationStrings.Format | Argument of type '{0}' could not be formatted ({1}); using a type-name placeholder. Check the argument's formatting implementation.",
                typeName, e.GetType().FullName);
            return "<unformattable: " + typeName + ">";
        }
    }

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
