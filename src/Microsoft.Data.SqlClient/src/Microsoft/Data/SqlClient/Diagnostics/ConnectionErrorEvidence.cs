// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using Microsoft.Data.SqlClient.Parser;

namespace Microsoft.Data.SqlClient.Diagnostics;

internal enum ConnectionErrorSource
{
    Unknown,
    SqlServer,
    Sni,
    OperatingSystem,
    Client
}

internal enum ConnectionErrorCategory
{
    Unknown,
    NameResolution,
    NetworkConnectivity,
    TlsSecurity,
    Authentication,
    AccessPolicy,
    DatabaseOpen,
    ResourceThrottling,
    ServerAvailability,
    Configuration,
    Timeout,
    Canceled,
    ProtocolError,
    ConnectionLifecycle,
    ConnectionRecovery,
    ClientResourceExhaustion,
    RoutingRedirect,
    InternalError
}

internal sealed class ConnectionErrorEvidence
{
    private const int MaximumCauseDepth = 32;

    private ConnectionErrorEvidence(
        ConnectionErrorSource source,
        int? code,
        ConnectionErrorCategory kind,
        string phase,
        bool ambiguous,
        int? nativeCode = null)
    {
        Source = source;
        Code = code;
        Kind = kind;
        Phase = phase;
        IsAmbiguous = ambiguous;
        NativeCode = nativeCode;
    }

    internal ConnectionErrorSource Source { get; }
    internal int? Code { get; }

    /// <summary>
    /// The operating system code that accompanies an SNI error (SqlError.Win32ErrorCode), used
    /// only to infer the failing transport sub-step. Null for other sources.
    /// </summary>
    internal int? NativeCode { get; }
    internal ConnectionErrorCategory Kind { get; }
    internal string Phase { get; }
    internal bool IsAmbiguous { get; }

