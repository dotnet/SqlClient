// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Diagnostics;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Describes why a connection acquisition entered its final wait.
    /// </summary>
    internal enum PoolAcquisitionWaitReason
    {
        Unknown,
        PoolFull,
        ConnectionCreationInProgress,
        ConnectionCreationRateLimited,
    }

    /// <summary>
    /// Classifies how one physical connection is currently held by the pool.
    /// </summary>
    internal enum PoolConnectionUsageState
    {
        Idle,
        CheckedOut,
        TransactionHeld,
        Abandoned,
        Unclassified,
    }

    /// <summary>
    /// Best-effort snapshot captured only when a pooled connection request times out.
    /// </summary>
    internal readonly struct PoolAcquisitionDiagnostics
    {
        internal const string DataKeyPrefix = "Microsoft.Data.SqlClient.ConnectionPool.";
        internal const string WaitReasonDataKey = DataKeyPrefix + "WaitReason";
        internal const string MaxPoolSizeDataKey = DataKeyPrefix + "MaxPoolSize";
        internal const string ConnectionCountDataKey = DataKeyPrefix + "ConnectionCount";
        internal const string IdleConnectionCountDataKey = DataKeyPrefix + "IdleConnectionCount";
        internal const string PendingConnectionOpenCountDataKey = DataKeyPrefix + "PendingConnectionOpenCount";
        internal const string WaitingRequestCountDataKey = DataKeyPrefix + "WaitingRequestCount";
        internal const string CheckedOutConnectionCountDataKey = DataKeyPrefix + "CheckedOutConnectionCount";
        internal const string TransactionConnectionCountDataKey = DataKeyPrefix + "TransactionConnectionCount";
        internal const string AbandonedConnectionCountDataKey = DataKeyPrefix + "AbandonedConnectionCount";
        internal const string UnclassifiedConnectionCountDataKey = DataKeyPrefix + "UnclassifiedConnectionCount";
        internal const string LongestCheckoutDurationDataKey = DataKeyPrefix + "LongestCheckoutDuration";
        internal const string ReclaimedConnectionCountDataKey = DataKeyPrefix + "ReclaimedConnectionCount";

        internal PoolAcquisitionDiagnostics(
            PoolAcquisitionWaitReason waitReason,
            int maxPoolSize,
            int connectionCount,
            int idleConnectionCount,
            int pendingConnectionOpenCount,
            int waitingRequestCount,
            int checkedOutConnectionCount,
            int transactionConnectionCount,
            int abandonedConnectionCount,
            int unclassifiedConnectionCount,
            TimeSpan longestCheckoutDuration,
            long reclaimedConnectionCount)
        {
            WaitReason = waitReason;
            MaxPoolSize = maxPoolSize;
            ConnectionCount = connectionCount;
            IdleConnectionCount = idleConnectionCount;
            PendingConnectionOpenCount = pendingConnectionOpenCount;
            WaitingRequestCount = waitingRequestCount;
            CheckedOutConnectionCount = checkedOutConnectionCount;
            TransactionConnectionCount = transactionConnectionCount;
            AbandonedConnectionCount = abandonedConnectionCount;
            UnclassifiedConnectionCount = unclassifiedConnectionCount;
            LongestCheckoutDuration = longestCheckoutDuration;
            ReclaimedConnectionCount = reclaimedConnectionCount;
        }

        internal PoolAcquisitionWaitReason WaitReason { get; }

        internal int MaxPoolSize { get; }

        internal int ConnectionCount { get; }

        internal int IdleConnectionCount { get; }

        internal int PendingConnectionOpenCount { get; }

        internal int WaitingRequestCount { get; }

        internal int CheckedOutConnectionCount { get; }

        internal int TransactionConnectionCount { get; }

        internal int AbandonedConnectionCount { get; }

        internal int UnclassifiedConnectionCount { get; }

        internal TimeSpan LongestCheckoutDuration { get; }

        internal long ReclaimedConnectionCount { get; }

        /// <summary>
        /// Adds structured values to the timeout's data dictionary.
        /// </summary>
        internal void AddTo(IDictionary data)
        {
            data[WaitReasonDataKey] = WaitReason.ToString();
            data[MaxPoolSizeDataKey] = MaxPoolSize;
            data[ConnectionCountDataKey] = ConnectionCount;
            data[IdleConnectionCountDataKey] = IdleConnectionCount;
            data[PendingConnectionOpenCountDataKey] = PendingConnectionOpenCount;
            data[WaitingRequestCountDataKey] = WaitingRequestCount;
            data[CheckedOutConnectionCountDataKey] = CheckedOutConnectionCount;
            data[TransactionConnectionCountDataKey] = TransactionConnectionCount;
            data[AbandonedConnectionCountDataKey] = AbandonedConnectionCount;
            data[UnclassifiedConnectionCountDataKey] = UnclassifiedConnectionCount;
            data[LongestCheckoutDurationDataKey] = LongestCheckoutDuration;
            data[ReclaimedConnectionCountDataKey] = ReclaimedConnectionCount;
        }

        /// <summary>
        /// Formats the snapshot for the user-visible pooled-open timeout.
        /// </summary>
        internal string GetMessage() =>
            StringsHelper.GetString(
                Strings.ADP_PooledOpenTimeoutDetails,
                WaitReason,
                MaxPoolSize,
                ConnectionCount,
                IdleConnectionCount,
                PendingConnectionOpenCount,
                WaitingRequestCount,
                CheckedOutConnectionCount,
                TransactionConnectionCount,
                AbandonedConnectionCount,
                UnclassifiedConnectionCount,
                LongestCheckoutDuration,
                ReclaimedConnectionCount);
    }

    /// <summary>
    /// Collects connection ownership information during a timeout-only pool scan.
    /// </summary>
    internal sealed class PoolAcquisitionDiagnosticsBuilder
    {
        private readonly DateTime _utcNow;

        internal PoolAcquisitionDiagnosticsBuilder(DateTime utcNow)
        {
            Debug.Assert(utcNow.Kind == DateTimeKind.Utc);
            _utcNow = utcNow;
        }

        internal int CheckedOutConnectionCount { get; private set; }

        internal int IdleConnectionCount { get; private set; }

        internal int TransactionConnectionCount { get; private set; }

        internal int AbandonedConnectionCount { get; private set; }

        internal int UnclassifiedConnectionCount { get; private set; }

        internal TimeSpan LongestCheckoutDuration { get; private set; }

        /// <summary>
        /// Records one connection while its monitor is held.
        /// </summary>
        internal void Observe(DbConnectionInternal connection)
        {
            switch (connection.GetPoolUsageState(_utcNow, out TimeSpan checkoutDuration))
            {
                case PoolConnectionUsageState.Idle:
                    IdleConnectionCount++;
                    break;

                case PoolConnectionUsageState.CheckedOut:
                    CheckedOutConnectionCount++;
                    if (checkoutDuration > LongestCheckoutDuration)
                    {
                        LongestCheckoutDuration = checkoutDuration;
                    }
                    break;

                case PoolConnectionUsageState.TransactionHeld:
                    TransactionConnectionCount++;
                    break;

                case PoolConnectionUsageState.Abandoned:
                    AbandonedConnectionCount++;
                    if (checkoutDuration > LongestCheckoutDuration)
                    {
                        LongestCheckoutDuration = checkoutDuration;
                    }
                    break;

                case PoolConnectionUsageState.Unclassified:
                    UnclassifiedConnectionCount++;
                    break;
            }
        }

        internal void ObserveLockContention() => UnclassifiedConnectionCount++;
    }
}
