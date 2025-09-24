// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
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
        internal void Abort(Exception e)
        {
            DbConnectionInternal innerConnection = _innerConnection;
            if (ConnectionState.Open == innerConnection.State)
            {
                Interlocked.CompareExchange(ref _innerConnection, DbConnectionClosedPreviouslyOpened.SingletonInstance, innerConnection);
                innerConnection.DoomThisConnection();
            }

            // NOTE: we put the tracing last, because the ToString() calls (and
            // the SqlClientEventSource.SqlClientEventSource.Log.Trace, for that matter) have no reliability contract and
            // will end the reliable try...
            if (e is OutOfMemoryException)
            {
                SqlClientEventSource.Log.TryTraceEvent("<prov.DbConnectionHelper.Abort|RES|INFO|CPOOL> {0}, Aborting operation due to asynchronous exception: OutOfMemory", ObjectID);
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("<prov.DbConnectionHelper.Abort|RES|INFO|CPOOL> {0}, Aborting operation due to asynchronous exception: {1}", ObjectID, e);
            }
        }

        internal void AddWeakReference(object value, int tag)
        {
            InnerConnection.AddWeakReference(value, tag);
        }

        partial void RepairInnerConnection();

        internal void PermissionDemand()
        {
            Debug.Assert(DbConnectionClosedConnecting.SingletonInstance == _innerConnection, "not connecting");
            DbConnectionPoolGroup poolGroup = PoolGroup;
            DbConnectionOptions connectionOptions = poolGroup != null ? poolGroup.ConnectionOptions : null;
            if (connectionOptions == null || connectionOptions.IsEmpty)
            {
                throw ADP.NoConnectionString();
            }
            DbConnectionOptions userConnectionOptions = UserConnectionOptions;
            Debug.Assert(userConnectionOptions != null, "null UserConnectionOptions");
        }

        internal void RemoveWeakReference(object value)
        {
            InnerConnection.RemoveWeakReference(value);
        }

        internal void SetInnerConnectionEvent(DbConnectionInternal to)
        {
            Debug.Assert(_innerConnection != null, "null InnerConnection");
            Debug.Assert(to != null, "to null InnerConnection");

            ConnectionState originalState = _innerConnection.State & ConnectionState.Open;
            ConnectionState currentState = to.State & ConnectionState.Open;
            if ((originalState != currentState) && (ConnectionState.Closed == currentState))
            {
                unchecked
                {
                    _closeCount++;
                }
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

        internal bool SetInnerConnectionFrom(DbConnectionInternal to, DbConnectionInternal from)
        {
            Debug.Assert(_innerConnection != null, "null InnerConnection");
            Debug.Assert(from != null, "from null InnerConnection");
            Debug.Assert(to != null, "to null InnerConnection");
            bool result = (from == Interlocked.CompareExchange<DbConnectionInternal>(ref _innerConnection, to, from));
            return result;
        }

        internal void SetInnerConnectionTo(DbConnectionInternal to)
        {
            Debug.Assert(_innerConnection != null, "null InnerConnection");
            Debug.Assert(to != null, "to null InnerConnection");
            _innerConnection = to;
        }
    }
}

