// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data.Common;
using System.Threading;
using Microsoft.Data.Common;

#if NET
using Microsoft.Data.SqlClient.Diagnostics;
#else
using System.Runtime.CompilerServices;
#endif

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/SqlTransaction/*' />
    public sealed partial class SqlTransaction : DbTransaction
    {
        #if NET
        private static readonly SqlDiagnosticListener s_diagnosticListener = new(SqlDiagnosticListener.DiagnosticListenerName);
        #endif

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Commit/*' />
        public override void Commit()
        {
            #if NET
            using DiagnosticTransactionScope diagnosticScope = s_diagnosticListener.CreateTransactionCommitScope(
                _isolationLevel,
                _connection,
                InternalTransaction);
            #endif

            ZombieCheck();

            using (TryEventScope.Create("SqlTransaction.Commit | API | Object Id {0}", ObjectID))
            {
                SqlStatistics statistics = null;

                SqlClientEventSource.Log.TryCorrelationTraceEvent(
                    "SqlTransaction.Commit | API | Correlation | Object Id {0}, Activity Id {1}, Client Connection Id {2}",
                    ObjectID,
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

                    _isFromAPI = true;

                    _internalTransaction.Commit();
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
                    #if NET
                    diagnosticScope.SetException(ex);
                    #endif

                    // GitHub Issue #130 - When a timeout exception has occurred on transaction completion request,
                    // this connection may not be in reusable state.
                    // We will abort this connection and make sure it does not go back to the pool.
                    if (ex.InnerException is Win32Exception innerException && innerException.NativeErrorCode == TdsEnums.SNI_WAIT_TIMEOUT)
                    {
                        _connection.Abort(ex);
                    }
                    throw;
                }
                #if NET
                catch (Exception ex)
                {
                    diagnosticScope.SetException(ex);
                    throw;
                }
                #endif
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                    _isFromAPI = false;
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
                        _internalTransaction.Dispose();
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
            #if NET
            using DiagnosticTransactionScope diagnosticScope = s_diagnosticListener.CreateTransactionRollbackScope(
                _isolationLevel,
                _connection,
                InternalTransaction,
                transactionName: null);
            #endif

            if (Is2005PartialZombie)
            {
                // Put something in the trace in case a customer has an issue
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    "SqlTransaction.Rollback | ADV | Object Id {0}, partial zombie no rollback required",
                    ObjectID);

                _internalTransaction = null; // 2005 zombification
            }
            else
            {
                ZombieCheck();

                SqlStatistics statistics = null;
                using (TryEventScope.Create("SqlTransaction.Rollback | API | Object Id {0}", ObjectID))
                {
                    SqlClientEventSource.Log.TryCorrelationTraceEvent(
                        "SqlTransaction.Rollback | API | Correlation | Object Id {0}, ActivityID {1}, Client Connection Id {2}",
                        ObjectID,
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

                        _isFromAPI = true;
                        _internalTransaction.Rollback();
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
                    #else
                    catch (Exception ex)
                    {
                        diagnosticScope.SetException(ex);
                        throw;
                    }
                    #endif
                    finally
                    {
                        SqlStatistics.StopTimer(statistics);
                        _isFromAPI = false;
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
            #if NET
            using DiagnosticTransactionScope diagnosticScope = s_diagnosticListener.CreateTransactionRollbackScope(
                _isolationLevel,
                _connection,
                InternalTransaction,
                transactionName);
            #endif

            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand(); // MDAC 81476
            #endif

            ZombieCheck();

            var eventScopeEnter = TryEventScope.Create(SqlClientEventSource.Log.TryScopeEnterEvent(
                "SqlTransaction.Rollback | API | Object Id {0}, Transaction Name='{1}', ActivityID {2}, Client Connection Id {3}",
                ObjectID,
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

                    _isFromAPI = true;
                    _internalTransaction.Rollback(transactionName);
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
                #else
                catch (Exception ex)
                {
                    diagnosticScope.SetException(ex);
                    throw;
                }
                #endif
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                    _isFromAPI = false;
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
            using (TryEventScope.Create("SqlTransaction.Save | API | Object Id {0} | Save Point Name '{1}'", ObjectID, savePointName))
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

                    _internalTransaction.Save(savePointName);
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
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                }
            }
        }
    }
}
