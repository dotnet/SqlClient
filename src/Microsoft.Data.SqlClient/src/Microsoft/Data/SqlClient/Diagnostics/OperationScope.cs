// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Data.SqlClient.Diagnostics;

/// <summary>
/// One SqlClient lifecycle scope: a root (connection.open, statement.execute), an attempt or a
/// connection phase. A scope wraps an optional sampled <see cref="System.Diagnostics.Activity"/>
/// and, only while the operation meter has a listener, the state needed to record metrics
/// without a span. When neither tracing nor metrics observe the scope, no scope is created.
/// </summary>
/// <remarks>
/// Meter-only scopes flow through their own <see cref="AsyncLocal{T}"/>, because no Activity
/// exists to carry parentage. The AsyncLocal is written only while metrics are enabled.
/// </remarks>
internal sealed class OperationScope : IDisposable
{
    internal const string ScopeProperty = "Microsoft.Data.SqlClient.Scope";

    private static readonly AsyncLocal<OperationScope> s_current = new();

    // Set once a routed (private endpoint) scope is created, so routed meter-only scopes can
    // find their parent through the flow. Until then the no-listener path never reads it.
    private static volatile bool s_routedFlow;

    private readonly OperationScope _previous;
    private readonly long _startTimestamp;
    private int _completed;

    private OperationScope(
        Activity activity, string name, bool isRoot, bool coreMetrics, IDisposable route)
    {
        bool tracked = coreMetrics || route != null;
        Activity = activity;
        Name = name;
        IsRoot = isRoot;
        Tracked = tracked;
        CoreMetrics = coreMetrics;
        Route = route;
        Parent = (tracked ? LiveFlow() : null) ??
            Live(activity?.Parent?.GetCustomProperty(ScopeProperty) as OperationScope);
        if (tracked)
        {
            if (activity == null)
            {
                _startTimestamp = Stopwatch.GetTimestamp();
            }
            _previous = s_current.Value;
            s_current.Value = this;
        }
        activity?.SetCustomProperty(ScopeProperty, this);
    }

    /// <summary>The sampled span, or null for a meter-only scope.</summary>
    internal Activity Activity { get; }

    /// <summary>The span name, also used as the bounded metric phase label.</summary>
    internal string Name { get; }

    internal bool IsRoot { get; }

    /// <summary>The live scope that was current when this scope began.</summary>
    internal OperationScope Parent { get; }

    /// <summary>True when the scope records metrics on completion.</summary>
    internal bool Tracked { get; }

    /// <summary>True when the scope records to the process-wide operation meter.</summary>
    internal bool CoreMetrics { get; }

    /// <summary>
    /// The private endpoint route (an opaque extension lease) this scope belongs to, or null.
    /// Routed scopes record endpoint metrics at scope end, independently of span sampling.
    /// </summary>
    internal IDisposable Route { get; }

    /// <summary>True when connection phases may be created beneath this scope.</summary>
    internal bool PhaseParent { get; set; }

    /// <summary>Scope-specific ownership state (attempt context or statement owner).</summary>
    internal object Context { get; set; }

    /// <summary>The bounded error category, set when the scope records a failure.</summary>
    internal string Category { get; set; }

    /// <summary>Attempts begun beneath a root.</summary>
    internal int Attempts { get; set; }

    /// <summary>
    /// The bounded reason the last attempt beneath a root failed, used to label the next retry.
    /// </summary>
    internal string LastFailureReason { get; set; }

    /// <summary>The bounded reason of the most recent retry beneath a root, or null.</summary>
    internal string RetryReason { get; set; }

    internal bool IsCompleted => Volatile.Read(ref _completed) != 0;

    /// <summary>
    /// Creates a scope for <paramref name="activity"/>, or a meter-only scope when metrics are
    /// enabled or the operation is routed to a private endpoint. Returns null when nothing
    /// observes the operation, so the disabled path allocates nothing.
    /// </summary>
    internal static OperationScope Create(
        Activity activity, string name, bool isRoot = false, IDisposable route = null)
    {
        bool coreMetrics = OperationMetrics.Enabled;
        if (activity == null && !coreMetrics && route == null)
        {
            return null;
        }
        if (route != null && !s_routedFlow)
        {
            s_routedFlow = true;
        }
        return new OperationScope(activity, name, isRoot, coreMetrics, route);
    }

    /// <summary>
    /// Returns the innermost live SqlClient scope: the meter-only flow when metrics are enabled
    /// or a routed scope has been created, otherwise the scope attached to
    /// <see cref="System.Diagnostics.Activity.Current"/>.
    /// </summary>
    internal static OperationScope Current
    {
        get
        {
            OperationScope scope = OperationMetrics.Enabled || s_routedFlow ? LiveFlow() : null;
            return scope ??
                Live(Activity.Current?.GetCustomProperty(ScopeProperty) as OperationScope);
        }
    }

    /// <summary>
    /// Returns the innermost live scope in the meter-only flow. A scope that ended on another
    /// asynchronous context can remain in this flow; its predecessor is then the live scope.
    /// </summary>
    private static OperationScope LiveFlow()
    {
        OperationScope scope = s_current.Value;
        while (scope != null && scope.IsCompleted)
        {
            scope = scope._previous;
        }
        return scope;
    }

    private static OperationScope Live(OperationScope scope) =>
        scope == null || scope.IsCompleted ? null : scope;

    /// <summary>
    /// Reads the raw meter-only flow value so callers that restore
    /// <see cref="System.Diagnostics.Activity.Current"/> can restore it too.
    /// </summary>
    internal static OperationScope CurrentFlow => s_current.Value;

    /// <summary>Restores the meter-only flow, writing only when it changes.</summary>
    internal static void RestoreFlow(OperationScope scope)
    {
        if (!ReferenceEquals(s_current.Value, scope))
        {
            s_current.Value = scope;
        }
    }

    /// <summary>
    /// Makes <paramref name="scope"/> current for both tracing and the meter-only flow, for
    /// operations whose children are started from another asynchronous context.
    /// </summary>
    internal static void MakeCurrent(OperationScope scope)
    {
        if (scope == null)
        {
            return;
        }
        if (scope.Activity != null)
        {
            Activity.Current = scope.Activity;
        }
        if (scope.Tracked)
        {
            RestoreFlow(scope);
        }
    }

    internal string Outcome
    {
        get
        {
            string category = Category;
            if (category == "timeout" || category == "canceled")
            {
                return category;
            }
            return category != null || Activity?.Status == ActivityStatusCode.Error
                ? "failure"
                : "success";
        }
    }

    /// <summary>
    /// Ends the scope once: stops the span, restores the meter-only flow and records metrics.
    /// A root's outcome is tagged before the span stops so exporters observe it.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        if (Tracked && ReferenceEquals(s_current.Value, this))
        {
            s_current.Value = _previous;
        }

        string outcome = Outcome;
        Activity activity = Activity;
        if (activity != null)
        {
            if (IsRoot)
            {
                activity.SetTag(OperationMetrics.Outcome, outcome);
            }
            activity.Stop();
        }

        if (Tracked)
        {
            double seconds = activity != null
                ? activity.Duration.TotalSeconds
                : (Stopwatch.GetTimestamp() - _startTimestamp) / (double)Stopwatch.Frequency;
            if (CoreMetrics)
            {
                OperationMetrics.Record(this, outcome, seconds);
            }
            if (Route != null)
            {
                OpenTelemetryExtensionBinding.Record(Route, this, outcome, seconds);
            }
        }
    }
}
