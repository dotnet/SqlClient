// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Transactions;
using Microsoft.Data.ProviderBase;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool;

/// <summary>
/// Manages database connections that are enlisted in transactions, providing a specialized 
/// pool that groups connections by their associated transactions. This class ensures that 
/// connections participating in the same transaction can be efficiently reused while 
/// maintaining transaction integrity and thread safety.
/// </summary>
/// <remarks>
/// The TransactedConnectionPool works in conjunction with the main connection pool to handle 
/// connections that are enlisted in System.Transactions. When a connection is enlisted in a 
/// transaction, it cannot be returned to the general pool until the transaction completes.
/// This class provides temporary storage for such connections, organized by transaction,
/// allowing for efficient reuse within the same transaction scope.
/// </remarks>
internal class TransactedConnectionPool
{
    /// <summary>
    /// A specialized list that holds database connections associated with a specific transaction.
    /// This class extends List&lt;DbConnectionInternal&gt; and maintains a reference to the 
    /// transaction for proper cleanup when the transaction completes.
    /// </summary>
    private sealed class TransactedConnectionList : List<DbConnectionInternal>
    {
        private Transaction _transaction;
        
        /// <summary>
        /// Initializes a new instance of the TransactedConnectionList class with the specified
        /// initial capacity and associated transaction.
        /// </summary>
        /// <param name="initialAllocation">The initial number of elements that the list can contain.</param>
        /// <param name="tx">The transaction associated with the connections in this list.</param>
        internal TransactedConnectionList(int initialAllocation, Transaction tx) : base(initialAllocation)
        {
            _transaction = tx;
        }

