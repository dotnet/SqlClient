// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.Data.SqlClient.Parser;

namespace Microsoft.Data.SqlClient.Diagnostics;

internal readonly record struct ConnectionErrorClassification(
    ConnectionErrorCategory Category,
    ConnectionErrorSource Source,
    string ErrorType,
    string ErrorCode,
    string Phase);

internal static class ConnectionErrorClassifier
{
    internal static ConnectionErrorClassification Classify(ConnectionErrorEvidence evidence)
    {
        ConnectionErrorCategory category = evidence.IsAmbiguous
            ? ConnectionErrorCategory.Unknown
            : evidence.Kind;
        if (!evidence.IsAmbiguous && evidence.Code.HasValue)
        {
            category = evidence.Source switch
            {
                ConnectionErrorSource.SqlServer => SqlServerCategory(evidence.Code.Value),
                ConnectionErrorSource.Sni => SniCategory(evidence.Code.Value),
                ConnectionErrorSource.OperatingSystem =>
                    OperatingSystemCategory(evidence.Code.Value, category),
                ConnectionErrorSource.Client when evidence.Code == TdsEnums.TIMEOUT_EXPIRED =>
                    ConnectionErrorCategory.Timeout,
                _ => category
            };
        }

        if (!evidence.IsAmbiguous && category == ConnectionErrorCategory.Unknown)
        {
            category = evidence.Phase switch
            {
                "configuration" => ConnectionErrorCategory.Configuration,
                "redirect" => ConnectionErrorCategory.RoutingRedirect,
                "token_request" => ConnectionErrorCategory.Authentication,
                "tls_setup" => ConnectionErrorCategory.TlsSecurity,
                "name_resolution" => ConnectionErrorCategory.NameResolution,
                _ => ConnectionErrorCategory.Unknown
            };
        }

        ConnectionErrorSource source = evidence.Source == ConnectionErrorSource.Unknown &&
            category != ConnectionErrorCategory.Unknown
                ? ConnectionErrorSource.Client
                : evidence.Source;
        string errorType = evidence.Code.HasValue && source != ConnectionErrorSource.Unknown
            ? ErrorTypePrefix(source) +
                evidence.Code.Value.ToString(CultureInfo.InvariantCulture)
            : category == ConnectionErrorCategory.Unknown
                ? "unknown"
                : ConnectionErrorAttributePolicy.ClientErrorTypePrefix + CategoryName(category);
        string errorCode = evidence.Code.HasValue && source != ConnectionErrorSource.Unknown
            ? ErrorTypePrefix(source).TrimEnd('.') + ":" +
                evidence.Code.Value.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        return new ConnectionErrorClassification(
            category, source, errorType, errorCode, evidence.Phase);
    }

    private static ConnectionErrorCategory SqlServerCategory(int code) => code switch
    {
        18456 or 18486 or 18488 => ConnectionErrorCategory.Authentication,
        4060 => ConnectionErrorCategory.DatabaseOpen,
        40615 => ConnectionErrorCategory.AccessPolicy,
        // 49918-49920 follow the cross-driver contract (open item O6): resource_throttling
        // for both connection and statement scopes.
        40501 or 10928 or 10929 or 49918 or 49919 or 49920 =>
            ConnectionErrorCategory.ResourceThrottling,
        40197 or 40143 or 40166 or 40540 or 40020 or 40613 or 4221 or 42108 or 42109 =>
            ConnectionErrorCategory.ServerAvailability,
        64 or 10053 or 10054 => ConnectionErrorCategory.NetworkConnectivity,
        _ => ConnectionErrorCategory.Unknown
    };

    private static ConnectionErrorCategory SniCategory(int code) => code switch
    {
        2 or 40 => ConnectionErrorCategory.NetworkConnectivity,
        5 or 8 or 25 or 47 or 48 or 49 or 51 or 52 or 53 or 54 or 55 or 56 or 57 =>
            ConnectionErrorCategory.Configuration,
        11 => ConnectionErrorCategory.Timeout,
        19 => ConnectionErrorCategory.ConnectionLifecycle,
        31 => ConnectionErrorCategory.TlsSecurity,
        35 => ConnectionErrorCategory.InternalError,
        _ => ConnectionErrorCategory.Unknown
    };

    private static ConnectionErrorCategory OperatingSystemCategory(
        int code, ConnectionErrorCategory capturedKind)
    {
        if (capturedKind != ConnectionErrorCategory.Unknown)
        {
            return capturedKind;
        }

        return code switch
        {
            258 or 10060 => ConnectionErrorCategory.Timeout,
            11001 or 11002 or 11003 or 11004 =>
                ConnectionErrorCategory.NameResolution,
            10051 or 10053 or 10054 or 10065 =>
                ConnectionErrorCategory.NetworkConnectivity,
            _ => ConnectionErrorCategory.Unknown
        };
    }

    private static string SourceName(ConnectionErrorSource source) => source switch
    {
        ConnectionErrorSource.SqlServer => "sql_server",
        ConnectionErrorSource.Sni => "sni",
        ConnectionErrorSource.OperatingSystem => "os",
        ConnectionErrorSource.Client => "client",
        _ => "unknown"
    };

    private static string ErrorTypePrefix(ConnectionErrorSource source) => source switch
    {
        ConnectionErrorSource.SqlServer => ConnectionErrorAttributePolicy.SqlServerErrorTypePrefix,
        ConnectionErrorSource.Sni => ConnectionErrorAttributePolicy.SniErrorTypePrefix,
        ConnectionErrorSource.OperatingSystem =>
            ConnectionErrorAttributePolicy.OperatingSystemErrorTypePrefix,
        ConnectionErrorSource.Client => ConnectionErrorAttributePolicy.ClientErrorTypePrefix,
        _ => string.Empty
    };

    private static string CategoryName(ConnectionErrorCategory category) => category switch
    {
        ConnectionErrorCategory.NameResolution => "name_resolution",
        ConnectionErrorCategory.NetworkConnectivity => "network_connectivity",
        ConnectionErrorCategory.TlsSecurity => "tls_security",
        ConnectionErrorCategory.Authentication => "authentication",
        ConnectionErrorCategory.AccessPolicy => "access_policy",
        ConnectionErrorCategory.DatabaseOpen => "database_open",
        ConnectionErrorCategory.ResourceThrottling => "resource_throttling",
        ConnectionErrorCategory.ServerAvailability => "server_availability",
        ConnectionErrorCategory.Configuration => "configuration",
        ConnectionErrorCategory.Timeout => "timeout",
        ConnectionErrorCategory.Canceled => "canceled",
        ConnectionErrorCategory.ProtocolError => "protocol_error",
        ConnectionErrorCategory.ConnectionLifecycle => "connection_lifecycle",
        ConnectionErrorCategory.ConnectionRecovery => "connection_recovery",
        ConnectionErrorCategory.ClientResourceExhaustion => "client_resource_exhaustion",
        ConnectionErrorCategory.RoutingRedirect => "routing_redirect",
        ConnectionErrorCategory.InternalError => "internal_error",
        _ => "unknown"
    };
}
