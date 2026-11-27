// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Connection;

namespace Microsoft.Data.SqlClient.Diagnostics
{
    internal static class ConnectionOpenActivity
    {
        internal const string RouteProperty = "Microsoft.Data.SqlClient.Connection.Route";
        internal static readonly ActivitySource Source =
            new ActivitySource("Microsoft.Data.SqlClient.Connection", ThisAssembly.FileVersion);

        // Creation-time marker that lets a private endpoint listener sample only its own route.
        // It is removed immediately after creation so it is never exported.
        internal const string RouteTag = "microsoft.data.sqlclient.internal.route";

        /// <summary>
        /// Starts an activity, passing a private route as a creation-time sampling tag. Unrouted
        /// activities use the plain overloads so they allocate nothing extra.
        /// </summary>
        internal static Activity StartRouted(
            ActivitySource source, string name, ActivityKind kind, string parentId, object route)
        {
            if (route == null)
            {
                return parentId == null
                    ? source.StartActivity(name, kind)
                    : source.StartActivity(name, kind, parentId);
            }

            KeyValuePair<string, object>[] tags = { new(RouteTag, route) };
            Activity activity = parentId == null
                ? source.StartActivity(name, kind, default(ActivityContext), tags)
                : source.StartActivity(name, kind, parentId, tags);
            if (activity != null)
            {
                activity.SetTag(RouteTag, null);
                activity.SetCustomProperty(RouteProperty, route);
            }
            return activity;
        }

        /// <summary>
        /// Starts the connection.open root. Returns null when neither tracing samples the root
        /// nor the operation meter is enabled.
        /// </summary>
        internal static OperationScope Start(IDisposable route = null)
        {
            Activity activity = Source.HasListeners() || route != null
                ? StartRouted(Source, "connection.open", ActivityKind.Client, null, route)
                : null;
            activity?.SetTag(ConnectionErrorAttributePolicy.DbSystemName, "microsoft.sql_server");
            activity?.SetTag(ConnectionErrorAttributePolicy.DbOperationName, "connection.open");
            return OperationScope.Create(activity, "connection.open", isRoot: true, route);
        }

        /// <summary>
        /// Starts one open attempt beneath <paramref name="parent"/>. A span is created only when
        /// the root has one, so meter-only roots never produce phantom attempt roots.
        /// </summary>
        internal static OperationScope StartAttempt(OperationScope parent, SqlConnection owner)
        {
            if (parent == null)
            {
                return null;
            }

            parent.Attempts++;
            Activity root = parent.Activity;
            root?.SetTag(OperationMetrics.AttemptCount, parent.Attempts);
            if (parent.Attempts > 1)
            {
                // Retry decision: why the previous attempt failed, from a closed set.
                parent.RetryReason = parent.LastFailureReason ?? "other";
                root?.SetTag(RetryReasonTag, parent.RetryReason);
            }
            Activity activity = root == null
                ? null
                : StartRouted(Source, "connection.open.attempt", ActivityKind.Internal, root.Id,
                    parent.Route);
            OperationScope scope = OperationScope.Create(activity, "connection.open.attempt",
                route: parent.Route);
            if (scope != null)
            {
                scope.Context = new AttemptContext(owner);
            }
            return scope;
        }

        internal static void FinishAttempt(OperationScope scope)
        {
            if (scope?.Context is AttemptContext context)
            {
                context.Finish();
            }
        }

        internal static OperationScope StartPhysical(SqlConnection owner, out IDisposable route)
        {
            route = null;
            OperationScope parent = OperationScope.Current;
            Activity parentActivity = parent?.Activity;
            if (owner == null || parent?.Context is not AttemptContext context ||
                !context.TryRetain(owner, parent.Route, out route))
            {
                return null;
            }
            try
            {
                Activity activity = parentActivity == null
                    ? null
                    : StartRouted(Source, "connection.physical_open", ActivityKind.Internal,
                        parentActivity.Id, route);
                OperationScope scope = OperationScope.Create(activity, "connection.physical_open",
                    route: route);
                if (scope == null)
                {
                    route?.Dispose();
                    route = null;
                    return null;
                }
                scope.PhaseParent = true;
                return scope;
            }
            catch
            {
                route?.Dispose();
                route = null;
                throw;
            }
        }

