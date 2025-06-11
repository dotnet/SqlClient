// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Server;
using System.Transactions;

namespace Microsoft.Data.SqlClient
{
    sealed internal class SqlInternalConnectionSmi : SqlInternalConnection
    {

        private SmiContext _smiContext;
        private SmiConnection _smiConnection;
        private SmiEventSink_Default _smiEventSink;
        private int _isInUse;            // 1 = Connected to open outer connection, 0 = not connected

        private SqlInternalTransaction _pendingTransaction; // transaction awaiting event signalling that it is active
        private SqlInternalTransaction _currentTransaction; // currently active non-context transaction.

        sealed private class EventSink : SmiEventSink_Default
        {

            SqlInternalConnectionSmi _connection;

            override internal string ServerVersion
            {
                // @TODO: Will always throw
                get => throw SQL.ContextUnavailableOutOfProc();
            }

            override protected void DispatchMessages(bool ignoreNonFatalMessages)
            {
                // Override this on the Connection event sink, since we can deal
                // with info messages here.
                SqlException exception = ProcessMessages(false, ignoreNonFatalMessages);

                if (exception != null)
                {
                    // SQLBUVSTS 225982, query for connection once to avoid race condition between GC (that may collect the connection) and the user thread
                    SqlConnection connection = _connection.Connection;
                    if (connection != null && connection.FireInfoMessageEventOnUserErrors)
                    {
                        connection.OnInfoMessage(new SqlInfoMessageEventArgs(exception));
                    }
                    else
                    {
                        _connection.OnError(exception, false);    // we can't really ever break the direct connection, can we?
                    }
                }
            }

            internal EventSink(SqlInternalConnectionSmi connection)
            {
                Debug.Assert(connection != null, "null connection?");
                _connection = connection;
            }

            internal override void DefaultDatabaseChanged(string databaseName)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.EventSink.DefaultDatabaseChanged|ADV> {0}, databaseName='{1}'.", _connection.ObjectID, databaseName);
                _connection.CurrentDatabase = databaseName;
            }

            internal override void TransactionCommitted(long transactionId)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.EventSink.TransactionCommitted|ADV> {0}, transactionId=0x{1}.", _connection.ObjectID, transactionId);
                _connection.TransactionEnded(transactionId, TransactionState.Committed);
            }

