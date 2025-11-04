// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Diagnostics;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

#if NETFRAMEWORK
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
#endif

namespace Microsoft.Data.SqlClient
{
    internal abstract class SqlInternalConnection : DbConnectionInternal
    {
        /// <summary>
        /// Cache the whereabouts (DTC Address) for exporting.
        /// </summary>
        private byte[] _whereAbouts;

        /// <summary>
        /// ID of the Azure SQL DB Transaction Manager (Non-MSDTC)
        /// </summary>
        private static readonly Guid s_globalTransactionTMID = new("1c742caf-6680-40ea-9c26-6b6846079764");

        internal SqlCommand.ExecuteReaderAsyncCallContext CachedCommandExecuteReaderAsyncContext;
        internal SqlCommand.ExecuteNonQueryAsyncCallContext CachedCommandExecuteNonQueryAsyncContext;
        internal SqlCommand.ExecuteXmlReaderAsyncCallContext CachedCommandExecuteXmlReaderAsyncContext;
        internal SqlDataReader.Snapshot CachedDataReaderSnapshot;
        internal SqlDataReader.IsDBNullAsyncCallContext CachedDataReaderIsDBNullContext;
        internal SqlDataReader.ReadAsyncCallContext CachedDataReaderReadAsyncContext;

        /// <summary>
        /// The current database for this connection.
        /// Null if the connection is not open yet.
        /// </summary>
        internal string CurrentDatabase { get; set; }

        /// <summary>
        /// The current data source for this connection.
        /// 
        /// if connection is not open yet, CurrentDataSource is null
        /// if connection is open:
        /// * for regular connections, it is set to the Data Source value from connection string
        /// * for failover connections, it is set to the FailoverPartner value from the connection string
        /// </summary>
        internal string CurrentDataSource { get; set; }

        /// <summary>
        /// The delegated (or promoted) transaction this connection is responsible for.
        /// </summary>
        internal SqlDelegatedTransaction DelegatedTransaction { get; set; }

        internal enum TransactionRequest
        {
            Begin,
            Promote,
            Commit,
            Rollback,
            IfRollback,
            Save
        };

        /// <summary>
        /// Constructs a new SqlInternalConnection object using the provided connection options.
        /// </summary>
        /// <param name="connectionOptions">The options to use for this connection.</param>
        internal SqlInternalConnection(SqlConnectionString connectionOptions) : base()
        {
            Debug.Assert(connectionOptions != null, "null connectionOptions?");
            ConnectionOptions = connectionOptions;
        }

        #region Properties

        /// <summary>
        /// A reference to the SqlConnection that owns this internal connection.
        /// </summary>
        internal SqlConnection Connection
        {
            get
            {
                return (SqlConnection)Owner;
            }
        }

        /// <summary>
        /// The connection options to be used for this connection.
        /// </summary>
        internal SqlConnectionString ConnectionOptions { get; init; }

        /// <summary>
        /// The Transaction currently associated with this connection.
        /// </summary>
        abstract internal SqlInternalTransaction CurrentTransaction { get; }

        // SQLBU 415870
        //  Get the internal transaction that should be hooked to a new outer transaction
        //  during a BeginTransaction API call.  In some cases (i.e. connection is going to
        //  be reset), CurrentTransaction should not be hooked up this way.
        /// <summary>
        /// TODO: need to understand this property better
        /// </summary>
        virtual internal SqlInternalTransaction AvailableInternalTransaction
        {
            get
            {
                return CurrentTransaction;
            }
        }

        /// <summary>
        /// TODO: need to understand this property better
        /// </summary>
        abstract internal SqlInternalTransaction PendingTransaction { get; }

        /// <summary>
        /// Whether this connection is the root of a delegated or promoted transaction.
        /// </summary>
        override internal bool IsTransactionRoot
        {
            get
            {
                SqlDelegatedTransaction delegatedTransaction = DelegatedTransaction;
                return delegatedTransaction != null && (delegatedTransaction.IsActive);
            }
        }

        /// <summary>
        /// Whether this connection has a local (non-delegated) transaction.
        /// </summary>
        internal bool HasLocalTransaction
        {
            get
            {
                SqlInternalTransaction currentTransaction = CurrentTransaction;
                bool result = currentTransaction != null && currentTransaction.IsLocal;
                return result;
            }
        }

