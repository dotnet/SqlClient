// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.Data.SqlClient.Diagnostics;

internal readonly record struct StatementErrorClassification(
    StatementErrorCategory Category,
    ConnectionErrorSource Source,
    string ErrorType,
    string ErrorCode,
    string Phase);

/// <summary>
/// Maps captured statement evidence to a category. Precedence follows the JDBC statement
/// taxonomy: driver-confirmed cancel/timeout, then the SQL Server number registry (other
/// confirmed server numbers become database_error), then SNI/OS codes, then the observed
/// client exception kind, then unknown. Retry/transient lists never define a category.
/// </summary>
internal static class StatementErrorClassifier
{
    internal static StatementErrorClassification Classify(StatementErrorEvidence evidence)
    {
        if (evidence.IsAmbiguous)
        {
            return new StatementErrorClassification(
                StatementErrorCategory.Unknown, ConnectionErrorSource.Unknown,
                "unknown", string.Empty, evidence.Phase);
        }

        StatementErrorCategory category = evidence.Kind;
        if (evidence.Code.HasValue)
        {
            category = evidence.Source switch
            {
                ConnectionErrorSource.SqlServer => SqlServerCategory(evidence.Code.Value),
                ConnectionErrorSource.Sni or ConnectionErrorSource.OperatingSystem =>
                    TransportCategory(evidence.Source, evidence.Code.Value),
                _ => category
            };
        }

        ConnectionErrorSource source = evidence.Source == ConnectionErrorSource.Unknown &&
            category != StatementErrorCategory.Unknown
                ? ConnectionErrorSource.Client
                : evidence.Source;
        bool hasCode = evidence.Code.HasValue && source != ConnectionErrorSource.Unknown;
        string code = hasCode
            ? evidence.Code!.Value.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        string errorType = hasCode
            ? ErrorTypePrefix(source) + code
            : category == StatementErrorCategory.Unknown
                ? "unknown"
                : ConnectionErrorAttributePolicy.ClientErrorTypePrefix +
                    StatementErrorAttributePolicy.CategoryName(category);
        string errorCode = hasCode ? ErrorTypePrefix(source).TrimEnd('.') + ":" + code : "";

        return new StatementErrorClassification(
            category, source, errorType, errorCode, evidence.Phase);
    }

    // JDBC statement registry. 49918-49920 are resource_throttling here and in the
    // connection classifier (cross-driver open item O6).
    private static StatementErrorCategory SqlServerCategory(int code) => code switch
    {
        102 or 156 or 207 or 208 or 2812 => StatementErrorCategory.QuerySyntaxSemantics,
        515 or 547 or 2601 or 2627 => StatementErrorCategory.ConstraintViolation,
        229 or 230 => StatementErrorCategory.Authorization,
        1205 or 1222 => StatementErrorCategory.ConcurrencyConflict,
        40143 or 40166 or 40197 or 40540 or 40613 or 4221 or 42108 or 42109 =>
            StatementErrorCategory.ServerAvailability,
        40501 or 10928 or 10929 or 49918 or 49919 or 49920 =>
            StatementErrorCategory.ResourceThrottling,
        64 or 10053 or 10054 => StatementErrorCategory.NetworkConnectivity,
        _ => StatementErrorCategory.DatabaseError
    };

    private static StatementErrorCategory TransportCategory(
        ConnectionErrorSource source, int code) => (source, code) switch
    {
        (ConnectionErrorSource.Sni, 2 or 40) => StatementErrorCategory.NetworkConnectivity,
        (ConnectionErrorSource.Sni, 11) => StatementErrorCategory.Timeout,
        (ConnectionErrorSource.Sni, 19) => StatementErrorCategory.ConnectionLifecycle,
        (ConnectionErrorSource.OperatingSystem, 258 or 10060) => StatementErrorCategory.Timeout,
        (ConnectionErrorSource.OperatingSystem, 10051 or 10053 or 10054 or 10065) =>
            StatementErrorCategory.NetworkConnectivity,
        _ => StatementErrorCategory.Unknown
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
}
