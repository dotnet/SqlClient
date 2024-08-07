// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClientX.Tds.State
{
    /// <summary>
    /// Captures TDS Snapshot state
    /// </summary>
    internal class TdsSnapshotState
    {
        [Flags]
        internal enum SnapshottedStateFlags : byte
        {
            None = 0,
            PendingData = 1 << 1,
            OpenResult = 1 << 2,
            ErrorTokenReceived = 1 << 3,  // Keep track of whether an error was received for the result. This is reset upon each done token
            ColMetaDataReceived = 1 << 4, // Used to keep track of when to fire StatementCompleted event.
            AttentionReceived = 1 << 5    // NOTE: Received is not volatile as it is only ever accessed\modified by TryRun its callees (i.e. single threaded access)
        }

        private SnapshottedStateFlags _snapshottedState;

        internal void SetSnapshottedState(SnapshottedStateFlags flag, bool value)
        {
            if (value)
            {
                _snapshottedState |= flag;
            }
            else
            {
                _snapshottedState &= ~flag;
            }
        }

        internal bool GetSnapshottedState(SnapshottedStateFlags flag)
        {
            return (_snapshottedState & flag) == flag;
        }

        internal bool HasOpenResult
        {
            get => GetSnapshottedState(SnapshottedStateFlags.OpenResult);
            set => SetSnapshottedState(SnapshottedStateFlags.OpenResult, value);
        }

        internal bool HasPendingData
        {
            get => GetSnapshottedState(SnapshottedStateFlags.PendingData);
            set => SetSnapshottedState(SnapshottedStateFlags.PendingData, value);
        }

        internal bool HasReceivedError
        {
            get => GetSnapshottedState(SnapshottedStateFlags.ErrorTokenReceived);
            set => SetSnapshottedState(SnapshottedStateFlags.ErrorTokenReceived, value);
        }

        internal bool HasReceivedAttention
        {
            get => GetSnapshottedState(SnapshottedStateFlags.AttentionReceived);
            set => SetSnapshottedState(SnapshottedStateFlags.AttentionReceived, value);
        }

        internal bool HasReceivedColumnMetadata
        {
            get => GetSnapshottedState(SnapshottedStateFlags.ColMetaDataReceived);
            set => SetSnapshottedState(SnapshottedStateFlags.ColMetaDataReceived, value);
        }
    }
}