        /// <summary>
        /// Whether this connection has a local transaction started from the API (i.e., SqlConnection.BeginTransaction)
        /// or had a TSQL transaction and later got wrapped by an API transaction.
        /// </summary>
        internal bool HasLocalTransactionFromAPI
        {
            get
            {
                SqlInternalTransaction currentTransaction = CurrentTransaction;
                bool result = currentTransaction != null && currentTransaction.HasParentTransaction;
                return result;
            }
        }

        /// <summary>
        /// Indicates whether the connection is currently enlisted in a transaction.
        /// </summary>
        internal bool IsEnlistedInTransaction { get; private set; }

        /// <summary>
        /// Whether this connection is locked for bulk copy operations.
        /// </summary>
        abstract internal bool IsLockedForBulkCopy { get; }

        /// <summary>
        /// Whether the server version is SQL Server 2008 or newer.
        /// </summary>
        abstract internal bool Is2008OrNewer {  get; }

        /// <summary>
        /// A token returned by the server when we promote transaction.
        /// </summary>
        internal byte[] PromotedDtcToken { get; set; }

        /// <summary>
        /// Whether this is a Global Transaction (Non-MSDTC, Azure SQL DB Transaction)
        /// TODO: overlaps with IsGlobalTransactionsEnabledForServer, need to consolidate to avoid bugs
        /// </summary>
        internal bool IsGlobalTransaction { get; set; }

        /// <summary>
        /// Whether Global Transactions are enabled. Only supported by Azure SQL.
        /// False if disabled or connected to on-prem SQL Server.
        /// </summary>
        internal bool IsGlobalTransactionsEnabledForServer { get; set; }

        /// <summary>
        /// Whether this connection is to an Azure SQL Database.
        /// </summary>
        internal bool IsAzureSqlConnection { get; set; }

        #endregion

        override public DbTransaction BeginTransaction(System.Data.IsolationLevel iso)
        {
            return BeginSqlTransaction(iso, null, false);
        }

        virtual internal SqlTransaction BeginSqlTransaction(System.Data.IsolationLevel iso, string transactionName, bool shouldReconnect)
        {
            SqlStatistics statistics = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Connection.Statistics);

                #if NETFRAMEWORK
                SqlConnection.ExecutePermission.Demand(); // MDAC 81476
                #endif

                ValidateConnectionForExecute(null);

                if (HasLocalTransactionFromAPI)
                {
                    throw ADP.ParallelTransactionsNotSupported(Connection);
                }

                if (iso == System.Data.IsolationLevel.Unspecified)
                {
                    iso = System.Data.IsolationLevel.ReadCommitted; // Default to ReadCommitted if unspecified.
                }

                SqlTransaction transaction = new(this, Connection, iso, AvailableInternalTransaction);
                transaction.InternalTransaction.RestoreBrokenConnection = shouldReconnect;
                ExecuteTransaction(TransactionRequest.Begin, transactionName, iso, transaction.InternalTransaction, false);
                transaction.InternalTransaction.RestoreBrokenConnection = false;
                return transaction;
            }
            // @TODO: CER Exception Handling was removed here (see GH#3581)
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        override public void ChangeDatabase(string database)
        {
            if (string.IsNullOrEmpty(database))
            {
                throw ADP.EmptyDatabaseName();
            }

            ValidateConnectionForExecute(null);

            ChangeDatabaseInternal(database);  // do the real work...
        }

        abstract protected void ChangeDatabaseInternal(string database);

        override protected void CleanupTransactionOnCompletion(Transaction transaction)
        {
            // Note: unlocked, potentially multi-threaded code, so pull delegate to local to
            //  ensure it doesn't change between test and call.
            SqlDelegatedTransaction delegatedTransaction = DelegatedTransaction;
            if (delegatedTransaction != null)
            {
                delegatedTransaction.TransactionEnded(transaction);
            }
        }

        override protected DbReferenceCollection CreateReferenceCollection()
        {
            return new SqlReferenceCollection();
        }

