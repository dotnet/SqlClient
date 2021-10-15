// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data.Common;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/SqlTransaction/*' />
    public sealed partial class SqlTransaction : DbTransaction
    {
        private static readonly SqlDiagnosticListener s_diagnosticListener = new(SqlClientDiagnosticListenerExtensions.DiagnosticListenerName);

        ////////////////////////////////////////////////////////////////////////////////////////
        // PUBLIC METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Commit/*' />
        override public void Commit()
        {
            Exception e = null;
            Guid operationId = s_diagnosticListener.WriteTransactionCommitBefore(_isolationLevel, _connection, InternalTransaction);

            ZombieCheck();

            SqlStatistics statistics = null;
            using (TryEventScope.Create("SqlTransaction.Commit | API | Object Id {0}", ObjectID))
            {
                SqlClientEventSource.Log.TryCorrelationTraceEvent("SqlTransaction.Commit | API | Correlation | Object Id {0}, Activity Id {1}, Client Connection Id {2}", ObjectID, ActivityCorrelator.Current, Connection?.ClientConnectionId);
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
                    if (ex.InnerException is Win32Exception innerException && innerException.NativeErrorCode == TdsEnums.SNI_WAIT_TIMEOUT)
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
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/DisposeDisposing/*' />
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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Rollback2/*' />
        override public void Rollback()
        {
            Exception e = null;
            Guid operationId = s_diagnosticListener.WriteTransactionRollbackBefore(_isolationLevel, _connection, InternalTransaction);

            if (IsYukonPartialZombie)
            {
                // Put something in the trace in case a customer has an issue
                SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlTransaction.Rollback | ADV | Object Id {0}, partial zombie no rollback required", ObjectID);
                _internalTransaction = null; // yukon zombification
            }
            else
            {
                ZombieCheck();

                SqlStatistics statistics = null;
                using (TryEventScope.Create("SqlTransaction.Rollback | API | Object Id {0}", ObjectID))
                {
                    SqlClientEventSource.Log.TryCorrelationTraceEvent("SqlTransaction.Rollback | API | Correlation | Object Id {0}, ActivityID {1}, Client Connection Id {2}", ObjectID, ActivityCorrelator.Current, Connection?.ClientConnectionId);
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
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/RollbackTransactionName/*' />
        public void Rollback(string transactionName)
        {
            Exception e = null;
            Guid operationId = s_diagnosticListener.WriteTransactionRollbackBefore(_isolationLevel, _connection, InternalTransaction, transactionName);

            ZombieCheck();
            using (TryEventScope.Create(SqlClientEventSource.Log.TryScopeEnterEvent("SqlTransaction.Rollback | API | Object Id {0}, Transaction Name='{1}', ActivityID {2}, Client Connection Id {3}", ObjectID, transactionName, ActivityCorrelator.Current, Connection?.ClientConnectionId)))
            {
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
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Save/*' />
        public void Save(string savePointName)
        {
            ZombieCheck();

            SqlStatistics statistics = null;
            using (TryEventScope.Create("SqlTransaction.Save | API | Object Id {0} | Save Point Name '{1}'", ObjectID, savePointName))
            {
                try
                {
                    statistics = SqlStatistics.StartTimer(Statistics);

                    _internalTransaction.Save(savePointName);
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                }
            }
        }
    }
}
