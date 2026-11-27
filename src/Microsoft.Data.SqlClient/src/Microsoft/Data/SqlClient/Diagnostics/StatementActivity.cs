// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.Diagnostics;

/// <summary>
/// Creates statement.execute roots and statement.attempt children around public SqlCommand
/// execute APIs. A root covers the execute call until it returns or faults; reading rows from
/// a returned reader is not part of the root. Query text is exported only when the
/// EnableOpenTelemetryQueryText switch is on, only for CommandType.Text, and only in masked form.
/// </summary>
internal static class StatementActivity
{
    internal const string SourceName = "Microsoft.Data.SqlClient.Command";
    internal const string RootName = "statement.execute";
    internal const string AttemptName = "statement.attempt";
    internal const string QueryText = "db.query.text";
    internal const string ParameterCount = "sqlclient.statement.parameter_count";
    internal const string CommandTimeout = "sqlclient.statement.timeout";
    internal const string AttemptNumber = "sqlclient.statement.attempt";

    internal static readonly ActivitySource Source = new(SourceName, ThisAssembly.FileVersion);

    /// <summary>
    /// Starts a root for <paramref name="command"/>, or returns null when neither tracing nor
    /// the operation meter observes it, or when this command already owns the current root (for
    /// example ExecuteScalarAsync delegating to ExecuteReaderAsync). On success the root is
    /// current.
    /// </summary>
    internal static OperationScope Start(SqlCommand command, string operation)
    {
        string endpoint = command.Connection?.ConnectionOptions?.OtelEndpoint;
        if (string.IsNullOrEmpty(endpoint) && !Source.HasListeners() &&
            !OperationMetrics.Enabled)
        {
            return null;
        }
        if (FindRoot(command) != null)
        {
            return null;
        }

        IDisposable route = OpenTelemetryExtensionBinding.Begin(endpoint);
        OperationScope scope;
        try
        {
            Activity activity = Source.HasListeners() || route != null
                ? ConnectionOpenActivity.StartRouted(
                    Source, RootName, ActivityKind.Client, null, route)
                : null;
            scope = OperationScope.Create(activity, RootName, isRoot: true, route);
        }
        catch
        {
            route?.Dispose();
            throw;
        }
        if (scope == null)
        {
            route?.Dispose();
            return null;
        }

        scope.Context = new Owner(command, route);
        Activity root = scope.Activity;
        if (root != null)
        {
            root.SetTag(ConnectionErrorAttributePolicy.DbSystemName, "microsoft.sql_server");
            root.SetTag(ConnectionErrorAttributePolicy.DbOperationName, operation);
            root.SetTag(ParameterCount, command.Parameters.Count);
            root.SetTag(CommandTimeout, command.CommandTimeout);
            if (LocalAppContextSwitches.EnableOpenTelemetryQueryText &&
                command.CommandType == CommandType.Text)
            {
                string masked = SqlStatementSanitizer.Mask(command.CommandText);
                if (masked != null)
                {
                    root.SetTag(QueryText, masked);
                }
            }
        }
        return scope;
    }

    /// <summary>
    /// Runs one retry attempt as a statement.attempt child of the command's current root.
    /// Without a root it simply runs <paramref name="attempt"/>.
    /// </summary>
    internal static T Attempt<T>(SqlCommand command, Func<T> attempt)
    {
        OperationScope root = FindRoot(command);
        if (root == null)
        {
            return attempt();
        }

        OperationScope scope = StartAttempt(root);
        try
        {
            return attempt();
        }
        catch (Exception ex)
        {
            RecordError(scope, ex);
            throw;
        }
        finally
        {
            scope?.Dispose();
            OperationScope.MakeCurrent(root);
        }
    }

    /// <summary>
    /// Runs one asynchronous retry attempt as a statement.attempt child that ends when the
    /// returned task completes. The root is restored as current so later attempts are siblings.
    /// </summary>
    internal static Task<T> AttemptAsync<T>(SqlCommand command, Func<Task<T>> attempt)
    {
        OperationScope root = FindRoot(command);
        if (root == null)
        {
            return attempt();
        }

        OperationScope scope = StartAttempt(root);
        Task<T> task;
        try
        {
            task = attempt();
        }
        catch (Exception ex)
        {
            RecordError(scope, ex);
            scope?.Dispose();
            throw;
        }
        finally
        {
            OperationScope.MakeCurrent(root);
        }
        return scope == null ? task : CompleteWhenDone(task, scope);
    }