    internal static ConnectionErrorEvidence Capture(Exception exception, string phase)
    {
        string safePhase = ConnectionErrorAttributePolicy.NormalizePhase(phase);
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

            AddCandidates(cause.Exception, cause.Depth, candidates);
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
            return new ConnectionErrorEvidence(
                ConnectionErrorSource.Unknown, null, ConnectionErrorCategory.Unknown, safePhase, false);
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
                return new ConnectionErrorEvidence(
                    ConnectionErrorSource.Unknown, null, ConnectionErrorCategory.Unknown, safePhase, true);
            }
            selected = candidate;
        }

        Candidate evidence = selected!.Value;
        return new ConnectionErrorEvidence(
            evidence.Source, evidence.Code, evidence.Kind, safePhase, false, evidence.NativeCode);
    }

    /// <summary>Creates evidence for tests and synthetic failures.</summary>
    internal static ConnectionErrorEvidence Create(
        ConnectionErrorSource source, int? code, int? nativeCode, string phase) =>
        new(source, code, ConnectionErrorCategory.Unknown,
            ConnectionErrorAttributePolicy.NormalizePhase(phase), false, nativeCode);

    internal static ConnectionErrorEvidence FromKind(
        ConnectionErrorCategory kind, string phase) =>
        new(ConnectionErrorSource.Client, null, kind,
            ConnectionErrorAttributePolicy.NormalizePhase(phase), false);

    private static void AddCandidates(Exception exception, int depth, List<Candidate> candidates)
    {
        if (exception is SqlException sqlException)
        {
            foreach (SqlError error in sqlException.Errors)
            {
                if (error.Class < TdsEnums.MIN_ERROR_CLASS)
                {
                    continue;
                }

                if (error.IsServerError)
                {
                    candidates.Add(new Candidate(
                        ConnectionErrorSource.SqlServer, error.Number, ConnectionErrorCategory.Unknown, depth));
                }
                else if (error.SniErrorNumber != 0)
                {
                    candidates.Add(new Candidate(
                        ConnectionErrorSource.Sni, unchecked((int)error.SniErrorNumber),
                        ConnectionErrorCategory.Unknown, depth,
                        error.Win32ErrorCode != 0 ? error.Win32ErrorCode : null));
                }
                else if (error.Win32ErrorCode != 0)
                {
                    candidates.Add(new Candidate(
                        ConnectionErrorSource.OperatingSystem, error.Win32ErrorCode,
                        ConnectionErrorCategory.Unknown, depth));
                }

                // Later entries describe the same failure, not independent causes.
                break;
            }
        }

        switch (exception)
        {
            case OperationCanceledException:
                candidates.Add(new Candidate(
                    ConnectionErrorSource.Client, null, ConnectionErrorCategory.Canceled, depth));
                break;
            case TimeoutException:
                candidates.Add(new Candidate(
                    ConnectionErrorSource.Client, null, ConnectionErrorCategory.Timeout, depth));
                break;
            case SocketException socketException:
                candidates.Add(new Candidate(
                    ConnectionErrorSource.OperatingSystem,
                    (int)socketException.SocketErrorCode,
                    SocketCategory(socketException.SocketErrorCode),
                    depth));
                break;
            case Win32Exception win32Exception:
                candidates.Add(new Candidate(
                    ConnectionErrorSource.OperatingSystem,
                    win32Exception.NativeErrorCode,
                    OperatingSystemCategory(win32Exception.NativeErrorCode),
                    depth));
                break;
            case AuthenticationException:
                candidates.Add(new Candidate(
                    ConnectionErrorSource.Client, null, ConnectionErrorCategory.TlsSecurity, depth));
                break;
            case EndOfStreamException:
                candidates.Add(new Candidate(
                    ConnectionErrorSource.Client, null, ConnectionErrorCategory.NetworkConnectivity, depth));
                break;
            case OutOfMemoryException:
                candidates.Add(new Candidate(
                    ConnectionErrorSource.Client, null,
                    ConnectionErrorCategory.ClientResourceExhaustion, depth));
                break;
            case ObjectDisposedException:
                candidates.Add(new Candidate(
                    ConnectionErrorSource.Client, null,
                    ConnectionErrorCategory.ConnectionLifecycle, depth));
                break;
        }
    }

    private static ConnectionErrorCategory SocketCategory(SocketError error) => error switch
    {
        SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoRecovery or SocketError.NoData =>
            ConnectionErrorCategory.NameResolution,
        SocketError.TimedOut => ConnectionErrorCategory.Timeout,
        SocketError.NetworkDown or SocketError.NetworkUnreachable or SocketError.NetworkReset or
        SocketError.ConnectionAborted or SocketError.ConnectionReset or SocketError.ConnectionRefused or
        SocketError.HostUnreachable or SocketError.Shutdown or SocketError.NotConnected =>
            ConnectionErrorCategory.NetworkConnectivity,
        _ => ConnectionErrorCategory.Unknown
    };

    private static ConnectionErrorCategory OperatingSystemCategory(int code) => code switch
    {
        258 or 10060 => ConnectionErrorCategory.Timeout,
        11001 or 11002 or 11003 or 11004 => ConnectionErrorCategory.NameResolution,
        10051 or 10053 or 10054 or 10065 =>
            ConnectionErrorCategory.NetworkConnectivity,
        _ => ConnectionErrorCategory.Unknown
    };

    private readonly record struct Cause(Exception Exception, int Depth);

    private readonly record struct Candidate(
        ConnectionErrorSource Source,
        int? Code,
        ConnectionErrorCategory Kind,
        int Depth,
        int? NativeCode = null);

    private sealed class ExceptionReferenceComparer : IEqualityComparer<Exception>
    {
        internal static readonly ExceptionReferenceComparer Instance = new();

        public bool Equals(Exception left, Exception right) => ReferenceEquals(left, right);
        public int GetHashCode(Exception exception) => RuntimeHelpers.GetHashCode(exception);
    }
}
