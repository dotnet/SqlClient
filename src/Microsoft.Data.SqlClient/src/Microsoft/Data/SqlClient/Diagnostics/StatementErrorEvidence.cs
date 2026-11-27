// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.Parser;

namespace Microsoft.Data.SqlClient.Diagnostics;

internal enum StatementErrorCategory
{
    Unknown,
    QuerySyntaxSemantics,
    ConstraintViolation,
    Authorization,
    ConcurrencyConflict,
    ServerAvailability,
    ResourceThrottling,
    DatabaseError,
    Timeout,
    Canceled,
    NetworkConnectivity,
    ConnectionLifecycle,
    ParameterValidation,
    DataConversion,
    ClientResourceExhaustion
}

/// <summary>
/// Immutable, exception-free evidence for one failed statement scope. It keeps only the
/// namespaced numeric code and a driver-observed kind; messages and exception references
/// are never retained.
/// </summary>
internal sealed class StatementErrorEvidence
{
    private const int MaximumCauseDepth = 32;

    private StatementErrorEvidence(
        ConnectionErrorSource source,
        int? code,
        StatementErrorCategory kind,
        string phase,
        bool ambiguous)
    {
        Source = source;
        Code = code;
        Kind = kind;
        Phase = phase;
        IsAmbiguous = ambiguous;
    }

    internal ConnectionErrorSource Source { get; }
    internal int? Code { get; }
    internal StatementErrorCategory Kind { get; }
    internal string Phase { get; }
    internal bool IsAmbiguous { get; }

    internal static StatementErrorEvidence Capture(Exception exception, string phase)
    {
        string safePhase = StatementErrorAttributePolicy.NormalizePhase(phase);
        List<Candidate> candidates = new();
        Stack<Cause> pending = new();
        HashSet<Exception> visited = new(ExceptionReferenceComparer.Instance);
        pending.Push(new Cause(exception, 0));

        while (pending.Count > 0)
        {
            Cause cause = pending.Pop();
            if (cause.Exception == null || cause.Depth > MaximumCauseDepth ||
                !visited.Add(cause.Exception))
            {
                continue;
            }

            AddCandidate(cause.Exception, cause.Depth, candidates);
            if (cause.Exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                {
                    pending.Push(new Cause(inner, cause.Depth + 1));
                }
            }
            else if (cause.Exception.InnerException != null)
            {
                pending.Push(new Cause(cause.Exception.InnerException, cause.Depth + 1));
            }
        }

        if (candidates.Count == 0)
        {
            return Unknown(safePhase, ambiguous: false);
        }

        int deepest = 0;
        foreach (Candidate candidate in candidates)
        {
            deepest = Math.Max(deepest, candidate.Depth);
        }

        Candidate? selected = null;
        foreach (Candidate candidate in candidates)
        {
            if (candidate.Depth != deepest)
            {
                continue;
            }
            if (selected.HasValue && selected.Value != candidate)
            {
                return Unknown(safePhase, ambiguous: true);
            }
            selected = candidate;
        }

        Candidate evidence = selected!.Value;
        return new StatementErrorEvidence(
            evidence.Source, evidence.Code, evidence.Kind, safePhase, false);
    }

    private static StatementErrorEvidence Unknown(string phase, bool ambiguous) =>
        new(ConnectionErrorSource.Unknown, null, StatementErrorCategory.Unknown, phase, ambiguous);

    // One candidate per exception: cancellation/timeout flags, then the primary SqlError,
    // then the observed exception type. Wrapper types that do not prove a cause add nothing.
    private static void AddCandidate(Exception exception, int depth, List<Candidate> candidates)
    {
        if (exception is SqlException sqlException)
        {
            foreach (SqlError error in sqlException.Errors)
            {
                if (error.Class < TdsEnums.MIN_ERROR_CLASS)
                {
                    continue;
                }

                if (error.IsCancellation)
                {
                    candidates.Add(new Candidate(
                        ConnectionErrorSource.Client, null, StatementErrorCategory.Canceled, depth));
                }
                else if (error.IsTimeout)
                {
                    candidates.Add(new Candidate(
                        ConnectionErrorSource.Client, null, StatementErrorCategory.Timeout, depth));
                }
                else if (error.IsServerError)
                {
                    candidates.Add(new Candidate(
                        ConnectionErrorSource.SqlServer, error.Number,
                        StatementErrorCategory.Unknown, depth));
                }
                else if (error.SniErrorNumber != 0)
                {
                    candidates.Add(new Candidate(
                        ConnectionErrorSource.Sni, unchecked((int)error.SniErrorNumber),
                        StatementErrorCategory.Unknown, depth));
                }
                else if (error.Win32ErrorCode != 0)
                {
                    candidates.Add(new Candidate(
                        ConnectionErrorSource.OperatingSystem, error.Win32ErrorCode,
                        StatementErrorCategory.Unknown, depth));
                }

                // Later entries describe the same failure, not independent causes.
                return;
            }
            return;
        }

        StatementErrorCategory kind = exception switch
        {
            OperationCanceledException => StatementErrorCategory.Canceled,
            TimeoutException => StatementErrorCategory.Timeout,
            ArgumentException => StatementErrorCategory.ParameterValidation,
            InvalidCastException or FormatException or OverflowException =>
                StatementErrorCategory.DataConversion,
            ObjectDisposedException => StatementErrorCategory.ConnectionLifecycle,
            EndOfStreamException => StatementErrorCategory.NetworkConnectivity,
            OutOfMemoryException => StatementErrorCategory.ClientResourceExhaustion,
            _ => StatementErrorCategory.Unknown
        };

        if (exception is SocketException socket)
        {
            candidates.Add(new Candidate(
                ConnectionErrorSource.OperatingSystem, (int)socket.SocketErrorCode,
                StatementErrorCategory.Unknown, depth));
        }
        else if (exception is Win32Exception win32)
        {
            candidates.Add(new Candidate(
                ConnectionErrorSource.OperatingSystem, win32.NativeErrorCode,
                StatementErrorCategory.Unknown, depth));
        }
        else if (kind != StatementErrorCategory.Unknown)
        {
            candidates.Add(new Candidate(ConnectionErrorSource.Client, null, kind, depth));
        }
    }

    private readonly record struct Cause(Exception Exception, int Depth);

    private readonly record struct Candidate(
        ConnectionErrorSource Source,
        int? Code,
        StatementErrorCategory Kind,
        int Depth);

    private sealed class ExceptionReferenceComparer : IEqualityComparer<Exception>
    {
        internal static readonly ExceptionReferenceComparer Instance = new();

        public bool Equals(Exception left, Exception right) => ReferenceEquals(left, right);
        public int GetHashCode(Exception exception) => RuntimeHelpers.GetHashCode(exception);
    }
}
