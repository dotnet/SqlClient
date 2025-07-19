// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Diagnostics;
#if NETFRAMEWORK
using System.Runtime.CompilerServices;
#endif

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/SqlTransaction/*' />
    public sealed class SqlTransaction : DbTransaction
    {
        private static readonly SqlDiagnosticListener s_diagnosticListener = new();

        private static int s_objectTypeCount; // EventSource Counter

        private readonly SqlConnection _connection;
        private readonly IsolationLevel _isolationLevel;
        private bool _isFromApi;

        internal SqlTransaction(
            SqlInternalConnection internalConnection,
            SqlConnection con,
            IsolationLevel iso,
            SqlInternalTransaction internalTransaction)
        {
            #if NETFRAMEWORK
            SqlConnection.VerifyExecutePermission();
            #endif
            _isolationLevel = iso;
            _connection = con;

            if (internalTransaction == null)
            {
                InternalTransaction = new SqlInternalTransaction(internalConnection, TransactionType.LocalFromAPI, this);
            }
            else
            {
                Debug.Assert(internalConnection.CurrentTransaction == internalTransaction, "Unexpected Parser.CurrentTransaction state!");
                InternalTransaction = internalTransaction;
                InternalTransaction.InitParent(this);
            }
        }

        #region Properties

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Connection/*' />
        public new SqlConnection Connection => IsZombied ? null : _connection;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/IsolationLevel/*' />
        public override IsolationLevel IsolationLevel
        {
            get
            {
                ZombieCheck();
                return _isolationLevel;
            }
        }

        internal SqlInternalTransaction InternalTransaction { get; private set; }

        internal bool IsZombied => InternalTransaction == null || InternalTransaction.IsCompleted;

        internal int ObjectId { get; } = Interlocked.Increment(ref s_objectTypeCount);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/DbConnection/*' />
        protected override DbConnection DbConnection => Connection;

        private bool Is2005PartialZombie => InternalTransaction?.IsCompleted == true;

        private SqlStatistics Statistics => _connection?.StatisticsEnabled == true ? _connection.Statistics : null;

        #endregion

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Commit/*' />
        public override void Commit()
        {
            using DiagnosticTransactionScope diagnosticScope = s_diagnosticListener.CreateTransactionCommitScope(
                _isolationLevel,
                _connection,
                InternalTransaction);

            ZombieCheck();

            using (TryEventScope.Create("SqlTransaction.Commit | API | Object Id {0}", ObjectId))
            {
                SqlStatistics statistics = null;

                SqlClientEventSource.Log.TryCorrelationTraceEvent(
                    "SqlTransaction.Commit | API | Correlation | Object Id {0}, Activity Id {1}, Client Connection Id {2}",
                    ObjectId,
                    ActivityCorrelator.Current,
                    Connection?.ClientConnectionId);

                #if NETFRAMEWORK
                TdsParser bestEffortCleanupTarget = null;
                RuntimeHelpers.PrepareConstrainedRegions();
                #endif
                try
                {
                    #if NETFRAMEWORK
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                    #endif

                    statistics = SqlStatistics.StartTimer(Statistics);

                    _isFromApi = true;

                    InternalTransaction.Commit();
                }
                #if NETFRAMEWORK
                catch (OutOfMemoryException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (StackOverflowException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (ThreadAbortException e)
                {
                    _connection.Abort(e);
                    SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                    throw;
                }
                #endif
                catch (SqlException ex)
                {
                    diagnosticScope.SetException(ex);

                    // GitHub Issue #130 - When a timeout exception has occurred on transaction completion request,
                    // this connection may not be in reusable state.
                    // We will abort this connection and make sure it does not go back to the pool.
                    if (ex.InnerException is Win32Exception innerException && innerException.NativeErrorCode == TdsEnums.SNI_WAIT_TIMEOUT)
                    {
                        _connection.Abort(ex);
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    diagnosticScope.SetException(ex);
                    throw;
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                    _isFromApi = false;
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/DisposeDisposing/*' />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                #if NETFRAMEWORK
                TdsParser bestEffortCleanupTarget = null;
                RuntimeHelpers.PrepareConstrainedRegions();
                #endif
                try
                {
                    #if NETFRAMEWORK
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                    #endif

                    if (!IsZombied && !Is2005PartialZombie)
                    {
                        InternalTransaction.Dispose();
                    }
                }
                catch (OutOfMemoryException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (StackOverflowException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (ThreadAbortException e)
                {
                    _connection.Abort(e);

                    #if NETFRAMEWORK
                    SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                    #endif

                    throw;
                }
            }

            base.Dispose(disposing);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Rollback2/*' />
        public override void Rollback()
        {
            using DiagnosticTransactionScope diagnosticScope = s_diagnosticListener.CreateTransactionRollbackScope(
                _isolationLevel,
                _connection,
                InternalTransaction,
                transactionName: null);

            if (Is2005PartialZombie)
            {
                // Put something in the trace in case a customer has an issue
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    "SqlTransaction.Rollback | ADV | Object Id {0}, partial zombie no rollback required",
                    ObjectId);

                InternalTransaction = null; // 2005 zombification
            }
            else
            {
                ZombieCheck();

                SqlStatistics statistics = null;
                using (TryEventScope.Create("SqlTransaction.Rollback | API | Object Id {0}", ObjectId))
                {
                    SqlClientEventSource.Log.TryCorrelationTraceEvent(
                        "SqlTransaction.Rollback | API | Correlation | Object Id {0}, ActivityID {1}, Client Connection Id {2}",
                        ObjectId,
                        ActivityCorrelator.Current,
                        Connection?.ClientConnectionId);

                    #if NETFRAMEWORK
                    TdsParser bestEffortCleanupTarget = null;
                    RuntimeHelpers.PrepareConstrainedRegions();
                    #endif
                    try
                    {
                        #if NETFRAMEWORK
                        bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                        #endif

                        statistics = SqlStatistics.StartTimer(Statistics);

                        _isFromApi = true;
                        InternalTransaction.Rollback();
                    }
                    #if NETFRAMEWORK
                    catch (OutOfMemoryException e)
                    {
                        _connection.Abort(e);
                        throw;
                    }
                    catch (StackOverflowException e)
                    {
                        _connection.Abort(e);
                        throw;
                    }
                    catch (ThreadAbortException e)
                    {
                        _connection.Abort(e);
                        SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                        throw;
                    }
                    #endif
                    catch (Exception ex)
                    {
                        diagnosticScope.SetException(ex);
                        throw;
                    }
                    finally
                    {
                        SqlStatistics.StopTimer(statistics);
                        _isFromApi = false;
                    }
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/RollbackTransactionName/*' />
        #if NET
        public override void Rollback(string transactionName)
        #else
        public void Rollback(string transactionName)
        #endif
        {
            using DiagnosticTransactionScope diagnosticScope = s_diagnosticListener.CreateTransactionRollbackScope(
                _isolationLevel,
                _connection,
                InternalTransaction,
                transactionName);

            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand(); // MDAC 81476
            #endif

            ZombieCheck();

            var eventScopeEnter = TryEventScope.Create(SqlClientEventSource.Log.TryScopeEnterEvent(
                "SqlTransaction.Rollback | API | Object Id {0}, Transaction Name='{1}', ActivityID {2}, Client Connection Id {3}",
                ObjectId,
                transactionName,
                ActivityCorrelator.Current,
                Connection?.ClientConnectionId));
            using (eventScopeEnter)
            {
                SqlStatistics statistics = null;

                #if NETFRAMEWORK
                TdsParser bestEffortCleanupTarget = null;
                RuntimeHelpers.PrepareConstrainedRegions();
                #endif
                try
                {
                    #if NETFRAMEWORK
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                    #endif

                    statistics = SqlStatistics.StartTimer(Statistics);

                    _isFromApi = true;
                    InternalTransaction.Rollback(transactionName);
                }
                #if NETFRAMEWORK
                catch (OutOfMemoryException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (StackOverflowException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (ThreadAbortException e)
                {
                    _connection.Abort(e);
                    SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                    throw;
                }
                #endif
                catch (Exception ex)
                {
                    diagnosticScope.SetException(ex);
                    throw;
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                    _isFromApi = false;
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Save/*' />
        #if NET
        public override void Save(string savePointName)
        #else
        public void Save(string savePointName)
        #endif
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand(); // MDAC 81476
            #endif

            ZombieCheck();

            SqlStatistics statistics = null;
            using (TryEventScope.Create("SqlTransaction.Save | API | Object Id {0} | Save Point Name '{1}'", ObjectId, savePointName))
            {
                #if NETFRAMEWORK
                TdsParser bestEffortCleanupTarget = null;
                RuntimeHelpers.PrepareConstrainedRegions();
                #endif
                try
                {
                    #if NETFRAMEWORK
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                    #endif

                    statistics = SqlStatistics.StartTimer(Statistics);

                    InternalTransaction.Save(savePointName);
                }
                #if NETFRAMEWORK
                catch (OutOfMemoryException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (StackOverflowException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (ThreadAbortException e)
                {
                    _connection.Abort(e);

                    #if NETFRAMEWORK
                    SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                    #endif

                    throw;
                }
                #endif
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                }
            }
        }

        internal void Zombie()
        {
            // For Yukon, we have to defer "zombification" until we get past the users' next
            // rollback, else we'll throw an exception there that is a breaking change. Of course,
            // if the connection is already closed, then we're free to zombify...
            if (_connection.InnerConnection is SqlInternalConnection internalConnection && !_isFromApi)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    "SqlTransaction.Zombie | ADV | Object Id {0} yukon deferred zombie",
                    ObjectId);
            }
            else
            {
                // pre SQL 2005 zombification
                InternalTransaction = null;
            }
        }

        private void ZombieCheck()
        {
            // If this transaction has been completed, throw exception since it is unusable.
            if (IsZombied)
            {
                if (Is2005PartialZombie)
                {
                    // SQL 2005 zombification
                    InternalTransaction = null;
                }

                throw ADP.TransactionZombied(this);
            }
        }
    }
}