    /// <summary>
    /// Runs an asynchronous execute API under a statement root that ends when the task completes.
    /// The caller's current activity and scope flow are restored before returning.
    /// </summary>
    internal static Task<T> ExecuteAsync<TState, T>(
        SqlCommand command,
        string operation,
        TState state,
        Func<SqlCommand, TState, Task<T>> execute)
    {
        Activity previous = Activity.Current;
        OperationScope previousFlow = OperationScope.CurrentFlow;
        OperationScope root = Start(command, operation);
        if (root == null)
        {
            return execute(command, state);
        }

        Task<T> task;
        try
        {
            task = execute(command, state);
        }
        catch (Exception ex)
        {
            RecordError(root, ex);
            Complete(root);
            throw;
        }
        finally
        {
            Activity.Current = previous;
            OperationScope.RestoreFlow(previousFlow);
        }
        return CompleteWhenDone(task, root);
    }

    /// <summary>
    /// Classifies the failure for the scope's metrics and, when a span exists, applies the
    /// exported error attributes.
    /// </summary>
    internal static void RecordError(OperationScope scope, Exception exception)
    {
        if (scope == null)
        {
            return;
        }
        StatementErrorEvidence evidence = StatementErrorEvidence.Capture(exception, scope.Name);
        StatementErrorClassification classification = StatementErrorClassifier.Classify(evidence);
        scope.Category = StatementErrorAttributePolicy.CategoryName(classification.Category);
        if (scope.Activity != null)
        {
            StatementErrorAttributePolicy.Apply(scope.Activity, classification);
        }
    }

    /// <summary>
    /// Completes a root: defaults the attempt count to one for non-retry executions, ends the
    /// scope (span and metrics) and releases any private route.
    /// </summary>
    internal static void Complete(OperationScope root)
    {
        if (root == null)
        {
            return;
        }
        if (root.Attempts == 0)
        {
            root.Attempts = 1;
            root.Activity?.SetTag(OperationMetrics.AttemptCount, 1);
        }
        try
        {
            root.Dispose();
        }
        finally
        {
            (root.Context as Owner)?.Route?.Dispose();
        }
    }

    private static OperationScope StartAttempt(OperationScope root)
    {
        root.Attempts++;
        root.Activity?.SetTag(OperationMetrics.AttemptCount, root.Attempts);
        // Parenting through Current (not a parent id) keeps root as the in-process Parent, so
        // stopping the attempt restores the root for the next retry.
        OperationScope.MakeCurrent(root);
        Activity activity = root.Activity == null
            ? null
            : ConnectionOpenActivity.StartRouted(Source, AttemptName, ActivityKind.Internal,
                null, root.Route);
        activity?.SetTag(AttemptNumber, root.Attempts);
        return OperationScope.Create(activity, AttemptName, route: root.Route);
    }

    private static OperationScope FindRoot(SqlCommand command)
    {
        for (OperationScope current = OperationScope.Current; current != null;
            current = current.Parent)
        {
            if (!current.IsCompleted && current.Context is Owner owner &&
                ReferenceEquals(owner.Command, command))
            {
                return current;
            }
        }
        return null;
    }

    private static Task<T> CompleteWhenDone<T>(Task<T> task, OperationScope scope)
    {
        return task.ContinueWith(
            (completed, state) =>
            {
                OperationScope owner = (OperationScope)state;
                if (completed.IsFaulted)
                {
                    RecordError(owner, completed.Exception);
                }
                else if (completed.IsCanceled)
                {
                    RecordError(owner, new OperationCanceledException());
                }

                if (owner.IsRoot)
                {
                    Complete(owner);
                }
                else
                {
                    owner.Dispose();
                }
                return completed;
            },
            scope,
            TaskScheduler.Default).Unwrap();
    }

    private sealed class Owner(SqlCommand command, IDisposable route)
    {
        internal SqlCommand Command { get; } = command;

        internal IDisposable Route { get; } = route;
    }
}