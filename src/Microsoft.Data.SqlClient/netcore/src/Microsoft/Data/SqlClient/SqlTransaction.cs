// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/SqlTransaction/*' />
    public sealed class SqlTransaction : DbTransaction
    {
        private static readonly SqlDiagnosticListener s_diagnosticListener = new SqlDiagnosticListener(SqlClientDiagnosticListenerExtensions.DiagnosticListenerName);
        private static int _objectTypeCount; // EventSource Counter
        internal readonly int _objectID = System.Threading.Interlocked.Increment(ref _objectTypeCount);
        internal readonly IsolationLevel _isolationLevel = IsolationLevel.ReadCommitted;

        private SqlInternalTransaction _internalTransaction;
        private SqlConnection _connection;

        private bool _isFromAPI;

        internal SqlTransaction(SqlInternalConnection internalConnection, SqlConnection con,
                                IsolationLevel iso, SqlInternalTransaction internalTransaction)
        {
            _isolationLevel = iso;
            _connection = con;

            if (internalTransaction == null)
            {
                _internalTransaction = new SqlInternalTransaction(internalConnection, TransactionType.LocalFromAPI, this);
            }
            else
            {
                Debug.Assert(internalConnection.CurrentTransaction == internalTransaction, "Unexpected Parser.CurrentTransaction state!");
                _internalTransaction = internalTransaction;
                _internalTransaction.InitParent(this);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // PROPERTIES
        ////////////////////////////////////////////////////////////////////////////////////////

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Connection/*' />
        new public SqlConnection Connection
        {
            get
            {
                if (IsZombied)
                {
                    return null;
                }
                else
                {
                    return _connection;
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/DbConnection/*' />
        override protected DbConnection DbConnection
        {
            get
            {
                return Connection;
            }
        }

        internal SqlInternalTransaction InternalTransaction
        {
            get
            {
                return _internalTransaction;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/IsolationLevel/*' />
        override public IsolationLevel IsolationLevel
        {
            get
            {
                ZombieCheck();
                return _isolationLevel;
            }
        }

        private bool IsYukonPartialZombie
        {
            get
            {
                return (null != _internalTransaction && _internalTransaction.IsCompleted);
            }
        }

        internal bool IsZombied
        {
            get
            {
                return (null == _internalTransaction || _internalTransaction.IsCompleted);
            }
        }

        internal int ObjectID
        {
            get
            {
                return _objectID;
            }
        }

        internal SqlStatistics Statistics
        {
            get
            {
                if (null != _connection)
                {
                    if (_connection.StatisticsEnabled)
                    {
                        return _connection.Statistics;
                    }
                }
                return null;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // PUBLIC METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Commit/*' />
        override public void Commit()
        {
            Exception e = null;
            Guid operationId = s_diagnosticListener.WriteTransactionCommitBefore(_isolationLevel, _connection, InternalTransaction);

            ZombieCheck();

            SqlStatistics statistics = null;
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlTransaction.Commit|API> {0}", ObjectID);
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlTransaction.Commit|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                _isFromAPI = true;

                _internalTransaction.Commit();
            }
            catch (SqlException ex)
            {
                // GitHub Issue #130 - When a timeout exception has occurred on transaction completion request, 
                // this connection may not be in reusable state.
                // We will abort this connection and make sure it does not go back to the pool.
                var innerException = ex.InnerException as Win32Exception;
                if (innerException != null && innerException.NativeErrorCode == TdsEnums.SNI_WAIT_TIMEOUT)
                {
                    _connection.Abort(ex);
                }
                e = ex;
                throw;
            }
            catch (Exception ex)
            {
                e = ex;
                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
                if (e != null)
                {
                    s_diagnosticListener.WriteTransactionCommitError(operationId, _isolationLevel, _connection, InternalTransaction, e);
                }
                else
                {
                    s_diagnosticListener.WriteTransactionCommitAfter(operationId, _isolationLevel, _connection, InternalTransaction);
                }

                _isFromAPI = false;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/DisposeDisposing/*' />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!IsZombied && !IsYukonPartialZombie)
                {
                    _internalTransaction.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Rollback2/*' />
        override public void Rollback()
        {
            Exception e = null;
            Guid operationId = s_diagnosticListener.WriteTransactionRollbackBefore(_isolationLevel, _connection, InternalTransaction);

            if (IsYukonPartialZombie)
            {
                // Put something in the trace in case a customer has an issue
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlTransaction.Rollback|ADV> {0} partial zombie no rollback required", ObjectID);
                _internalTransaction = null; // yukon zombification
            }
            else
            {
                ZombieCheck();

                SqlStatistics statistics = null;
                long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlTransaction.Rollback|API> {0}", ObjectID);
                SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlTransaction.Rollback|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);
                try
                {
                    statistics = SqlStatistics.StartTimer(Statistics);

                    _isFromAPI = true;

                    _internalTransaction.Rollback();
                }
                catch (Exception ex)
                {
                    e = ex;
                    throw;
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                    SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
                    if (e != null)
                    {
                        s_diagnosticListener.WriteTransactionRollbackError(operationId, _isolationLevel, _connection, InternalTransaction, e);
                    }
                    else
                    {
                        s_diagnosticListener.WriteTransactionRollbackAfter(operationId, _isolationLevel, _connection, InternalTransaction);
                    }
                    _isFromAPI = false;
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/RollbackTransactionName/*' />
        public void Rollback(string transactionName)
        {
            Exception e = null;
            Guid operationId = s_diagnosticListener.WriteTransactionRollbackBefore(_isolationLevel, _connection, InternalTransaction, transactionName);

            ZombieCheck();
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlTransaction.Rollback|API> {0} transactionName='{1}'", ObjectID, transactionName);
            SqlStatistics statistics = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                _isFromAPI = true;

                _internalTransaction.Rollback(transactionName);
            }
            catch (Exception ex)
            {
                e = ex;
                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
                if (e != null)
                {
                    s_diagnosticListener.WriteTransactionRollbackError(operationId, _isolationLevel, _connection, InternalTransaction, e, transactionName);
                }
                else
                {
                    s_diagnosticListener.WriteTransactionRollbackAfter(operationId, _isolationLevel, _connection, InternalTransaction, transactionName);
                }

                _isFromAPI = false;

            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Save/*' />
        public void Save(string savePointName)
        {
            ZombieCheck();

            SqlStatistics statistics = null;
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlTransaction.Save|API> {0} savePointName='{1}'", ObjectID, savePointName);
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                _internalTransaction.Save(savePointName);
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // INTERNAL METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        internal void Zombie()
        {
            // For Yukon, we have to defer "zombification" until
            //                 we get past the users' next rollback, else we'll
            //                 throw an exception there that is a breaking change.
            //                 Of course, if the connection is already closed, 
            //                 then we're free to zombify...
            SqlInternalConnection internalConnection = (_connection.InnerConnection as SqlInternalConnection);
            if (null != internalConnection && !_isFromAPI)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlTransaction.Zombie|ADV> {0} yukon deferred zombie", ObjectID);
            }
            else
            {
                _internalTransaction = null; // pre-yukon zombification
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // PRIVATE METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        private void ZombieCheck()
        {
            // If this transaction has been completed, throw exception since it is unusable.
            if (IsZombied)
            {
                if (IsYukonPartialZombie)
                {
                    _internalTransaction = null; // yukon zombification
                }

                throw ADP.TransactionZombied(this);
            }
        }
    }
}
