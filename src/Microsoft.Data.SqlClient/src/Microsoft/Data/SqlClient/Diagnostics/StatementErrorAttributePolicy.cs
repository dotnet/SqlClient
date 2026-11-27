// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Diagnostics;

/// <summary>
/// Decides which statement error attributes are exported. Only allow-listed categories,
/// phases, namespaced numeric codes and fixed client types pass; messages, SQL text,
/// SQLSTATE, stacks and exception events are never emitted.
/// </summary>
internal static class StatementErrorAttributePolicy
{
    private static readonly string[] s_phases = ["execute", "attempt", "unknown"];

    internal static IReadOnlyDictionary<string, string> Create(
        StatementErrorClassification classification)
    {
        Dictionary<string, string> attributes = new()
        {
            [ConnectionErrorAttributePolicy.ErrorType] = NormalizeErrorType(
                classification.ErrorType),
            [ConnectionErrorAttributePolicy.Category] = CategoryName(classification.Category),
            [ConnectionErrorAttributePolicy.Source] = SourceName(classification.Source),
            [ConnectionErrorAttributePolicy.Phase] = NormalizePhaseValue(classification.Phase)
        };
        string code = NormalizeCode(classification.ErrorCode);
        if (code.Length > 0)
        {
            attributes[ConnectionErrorAttributePolicy.Code] = code;
        }
        return new ReadOnlyDictionary<string, string>(attributes);
    }

    internal static void Apply(Activity activity, StatementErrorClassification classification)
    {
        activity.SetStatus(ActivityStatusCode.Error);
        foreach (KeyValuePair<string, string> attribute in Create(classification))
        {
            activity.SetTag(attribute.Key, attribute.Value);
        }
    }

    internal static string NormalizePhase(string phase)
    {
        if (string.IsNullOrEmpty(phase) ||
            !phase.StartsWith("statement.", StringComparison.Ordinal))
        {
            return "unknown";
        }
        return NormalizePhaseValue(phase.Substring("statement.".Length));
    }

    internal static string CategoryName(StatementErrorCategory category) => category switch
    {
        StatementErrorCategory.QuerySyntaxSemantics => "query_syntax_semantics",
        StatementErrorCategory.ConstraintViolation => "constraint_violation",
        StatementErrorCategory.Authorization => "authorization",
        StatementErrorCategory.ConcurrencyConflict => "concurrency_conflict",
        StatementErrorCategory.ServerAvailability => "server_availability",
        StatementErrorCategory.ResourceThrottling => "resource_throttling",
        StatementErrorCategory.DatabaseError => "database_error",
        StatementErrorCategory.Timeout => "timeout",
        StatementErrorCategory.Canceled => "canceled",
        StatementErrorCategory.NetworkConnectivity => "network_connectivity",
        StatementErrorCategory.ConnectionLifecycle => "connection_lifecycle",
        StatementErrorCategory.ParameterValidation => "parameter_validation",
        StatementErrorCategory.DataConversion => "data_conversion",
        StatementErrorCategory.ClientResourceExhaustion => "client_resource_exhaustion",
        _ => "unknown"
    };

    private static string NormalizePhaseValue(string phase) =>
        Array.IndexOf(s_phases, phase) >= 0 ? phase : "unknown";

    private static string NormalizeErrorType(string value)
    {
        if (value == "unknown")
        {
            return value;
        }
        foreach (string prefix in new[]
        {
            ConnectionErrorAttributePolicy.SqlServerErrorTypePrefix,
            ConnectionErrorAttributePolicy.SniErrorTypePrefix,
            ConnectionErrorAttributePolicy.OperatingSystemErrorTypePrefix
        })
        {
            if (IsNumeric(value, prefix))
            {
                return value;
            }
        }
        if (value.StartsWith(ConnectionErrorAttributePolicy.ClientErrorTypePrefix,
            StringComparison.Ordinal))
        {
            string name = value.Substring(
                ConnectionErrorAttributePolicy.ClientErrorTypePrefix.Length);
            foreach (StatementErrorCategory category in
                (StatementErrorCategory[])Enum.GetValues(typeof(StatementErrorCategory)))
            {
                if (category != StatementErrorCategory.Unknown && CategoryName(category) == name)
                {
                    return value;
                }
            }
        }
        return "unknown";
    }

    private static string NormalizeCode(string value)
    {
        foreach (string prefix in new[] { "sqlserver:", "sni:", "os:" })
        {
            if (IsNumeric(value, prefix))
            {
                return value;
            }
        }
        return string.Empty;
    }

    private static bool IsNumeric(string value, string prefix) =>
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
}