            internal override void TransactionDefected(long transactionId)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.EventSink.TransactionDefected|ADV> {0}, transactionId=0x{1}.", _connection.ObjectID, transactionId);
                _connection.TransactionEnded(transactionId, TransactionState.Unknown);
            }

            internal override void TransactionEnlisted(long transactionId)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.EventSink.TransactionEnlisted|ADV> {0}, transactionId=0x{1}.", _connection.ObjectID, transactionId);
                _connection.TransactionStarted(transactionId, true); // distributed;
            }

            internal override void TransactionEnded(long transactionId)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.EventSink.TransactionEnded|ADV> {0}, transactionId=0x{1}.", _connection.ObjectID, transactionId);
                _connection.TransactionEndedByServer(transactionId, TransactionState.Unknown);
            }

            internal override void TransactionRolledBack(long transactionId)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.EventSink.TransactionRolledBack|ADV> {0}, transactionId=0x{1}.", _connection.ObjectID, transactionId);

                // Dev11 1066: ensure delegated transaction is rolled back
                _connection.TransactionEndedByServer(transactionId, TransactionState.Aborted);
            }

            internal override void TransactionStarted(long transactionId)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.EventSink.TransactionStarted|ADV> {0}, transactionId=0x{1}.", _connection.ObjectID, transactionId);
                _connection.TransactionStarted(transactionId, false); // not distributed;
            }
        }

        // @TODO: No longer used -- delete!
        internal SqlInternalConnectionSmi(SqlConnectionString connectionOptions, SmiContext smiContext) : base(connectionOptions)
        {
            Debug.Assert(smiContext != null, "null smiContext?");

            _smiContext = smiContext;
            _smiContext.OutOfScope += new EventHandler(OnOutOfScope);

            _smiConnection = _smiContext.ContextConnection;
            Debug.Assert(_smiConnection != null, "null SmiContext.ContextConnection?");

            _smiEventSink = new EventSink(this);
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.ctor|ADV> {0}, constructed new SMI internal connection", ObjectID);
        }

        internal SmiEventSink CurrentEventSink
        {
            get
            {
                return _smiEventSink;
            }
        }

        override internal SqlInternalTransaction CurrentTransaction
        {
            get
            {
                return _currentTransaction;
            }
        }

        override internal bool IsLockedForBulkCopy
        {
            get
            {
                return false;   // no bulk copy in the Direct connection case.
            }
        }

        override internal bool Is2008OrNewer
        {
            // @TODO: Will always throw
            get => throw SQL.ContextUnavailableOutOfProc();
        }

        override internal SqlInternalTransaction PendingTransaction
        {
            get
            {
                return CurrentTransaction; // there are no differences between pending and current in proc.
            }
        }

        override public string ServerVersion
        {
            // @TODO: Will always throw
            get => throw SQL.ContextUnavailableOutOfProc();
        }

        /// <summary>
        /// Get boolean that specifies whether an enlisted transaction can be unbound from 
        /// the connection when that transaction completes.
        /// </summary>
        /// <value>
        /// True if the connection string property "TransactionBinding" is set to TransactionBindingEnum.ImplicitUnbind;
        /// otherwise, false.
        /// </value>
        protected override bool UnbindOnTransactionCompletion
        {
            get
            {
                return ConnectionOptions.TransactionBinding == SqlConnectionString.TransactionBindingEnum.ImplicitUnbind;
            }
        }

        // Workaround to access context transaction without rewriting connection pool & internalconnections properly.
        // Context transactions SHOULD be considered enlisted.
        //   This works for now only because we can't unenlist from the context transaction
        // DON'T START USING THIS ANYWHERE EXCEPT IN InternalTransaction and in InternalConnectionSmi!!!
        private Transaction ContextTransaction
        {
            get;
            set;
        }

        private Transaction InternalEnlistedTransaction
        {
            get
            {
                // Workaround to access context transaction without rewriting connection pool & internalconnections properly.
                // This SHOULD be a simple wrapper around EnlistedTransaction.
                //   This works for now only because we can't unenlist from the context transaction
                Transaction tx = EnlistedTransaction;

                if (tx == null)
                {
                    tx = ContextTransaction;
                }

                return tx;
            }
        }

        override protected void Activate(Transaction transaction)
        {
            Debug.Assert(false, "Activating an internal SMI connection?"); // we should never be activating, because that would indicate we're being pooled.
        }

        // @TODO: No longer used -- delete
        internal void Activate()
        {
            int wasInUse = System.Threading.Interlocked.Exchange(ref _isInUse, 1);
            if (0 != wasInUse)
            {
                throw SQL.ContextConnectionIsInUse();
            }

            CurrentDatabase = _smiConnection.GetCurrentDatabase(_smiEventSink);

            _smiEventSink.ProcessMessagesAndThrow();
        }

        internal void AutomaticEnlistment()
        {
            Transaction currentSystemTransaction = ADP.GetCurrentTransaction();      // NOTE: Must be first to ensure _smiContext.ContextTransaction is set!
            Transaction contextTransaction = _smiContext.ContextTransaction; // returns the transaction that was handed to SysTx that wraps the ContextTransactionId.
            long contextTransactionId = _smiContext.ContextTransactionId;
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.AutomaticEnlistment|ADV> {0}, contextTransactionId=0x{1}, contextTransaction={2}, currentSystemTransaction={3}.",
                ObjectID,
                contextTransactionId,
                contextTransaction != null ? contextTransaction.GetHashCode() : 0,
                currentSystemTransaction != null ? currentSystemTransaction.GetHashCode() : 0);

            if (SqlInternalTransaction.NullTransactionId != contextTransactionId)
            {
                if (currentSystemTransaction != null && contextTransaction != currentSystemTransaction)
                {
                    throw SQL.NestedTransactionScopesNotSupported();    // can't use TransactionScope(RequiresNew) inside a Sql Transaction.
                }

                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionSmi.AutomaticEnlistment|ADV> {0}, using context transaction with transactionId=0x{1}", ObjectID, contextTransactionId);
                _currentTransaction = new SqlInternalTransaction(this, TransactionType.Context, null, contextTransactionId);
                ContextTransaction = contextTransaction;
            }
            else if (currentSystemTransaction == null)
            {
                _currentTransaction = null;  // there really isn't a transaction.
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.AutomaticEnlistment|ADV> {0}, no transaction.", ObjectID);
            }
            else
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.AutomaticEnlistment|ADV> {0}, using current System.Transaction.", ObjectID);
                base.Enlist(currentSystemTransaction);
            }
        }

        override protected void ChangeDatabaseInternal(string database)
        {
            _smiConnection.SetCurrentDatabase(database, _smiEventSink);
            _smiEventSink.ProcessMessagesAndThrow();
        }

        override protected void InternalDeactivate()
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.Deactivate|ADV> {0}, Deactivating.", ObjectID);

            // When we put this to bed, we should not hold on to the transaction
            // or any activity (commit/rollback) may cause it to stop responding.
            if (!IsNonPoolableTransactionRoot)
            {
                base.Enlist(null);
            }

            if (_currentTransaction != null)
            {
                if (_currentTransaction.IsContext)
                {
                    _currentTransaction = null;
                }
                else if (_currentTransaction.IsLocal)
                {
                    _currentTransaction.CloseFromConnection();
                }
            }

            ContextTransaction = null;

            _isInUse = 0;  // don't need compare-exchange.
        }

        override internal void DelegatedTransactionEnded()
        {
            base.DelegatedTransactionEnded();
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.DelegatedTransactionEnded|ADV> {0}, cleaning up after Delegated Transaction Completion", ObjectID);
            _currentTransaction = null;           // clean up our current transaction too
        }

        override internal void DisconnectTransaction(SqlInternalTransaction internalTransaction)
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.DisconnectTransaction|ADV> {0}, Disconnecting Transaction {1}.", ObjectID, internalTransaction.ObjectID);

            // VSTS 215465/15029: allow _currentTransaction to be null - it can be cleared before by server's callback
            Debug.Assert(_currentTransaction == null || _currentTransaction == internalTransaction, "disconnecting different transaction");

            if (_currentTransaction != null && _currentTransaction == internalTransaction)
            {
                _currentTransaction = null;
            }
        }

        override public void Dispose()
        {
            _smiContext.OutOfScope -= new EventHandler(OnOutOfScope);
            base.Dispose();
        }

        override internal void ExecuteTransaction(
                    TransactionRequest transactionRequest,
                    string transactionName,
                    System.Data.IsolationLevel iso,
                    SqlInternalTransaction internalTransaction,
                    bool isDelegateControlRequest)
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                "<sc.SqlInternalConnectionSmi.ExecuteTransaction|ADV> {0}, transactionRequest={1}, " +
                "transactionName='{2}', isolationLevel={3}, internalTransaction=#{4} transactionId=0x{5}.",
                ObjectID,
                transactionRequest,
                transactionName,
                iso,
                internalTransaction != null ? internalTransaction.ObjectID : 0,
                internalTransaction != null ? internalTransaction.TransactionId : SqlInternalTransaction.NullTransactionId);

            switch (transactionRequest)
            {
                case TransactionRequest.Begin:
                    try
                    {
                        _pendingTransaction = internalTransaction; // store this for the time being.

                        _smiConnection.BeginTransaction(transactionName, iso, _smiEventSink);
                    }
                    finally
                    {
                        _pendingTransaction = null;
                    }

                    Debug.Assert(_smiEventSink.HasMessages || _currentTransaction != null, "begin transaction without TransactionStarted event?");
                    break;

                case TransactionRequest.Commit:
                    Debug.Assert(_currentTransaction != null, "commit transaction without TransactionStarted event?");

                    _smiConnection.CommitTransaction(_currentTransaction.TransactionId, _smiEventSink);
                    break;

                case TransactionRequest.Promote:
                    Debug.Assert(_currentTransaction != null, "promote transaction without TransactionStarted event?");
                    PromotedDTCToken = _smiConnection.PromoteTransaction(_currentTransaction.TransactionId, _smiEventSink);
                    break;

                case TransactionRequest.Rollback:
                case TransactionRequest.IfRollback:
                    Debug.Assert(_currentTransaction != null, "rollback/ifrollback transaction without TransactionStarted event?");
                    _smiConnection.RollbackTransaction(_currentTransaction.TransactionId, transactionName, _smiEventSink);
                    break;

                case TransactionRequest.Save:
                    Debug.Assert(_currentTransaction != null, "save transaction without TransactionStarted event?");
                    _smiConnection.CreateTransactionSavePoint(_currentTransaction.TransactionId, transactionName, _smiEventSink);
                    break;

                default:
                    Debug.Assert(false, "unhandled case for TransactionRequest");
                    break;
            }

            _smiEventSink.ProcessMessagesAndThrow();
        }

        override protected byte[] GetDTCAddress()
        {
            byte[] whereAbouts = _smiConnection.GetDTCAddress(_smiEventSink);     // might want to store this on the SmiLink because it doesn't change, but we want to be compatible with TDS which doesn't have a link yet.

            _smiEventSink.ProcessMessagesAndThrow();

            if (whereAbouts != null)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.GetDTCAddress|ADV> whereAbouts = {0}, Length = {1}", whereAbouts, (ushort)whereAbouts.Length);
            }
            else
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.GetDTCAddress|ADV> whereAbouts=null");
            }

            return whereAbouts;
        }

        // @TODO: No longer used -- delete!
        internal void GetCurrentTransactionPair(out long transactionId, out Transaction transaction)
        {
            // SQLBU 214740: Transaction state could change between obtaining tranid and transaction
            //  due to background SqlDelegatedTransaction processing. Lock the connection to prevent that.
            lock (this)
            {
                transactionId = CurrentTransaction != null ? CurrentTransaction.TransactionId : 0;
                transaction = null;
                if (0 != transactionId)
                {
                    transaction = InternalEnlistedTransaction;
                }
            }
        }

        private void OnOutOfScope(object s, EventArgs e)
        {
            // Called whenever the context goes out of scope, we need to make
            // sure that we close the connection, or the next person that uses
            // the context may appear to have the connection in use.
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.OutOfScope|ADV> {0} context is out of scope", ObjectID);

            // TODO: This is suspect to me -- if I go out of scope, who will commit the transaction?
            DelegatedTransaction = null;     // we don't want to hold this over to the next usage; it will automatically be reused as the context transaction...

            DbConnection owningObject = (DbConnection)Owner;

            try
            {
                if (owningObject != null && 1 == _isInUse)
                {
                    // SQLBU 369953
                    //  for various reasons, the owning object may no longer be connection to this
                    //  so call close on the owner, rather than trying to bypass to use internal close logic.
                    owningObject.Close();
                }
            }
            finally
            {
                // Now make sure this object is not left in an in-use state
                // this is safe, because no user code should be accessing the connection by this time
                ContextTransaction = null;
                _isInUse = 0;
            }
        }

        override protected void PropagateTransactionCookie(byte[] transactionCookie)
        {

            if (transactionCookie != null)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.PropagateTransactionCookie|ADV> transactionCookie", transactionCookie, (UInt16)transactionCookie.Length);
            }
            else
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionSmi.PropagateTransactionCookie|ADV> null");
            }

            // Propagate the transaction cookie to the server
            _smiConnection.EnlistTransaction(transactionCookie, _smiEventSink);
            _smiEventSink.ProcessMessagesAndThrow();
        }

        private void TransactionEndedByServer(long transactionId, TransactionState transactionState)
        {
            // Some extra steps required when the server initiates the ending of a transaction unilaterally
            //  as opposed to the client initiating it.
            //  Basically, we have to make the delegated transaction (if there is one) aware of the situation.

            SqlDelegatedTransaction delegatedTransaction = DelegatedTransaction;
            if (delegatedTransaction != null)
            {
                delegatedTransaction.Transaction.Rollback();    // just to make sure...
                DelegatedTransaction = null;   // He's dead, Jim.
            }

            // Now handle the standard transaction-ended stuff.
            TransactionEnded(transactionId, transactionState);
        }

        private void TransactionEnded(long transactionId, TransactionState transactionState)
        {
            // When we get notification of a completed transaction
            // we null out the current transaction.

            if (_currentTransaction != null)
            {
#if DEBUG
                // Check null for case where Begin and Rollback obtained in the same message.
                if (0 != _currentTransaction.TransactionId)
                {
                    Debug.Assert(_currentTransaction.TransactionId == transactionId, "transaction id's are not equal!");
                }
#endif
                _currentTransaction.Completed(transactionState);
                _currentTransaction = null;
            }
        }

        private void TransactionStarted(long transactionId, bool isDistributed)
        {
            // When we get notification from the server of a new
            // transaction, we move any pending transaction over to
            // the current transaction, then we store the token in it.
            // if there isn't a pending transaction, then it's either
            // a TSQL transaction or a distributed transaction.
            Debug.Assert(_currentTransaction == null, "non-null current transaction with an env change");
            _currentTransaction = _pendingTransaction;
            _pendingTransaction = null;

            if (_currentTransaction != null)
            {
                _currentTransaction.TransactionId = transactionId;   // this is defined as a ULongLong in the server and in the TDS Spec.
            }
            else
            {
                TransactionType transactionType = (isDistributed) ? TransactionType.Distributed : TransactionType.LocalFromTSQL;
                _currentTransaction = new SqlInternalTransaction(this, transactionType, null, transactionId);
            }
            _currentTransaction.Activate(); // SQLBUDT #376531 -- ensure this is activated to prevent asserts later.
        }

        override internal void ValidateConnectionForExecute(SqlCommand command)
        {
            SqlDataReader reader = FindLiveReader(null);
            if (reader != null)
            {
                // if MARS is on, then a datareader associated with the command exists
                // or if MARS is off, then a datareader exists
                TdsParser parser = command.Connection.Parser;
                throw ADP.OpenReaderExists(parser.MARSOn); // MDAC 66411
            }
        }
    }
}

