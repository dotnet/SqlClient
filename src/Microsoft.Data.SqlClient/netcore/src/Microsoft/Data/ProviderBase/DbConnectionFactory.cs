// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.ProviderBase
{
    internal abstract partial class DbConnectionFactory
    {
        private static readonly Action<Task<DbConnectionInternal>, object> s_tryGetConnectionCompletedContinuation = TryGetConnectionCompletedContinuation;

        internal bool TryGetConnection(DbConnection owningConnection, TaskCompletionSource<DbConnectionInternal> retry, DbConnectionOptions userOptions, DbConnectionInternal oldConnection, out DbConnectionInternal connection)
        {
            Debug.Assert(null != owningConnection, "null owningConnection?");

            DbConnectionPoolGroup poolGroup;
            DbConnectionPool connectionPool;
            connection = null;

            //  Work around race condition with clearing the pool between GetConnectionPool obtaining pool 
            //  and GetConnection on the pool checking the pool state.  Clearing the pool in this window
            //  will switch the pool into the ShuttingDown state, and GetConnection will return null.
            //  There is probably a better solution involving locking the pool/group, but that entails a major
            //  re-design of the connection pooling synchronization, so is postponed for now.

            // Use retriesLeft to prevent CPU spikes with incremental sleep
            // start with one msec, double the time every retry
            // max time is: 1 + 2 + 4 + ... + 2^(retries-1) == 2^retries -1 == 1023ms (for 10 retries)
            int retriesLeft = 10;
            int timeBetweenRetriesMilliseconds = 1;

            do
            {
                poolGroup = GetConnectionPoolGroup(owningConnection);
                // Doing this on the callers thread is important because it looks up the WindowsIdentity from the thread.
                connectionPool = GetConnectionPool(owningConnection, poolGroup);
                if (null == connectionPool)
                {
                    // If GetConnectionPool returns null, we can be certain that
                    // this connection should not be pooled via DbConnectionPool
                    // or have a disabled pool entry.
                    poolGroup = GetConnectionPoolGroup(owningConnection); // previous entry have been disabled

                    if (retry != null)
                    {
                        Task<DbConnectionInternal> newTask;
                        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                        lock (s_pendingOpenNonPooled)
                        {
                            // look for an available task slot (completed or empty)
                            int idx;
                            for (idx = 0; idx < s_pendingOpenNonPooled.Length; idx++)
                            {
                                Task task = s_pendingOpenNonPooled[idx];
                                if (task == null)
                                {
                                    s_pendingOpenNonPooled[idx] = GetCompletedTask();
                                    break;
                                }
                                else if (task.IsCompleted)
                                {
                                    break;
                                }
                            }

                            // if didn't find one, pick the next one in round-robin fashion
                            if (idx == s_pendingOpenNonPooled.Length)
                            {
                                idx = (int)(s_pendingOpenNonPooledNext % s_pendingOpenNonPooled.Length);
                                unchecked
                                {
                                    s_pendingOpenNonPooledNext++;
                                }
                            }

                            // now that we have an antecedent task, schedule our work when it is completed.
                            // If it is a new slot or a completed task, this continuation will start right away.
                            newTask = CreateReplaceConnectionContinuation(s_pendingOpenNonPooled[idx], owningConnection, retry, userOptions, oldConnection, poolGroup, cancellationTokenSource);

                            // Place this new task in the slot so any future work will be queued behind it
                            s_pendingOpenNonPooled[idx] = newTask;
                        }

                        // Set up the timeout (if needed)
                        if (owningConnection.ConnectionTimeout > 0)
                        {
                            int connectionTimeoutMilliseconds = owningConnection.ConnectionTimeout * 1000;
                            cancellationTokenSource.CancelAfter(connectionTimeoutMilliseconds);
                        }

                        // once the task is done, propagate the final results to the original caller
                        newTask.ContinueWith(
                            continuationAction: s_tryGetConnectionCompletedContinuation, 
                            state: Tuple.Create(cancellationTokenSource, retry),
                            scheduler: TaskScheduler.Default
                        );

                        return false;
                    }

                    connection = CreateNonPooledConnection(owningConnection, poolGroup, userOptions);
                }
                else
                {
                    if (((SqlClient.SqlConnection)owningConnection).ForceNewConnection)
                    {
                        Debug.Assert(!(oldConnection is DbConnectionClosed), "Force new connection, but there is no old connection");
                        connection = connectionPool.ReplaceConnection(owningConnection, userOptions, oldConnection);
                    }
                    else
                    {
                        if (!connectionPool.TryGetConnection(owningConnection, retry, userOptions, out connection))
                        {
                            return false;
                        }
                    }

                    if (connection == null)
                    {
                        // connection creation failed on semaphore waiting or if max pool reached
                        if (connectionPool.IsRunning)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<prov.DbConnectionFactory.GetConnection|RES|CPOOL> {0}, GetConnection failed because a pool timeout occurred.", ObjectID);
                            // If GetConnection failed while the pool is running, the pool timeout occurred.
                            throw ADP.PooledOpenTimeout();
                        }
                        else
                        {
                            // We've hit the race condition, where the pool was shut down after we got it from the group.
                            // Yield time slice to allow shut down activities to complete and a new, running pool to be instantiated
                            //  before retrying.
                            System.Threading.Thread.Sleep(timeBetweenRetriesMilliseconds);
                            timeBetweenRetriesMilliseconds *= 2; // double the wait time for next iteration
                        }
                    }
                }
            } while (connection == null && retriesLeft-- > 0);

            if (connection == null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<prov.DbConnectionFactory.GetConnection|RES|CPOOL> {0}, GetConnection failed because a pool timeout occurred and all retries were exhausted.", ObjectID);
                // exhausted all retries or timed out - give up
                throw ADP.PooledOpenTimeout();
            }

            return true;
        }

        private Task<DbConnectionInternal> CreateReplaceConnectionContinuation(Task<DbConnectionInternal> task, DbConnection owningConnection, TaskCompletionSource<DbConnectionInternal> retry, DbConnectionOptions userOptions, DbConnectionInternal oldConnection, DbConnectionPoolGroup poolGroup, CancellationTokenSource cancellationTokenSource)
        {
            return task.ContinueWith(
                (_) =>
                {
                    Transaction originalTransaction = ADP.GetCurrentTransaction();
                    try
                    {
                        ADP.SetCurrentTransaction(retry.Task.AsyncState as Transaction);
                        var newConnection = CreateNonPooledConnection(owningConnection, poolGroup, userOptions);
                        if ((oldConnection != null) && (oldConnection.State == ConnectionState.Open))
                        {
                            oldConnection.PrepareForReplaceConnection();
                            oldConnection.Dispose();
                        }
                        return newConnection;
                    }
                    finally
                    {
                        ADP.SetCurrentTransaction(originalTransaction);
                    }
                },
                cancellationTokenSource.Token,
                TaskContinuationOptions.LongRunning,
                TaskScheduler.Default
            );
        }

        private static void TryGetConnectionCompletedContinuation(Task<DbConnectionInternal> task, object state)
        {
            Tuple<CancellationTokenSource, TaskCompletionSource<DbConnectionInternal>> parameters = (Tuple<CancellationTokenSource, TaskCompletionSource<DbConnectionInternal>>)state;
            CancellationTokenSource source = parameters.Item1;
            source.Dispose();

            TaskCompletionSource<DbConnectionInternal> retryCompletionSource = parameters.Item2;

            if (task.IsCanceled)
            {
                retryCompletionSource.TrySetException(ADP.ExceptionWithStackTrace(ADP.NonPooledOpenTimeout()));
            }
            else if (task.IsFaulted)
            {
                retryCompletionSource.TrySetException(task.Exception.InnerException);
            }
            else
            {
                if (!retryCompletionSource.TrySetResult(task.Result))
                {
                    // The outer TaskCompletionSource was already completed
                    // Which means that we don't know if someone has messed with the outer connection in the middle of creation
                    // So the best thing to do now is to destroy the newly created connection
                    task.Result.DoomThisConnection();
                    task.Result.Dispose();
                }
            }
        }
    }
}