        internal static OperationScope StartPhase(string name)
        {
            OperationScope parent = OperationScope.Current;
            if (parent == null || !parent.PhaseParent)
            {
                return null;
            }

            Activity parentActivity = parent.Activity;
            Activity activity = parentActivity == null
                ? null
                : StartRouted(Source, name, ActivityKind.Internal, parentActivity.Id,
                    parent.Route);
            OperationScope scope = OperationScope.Create(activity, name, route: parent.Route);
            if (scope != null)
            {
                scope.PhaseParent = true;
            }
            return scope;
        }

        internal static void RecordError(OperationScope scope, Exception exception)
        {
            if (scope == null)
            {
                return;
            }

            ConnectionErrorEvidence evidence =
                ConnectionErrorEvidence.Capture(exception, scope.Name);
            RecordError(scope, evidence);
        }

        /// <summary>
        /// Classifies the failure for the scope's metrics and, when a span exists, applies the
        /// exported error attributes.
        /// </summary>
        internal static void RecordError(OperationScope scope, ConnectionErrorEvidence evidence)
        {
            if (scope == null)
            {
                return;
            }

            ConnectionErrorClassification classification =
                ConnectionErrorClassifier.Classify(evidence);
            scope.Category = ConnectionErrorAttributePolicy.CategoryName(classification.Category);
            if (scope.Name == "connection.open.attempt" && scope.Parent != null)
            {
                scope.Parent.LastFailureReason = RetryReason(evidence, classification.Category);
            }
            if (scope.Activity != null)
            {
                ConnectionErrorAttributePolicy.Apply(scope.Activity, classification);
                if (scope.Name == "connection.transport")
                {
                    scope.Activity.SetTag(ConnectionErrorAttributePolicy.Step,
                        ConnectionErrorAttributePolicy.TransportStep(evidence));
                }
            }
        }

        internal const string RetryReasonTag = "sqlclient.retry.reason";
        internal const string ActivityIdTag = "mssqldriver.activity_id";

        /// <summary>
        /// Tags the recorded connection.open root with the ActivityId the driver already sends in
        /// PRELOGIN, so a span can be joined to server-side XEvents. Does nothing (and formats
        /// nothing) when no root span is recorded. The wire value is unchanged.
        /// </summary>
        internal static void RecordActivityId(ActivityCorrelator.ActivityId id)
        {
            if (id == null || !Source.HasListeners())
            {
                return;
            }
            OperationScope scope = OperationScope.Current;
            for (; scope != null; scope = scope.Parent)
            {
                if (scope.IsRoot)
                {
                    if (scope.Activity is { IsAllDataRequested: true } root &&
                        scope.Name == "connection.open")
                    {
                        root.SetTag(ActivityIdTag, id.ToString());
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Maps a failed attempt to a closed retry reason, using only the error number and the
        /// driver's existing login transient-error list: transient_server_error, timeout or
        /// other.
        /// </summary>
        internal static string RetryReason(
            ConnectionErrorEvidence evidence, ConnectionErrorCategory category)
        {
            if (evidence != null && !evidence.IsAmbiguous &&
                evidence.Source == ConnectionErrorSource.SqlServer && evidence.Code.HasValue &&
                SqlConnectionInternal.IsTransientErrorNumber(evidence.Code.Value))
            {
                return "transient_server_error";
            }
            return category == ConnectionErrorCategory.Timeout ? "timeout" : "other";
        }

        private sealed class AttemptContext(SqlConnection owner)
        {
            private readonly WeakReference<SqlConnection> _owner = new(owner);
            private readonly object _gate = new();
            private bool _active = true;

            internal bool TryRetain(SqlConnection owner, IDisposable source, out IDisposable route)
            {
                lock (_gate)
                {
                    route = null;
                    if (!_active || !_owner.TryGetTarget(out SqlConnection target) ||
                        !ReferenceEquals(owner, target))
                    {
                        return false;
                    }

                    route = OpenTelemetryExtensionBinding.Retain(source);
                    return true;
                }
            }

            internal void Finish()
            {
                lock (_gate)
                {
                    _active = false;
                }
            }
        }
    }
}
