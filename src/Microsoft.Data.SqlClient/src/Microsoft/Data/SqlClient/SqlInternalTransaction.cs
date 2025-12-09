// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Threading;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal enum TransactionState
    {
        Pending = 0,
        Active = 1,
        Aborted = 2,
        Committed = 3,
        Unknown = 4,
    }

    internal enum TransactionType
    {
        LocalFromTSQL = 1,
        LocalFromAPI = 2,
        Delegated = 3,
        Distributed = 4,
        Context = 5,     // only valid in proc.
    }

    sealed internal class SqlInternalTransaction
    {
        internal const long NullTransactionId = 0;

        private TransactionState _transactionState;
        private readonly TransactionType _transactionType;
        private long _transactionId;             // passed in the MARS headers
        private int _openResultCount;           // passed in the MARS headers
        private SqlInternalConnection _innerConnection;
        private bool _disposing;                 // used to prevent us from throwing exceptions while we're disposing
        private WeakReference<SqlTransaction> _parent;                    // weak ref to the outer transaction object; needs to be weak to allow GC to occur.

        private static int s_objectTypeCount; // EventSource counter
        internal readonly int _objectID = Interlocked.Increment(ref s_objectTypeCount);

        internal bool RestoreBrokenConnection { get; set; }
        internal bool ConnectionHasBeenRestored { get; set; }

        internal SqlInternalTransaction(SqlInternalConnection innerConnection, TransactionType type, SqlTransaction outerTransaction) : this(innerConnection, type, outerTransaction, NullTransactionId)
        {
        }

        internal SqlInternalTransaction(SqlInternalConnection innerConnection, TransactionType type, SqlTransaction outerTransaction, long transactionId)
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent("SqlInternalTransaction.ctor | RES | CPOOL | Object Id {0}, Created for connection {1}, outer transaction {2}, Type {3}", ObjectID, innerConnection.ObjectID, outerTransaction?.ObjectId, (int)type);
            _innerConnection = innerConnection;
            _transactionType = type;

            if (outerTransaction != null)
            {
                _parent = new WeakReference<SqlTransaction>(outerTransaction);
            }

            _transactionId = transactionId;
            RestoreBrokenConnection = false;
            ConnectionHasBeenRestored = false;
        }

        internal bool HasParentTransaction =>
                // Return true if we are an API started local transaction, or if we were a TSQL
                // started local transaction and were then wrapped with a parent transaction as
                // a result of a later API begin transaction.
                (_transactionType == TransactionType.LocalFromAPI) ||
                                (_transactionType == TransactionType.LocalFromTSQL && _parent != null);

        internal bool IsAborted => _transactionState == TransactionState.Aborted;

        internal bool IsActive => _transactionState == TransactionState.Active;

        internal bool IsCommitted => _transactionState == TransactionState.Committed;

        internal bool IsCompleted => _transactionState == TransactionState.Aborted
                     || _transactionState == TransactionState.Committed
                     || _transactionState == TransactionState.Unknown;

        internal bool IsDelegated =>_transactionType == TransactionType.Delegated;

        internal bool IsDistributed => _transactionType == TransactionType.Distributed;

#if NETFRAMEWORK
        internal bool IsContext => _transactionType == TransactionType.Context;
#endif

        internal bool IsLocal => _transactionType == TransactionType.LocalFromTSQL
                            || _transactionType == TransactionType.LocalFromAPI
#if NETFRAMEWORK
                            || IsContext