        /// <summary>
        /// Releases the resources used by the TransactedConnectionList, including 
        /// disposing of the associated transaction reference.
        /// </summary>
        internal void Dispose()
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
            }
        }
    }

    Dictionary<Transaction, TransactedConnectionList> _transactedCxns;

    private static int _objectTypeCount; // EventSource Counter
    internal readonly int _objectID = System.Threading.Interlocked.Increment(ref _objectTypeCount);

    /// <summary>
    /// Initializes a new instance of the TransactedConnectionPool class for the specified connection pool.
    /// </summary>
    /// <param name="pool">The main connection pool that this transacted pool is associated with.</param>
    /// <remarks>
    /// The transacted connection pool works as a companion to the main connection pool,
    /// temporarily holding connections that are enlisted in transactions until those
    /// transactions complete.
    /// </remarks>
    internal TransactedConnectionPool(IDbConnectionPool pool)
    {
        Pool = pool;
        _transactedCxns = new Dictionary<Transaction, TransactedConnectionList>();
        SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.TransactedConnectionPool|RES|CPOOL> {0}, Constructed for connection pool {1}", Id, Pool.Id);
    }

    /// <summary>
    /// Gets the unique identifier for this transacted connection pool instance.
    /// </summary>
    /// <value>A unique integer identifier used for logging and diagnostics.</value>
    internal int Id => _objectID;

    /// <summary>
    /// Gets the main connection pool that this transacted pool is associated with.
    /// </summary>
    /// <value>The IDbConnectionPool instance that owns this transacted pool.</value>
    internal IDbConnectionPool Pool { get; private init; }

    /// <summary>
    /// Retrieves a database connection that is already enlisted in the specified transaction.
    /// </summary>
    /// <param name="transaction">The transaction to look for an existing enlisted connection.</param>
    /// <returns>
    /// A DbConnectionInternal instance that is enlisted in the specified transaction, 
    /// or null if no such connection is available in the pool.
    /// </returns>
    /// <remarks>
    /// This method is only used when AutoEnlist is true and there is a valid ambient transaction.
    /// The method is thread-safe and will return the most recently added connection for the 
    /// specified transaction. If a connection is found and returned, it is removed from the
    /// transacted pool.
    /// </remarks>
    internal DbConnectionInternal? GetTransactedObject(Transaction transaction)
    {
        DbConnectionInternal? transactedObject = null;

        TransactedConnectionList? connections;
        bool txnFound = false;

        lock (_transactedCxns)
        {
            txnFound = _transactedCxns.TryGetValue(transaction, out connections);
        }

        // NOTE: GetTransactedObject is only used when AutoEnlist = True and the ambient transaction 
        //   (Sys.Txns.Txn.Current) is still valid/non-null. This, in turn, means that we don't need 
        //   to worry about a pending asynchronous TransactionCompletedEvent to trigger processing in
        //   TransactionEnded below and potentially wipe out the connections list underneath us. It
        //   is similarly alright if a pending addition to the connections list in PutTransactedObject
        //   below is not completed prior to the lock on the connections object here...getting a new
        //   connection is probably better than unnecessarily locking
        if (txnFound && connections is not null)
        {

            // synchronize multi-threaded access with PutTransactedObject (TransactionEnded should
            //   not be a concern, see comments above)
            lock (connections)
            {
                int i = connections.Count - 1;
                if (0 <= i)
                {
                    transactedObject = connections[i];
                    connections.RemoveAt(i);
                }
            }
        }

        if (transactedObject != null)
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.GetTransactedObject|RES|CPOOL> {0}, Transaction {1}, Connection {2}, Popped.", Id, transaction.GetHashCode(), transactedObject.ObjectID);
        }
        return transactedObject;
    }

    /// <summary>
    /// Adds a database connection to the transacted pool, associating it with the specified transaction.
    /// </summary>
    /// <param name="transaction">The transaction that the connection is enlisted in.</param>
    /// <param name="transactedObject">The database connection to add to the transacted pool.</param>
    /// <remarks>
    /// This method handles the complex synchronization required when multiple threads may be
    /// attempting to add connections to the same transaction pool simultaneously. If a pool
    /// for the specified transaction doesn't exist, it will be created. The method uses
    /// transaction cloning to ensure that the pool maintains a valid reference to the transaction
    /// even if the original transaction object is disposed.
    /// 
    /// Due to the asynchronous nature of transaction completion notifications, this method
    /// must handle race conditions where TransactionEnded might be called before or during
    /// the execution of this method.
    /// </remarks>
    internal void PutTransactedObject(Transaction transaction, DbConnectionInternal transactedObject)
    {
        TransactedConnectionList? connections;
        bool txnFound = false;

        // NOTE: because TransactionEnded is an asynchronous notification, there's no guarantee
        //   around the order in which PutTransactionObject and TransactionEnded are called. 

        lock (_transactedCxns)
        {
            // Check if a transacted pool has been created for this transaction
            if ((txnFound = _transactedCxns.TryGetValue(transaction, out connections)) 
                && connections is not null)
            {
                // synchronize multi-threaded access with GetTransactedObject
                lock (connections)
                {
                    Debug.Assert(0 > connections.IndexOf(transactedObject), "adding to pool a second time?");
                    SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.PutTransactedObject|RES|CPOOL> {0}, Transaction {1}, Connection {2}, Pushing.", Id, transaction.GetHashCode(), transactedObject.ObjectID);
                    connections.Add(transactedObject);
                }
            }
        }

        // CONSIDER: the following code is more complicated than it needs to be to avoid cloning the 
        //   transaction and allocating memory within a lock. Is that complexity really necessary?
        if (!txnFound)
        {
            // create the transacted pool, making sure to clone the associated transaction
            //   for use as a key in our internal dictionary of transactions and connections
            Transaction? transactionClone = null;
            TransactedConnectionList? newConnections = null;

            try
            {
                transactionClone = transaction.Clone();
                newConnections = new TransactedConnectionList(2, transactionClone); // start with only two connections in the list; most times we won't need that many.

                lock (_transactedCxns)
                {
                    // NOTE: in the interim between the locks on the transacted pool (this) during 
                    //   execution of this method, another thread (threadB) may have attempted to 
                    //   add a different connection to the transacted pool under the same 
                    //   transaction. As a result, threadB may have completed creating the
                    //   transacted pool while threadA was processing the above instructions.
                    if ((txnFound = _transactedCxns.TryGetValue(transaction, out connections))
                        && connections is not null)
                    {
                        // synchronize multi-threaded access with GetTransactedObject
                        lock (connections)
                        {
                            Debug.Assert(0 > connections.IndexOf(transactedObject), "adding to pool a second time?");
                            SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.PutTransactedObject|RES|CPOOL> {0}, Transaction {1}, Connection {2}, Pushing.", Id, transaction.GetHashCode(), transactedObject.ObjectID);
                            connections.Add(transactedObject);
                        }
                    }
                    else
                    {
                        SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.PutTransactedObject|RES|CPOOL> {0}, Transaction {1}, Connection {2}, Adding List to transacted pool.", Id, transaction.GetHashCode(), transactedObject.ObjectID);

                        // add the connection/transacted object to the list
                        newConnections.Add(transactedObject);

                        _transactedCxns.Add(transactionClone, newConnections);
                        transactionClone = null; // we've used it -- don't throw it or the TransactedConnectionList that references it away.                                
                    }
                }
            }
            finally
            {
                if (transactionClone != null)
                {
                    if (newConnections != null)
                    {
                        // another thread created the transaction pool and thus the new 
                        //   TransactedConnectionList was not used, so dispose of it and
                        //   the transaction clone that it incorporates.
                        newConnections.Dispose();
                    }
                    else
                    {
                        // memory allocation for newConnections failed...clean up unused transactionClone
                        transactionClone.Dispose();
                    }
                }
            }
            SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.PutTransactedObject|RES|CPOOL> {0}, Transaction {1}, Connection {2}, Added.", Id, transaction.GetHashCode(), transactedObject.ObjectID);
        }

        SqlClientEventSource.Metrics.EnterFreeConnection();
    }

    /// <summary>
    /// Handles the completion of a transaction by removing the associated connection from the 
    /// transacted pool and returning it to the main connection pool.
    /// </summary>
    /// <param name="transaction">The transaction that has completed.</param>
    /// <param name="transactedObject">The database connection that was enlisted in the completed transaction.</param>
    /// <remarks>
    /// This method is called when a transaction completes (either by committing or rolling back).
    /// It removes the specified connection from the transacted pool and returns it to the main
    /// connection pool for reuse. If this was the last connection in the transaction's pool,
    /// the entire transaction pool entry is removed and disposed.
    /// 
    /// Due to the asynchronous nature of transaction completion notifications, there's no guarantee
    /// about the order in which PutTransactedObject and TransactionEnded are called. This method
    /// handles cases where the transaction pool may not yet exist when the transaction completes.
    /// </remarks>
    internal void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
    {
        SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.TransactionEnded|RES|CPOOL> {0}, Transaction {1}, Connection {2}, Transaction Completed", Id, transaction.GetHashCode(), transactedObject.ObjectID);
        TransactedConnectionList? connections;
        int entry = -1;

        // NOTE: because TransactionEnded is an asynchronous notification, there's no guarantee
        //   around the order in which PutTransactionObject and TransactionEnded are called. As
        //   such, it is possible that the transaction does not yet have a pool created.

        // TODO: is this a plausible and/or likely scenario? Do we need to have a mechanism to ensure
        // TODO:   that the pending creation of a transacted pool for this transaction is aborted when
        // TODO:   PutTransactedObject finally gets some CPU time?

        lock (_transactedCxns)
        {
            if (_transactedCxns.TryGetValue(transaction, out connections)
                && connections is not null)
            {
                bool shouldDisposeConnections = false;

                // Lock connections to avoid conflict with GetTransactionObject
                lock (connections)
                {
                    entry = connections.IndexOf(transactedObject);

                    if (entry >= 0)
                    {
                        connections.RemoveAt(entry);
                    }

                    // Once we've completed all the ended notifications, we can
                    // safely remove the list from the transacted pool.
                    if (0 >= connections.Count)
                    {
                        SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.TransactionEnded|RES|CPOOL> {0}, Transaction {1}, Removing List from transacted pool.", Id, transaction.GetHashCode());
                        _transactedCxns.Remove(transaction);

                        // we really need to dispose our connection list; it may have 
                        // native resources via the tx and GC may not happen soon enough.
                        shouldDisposeConnections = true;
                    }
                }

                if (shouldDisposeConnections)
                {
                    connections.Dispose();
                }
            }
            else
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.TransactedConnectionPool.TransactionEnded|RES|CPOOL> {0}, Transaction {1}, Connection {2}, Transacted pool not yet created prior to transaction completing. Connection may be leaked.", Id, transaction.GetHashCode(), transactedObject.ObjectID);
            }
        }

        // If (and only if) we found the connection in the list of
        // connections, we'll put it back...
        if (0 <= entry)
        {
            SqlClientEventSource.Metrics.ExitFreeConnection();
            Pool.PutObjectFromTransactedPool(transactedObject);
        }
    }

}