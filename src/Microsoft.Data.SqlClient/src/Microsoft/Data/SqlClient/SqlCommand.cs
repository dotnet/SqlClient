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
using Microsoft.Data.SqlClient.Utilities;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/SqlCommand/*'/>
    [DefaultEvent("RecordsAffected")]
    [DesignerCategory("")]
    [ToolboxItem(true)]
    public sealed partial class SqlCommand : DbCommand, ICloneable
    {
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
        
        #region Properties
        
        internal bool InPrepare => _inPrepare;
        
        // @TODO: Rename to match conventions.
        internal int ObjectID { get; } = Interlocked.Increment(ref _objectTypeCount);

        private bool IsDirty
        {
            get
            {
                // @TODO: Factor out closeCount/reconnectCount checks to properties and clean up.
                // To wit: closeCount checks whether the connection has been closed after preparation,
                //    reconnectCount, the same only with reconnections.
                
                // only dirty if prepared
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

        #endregion
        
        #region Public Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Prepare/*'/>
        public override void Prepare()
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif

            using var _ = TryEventScope.Create("SqlCommand.Prepare | API | Object Id {0}", ObjectID);
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.Prepare | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"ActivityID {ActivityCorrelator.Current}, " +
                $"Client Connection Id {Connection?.ClientConnectionId}");

            // Reset _pendingCancel upon entry into any Execute - used to synchronize state
            // between entry into Execute* API and the thread obtaining the stateObject.
            _pendingCancel = false;
            
            SqlStatistics statistics = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                
                // Only prepare batch has parameters
                // @TODO: Factor out stored proc/text+parameter count to property
                // @TODO: Not using parentheses is confusing here b/c it relies on order of operations knowledge
                if (IsPrepared && !IsDirty || CommandType == CommandType.StoredProcedure
                                           || (CommandType == CommandType.Text && GetParameterCount(_parameters) == 0))
                {
                    Statistics?.SafeIncrement(ref Statistics._prepares);
                    _hiddenPrepare = false;
                }
                else
                {
                    // Validate the command outside the try\catch to avoid putting the _stateObj on error
                    ValidateCommand(isAsync: false);

                    bool processFinallyBlock = true;
                    try
                    {
                        #if NET
                        PrepareInternal();
                        #else
                        ConstrainedExecutionHelper.RunWithConnectionAbortAndCleanup(PrepareInternal, _activeConnection);
                        #endif
                    }
                    catch (Exception e)
                    {
                        // NOTE: This will catch any CER exceptions that were rethrown in netfx.
                        //    they will always be uncatchable, so this is equivalent to setting
                        //    processFinallyBlock to false inside the CER handlers.
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

        private void PrepareInternal()
        {
            // NOTE: The state object isn't actually needed for this, but it is still here for
            // back-compat (since it does a bunch of checks)
            GetStateObject();

            // Loop through parameters ensuring that we do not have unspecified types, sizes,
            // scales, or precisions
            if (_parameters is not null)
            {
                int count = _parameters.Count;
                for (int i = 0; i < count; ++i)
                {
                    _parameters[i].Prepare(this);
                }
            }
            
            if (IsDirty)
            {
                Debug.Assert(_cachedMetaData is null || !_dirty, "dirty query should not have cached metadata!");

                // Someone changed the command text or the parameter schema so we must unprepare the command
                this.Unprepare();
                IsDirty = false;
            }
            
            Debug.Assert(_execType is not EXECTYPE.PREPARED, "Invalid attempt to Prepare already Prepared command!");
            Debug.Assert(_activeConnection is not null, "must have an open connection to Prepare");
            Debug.Assert(_stateObj is not null, "TdsParserStateObject should not be null");
            Debug.Assert(_stateObj.Parser is not null, "TdsParser class should not be null in Command.Execute!");
            Debug.Assert(_stateObj.Parser == _activeConnection.Parser, "stateobject parser not same as connection parser");
            Debug.Assert(!_inPrepare, "Already in Prepare cycle, this.inPrepare should be false!");

            // Remember that the user wants to do a prepare but don't actually do an rpc
            _execType = EXECTYPE.PREPAREPENDING;
            
            // Note the current close count of the connection - this will tell us if the connection has been
            // closed between calls to Prepare and Execute
            _preparedConnectionCloseCount = _activeConnection.CloseCount;
            _preparedConnectionReconnectCount = _activeConnection.ReconnectCount;

            Statistics?.SafeIncrement(ref Statistics._prepares);
        }
        
        #endregion
    }
}
