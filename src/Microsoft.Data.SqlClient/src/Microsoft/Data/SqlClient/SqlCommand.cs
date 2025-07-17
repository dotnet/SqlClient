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

#if NET
using Microsoft.Data.SqlClient.Diagnostics;
#endif

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/SqlCommand/*'/>
    [DefaultEvent("RecordsAffected")]
    [DesignerCategory("")]
    [ToolboxItem(true)]
    public sealed partial class SqlCommand : DbCommand, ICloneable
    {
        #region Constants

        /// <summary>
        /// Pre-boxed invalid prepare handle - used to optimize boxing behavior.
        /// </summary>
        private static readonly object s_cachedInvalidPrepareHandle = (object)-1;

        #endregion

        #region Fields

        /// <summary>
        /// Number of instances of SqlCommand that have been created. Used to generate ObjectId
        /// </summary>
        private static int _objectTypeCount = 0;

        /// <summary>
        /// Connection that will be used to process the current instance.
        /// </summary>
        private SqlConnection _activeConnection;

        /// <summary>
        /// Text to execute when executing the command.
        /// </summary>
        private string _commandText;

        /// <summary>
        /// Maximum amount of time, in seconds, the command will execute before timing out.
        /// </summary>
        private int? _commandTimeout;
        
        /// <summary>
        /// Type of the command to execute.
        /// </summary>
        private CommandType _commandType;
        
        /// <summary>
        /// Current state of preparation of the command.
        /// By default, assume the user is not sharing a connection so the command has not been prepared.
        /// </summary>
        private EXECTYPE _execType = EXECTYPE.UNPREPARED;

        /// <summary>
        /// True if the user changes the command text or number of parameters after the command has
        /// already prepared.
        /// </summary>
        // @TODO: Consider renaming "_IsUserDirty"
        private bool _dirty = false;
        
        /// <summary>
        /// On 8.0 and above the Prepared state cannot be left. Once a command is prepared it will
        /// always be prepared. A change in parameters, command text, etc (IsDirty) automatically
        /// causes a hidden prepare.
        /// </summary>
        private bool _hiddenPrepare = false;
        
        /// <summary>
        /// _inPrepare will be set immediately before the actual prepare is done. The OnReturnValue
        /// function will test this flag to determine whether the returned value is a
        /// _prepareHandle or something else.
        /// </summary>
        // @TODO: Make auto-property
        private bool _inPrepare = false;

        /// <summary>
        /// Parameters that have been added to the current instance.
        /// </summary>
        private SqlParameterCollection _parameters;

        /// <summary>
        /// Volatile bool used to synchronize with cancel thread the state change of an executing
        /// command going from pre-processing to obtaining a stateObject. The cancel
        /// synchronization we require in the command is only from entering an Execute* API to
        /// obtaining a stateObj. Once a stateObj is successfully obtained, cancel synchronization
        /// is handled by the stateObject.
        /// </summary>
        private volatile bool _pendingCancel;

        /// <summary>
        /// Number of times the connection was closed when the command was prepared. Used to
        /// determine if the connection has closed between prepare and execute.
        /// </summary>
        private int _preparedConnectionCloseCount = -1;

        /// <summary>
        /// Number of times the connection was reconnected when the command was prepared. Used to
        /// determine if the connection has reconnected between prepare and execute.
        /// </summary>
        private int _preparedConnectionReconnectCount = -1;

        /// <summary>
        /// the handle of a prepared command. Apparently there can be multiple prepared commands at
        /// a time - a feature that we do not support yet. This is an int which is used in the
        /// object typed SqlParameter.Value field, avoid repeated boxing by storing in a box.
        /// </summary>
        private object _prepareHandle = s_cachedInvalidPrepareHandle;

        /// <summary>
        /// Retry logic provider to use for execution of the current instance.
        /// </summary>
        private SqlRetryLogicBaseProvider _retryLogicProvider;

        /// <summary>
        /// TDS session the current instance is using.
        /// </summary>
        private TdsParserStateObject _stateObj;
        
        #endregion

        #region Enums
        
        // @TODO: Rename to match naming conventions
        private enum EXECTYPE
        {
            /// <summary>
            /// Execute unprepared commands, all server versions (results in sp_execsql call)
            /// </summary>
            UNPREPARED, 
            
            /// <summary>
            /// Prepare and execute command, 8.0 and above only  (results in sp_prepexec call)
            /// </summary>
            PREPAREPENDING,
            
            /// <summary>
            /// execute prepared commands, all server versions   (results in sp_exec call)
            /// </summary>
            PREPARED,           
        }
        
        #endregion
        
        #region Public Properties

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CommandTimeout/*'/>
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Data)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_CommandTimeout)]
        public override int CommandTimeout
        {
            get => _commandTimeout ?? DefaultCommandTimeout;
            set
            {
                if (value < 0)
                {
                    throw ADP.InvalidCommandTimeout(value);
                }

                if (value != _commandTimeout)
                {
                    PropertyChanging();
                    _commandTimeout = value;
                }
                
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.Set_CommandTimeout | API | " +
                    $"Object Id {ObjectID}, " +
                    $"Command Timeout value {value}, " +
                    $"Client Connection Id {Connection?.ClientConnectionId}");
            }
        }
        
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CommandText/*'/>
        [DefaultValue("")]
        [RefreshProperties(RefreshProperties.All)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Data)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_CommandText)]
        public override string CommandText
        {
            get => _commandText ?? string.Empty;
            set
            {
                if (_commandText != value)
                {
                    PropertyChanging();
                    _commandText = value;
                }

                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.Set_CommandText | API | " +
                    $"Object Id {ObjectID}, " +
                    $"String Value = '{value}', " +
                    $"Client Connection Id {Connection?.ClientConnectionId}");
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Connection/*'/>
        [DefaultValue(null)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Data)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_Connection)]
        public new SqlConnection Connection
        {
            get => _activeConnection;
            set
            {
                // Don't allow the connection to be changed while in an async operation
                // @TODO: Factor out
                if (_activeConnection != value && _activeConnection != null)
                {
                    // If new value...
                    if (CachedAsyncState != null && CachedAsyncState.PendingAsyncOperation)
                    {
                        // If in pending async state, throw.
                        throw SQL.CannotModifyPropertyAsyncOperationInProgress();
                    }
                }

                // Check to see if the currently set transaction has completed. If so, null out
                // our local reference.
                if (_transaction?.Connection is null)
                {
                    _transaction = null;
                }

                if (IsPrepared)
                {
                    if (_activeConnection != value && _activeConnection != null)
                    {
                        try
                        {
                            Unprepare();
                        }
                        // @TODO: CER Exception Handling was removed here (see GH#3581)
                        catch (Exception)
                        {
                            // We do not really care about errors in unprepare (maybe the old
                            // connection went bad?)
                        }
                        finally
                        {
                            // Clean prepare status (even successful unprepare does not do that)
                            // @TODO: ... but it does?
                            _prepareHandle = s_cachedInvalidPrepareHandle;
                            _execType = EXECTYPE.UNPREPARED;
                        }
                    }
                }

                _activeConnection = value;

                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.Set_Connection | API | " +
                    $"Object Id {ObjectID}, " +
                    $"Client Connection Id {value?.ClientConnectionId}");
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/RetryLogicProvider/*' />
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SqlRetryLogicBaseProvider RetryLogicProvider
        {
            get
            {
                _retryLogicProvider ??= SqlConfigurableRetryLogicManager.CommandProvider;
                return _retryLogicProvider;
            }
            set => _retryLogicProvider = value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Transaction/*'/>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_Transaction)]
        public new SqlTransaction Transaction
        {
            get
            {
                // If the transaction object has been zombied, just return null
                if (_transaction is not null && _transaction.Connection is null)
                {
                    _transaction = null;
                }

                return _transaction;
            }
            set
            {
                // Don't allow the transaction to be changed while in an async operation
                if (_transaction != value && _activeConnection is not null)
                {
                    // If new value...
                    if (CachedAsyncState != null && CachedAsyncState.PendingAsyncOperation)
                    {
                        // If in pending async state, throw.
                        throw SQL.CannotModifyPropertyAsyncOperationInProgress();
                    }
                }

                _transaction = value;

                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.Set_Transaction | API | " +
                    $"Object Id {ObjectID}, " +
                    $"Internal Transaction Id {value?.InternalTransaction?.TransactionId}, " +
                    $"Client Connection Id {Connection?.ClientConnectionId}");
            }
        }

        #endregion

        #region Internal/Protected/Private Properties
        
        internal bool InPrepare => _inPrepare;
        
        // @TODO: Rename to match conventions.
        internal int ObjectID { get; } = Interlocked.Increment(ref _objectTypeCount);

        internal SqlStatistics Statistics
        {
            get
            {
                if (_activeConnection is not null)
                {
                    #if NET
                    bool isStatisticsEnabled = _activeConnection.StatisticsEnabled ||
                                               s_diagnosticListener.IsEnabled(SqlClientCommandAfter.Name);
                    #else
                    bool isStatisticsEnabled = _activeConnection.StatisticsEnabled;
                    #endif

                    if (isStatisticsEnabled)
                    {
                        return _activeConnection.Statistics;
                    }
                }

                return null;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DbConnection/*'/>
        protected override DbConnection DbConnection
        {
            get => Connection;
            // @TODO: Does set need a trace event like DbTransaction?
            set => Connection = (SqlConnection)value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DbTransaction/*'/>
        protected override DbTransaction DbTransaction
        {
            get => Transaction;
            set
            {
                Transaction = (SqlTransaction)value;
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.Set_DbTransaction | API | " +
                    $"Object Id {ObjectID}, " +
                    $"Client Connection Id {Connection?.ClientConnectionId}");
            }
        }

        private bool IsDirty
        {
            get
            {
                // @TODO: Factor out closeCount/reconnectCount checks to properties and clean up.
                // To wit: closeCount checks whether the connection has been closed after preparation,
                //    reconnectCount, the same only with reconnections.
                
                // only dirty if prepared
                // @TODO: we probably do not need to store this as a temp variable.
                var activeConnection = _activeConnection;
                return IsPrepared &&
                       (_dirty ||
                        (_parameters != null && _parameters.IsDirty) ||
                        (activeConnection != null && (activeConnection.CloseCount != _preparedConnectionCloseCount || activeConnection.ReconnectCount != _preparedConnectionReconnectCount)));
            }
            set
            {
                // @TODO: Consider reworking to do this in a helper method, since setting, sets to the
                // _dirty, but that's not the only consideration when determining dirtiness.
                
                // only mark the command as dirty if it is already prepared
                // but always clear the value if we are clearing the dirty flag
                _dirty = value ? IsPrepared : false;
                if (_parameters != null)
                {
                    _parameters.IsDirty = _dirty;
                }

                _cachedMetaData = null;
            }
        }

        private bool IsPrepared => _execType is not EXECTYPE.UNPREPARED;
        
        // @TODO: IsPrepared is part of IsDirty - this is confusing.
        private bool IsUserPrepared => IsPrepared && !_hiddenPrepare && !IsDirty;

        private bool IsStoredProcedure => CommandType is CommandType.StoredProcedure;

        private bool IsSimpleTextQuery => CommandType is CommandType.Text &&
                                          (_parameters is null || _parameters.Count == 0);

        #endregion

        #region Public/Internal Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Prepare/*'/>
        public override void Prepare()
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif

            using var eventScope = TryEventScope.Create($"SqlCommand.Prepare | API | Object Id {ObjectID}");
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.Prepare | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"ActivityID {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}");

            // Reset _pendingCancel upon entry into any Execute - used to synchronize state
            // between entry into Execute* API and the thread obtaining the stateObject.
            _pendingCancel = false;

            SqlStatistics statistics = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                // Only prepare batch that has parameters
                // @TODO: IsPrepared is part of IsDirty - this is confusing.
                if ((IsPrepared && !IsDirty) || IsStoredProcedure || IsSimpleTextQuery)
                {
                    // @TODO: Make a simpler SafeIncrementPrepares
                    Statistics?.SafeIncrement(ref Statistics._prepares);
                    _hiddenPrepare = false;
                }
                else
                {
                    // @TODO: Makethis whole else block "Prepare Internal"

                    // Validate the command outside the try\catch to avoid putting the _stateObj on error
                    ValidateCommand(isAsync: false);

                    bool processFinallyBlock = true;
                    try
                    {
                        // NOTE: The state object isn't actually needed for this, but it is still here for back-compat (since it does a bunch of checks)
                        GetStateObject();

                        // Loop through parameters ensuring that we do not have unspecified types, sizes, scales, or precisions
                        if (_parameters != null)
                        {
                            int count = _parameters.Count;
                            for (int i = 0; i < count; ++i)
                            {
                                _parameters[i].Prepare(this);
                            }
                        }

                        InternalPrepare();
                    }
                    // @TODO: CER Exception Handling was removed here (see GH#3581)
                    catch (Exception e)
                    {
                        processFinallyBlock = ADP.IsCatchableExceptionType(e);
                        throw;
                    }
                    finally
                    {
                        if (processFinallyBlock)
                        {
                            // The command is now officially prepared
                            _hiddenPrepare = false;
                            ReliablePutStateObject();
                        }
                    }
                }
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        #endregion

        #region Private Methods

        // @TODO: Rename to PrepareInternal
        private void InternalPrepare()
        {
            if (IsDirty)
            {
                Debug.Assert(_cachedMetaData is null || !_dirty, "dirty query should not have cached metadata!");

                // Someone changed the command text or the parameter schema so we must unprepare the command
                Unprepare();
                IsDirty = false;
            }

            Debug.Assert(_execType is not EXECTYPE.PREPARED, "Invalid attempt to Prepare already Prepared command!");
            Debug.Assert(_activeConnection is not null, "must have an open connection to Prepare");
            Debug.Assert(_stateObj is not null, "TdsParserStateObject should not be null");
            Debug.Assert(_stateObj.Parser is not null, "TdsParser class should not be null in Command.Execute!");
            Debug.Assert(_stateObj.Parser == _activeConnection.Parser, "stateobject parser not same as connection parser");
            Debug.Assert(!_inPrepare, "Already in Prepare cycle, this.inPrepare should be false!");

            // Remember that the user wants to prepare but don't actually do an RPC
            _execType = EXECTYPE.PREPAREPENDING;

            // Note the current close count of the connection - this will tell us if the
            // connection has been closed between calls to Prepare() and Execute
            _preparedConnectionCloseCount = _activeConnection.CloseCount;
            _preparedConnectionReconnectCount = _activeConnection.ReconnectCount;

            Statistics?.SafeIncrement(ref Statistics._prepares);
        }

        private void Unprepare()
        {
            Debug.Assert(IsPrepared, "Invalid attempt to Unprepare a non-prepared command!");
            Debug.Assert(_activeConnection is not null, "must have an open connection to UnPrepare");
            Debug.Assert(!_inPrepare, "_inPrepare should be false!");

            SqlClientEventSource.Log.TryTraceEvent(
                "SqlCommand.UnPrepare | Info | " +
                $"Object Id {ObjectID}, " +
                $"Current Prepared Handle {_prepareHandle}");

            _execType = EXECTYPE.PREPAREPENDING;

            // Don't zero out the handle because we'll pass it in to sp_prepexec on the next prepare
            // Unless the close count isn't the same as when we last prepared
            if (_activeConnection.CloseCount != _preparedConnectionCloseCount ||
                _activeConnection.ReconnectCount != _preparedConnectionReconnectCount)
            {
                // Reset our handle
                _prepareHandle = s_cachedInvalidPrepareHandle;
            }

            _cachedMetaData = null;

            SqlClientEventSource.Log.TryTraceEvent(
                $"SqlCommand.UnPrepare | Info | " +
                $"Object Id {ObjectID}, Command unprepared.");
        }

        #endregion
    }
}
