// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Diagnostics;

internal static class ConnectionErrorAttributePolicy
{
    internal const string ErrorType = "error.type";
    internal const string SqlServerErrorTypePrefix = "sqlserver.";
    internal const string SniErrorTypePrefix = "sni.";
    internal const string OperatingSystemErrorTypePrefix = "os.";
    internal const string ClientErrorTypePrefix = "client.";
    internal const string Category = "sqlclient.error.category";
    internal const string Source = "sqlclient.error.source";
    internal const string Code = "sqlclient.error.code";
    internal const string Phase = "sqlclient.error.phase";
    internal const string DbSystemName = "db.system.name";
    internal const string DbOperationName = "db.operation.name";
    internal const string Step = "sqlclient.error.step";

    /// <summary>
    /// Infers which sub-step of an aggregate transport scope failed, from numbers only: the SNI
    /// error number and the accompanying operating system code. Native SNI exposes no per-step
    /// timings (as in msodbcsql), so this is the only step signal there; managed SNI produces the
    /// same SqlError numbers, so both report the same value. Returns one of name_resolution,
    /// socket_connect, instance_discovery, tls or unknown.
    /// </summary>
    internal static string TransportStep(ConnectionErrorEvidence evidence)
    {
        if (evidence == null || evidence.IsAmbiguous)
        {
            return "unknown";
        }

        if (evidence.Source == ConnectionErrorSource.Sni)
        {
            switch (evidence.Code)
            {
                // SNI 26: error locating server/instance (SQL Browser / SSRP).
                case 26:
                    return "instance_discovery";
                // SNI 31: SSL/TLS handshake failure.
                case 31:
                    return "tls";
            }
        }

        int? native = evidence.Source switch
        {
            ConnectionErrorSource.Sni => evidence.NativeCode,
            ConnectionErrorSource.OperatingSystem => evidence.Code,
            _ => null
        };
        if (native.HasValue)
        {
            string step = NativeStep(native.Value);
            if (step != null)
            {
                return step;
            }
        }

        return evidence.Kind switch
        {
            ConnectionErrorCategory.NameResolution => "name_resolution",
            ConnectionErrorCategory.TlsSecurity => "tls",
            _ => "unknown"
        };
    }

    private static string NativeStep(int code)
    {
        switch (code)
        {
            // WSAHOST_NOT_FOUND, WSATRY_AGAIN, WSANO_RECOVERY, WSANO_DATA.
            case 11001 or 11002 or 11003 or 11004:
                return "name_resolution";
            // WSAENETUNREACH, WSAETIMEDOUT, WSAECONNREFUSED, WSAEHOSTUNREACH.
            case 10051 or 10060 or 10061 or 10065:
                return "socket_connect";
        }
        // SEC_E_* SSPI/Schannel failures are HRESULTs 0x8009xxxx.
        return (code & unchecked((int)0xFFFF0000)) == unchecked((int)0x80090000) ? "tls" : null;
    }

    private static readonly string[] s_phases =
    [
        "open", "open_attempt", "physical_open", "endpoint_attempt", "configuration",
        "transport", "instance_discovery", "name_resolution", "socket_connect", "tls_setup",
        "prelogin_send", "prelogin_receive", "login", "token_request", "redirect", "initialization",
        "unknown"
    ];

    private static readonly string[] s_clientErrorTypes =
    [
        "client.name_resolution", "client.network_connectivity", "client.tls_security",
        "client.authentication", "client.access_policy", "client.database_open",
        "client.resource_throttling", "client.server_availability", "client.configuration",
        "client.timeout", "client.canceled", "client.protocol_error",
        "client.connection_lifecycle", "client.connection_recovery",
        "client.client_resource_exhaustion", "client.routing_redirect", "client.internal_error"
    ];

    internal static IReadOnlyDictionary<string, string> Create(
        ConnectionErrorClassification classification)
    {
        Dictionary<string, string> attributes = new()
        {
            [ErrorType] = NormalizeErrorType(classification.ErrorType),
            [Category] = CategoryName(classification.Category),
            [Source] = SourceName(classification.Source),
            [Phase] = NormalizePhaseValue(classification.Phase)
        };
        string code = NormalizeCode(classification.ErrorCode);
        if (code.Length > 0)
        {
            attributes[Code] = code;
        }
        return new ReadOnlyDictionary<string, string>(attributes);
    }

    internal static void Apply(Activity activity, ConnectionErrorClassification classification)
    {
        activity.SetStatus(ActivityStatusCode.Error);
        foreach (KeyValuePair<string, string> attribute in Create(classification))
        {
            activity.SetTag(attribute.Key, attribute.Value);
        }
    }

    internal static string NormalizePhase(string phase)
    {
        if (string.IsNullOrEmpty(phase) || !phase.StartsWith("connection.", StringComparison.Ordinal))
        {
            return "unknown";
        }

        return NormalizePhaseValue(phase.Substring("connection.".Length).Replace('.', '_'));
    }

    private static string NormalizePhaseValue(string phase) =>
        Array.IndexOf(s_phases, phase) >= 0 ? phase : "unknown";

    private static string NormalizeErrorType(string value)
    {
        if (value == "unknown" ||         Array.IndexOf(s_clientErrorTypes, value) >= 0 ||
            IsNamespacedCode(value, SqlServerErrorTypePrefix) ||
            IsNamespacedCode(value, SniErrorTypePrefix) ||
            IsNamespacedCode(value, OperatingSystemErrorTypePrefix) ||
            IsNamespacedCode(value, ClientErrorTypePrefix))
        {
            return value;
        }
        return "unknown";
    }

    private static string NormalizeCode(string value)
    {
        foreach (string prefix in new[] { "sqlserver:", "sni:", "os:", "client:" })
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(value.Substring(prefix.Length), out _))
            {
                return value;
            }
        }
        return string.Empty;
    }

    private static bool IsNamespacedCode(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.Ordinal) &&
        int.TryParse(value.Substring(prefix.Length), out _);

    private static string SourceName(ConnectionErrorSource source) => source switch
    {
        ConnectionErrorSource.SqlServer => "sql_server",
        ConnectionErrorSource.Sni => "sni",
        ConnectionErrorSource.OperatingSystem => "os",
        ConnectionErrorSource.Client => "client",
        _ => "unknown"
    };

    internal static string CategoryName(ConnectionErrorCategory category) => category switch
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