#endif
                            ;

        internal bool IsOrphaned
        {
            get
            {
                // An internal transaction is orphaned when its parent has been
                // reclaimed by GC.
                bool result;
                if (_parent == null)
                {
                    // No parent, so we better be LocalFromTSQL.  Should we even return in this case -
                    // since it could be argued this is invalid?
                    Debug.Fail("Why are we calling IsOrphaned with no parent?");
                    Debug.Assert(_transactionType == TransactionType.LocalFromTSQL, "invalid state");
                    result = false;
                }
                else if (!_parent.TryGetTarget(out _))
                {
                    // We had a parent, but parent was GC'ed.
                    result = true;
                }
                else
                {
                    // We have a parent, and parent is alive.
                    result = false;
                }

                return result;
            }
        }

        internal bool IsZombied => _innerConnection == null;

        internal int ObjectID => _objectID;

        internal int OpenResultsCount => _openResultCount;

        internal SqlTransaction Parent
        {
            get
            {
                SqlTransaction result = null;
                // Should we protect against this, since this probably is an invalid state?
                Debug.Assert(_parent != null, "Why are we calling Parent with no parent?");
                if (_parent != null && _parent.TryGetTarget(out SqlTransaction target))
                {
                    result = target;
                }
                return result;
            }
        }

        internal long TransactionId
        {
            get => _transactionId;
            set
            {
                Debug.Assert(NullTransactionId == _transactionId, "setting transaction cookie while one is active?");
                _transactionId = value;
            }
        }

        internal void Activate() => _transactionState = TransactionState.Active;

        private void CheckTransactionLevelAndZombie()
        {
            try
            {
                if (!IsZombied && GetServerTransactionLevel() == 0)
                {
                    // If not zombied, not closed, and not in transaction, zombie.
                    Zombie();
                }
            }
            catch (Exception e)
            {
                if (!ADP.IsCatchableExceptionType(e))
                {
                    throw;
                }
#if NETFRAMEWORK
                ADP.TraceExceptionWithoutRethrow(e);
#endif

                Zombie(); // If exception caught when trying to check level, zombie.
            }
        }

        internal void CloseFromConnection()
        {
            SqlInternalConnection innerConnection = _innerConnection;

            Debug.Assert(innerConnection != null, "How can we be here if the connection is null?");
            SqlClientEventSource.Log.TryPoolerTraceEvent("SqlInternalTransaction.CloseFromConnection | RES | CPOOL | Object Id {0}, Closing transaction", ObjectID);
            bool processFinallyBlock = true;
            try
            {
                innerConnection.ExecuteTransaction(TransactionRequest.IfRollback, null, IsolationLevel.Unspecified, null, false);
            }
            catch (Exception e)
            {
                processFinallyBlock = ADP.IsCatchableExceptionType(e);
                throw;
            }
            finally
            {
                if (processFinallyBlock)
                {
                    // Always ensure we're zombied; 2005 will send an EnvChange that
                    // will cause the zombie, but only if we actually go to the wire;
                    // 7.0 and 2000 won't send the env change, so we have to handle
                    // them ourselves.
                    Zombie();
                }
            }
        }

        internal void Commit()
        {
            using (TryEventScope.Create("SqlInternalTransaction.Commit | API | Object Id {0}", ObjectID))
            {
                if (_innerConnection.IsLockedForBulkCopy)
                {
                    throw SQL.ConnectionLockedForBcpEvent();
                }

                _innerConnection.ValidateConnectionForExecute(null);

                // If this transaction has been completed, throw exception since it is unusable.
                try
                {
                    // COMMIT ignores transaction names, and so there is no reason to pass it anything.  COMMIT
                    // simply commits the transaction from the most recent BEGIN, nested or otherwise.
                    _innerConnection.ExecuteTransaction(TransactionRequest.Commit, null, IsolationLevel.Unspecified, null, false);
                    ZombieParent();
                }
                catch (Exception e)
                {
                    if (ADP.IsCatchableExceptionType(e))
                    {
                        CheckTransactionLevelAndZombie();
                    }

                    throw;
                }
            }
        }

        internal void Completed(TransactionState transactionState)
        {
            Debug.Assert(TransactionState.Active < transactionState, "invalid transaction completion state?");
            _transactionState = transactionState;
            Zombie();
        }

        internal int DecrementAndObtainOpenResultCount()
        {
            int openResultCount = Interlocked.Decrement(ref _openResultCount);
            if (openResultCount < 0)
            {
                throw SQL.OpenResultCountExceeded();
            }
            return openResultCount;
        }

        internal void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent("SqlInternalTransaction.Dispose | RES | CPOOL | Object Id {0}, Disposing", ObjectID);
            if (disposing)
            {
                if (_innerConnection != null)
                {
                    // implicitly rollback if transaction still valid
                    _disposing = true;
                    this.Rollback();
                }
            }
        }
        /// <summary>
        /// This function is needed for those times when it is impossible to determine the server's
        /// transaction level, unless the user's arguments were parsed - which is something we don't want
        ///to do.  An example when it is impossible to determine the level is after a rollback.
        /// </summary>
        /// <returns></returns>
        private int GetServerTransactionLevel()
        {
            using (SqlCommand transactionLevelCommand = new SqlCommand("set @out = @@trancount", (SqlConnection)(_innerConnection.Owner)))
            {
                transactionLevelCommand.Transaction = Parent;

                SqlParameter parameter = new SqlParameter("@out", SqlDbType.Int);
                parameter.Direction = ParameterDirection.Output;
                transactionLevelCommand.Parameters.Add(parameter);
                transactionLevelCommand.RunExecuteReader(CommandBehavior.Default, RunBehavior.UntilDone, returnStream: false, nameof(GetServerTransactionLevel));

                return (int)parameter.Value;
            }
        }

        internal int IncrementAndObtainOpenResultCount()
        {
            int openResultCount = Interlocked.Increment(ref _openResultCount);

            if (openResultCount < 0)
            {
                throw SQL.OpenResultCountExceeded();
            }
            return openResultCount;
        }

        internal void InitParent(SqlTransaction transaction)
        {
            Debug.Assert(_parent == null, "Why do we have a parent on InitParent?");
            _parent = new WeakReference<SqlTransaction>(transaction);
        }

        internal void Rollback()
        {
            using (TryEventScope.Create("SqlInternalTransaction.Rollback | API | Object Id {0}", ObjectID))
            {
                if (_innerConnection.IsLockedForBulkCopy)
                {
                    throw SQL.ConnectionLockedForBcpEvent();
                }

                _innerConnection.ValidateConnectionForExecute(null);

                try
                {
                    // If no arg is given to ROLLBACK it will rollback to the outermost begin - rolling back
                    // all nested transactions as well as the outermost transaction.
                    _innerConnection.ExecuteTransaction(TransactionRequest.IfRollback, null, IsolationLevel.Unspecified, null, false);

                    // Since Rollback will rollback to outermost begin, no need to check
                    // server transaction level.  This transaction has been completed.
                    Zombie();
                }
                catch (Exception e)
                {
                    if (ADP.IsCatchableExceptionType(e))
                    {
                        CheckTransactionLevelAndZombie();

                        if (!_disposing)
                        {
                            throw;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        internal void Rollback(string transactionName)
        {
            using (TryEventScope.Create("SqlInternalTransaction.Rollback | API | Object Id {0}, Transaction Name {1}", ObjectID, transactionName))
            {
                if (_innerConnection.IsLockedForBulkCopy)
                {
                    throw SQL.ConnectionLockedForBcpEvent();
                }

                _innerConnection.ValidateConnectionForExecute(null);

                // ROLLBACK takes either a save point name or a transaction name.  It will rollback the
                // transaction to either the save point with the save point name or begin with the
                // transaction name.  NOTE: for simplicity it is possible to give all save point names
                // the same name, and ROLLBACK will simply rollback to the most recent save point with the
                // save point name.
                if (string.IsNullOrEmpty(transactionName))
                {
                    throw SQL.NullEmptyTransactionName();
                }

                try
                {
                    _innerConnection.ExecuteTransaction(TransactionRequest.Rollback, transactionName, IsolationLevel.Unspecified, null, false);
                }
                catch (Exception e)
                {
                    if (ADP.IsCatchableExceptionType(e))
                    {
                        CheckTransactionLevelAndZombie();
                    }
                    throw;
                }
            }
        }

        internal void Save(string savePointName)
        {
            using (TryEventScope.Create("SqlInternalTransaction.Save | API | Object Id {0}, Save Point Name {1}", ObjectID, savePointName))
            {
                _innerConnection.ValidateConnectionForExecute(null);

                // ROLLBACK takes either a save point name or a transaction name.  It will rollback the
                // transaction to either the save point with the save point name or begin with the
                // transaction name.  So, to rollback a nested transaction you must have a save point.
                // SAVE TRANSACTION MUST HAVE AN ARGUMENT!!!  Save Transaction without an arg throws an
                // exception from the server.  So, an overload for SaveTransaction without an arg doesn't make
                // sense to have.  Save Transaction does not affect the transaction level.
                if (string.IsNullOrEmpty(savePointName))
                {
                    throw SQL.NullEmptyTransactionName();
                }

                try
                {
                    _innerConnection.ExecuteTransaction(TransactionRequest.Save, savePointName, IsolationLevel.Unspecified, null, false);
                }
                catch (Exception e)
                {
                    if (ADP.IsCatchableExceptionType(e))
                    {
                        CheckTransactionLevelAndZombie();
                    }

                    throw;
                }
            }
        }

        internal void Zombie()
        {
            // Called by several places in the code to ensure that the outer
            // transaction object has been zombied and the parser has broken
            // it's reference to us.

            // NOTE: we'll be called from the TdsParser when it gets appropriate
            // ENVCHANGE events that indicate the transaction has completed, however
            // we cannot rely upon those events occurring in the case of pre-2005
            // servers (and when we don't go to the wire because the connection
            // is broken) so we can also be called from the Commit/Rollback/Save
            // methods to handle that case as well.

            // There are two parts to a full zombie:
            // 1) Zombie parent and disconnect outer transaction from internal transaction
            // 2) Disconnect internal transaction from connection and parser
            // Number 1 needs to be done whenever a SqlTransaction object is completed.  Number
            // 2 is only done when a transaction is actually completed.  Since users can begin
            // transactions both in and outside of the API, and since nested begins are not actual
            // transactions we need to distinguish between #1 and #2.See SQL BU DT 291159
            // for further details.

            ZombieParent();

            SqlInternalConnection innerConnection = _innerConnection;
            _innerConnection = null;

            if (innerConnection != null)
            {
                innerConnection.DisconnectTransaction(this);
            }
        }

        private void ZombieParent()
        {
            if (_parent != null && _parent.TryGetTarget(out SqlTransaction parent))
            {
                parent.Zombie();
            }
            _parent = null;
        }

        internal string TraceString() => string.Format(/*IFormatProvider*/ null, 
            "(ObjId={0}, tranId={1}, state={2}, type={3}, open={4}, disp={5}",
            ObjectID, _transactionId, _transactionState, _transactionType, _openResultCount, _disposing);


    }
}
