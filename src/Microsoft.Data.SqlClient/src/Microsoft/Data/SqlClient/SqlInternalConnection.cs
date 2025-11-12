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
        protected byte[] _whereAbouts;

        /// <summary>
        /// ID of the Azure SQL DB Transaction Manager (Non-MSDTC)
        /// </summary>
        protected static readonly Guid s_globalTransactionTMID = new("1c742caf-6680-40ea-9c26-6b6846079764");

        internal SqlCommand.ExecuteReaderAsyncCallContext CachedCommandExecuteReaderAsyncContext;
        internal SqlCommand.ExecuteNonQueryAsyncCallContext CachedCommandExecuteNonQueryAsyncContext;
        internal SqlCommand.ExecuteXmlReaderAsyncCallContext CachedCommandExecuteXmlReaderAsyncContext;
        internal SqlDataReader.Snapshot CachedDataReaderSnapshot;
        internal SqlDataReader.IsDBNullAsyncCallContext CachedDataReaderIsDBNullContext;
        internal SqlDataReader.ReadAsyncCallContext CachedDataReaderReadAsyncContext;

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

        // SQLBU 415870
        //  Get the internal transaction that should be hooked to a new outer transaction
        //  during a BeginTransaction API call.  In some cases (i.e. connection is going to
        //  be reset), CurrentTransaction should not be hooked up this way.
        /// <summary>
        /// TODO: need to understand this property better
        /// </summary>
        virtual internal SqlInternalTransaction AvailableInternalTransaction => CurrentTransaction;

        /// <summary>
        /// A reference to the SqlConnection that owns this internal connection.
        /// </summary>
        internal SqlConnection Connection => (SqlConnection)Owner;

        /// <summary>
        /// The connection options to be used for this connection.
        /// </summary>
        internal SqlConnectionString ConnectionOptions { get; init; }

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
        /// The Transaction currently associated with this connection.
        /// </summary>
        abstract internal SqlInternalTransaction CurrentTransaction { get; }

        /// <summary>
        /// The delegated (or promoted) transaction this connection is responsible for.
        /// </summary>
        internal SqlDelegatedTransaction DelegatedTransaction { get; set; }

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
        /// Whether the server version is SQL Server 2008 or newer.
        /// </summary>
        abstract internal bool Is2008OrNewer { get; }

        /// <summary>
        /// Whether this connection is to an Azure SQL Database.
        /// </summary>
        internal bool IsAzureSqlConnection { get; set; }

        /// <summary>
        /// Indicates whether the connection is currently enlisted in a transaction.
        /// </summary>
        internal bool IsEnlistedInTransaction { get; set; }

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
        /// Whether this connection is locked for bulk copy operations.
        /// </summary>
        abstract internal bool IsLockedForBulkCopy { get; }

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
        /// TODO: need to understand this property better
        /// </summary>
        abstract internal SqlInternalTransaction PendingTransaction { get; }

        /// <summary>
        /// A token returned by the server when we promote transaction.
        /// </summary>
        internal byte[] PromotedDtcToken { get; set; }

        #endregion

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

        /// <inheritdoc/>
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

        static protected byte[] GetTransactionCookie(Transaction transaction, byte[] whereAbouts)
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
    }
}
