// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.Data.Common;

#if NET
using Microsoft.Data.SqlClient.Diagnostics;
#else
using System.Threading;
#endif

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/SqlTransaction/*' />
    public sealed partial class SqlTransaction : DbTransaction
    {
        #if NET
        private static readonly SqlDiagnosticListener s_diagnosticListener = new(SqlDiagnosticListener.DiagnosticListenerName);
        #endif

        ////////////////////////////////////////////////////////////////////////////////////////
        // PUBLIC METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Commit/*' />
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/DisposeDisposing/*' />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TdsParser bestEffortCleanupTarget = null;

                #if NETFRAMEWORK
                RuntimeHelpers.PrepareConstrainedRegions();
                #endif
                try
                {
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                    if (!IsZombied && !Is2005PartialZombie)
                    {
                        _internalTransaction.Dispose();
                    }
                }
                catch (System.OutOfMemoryException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (System.StackOverflowException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (System.Threading.ThreadAbortException e)
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Rollback2/*' />
        public override void Rollback()
        {
            //netcore using (DiagnosticTransactionScope diagnosticScope = s_diagnosticListener.CreateTransactionRollbackScope(_isolationLevel, _connection, InternalTransaction, null))
            //netcore {
            if (Is2005PartialZombie)
            {
                // Put something in the trace in case a customer has an issue
                //netcore SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlTransaction.Rollback | ADV | Object Id {0}, partial zombie no rollback required", ObjectID);
                //netfx   SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlTransaction.Rollback|ADV> {0} partial zombie no rollback required", ObjectID);
                _internalTransaction = null; // 2005 zombification
            }
            else
            {
                ZombieCheck();

                SqlStatistics statistics = null;
                //netcore using (TryEventScope.Create("SqlTransaction.Rollback | API | Object Id {0}", ObjectID))
                //netfx   using (TryEventScope.Create("<sc.SqlTransaction.Rollback|API> {0}", ObjectID))
                {
                    //netcore SqlClientEventSource.Log.TryCorrelationTraceEvent("SqlTransaction.Rollback | API | Correlation | Object Id {0}, ActivityID {1}, Client Connection Id {2}", ObjectID, ActivityCorrelator.Current, Connection?.ClientConnectionId);
                    //netfx   SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlTransaction.Rollback|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);

                    TdsParser bestEffortCleanupTarget = null;

                    #if NETFRAMEWORK
                    RuntimeHelpers.PrepareConstrainedRegions();
                    #endif
                    try
                    {
                        bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                        statistics = SqlStatistics.StartTimer(Statistics);

                        _isFromAPI = true;

                        _internalTransaction.Rollback();
                    }
                    catch (System.OutOfMemoryException e)
                    {
                        //netcore diagnosticScope.SetException(e);
                        _connection.Abort(e);
                        throw;
                    }
                    catch (System.StackOverflowException e)
                    {
                        //netcore diagnosticScope.SetException(e);
                        _connection.Abort(e);
                        throw;
                    }
                    catch (System.Threading.ThreadAbortException e)
                    {
                        //netcore diagnosticScope.SetException(e);
                        _connection.Abort(e);

                        #if NETFRAMEWORK
                        SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                        #endif
                        throw;
                    }
                    //netcore---
                    catch (Exception ex)
                    {
                        diagnosticScope.SetException(ex);
                        throw;
                    }
                    //---netcore
                    finally
                    {
                        SqlStatistics.StopTimer(statistics);
                        _isFromAPI = false;
                    }
                }
            }
            //netcore }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/RollbackTransactionName/*' />
       public override void Rollback(string transactionName)
        {
            //netcore using (DiagnosticTransactionScope diagnosticScope = s_diagnosticListener.CreateTransactionRollbackScope(_isolationLevel, _connection, InternalTransaction, transactionName))
            //netcore {

            //netfx SqlConnection.ExecutePermission.Demand(); // MDAC 81476

            ZombieCheck();

            //netcore using (TryEventScope.Create(SqlClientEventSource.Log.TryScopeEnterEvent("SqlTransaction.Rollback | API | Object Id {0}, Transaction Name='{1}', ActivityID {2}, Client Connection Id {3}", ObjectID, transactionName, ActivityCorrelator.Current, Connection?.ClientConnectionId)))
            //netfx   using (TryEventScope.Create("<sc.SqlTransaction.Rollback|API> {0} transactionName='{1}'", ObjectID, transactionName))
            {
                SqlStatistics statistics = null;
                TdsParser bestEffortCleanupTarget = null;

                #if NETFRAMEWORK
                RuntimeHelpers.PrepareConstrainedRegions();
                #endif
                try
                {
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                    statistics = SqlStatistics.StartTimer(Statistics);

                    _isFromAPI = true;

                    _internalTransaction.Rollback(transactionName);
                }
                catch (System.OutOfMemoryException e)
                {
                    //netcore diagnosticScope.SetException(e);
                    _connection.Abort(e);
                    throw;
                }
                catch (System.StackOverflowException e)
                {
                    //netcore diagnosticScope.SetException(e);
                    _connection.Abort(e);
                    throw;
                }
                catch (System.Threading.ThreadAbortException e)
                {
                    //netcore diagnosticScope.SetException(e);
                    _connection.Abort(e);

                    #if NETFRAMEWORK
                    SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                    #endif
                    throw;
                }
                //netcore---
                catch (Exception ex)
                {
                    diagnosticScope.SetException(ex);
                    throw;
                }
                //---netcore
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                    _isFromAPI = false;
                }
            }
            //netcore }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Save/*' />
        public override void Save(string savePointName)
        {
            //netfx SqlConnection.ExecutePermission.Demand(); // MDAC 81476

            ZombieCheck();

            SqlStatistics statistics = null;
            //netcore using (TryEventScope.Create("SqlTransaction.Save | API | Object Id {0} | Save Point Name '{1}'", ObjectID, savePointName))
            //netfx   using ("<sc.SqlTransaction.Save|API> {0} savePointName='{1}'", ObjectID, savePointName))
            {
                TdsParser bestEffortCleanupTarget = null;

                #if NETFRAMEWORK
                RuntimeHelpers.PrepareConstrainedRegions();
                #endif
                try
                {
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(_connection);
                    statistics = SqlStatistics.StartTimer(Statistics);

                    _internalTransaction.Save(savePointName);
                }
                catch (System.OutOfMemoryException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (System.StackOverflowException e)
                {
                    _connection.Abort(e);
                    throw;
                }
                catch (System.Threading.ThreadAbortException e)
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