        override protected void Deactivate()
        {
            try
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlInternalConnection.Deactivate | ADV | Object Id {0} deactivating, Client Connection Id {1}", ObjectID, Connection?.ClientConnectionId);

                SqlReferenceCollection referenceCollection = (SqlReferenceCollection)ReferenceCollection;
                if (referenceCollection != null)
                {
                    referenceCollection.Deactivate();
                }

                // Invoke subclass-specific deactivation logic
                InternalDeactivate();
            }
            // @TODO: CER Exception Handling was removed here (see GH#3581)
            catch (Exception e)
            {
                if (!ADP.IsCatchableExceptionType(e))
                {
                    throw;
                }

                // if an exception occurred, the inner connection will be
                // marked as unusable and destroyed upon returning to the
                // pool
                DoomThisConnection();
#if NETFRAMEWORK
                ADP.TraceExceptionWithoutRethrow(e);
#endif
            }
        }

        abstract internal void DisconnectTransaction(SqlInternalTransaction internalTransaction);

        override public void Dispose()
        {
            _whereAbouts = null;
            base.Dispose();
        }

        protected void Enlist(Transaction tx)
        {
            // This method should not be called while the connection has a
            // reference to an active delegated transaction.
            // Manual enlistment via SqlConnection.EnlistTransaction
            // should catch this case and throw an exception.
            //
            // Automatic enlistment isn't possible because
            // Sys.Tx keeps the connection alive until the transaction is completed.
            // TODO: why do we assert pooling status? shouldn't we just be checking
            // whether the connection is the root of the transaction?
            Debug.Assert(!(IsTransactionRoot && Pool == null), "cannot defect an active delegated transaction!");  // potential race condition, but it's an assert

            if (tx == null)
            {
                if (IsEnlistedInTransaction)
                {
                    EnlistNull();
                }
                else
                {
                    // When IsEnlistedInTransaction is false, it means we are in one of two states:
                    // 1. EnlistTransaction is null, so the connection is truly not enlisted in a transaction, or
                    // 2. Connection is enlisted in a SqlDelegatedTransaction.
                    //
                    // For #2, we have to consider whether or not the delegated transaction is active.
                    // If it is not active, we allow the enlistment in the NULL transaction.
                    //
                    // If it is active, technically this is an error.
                    // However, no exception is thrown as this was the precedent (and this case is silently ignored, no error, but no enlistment either).
                    // There are two mitigations for this:
                    // 1. SqlConnection.EnlistTransaction checks that the enlisted transaction has completed before allowing a different enlistment.
                    // 2. For debug builds, the assert at the beginning of this method checks for an enlistment in an active delegated transaction.
                    Transaction enlistedTransaction = EnlistedTransaction;
                    if (enlistedTransaction != null && enlistedTransaction.TransactionInformation.Status != TransactionStatus.Active)
                    {
                        EnlistNull();
                    }
                }
            }
            // Only enlist if it's different...
            else if (!tx.Equals(EnlistedTransaction))
            { // WebData 20000024 - Must use Equals, not !=
                EnlistNonNull(tx);
            }
        }

        private void EnlistNonNull(Transaction tx)
        {
            Debug.Assert(tx != null, "null transaction?");
            SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlInternalConnection.EnlistNonNull | ADV | Object {0}, Transaction Id {1}, attempting to delegate.", ObjectID, tx?.TransactionInformation?.LocalIdentifier);
            bool hasDelegatedTransaction = false;

            // Promotable transactions are only supported on 2005
            // servers or newer.
            SqlDelegatedTransaction delegatedTransaction = new(this, tx);

            try
            {
                // NOTE: System.Transactions claims to resolve all
                // potential race conditions between multiple delegate
                // requests of the same transaction to different
                // connections in their code, such that only one
                // attempt to delegate will succeed.

                // NOTE: PromotableSinglePhaseEnlist will eventually
                // make a round trip to the server; doing this inside
                // a lock is not the best choice.  We presume that you
                // aren't trying to enlist concurrently on two threads
                // and leave it at that -- We don't claim any thread
                // safety with regard to multiple concurrent requests
                // to enlist the same connection in different
                // transactions, which is good, because we don't have
                // it anyway.

                // PromotableSinglePhaseEnlist may not actually promote
                // the transaction when it is already delegated (this is
                // the way they resolve the race condition when two
                // threads attempt to delegate the same Lightweight
                // Transaction)  In that case, we can safely ignore
                // our delegated transaction, and proceed to enlist
                // in the promoted one.

                // NOTE: Global Transactions is an Azure SQL DB only
                // feature where the Transaction Manager (TM) is not
                // MS-DTC. Sys.Tx added APIs to support Non MS-DTC
                // promoter types/TM in .NET 4.6.2. Following directions
                // from .NETFX shiproom, to avoid a "hard-dependency"
                // (compile time) on Sys.Tx, we use reflection to invoke
                // the new APIs. Further, the IsGlobalTransaction flag
                // indicates that this is an Azure SQL DB Transaction
                // that could be promoted to a Global Transaction (it's
                // always false for on-prem Sql Server). The Promote()
                // call in SqlDelegatedTransaction makes sure that the
                // right Sys.Tx.dll is loaded and that Global Transactions
                // are actually allowed for this Azure SQL DB.

                if (IsGlobalTransaction)
                {
                    if (SysTxForGlobalTransactions.EnlistPromotableSinglePhase == null)
                    {
                        // This could be a local Azure SQL DB transaction.
                        hasDelegatedTransaction = tx.EnlistPromotableSinglePhase(delegatedTransaction);
                    }
                    else
                    {
                        hasDelegatedTransaction = (bool)SysTxForGlobalTransactions.EnlistPromotableSinglePhase.Invoke(tx, new object[] { delegatedTransaction, s_globalTransactionTMID });
                    }
                }
                else
                {
                    // This is an MS-DTC distributed transaction
                    hasDelegatedTransaction = tx.EnlistPromotableSinglePhase(delegatedTransaction);
                }

                if (hasDelegatedTransaction)
                {
                    DelegatedTransaction = delegatedTransaction;
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlInternalConnection.EnlistNonNull | ADV | Object Id {0}, Client Connection Id {1} delegated to transaction {1} with transactionId {2}", ObjectID, Connection?.ClientConnectionId, delegatedTransaction?.ObjectID, delegatedTransaction?.Transaction?.TransactionInformation?.LocalIdentifier);
                }
            }
            catch (SqlException e)
            {
                // we do not want to eat the error if it is a fatal one
                if (e.Class >= TdsEnums.FATAL_ERROR_CLASS)
                {
                    throw;
                }

                // if the parser is null or its state is not openloggedin, the connection is no longer good.
                if (this is SqlInternalConnectionTds tdsConnection)
                {
                    TdsParser parser = tdsConnection.Parser;
                    if (parser == null || parser.State != TdsParserState.OpenLoggedIn)
                    {
                        throw;
                    }
                }

#if NETFRAMEWORK
                ADP.TraceExceptionWithoutRethrow(e);
#endif
                // In this case, SqlDelegatedTransaction.Initialize
                // failed and we don't necessarily want to reject
                // things -- there may have been a legitimate reason
                // for the failure.
            }

            if (!hasDelegatedTransaction)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlInternalConnection.EnlistNonNull | ADV | Object Id {0}, delegation not possible, enlisting.", ObjectID);
                byte[] cookie = null;

                if (IsGlobalTransaction)
                {
                    if (SysTxForGlobalTransactions.GetPromotedToken == null)
                    {
                        throw SQL.UnsupportedSysTxForGlobalTransactions();
                    }

                    cookie = (byte[])SysTxForGlobalTransactions.GetPromotedToken.Invoke(tx, null);
                }
                else
                {
                    if (_whereAbouts == null)
                    {
                        byte[] dtcAddress = GetDTCAddress();
                        _whereAbouts = dtcAddress ?? throw SQL.CannotGetDTCAddress();
                    }
                    cookie = GetTransactionCookie(tx, _whereAbouts);
                }

                // send cookie to server to finish enlistment
                PropagateTransactionCookie(cookie);

                IsEnlistedInTransaction = true;
                SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlInternalConnection.EnlistNonNull | ADV | Object Id {0}, Client Connection Id {1}, Enlisted in transaction with transactionId {2}", ObjectID, Connection?.ClientConnectionId, tx?.TransactionInformation?.LocalIdentifier);
            }

            EnlistedTransaction = tx; // Tell the base class about our enlistment


            // If we're on a 2005 or newer server, and we delegate the
            // transaction successfully, we will have done a begin transaction,
            // which produces a transaction id that we should execute all requests
            // on.  The TdsParser or SmiEventSink will store this information as
            // the current transaction.
            //
            // Likewise, propagating a transaction to a 2005 or newer server will
            // produce a transaction id that The TdsParser or SmiEventSink will
            // store as the current transaction.
            //
            // In either case, when we're working with a 2005 or newer server
            // we better have a current transaction by now.

            Debug.Assert(CurrentTransaction != null, "delegated/enlisted transaction with null current transaction?");
        }

        internal void EnlistNull()
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlInternalConnection.EnlistNull | ADV | Object Id {0}, unenlisting.", ObjectID);
            // We were in a transaction, but now we are not - so send
            // message to server with empty transaction - confirmed proper
            // behavior from Sameet Agarwal
            //
            // The connection pooler maintains separate pools for enlisted
            // transactions, and only when that transaction is committed or
            // rolled back will those connections be taken from that
            // separate pool and returned to the general pool of connections
            // that are not affiliated with any transactions.  When this
            // occurs, we will have a new transaction of null and we are
            // required to send an empty transaction payload to the server.

            PropagateTransactionCookie(null);

            IsEnlistedInTransaction = false;
            EnlistedTransaction = null; // Tell the base class about our enlistment

            SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlInternalConnection.EnlistNull | ADV | Object Id {0}, unenlisted.", ObjectID);

            // The EnlistTransaction above will return an TransactionEnded event,
            // which causes the TdsParser or SmiEventSink should to clear the
            // current transaction.
            //
            // In either case, when we're working with a 2005 or newer server
            // we better not have a current transaction at this point.

            Debug.Assert(CurrentTransaction == null, "unenlisted transaction with non-null current transaction?");   // verify it!
        }

        override public void EnlistTransaction(Transaction transaction)
        {
#if NETFRAMEWORK
            SqlConnection.VerifyExecutePermission();
#endif
            ValidateConnectionForExecute(null);

            // If a connection has a local transaction outstanding and you try
            // to enlist in a DTC transaction, SQL Server will rollback the
            // local transaction and then do the enlist (7.0 and 2000).  So, if
            // the user tries to do this, throw.
            if (HasLocalTransaction)
            {
                throw ADP.LocalTransactionPresent();
            }

            if (transaction != null && transaction.Equals(EnlistedTransaction))
            {
                // No-op if this is the current transaction
                return;
            }

            // If a connection is already enlisted in a DTC transaction and you
            // try to enlist in another one, in 7.0 the existing DTC transaction
            // would roll back and then the connection would enlist in the new
            // one. In SQL 2000 & 2005, when you enlist in a DTC transaction
            // while the connection is already enlisted in a DTC transaction,
            // the connection simply switches enlistments.  Regardless, simply
            // enlist in the user specified distributed transaction.  This
            // behavior matches OLEDB and ODBC.

            Enlist(transaction);
            // @TODO: CER Exception Handling was removed here (see GH#3581)
        }

        abstract internal void ExecuteTransaction(TransactionRequest transactionRequest, string name, System.Data.IsolationLevel iso, SqlInternalTransaction internalTransaction, bool isDelegateControlRequest);

        internal SqlDataReader FindLiveReader(SqlCommand command)
        {
            SqlDataReader reader = null;
            SqlReferenceCollection referenceCollection = (SqlReferenceCollection)ReferenceCollection;
            if (referenceCollection != null)
            {
                reader = referenceCollection.FindLiveReader(command);
            }
            return reader;
        }

        abstract protected byte[] GetDTCAddress();

        static private byte[] GetTransactionCookie(Transaction transaction, byte[] whereAbouts)
        {
            byte[] transactionCookie = null;
            if (transaction != null)
            {
                transactionCookie = TransactionInterop.GetExportCookie(transaction, whereAbouts);
            }
            return transactionCookie;
        }

        virtual protected void InternalDeactivate()
        {
        }

        // If wrapCloseInAction is defined, then the action it defines will be run with the connection close action passed in as a parameter
        // The close action also supports being run asynchronously
        internal void OnError(SqlException exception, bool breakConnection, Action<Action> wrapCloseInAction = null)
        {
            if (breakConnection)
            {
                DoomThisConnection();
            }

            SqlConnection connection = Connection;
            if (connection != null)
            {
                connection.OnError(exception, breakConnection, wrapCloseInAction);
            }
            else if (exception.Class >= TdsEnums.MIN_ERROR_CLASS)
            {
                // It is an error, and should be thrown.  Class of TdsEnums.MIN_ERROR_CLASS
                // or above is an error, below TdsEnums.MIN_ERROR_CLASS denotes an info message.
                throw exception;
            }
        }

        abstract protected void PropagateTransactionCookie(byte[] transactionCookie);

        abstract internal void ValidateConnectionForExecute(SqlCommand command);
    }
}
