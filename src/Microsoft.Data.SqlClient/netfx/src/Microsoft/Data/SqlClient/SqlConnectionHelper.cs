// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlConnection : DbConnection
    {
        partial void RepairInnerConnection();

        // OpenBusy->Closed (previously opened)
        // Connecting->Open
        internal void SetInnerConnectionEvent(DbConnectionInternal to)
        {
            // Set's the internal connection without verifying that it's a specific value
            Debug.Assert(_innerConnection != null, "null InnerConnection");
            Debug.Assert(to != null, "to null InnerConnection");

            ConnectionState originalState = _innerConnection.State & ConnectionState.Open;
            ConnectionState currentState = to.State & ConnectionState.Open;

            if ((originalState != currentState) && (ConnectionState.Closed == currentState))
            {
                // Increment the close count whenever we switch to Closed
                unchecked
                { _closeCount++; }
            }

            _innerConnection = to;

            if (ConnectionState.Closed == originalState && ConnectionState.Open == currentState)
            {
                OnStateChange(DbConnectionInternal.StateChangeOpen);
            }
            else if (ConnectionState.Open == originalState && ConnectionState.Closed == currentState)
            {
                OnStateChange(DbConnectionInternal.StateChangeClosed);
            }
            else
            {
                Debug.Fail("unexpected state switch");
                if (originalState != currentState)
                {
                    OnStateChange(new StateChangeEventArgs(originalState, currentState));
                }
            }
        }

        // this method is used to securely change state with the resource being
        // the open connection protected by the connectionstring via a permission demand

        // Closed->Connecting: prevent set_ConnectionString during Open
        // Open->OpenBusy: guarantee internal connection is returned to correct pool
        // Closed->ClosedBusy: prevent Open during set_ConnectionString
        internal bool SetInnerConnectionFrom(DbConnectionInternal to, DbConnectionInternal from)
        {
            // Set's the internal connection, verifying that it's a specific value before doing so.
            Debug.Assert(_innerConnection != null, "null InnerConnection");
            Debug.Assert(from != null, "from null InnerConnection");
            Debug.Assert(to != null, "to null InnerConnection");

            bool result = (from == Interlocked.CompareExchange<DbConnectionInternal>(ref _innerConnection, to, from));
            return result;
        }

        // ClosedBusy->Closed (never opened)
        // Connecting->Closed (exception during open, return to previous closed state)
        internal void SetInnerConnectionTo(DbConnectionInternal to)
        {
            // Set's the internal connection without verifying that it's a specific value
            Debug.Assert(_innerConnection != null, "null InnerConnection");
            Debug.Assert(to != null, "to null InnerConnection");
            _innerConnection = to;
        }
    }
}

