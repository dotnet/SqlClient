// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.Data.SqlClient.Diagnostics;

/// <summary>
/// OpenTelemetry-style operation metrics recorded for every SqlClient lifecycle scope (roots,
/// attempts and connection phases), independently of span sampling. Labels are bounded: system,
/// phase (the scope name), operation kind, schema version, outcome and error category. No
/// server, database, query text, error number or exception detail is used as a label.
/// </summary>
/// <remarks>
/// When no <see cref="MeterListener"/> enables an instrument, <see cref="Enabled"/> is false
/// and scopes take no timestamps and allocate nothing for metrics.
/// </remarks>
internal static class OperationMetrics
{
    internal const string MeterName = "Microsoft.Data.SqlClient";
    internal const string Outcome = "sqlclient.operation.outcome";
    internal const string AttemptCount = "sqlclient.operation.attempt_count";
    internal const string OperationCount = "db.client.operation.count";
    internal const string OperationDuration = "db.client.operation.duration";
    internal const string ErrorCount = "db.client.operation.error.count";
    internal const string RetryCount = "db.client.operation.retry.count";

    private static readonly double[] s_durationBuckets =
    [
        0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1,
        2.5, 5, 10, 30
    ];

    internal static readonly Meter Meter = new(MeterName, ThisAssembly.FileVersion);

    private static readonly Counter<long> s_operations = Meter.CreateCounter<long>(
        OperationCount, "{operation}", "Completed SqlClient operations");

    private static readonly Histogram<double> s_durations = Meter.CreateHistogram(
        OperationDuration, "s", "SqlClient operation duration", tags: null,
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_durationBuckets });

    private static readonly Counter<long> s_errors = Meter.CreateCounter<long>(
        ErrorCount, "{error}", "Completed SqlClient operations with a terminal error");

    private static readonly Counter<long> s_retries = Meter.CreateCounter<long>(
        RetryCount, "{retry}", "Additional SqlClient attempts begun");

    // Metric label names are shared with the JDBC driver so one dashboard can query both.
    // They are provisional and metrics-only; span attributes keep their sqlclient.* names.
    internal const string Phase = "mssql.performance.activity";
    internal const string Kind = "mssql.operation.kind";
    internal const string SchemaVersion = "mssql.telemetry.schema.version";
    internal const string MetricOutcome = "mssql.operation.outcome";
    internal const string MetricCategory = "mssql.error.category";
    internal const string RetryReason = "mssql.retry.reason";
    internal const string SchemaVersionValue = "1.0";

    /// <summary>True when any operation instrument has a listener.</summary>
    internal static bool Enabled =>
        s_operations.Enabled || s_durations.Enabled || s_errors.Enabled || s_retries.Enabled;

    /// <summary>
    /// Records one completed scope. Retries are recorded only on roots, as attempts beyond the
    /// first, labelled with the closed reason derived from the previous attempt's failure.
    /// </summary>
    internal static void Record(OperationScope scope, string outcome, double seconds)
    {
        string name = scope.Name;
        string activity = ActivityName(name);
        string kind = name.StartsWith("statement.", StringComparison.Ordinal)
            ? "statement"
            : "connection";

        TagList tags = new()
        {
            { ConnectionErrorAttributePolicy.DbSystemName, "microsoft.sql_server" },
            { Phase, activity },
            { Kind, kind },
            { SchemaVersion, SchemaVersionValue },
            { MetricOutcome, outcome }
        };
        s_operations.Add(1, tags);
        s_durations.Record(seconds, tags);

        if (outcome != "success" && s_errors.Enabled)
        {
            TagList errorTags = new()
            {
                { ConnectionErrorAttributePolicy.DbSystemName, "microsoft.sql_server" },
                { Phase, activity },
                { Kind, kind },
                { SchemaVersion, SchemaVersionValue },
                { MetricCategory, scope.Category ?? "unknown" }
            };
            s_errors.Add(1, errorTags);
        }

        if (scope.IsRoot && scope.Attempts > 1 && s_retries.Enabled)
        {
            TagList retryTags = new()
            {
                { ConnectionErrorAttributePolicy.DbSystemName, "microsoft.sql_server" },
                { Phase, activity },
                { Kind, kind },
                { SchemaVersion, SchemaVersionValue },
                { RetryReason, scope.RetryReason ?? "not_applicable" }
            };
            s_retries.Add(scope.Attempts - 1, retryTags);
        }
    }

    /// <summary>
    /// Maps a SqlClient scope name to the shared <c>mssql.performance.activity</c> value. Scopes
    /// with a JDBC counterpart use the JDBC name; SqlClient-only scopes use a
    /// <c>connection.*</c> name in the same single-segment style.
    /// </summary>
    internal static string ActivityName(string scopeName) => scopeName switch
    {
        "connection.open.attempt" => "connection.attempt",
        "connection.name_resolution" => "connection.dns",
        "connection.tls.setup" => "connection.tls",
        "connection.initialization" => "connection.initialize",
        "connection.prelogin.send" => "connection.prelogin_send",
        "connection.prelogin.receive" => "connection.prelogin_receive",
        _ => scopeName
    };
}