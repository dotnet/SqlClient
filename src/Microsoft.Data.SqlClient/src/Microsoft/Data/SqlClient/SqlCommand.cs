// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.Sql;
using Microsoft.Data.SqlClient.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/SqlCommand/*'/>
    /// <content>
    /// This partial contains the general-purpose public API methods and shared components of the
    /// SqlCommand class: constants, member variables, properties, and helper methods that are used
    /// across the other partials.
    /// The class is split into multiple partials to separate related functionality such that the
    /// files are small enough to be digestible by engineers.
    /// </content>
    [DefaultEvent("RecordsAffected")]
    [DesignerCategory("")]
    [ToolboxItem(true)]
    public sealed partial class SqlCommand : DbCommand, ICloneable
    {
        #region Constants

        // @TODO: Rename to match naming conventions
        private const int MaxRPCNameLength = 1046;

        // @TODO: Make property (externally visible fields are bad)
        internal static readonly Action<object> s_cancelIgnoreFailure = CancelIgnoreFailureCallback;

        /// <summary>
        /// Pre-boxed invalid prepare handle - used to optimize boxing behavior.
        /// </summary>
        private static readonly object s_cachedInvalidPrepareHandle = (object)-1;

        /// <summary>
        /// 2005- column ordinals (this array indexed by ProcParamsColIndex
        /// </summary>
        // @TODO: There's gotta be a better way to define these than with 3x static structures
        // @TODO: Rename to ProcParamNamesPreSql2008
        private static readonly string[] PreSql2008ProcParamsNames = new string[]
        {
            "PARAMETER_NAME",           // ParameterName,
            "PARAMETER_TYPE",           // ParameterType,
            "DATA_TYPE",                // DataType
            null,                       // ManagedDataType, introduced in 2008
            "CHARACTER_MAXIMUM_LENGTH", // CharacterMaximumLength,
            "NUMERIC_PRECISION",        // NumericPrecision,
            "NUMERIC_SCALE",            // NumericScale,
            "UDT_CATALOG",              // TypeCatalogName,
            "UDT_SCHEMA",               // TypeSchemaName,
            "TYPE_NAME",                // TypeName,
            "XML_CATALOGNAME",          // XmlSchemaCollectionCatalogName,
            "XML_SCHEMANAME",           // XmlSchemaCollectionSchemaName,
            "XML_SCHEMACOLLECTIONNAME", // XmlSchemaCollectionName
            "UDT_NAME",                 // UdtTypeName
            null,                       // Scale for datetime types with scale, introduced in 2008
        };

        /// <summary>
        /// 2008+ column ordinals (this array indexed by ProcParamsColIndex.
        /// </summary>
        // @TODO: There's gotta be a better way to define these than with 3x static structures
        // @TODO: Rename to ProcParamNamesPostSql2008
        internal static readonly string[] Sql2008ProcParamsNames = new[] {
            "PARAMETER_NAME",           // ParameterName,
            "PARAMETER_TYPE",           // ParameterType,
            null,                       // DataType, removed from 2008+
            "MANAGED_DATA_TYPE",        // ManagedDataType,
            "CHARACTER_MAXIMUM_LENGTH", // CharacterMaximumLength,
            "NUMERIC_PRECISION",        // NumericPrecision,
            "NUMERIC_SCALE",            // NumericScale,
            "TYPE_CATALOG_NAME",        // TypeCatalogName,
            "TYPE_SCHEMA_NAME",         // TypeSchemaName,
            "TYPE_NAME",                // TypeName,
            "XML_CATALOGNAME",          // XmlSchemaCollectionCatalogName,
            "XML_SCHEMANAME",           // XmlSchemaCollectionSchemaName,
            "XML_SCHEMACOLLECTIONNAME", // XmlSchemaCollectionName
            null,                       // UdtTypeName, removed from 2008+
            "SS_DATETIME_PRECISION",    // Scale for datetime types with scale
        };

        #endregion

        #region Fields
        #region Test-Only Behavior Overrides
        #if DEBUG
        /// <summary>
        /// Force the client to sleep during sp_describe_parameter_encryption in the function TryFetchInputParameterEncryptionInfo.
        /// </summary>
        private static bool _sleepDuringTryFetchInputParameterEncryptionInfo = false;

        /// <summary>
        /// Force the client to sleep during sp_describe_parameter_encryption in the function RunExecuteReaderTds.
        /// </summary>
        private static bool _sleepDuringRunExecuteReaderTdsForSpDescribeParameterEncryption = false;

        /// <summary>
        /// Force the client to sleep during sp_describe_parameter_encryption after ReadDescribeEncryptionParameterResults.
        /// </summary>
        private static bool _sleepAfterReadDescribeEncryptionParameterResults = false;

        /// <summary>
        /// Internal flag for testing purposes that forces all queries to internally end async calls.
        /// </summary>
        private static bool _forceInternalEndQuery = false;

        /// <summary>
        /// Internal flag for testing purposes that forces one RetryableEnclaveQueryExecutionException during GenerateEnclavePackage
        /// </summary>
        private static bool _forceRetryableEnclaveQueryExecutionExceptionDuringGenerateEnclavePackage = false;
        #endif
        #endregion

        // @TODO: Make property - non-private fields are bad
        // @TODO: Rename to match naming convention _enclavePackage
        internal EnclavePackage enclavePackage = null;

        // @TODO: Make property - non-private fields are bad (this should be read-only externally)
        internal ConcurrentDictionary<int, SqlTceCipherInfoEntry> keysToBeSentToEnclave;

        // @TODO: Make property - non-private fields are bad (this can be read-only externally)
        internal bool requiresEnclaveComputations = false;

        // @TODO: Make property - non-private fields are bad
        internal SqlDependency _sqlDep;

        // @TODO: Rename _batchRpcMode to follow pattern
        private bool _batchRPCMode;

        /// <summary>
        /// Cached information for asynchronous execution.
        /// </summary>
        private AsyncState _cachedAsyncState = null;

        private int _currentlyExecutingBatch;

        /// <summary>
        /// Number of instances of SqlCommand that have been created. Used to generate ObjectId
        /// </summary>
        private static int _objectTypeCount = 0;

        /// <summary>
        /// Static instance of the <see cref="SqlDiagnosticListener"/> used for capturing and emitting
        /// diagnostic events related to SqlCommand operations.
        /// </summary>
        private static readonly SqlDiagnosticListener s_diagnosticListener = new();

        /// <summary>
        /// Connection that will be used to process the current instance.
        /// </summary>
        private SqlConnection _activeConnection;

        /// <summary>
        /// Cut down on object creation and cache all the cached metadata
        /// </summary>
        private _SqlMetaDataSet _cachedMetaData;

        /// <summary>
        /// Column Encryption Override. Defaults to SqlConnectionSetting, in which case it will be
        /// Enabled if SqlConnectionOptions.IsColumnEncryptionSettingEnabled = true, Disabled if
        /// false. This may also be used to set other behavior which overrides connection level
        /// setting.
        /// </summary>
        // @TODO: Make auto-property, also make nullable.
        private SqlCommandColumnEncryptionSetting _columnEncryptionSetting =
            SqlCommandColumnEncryptionSetting.UseConnectionSetting;
        
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
        /// This variable is used to keep track of which RPC batch's results are being read when reading the results of
        /// describe parameter encryption RPC requests in BatchRPCMode.
        /// </summary>
        // @TODO: Rename to match naming conventions
        private int _currentlyExecutingDescribeParameterEncryptionRPC;

        /// <summary>
        /// Per-command custom providers. It can be provided by the user and can be set more than
        /// once.
        /// </summary>
        private IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> _customColumnEncryptionKeyStoreProviders;

        // @TODO: Rename to indicate that this is for enclave stuff, I think...
        private byte[] customData = null;

        // @TODO: Rename to indicate that this is for enclave stuff. Or just get rid of it and use the length of customData if possible.
        private int customDataLength = 0;

        /// <summary>
        /// By default, the cmd object is visible on the design surface (i.e. VS7 Server Tray) to
        /// limit the number of components that clutter the design surface, when the DataAdapter
        /// design wizard generates the insert/update/delete commands it will set the
        /// DesignTimeVisible property to false so that cmds won't appear as individual objects
        /// </summary>
        // @TODO: Make auto-property
        private bool _designTimeInvisible;

        /// <summary>
        /// True if the user changes the command text or number of parameters after the command has
        /// already prepared.
        /// </summary>
        // @TODO: Consider renaming "_IsUserDirty"
        private bool _dirty = false;

        /// <summary>
        /// Current state of preparation of the command.
        /// By default, assume the user is not sharing a connection so the command has not been prepared.
        /// </summary>
        private EXECTYPE _execType = EXECTYPE.UNPREPARED;

        // @TODO: Rename to match naming conventions _enclaveAttestationParameters
        private SqlEnclaveAttestationParameters enclaveAttestationParameters = null;

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
        /// A flag to indicate if EndExecute was already initiated by the Begin call.
        /// </summary>
        private volatile bool _internalEndExecuteInitiated;

        private SqlNotificationRequest _notification;
        
        #if NETFRAMEWORK
        // @TODO: Make auto-property
        private bool _notificationAutoEnlist = true;
        #endif
        
        /// <summary>
        /// Parameters that have been added to the current instance.
        /// </summary>
        private SqlParameterCollection _parameters;

        /// <summary>
        /// Prevents the completion events for ExecuteReader from being fired if ExecuteReader is being
        /// called as part of a parent operation (e.g. ExecuteScalar, or SqlBatch.ExecuteScalar.)
        /// </summary>
        private bool _parentOperationStarted = false;

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
        /// Last TaskCompletionSource for reconnect task - use for cancellation only.
        /// </summary>
        // @TODO: Ideally we should have this as a Task.
        private TaskCompletionSource<object> _reconnectionCompletionSource = null;

        /// <summary>
        /// Retry logic provider to use for execution of the current instance.
        /// </summary>
        private SqlRetryLogicBaseProvider _retryLogicProvider;

        /// <summary>
        /// Sql reader will pull this value out for each NextResult call. It is not cumulative.
        /// _rowsAffected is cumulative for ExecuteNonQuery across all rpc batches
        /// </summary>
        // @TODO: Use int? and replace -1 usage with null
        private int _rowsAffected = -1;

        /// <summary>
        /// number of rows affected by sp_describe_parameter_encryption.
        /// </summary>
        // @TODO: Use int? and replace -1 usage with null
        // @TODO: This is only used for debug asserts?
        // @TODO: Rename to drop Sp
        private int _rowsAffectedBySpDescribeParameterEncryption = -1;

        /// <summary>
        /// Used for RPC executes.
        /// </summary>
        // @TODO: This is very unclear why it needs to be an array
        private _SqlRPC[] _rpcArrayOf1 = null;

        /// <summary>
        /// RPC for tracking execution of sp_describe_parameter_encryption.
        /// </summary>
        private _SqlRPC _rpcForEncryption = null;

        // @TODO: Rename to match naming conventions
        // @TODO: This could probably be an array
        private List<_SqlRPC> _RPCList;

        // @TODO: Rename to match naming convention
        private _SqlRPC[] _sqlRPCParameterEncryptionReqArray;

        /// <summary>
        /// TDS session the current instance is using.
        /// </summary>
        private TdsParserStateObject _stateObj;

        /// <summary>
        /// Event to call when a statement completes.
        /// </summary>
        // @TODO: Make auto-event?
        private StatementCompletedEventHandler _statementCompletedEventHandler;

        /// <summary>
        /// Current transaction the command is participating in.
        /// </summary>
        private SqlTransaction _transaction;
        
        /// <summary>
        /// How command results are applied to a DataRow when used by the update method of
        /// DbDataAdapter.
        /// </summary>
        private UpdateRowSource _updatedRowSource = UpdateRowSource.Both;

        /// <summary>
        /// Indicates if the column encryption setting was set at-least once in the batch rpc mode,
        /// when using AddBatchCommand.
        /// </summary>
        // @TODO: can be replaced by using nullable for _columnEncryptionSetting.
        private bool _wasBatchModeColumnEncryptionSettingSetOnce;

        #endregion

        #region Constructors

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="default"]/*'/>
        public SqlCommand()
        {
            GC.SuppressFinalize(this);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextString"]/*'/>
        public SqlCommand(string cmdText)
            : this()
        {
            CommandText = cmdText;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextStringAndSqlConnection"]/*'/>
        public SqlCommand(string cmdText, SqlConnection connection)
            : this()
        {
            CommandText = cmdText;
            Connection = connection;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextStringAndSqlConnectionAndSqlTransaction"]/*'/>
        public SqlCommand(string cmdText, SqlConnection connection, SqlTransaction transaction)
            : this()
        {
            CommandText = cmdText;
            Connection = connection;
            Transaction = transaction;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextStringAndSqlConnectionAndSqlTransactionAndSqlCommandColumnEncryptionSetting"]/*'/>
        public SqlCommand(
            string cmdText,
            SqlConnection connection,
            SqlTransaction transaction,
            SqlCommandColumnEncryptionSetting columnEncryptionSetting)
            : this()
        {
            CommandText = cmdText;
            Connection = connection;
            Transaction = transaction;
            _columnEncryptionSetting = columnEncryptionSetting;
        }

        private SqlCommand(SqlCommand from)
        {
            CommandText = from.CommandText;
            CommandTimeout = from.CommandTimeout;
            CommandType = from.CommandType;
            Connection = from.Connection;
            DesignTimeVisible = from.DesignTimeVisible;
            Transaction = from.Transaction;
            UpdatedRowSource = from.UpdatedRowSource;
            _columnEncryptionSetting = from.ColumnEncryptionSetting;

            SqlParameterCollection parameters = Parameters;
            foreach (object parameter in from.Parameters)
            {
                object parameterToAdd = parameter is ICloneable cloneableParameter
                    ? cloneableParameter.Clone()
                    : parameter;
                parameters.Add(parameterToAdd);
            }
        }

        #endregion

        #region Events
        
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/StatementCompleted/*'/>
        [ResCategory(StringsHelper.ResourceNames.DataCategory_StatementCompleted)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_StatementCompleted)]
        public event StatementCompletedEventHandler StatementCompleted
        {
            add
            {
                _statementCompletedEventHandler += value;
            }
            remove
            {
                _statementCompletedEventHandler -= value;
            }
        }
        
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

        // Index into indirection arrays for columns of interest to DeriveParameters
        private enum ProcParamsColIndex
        {
            ParameterName = 0,
            ParameterType,
            DataType,        // Obsolete in 2008, use ManagedDataType instead
            ManagedDataType, // New in 2008
            CharacterMaximumLength,
            NumericPrecision,
            NumericScale,
            TypeCatalogName,
            TypeSchemaName,
            TypeName,
            XmlSchemaCollectionCatalogName,
            XmlSchemaCollectionSchemaName,
            XmlSchemaCollectionName,
            UdtTypeName,  // Obsolete in 2008.  Holds the actual typename if UDT, since TypeName didn't back then.
            DateTimeScale // New in 2008
        }

        #endregion

        #region Public Properties

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ColumnEncryptionSetting/*'/>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Data)]
        [ResDescription(StringsHelper.ResourceNames.TCE_SqlCommand_ColumnEncryptionSetting)]
        public SqlCommandColumnEncryptionSetting ColumnEncryptionSetting => _columnEncryptionSetting;
        
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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CommandType/*'/>
        [DefaultValue(CommandType.Text)]
        [RefreshProperties(RefreshProperties.All)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Data)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_CommandType)]
        public override CommandType CommandType
        {
            get => _commandType != 0 ? _commandType : CommandType.Text;
            set
            {
                if (_commandType != value)
                {
                    switch (value)
                    {
                        case CommandType.Text:
                        case CommandType.StoredProcedure:
                            PropertyChanging();
                            _commandType = value;
                            break;
                        case CommandType.TableDirect:
                            throw SQL.NotSupportedCommandType(value);
                        default:
                            throw ADP.InvalidCommandType(value);
                    }

                    // @TODO: Either move this outside the if block or move all the other instances inside the if block.
                    SqlClientEventSource.Log.TryTraceEvent(
                        "SqlCommand.Set_CommandType | API | " +
                        $"Object Id {ObjectID}, " +
                        $"Command Type value {(int)value}, " +
                        $"Client Connection Id {Connection?.ClientConnectionId}");
                }
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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DesignTimeVisible/*'/>
        [DefaultValue(true)]
        [DesignOnly(true)]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool DesignTimeVisible
        {
            get => !_designTimeInvisible;
            set
            {
                _designTimeInvisible = !value;

                #if NETFRAMEWORK
                TypeDescriptor.Refresh(this);
                #endif
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EnableOptimizedParameterBinding/*'/>
        public bool EnableOptimizedParameterBinding { get; set; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Notification/*'/>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Notification)]
        [ResDescription(StringsHelper.ResourceNames.SqlCommand_Notification)]
        public SqlNotificationRequest Notification
        {
            get => _notification;
            set
            {
                _sqlDep = null;
                _notification = value;
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.Set_Notification | API | " +
                    $"Object Id {ObjectID}");
            }
        }
        
        #if NETFRAMEWORK
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/NotificationAutoEnlist/*'/>
        [DefaultValue(true)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Notification)]
        [ResDescription(StringsHelper.ResourceNames.SqlCommand_NotificationAutoEnlist)]
        public bool NotificationAutoEnlist
        {
            get => _notificationAutoEnlist;
            set => _notificationAutoEnlist = value;
        }
        #endif
        
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Parameters/*'/>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Data)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_Parameters)]
        public new SqlParameterCollection Parameters
        {
            get
            {
                // Delay the creation of the SqlParameterCollection until user actually uses the
                // Parameters property.
                _parameters ??= new SqlParameterCollection();
                return _parameters;
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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/UpdatedRowSource/*'/>
        [DefaultValue(UpdateRowSource.Both)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Update)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_UpdatedRowSource)]
        public override UpdateRowSource UpdatedRowSource
        {
            get => _updatedRowSource;
            set
            {
                switch (value)
                {
                    case UpdateRowSource.None:
                    case UpdateRowSource.OutputParameters:
                    case UpdateRowSource.FirstReturnedRecord:
                    case UpdateRowSource.Both:
                        _updatedRowSource = value;
                        break;
                    default:
                        throw ADP.InvalidUpdateRowSource(value);
                }
                
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.UpdatedRowSource | API | " +
                    $"Object Id {ObjectID}, " +
                    $"Updated Row Source value {(int)value}, " +
                    $"Client Connection Id {Connection?.ClientConnectionId}");
            }
        }

        #endregion

        #region Internal/Protected/Private Properties

        #if DEBUG
        // @TODO: 1) This is never set, 2) This is only used in TdsParserStateObject
        internal static int DebugForceAsyncWriteDelay { get; set; }
        #endif

        /// <summary>
        /// A flag to indicate whether we postponed caching the query metadata for this command.
        /// </summary>
        internal bool CachingQueryMetadataPostponed { get; set; }

        internal bool HasColumnEncryptionKeyStoreProvidersRegistered
        {
            get => _customColumnEncryptionKeyStoreProviders?.Count > 0;
        }

        internal bool InPrepare => _inPrepare;

        // @TODO: Rename RowsAffectedInternal or
        // @TODO: Make `int?`
        internal int InternalRecordsAffected
        {
            get => _rowsAffected;
            set
            {
                if (_rowsAffected == -1)
                {
                    _rowsAffected = value;
                }
                else if (value > 0)
                {
                    _rowsAffected += value;
                }
            }
        }

        /// <summary>
        /// A flag to indicate if we have in-progress describe parameter encryption RPC requests.
        /// Reset to false when completed.
        /// </summary>
        // @TODO: Rename to match naming conventions
        // @TODO: Can be made private?
        internal bool IsDescribeParameterEncryptionRPCCurrentlyInProgress { get; private set; }

        // @TODO: Autoproperty
        // @TODO: MetaData or Metadata?
        internal _SqlMetaDataSet MetaData => _cachedMetaData;

        // @TODO: Rename to match conventions.
        internal int ObjectID { get; } = Interlocked.Increment(ref _objectTypeCount);

        /// <summary>
        /// Get or add to the number of records affected by SpDescribeParameterEncryption.
        /// The below line is used only for debug asserts and not exposed publicly or impacts functionality otherwise.
        /// </summary>
        internal int RowsAffectedByDescribeParameterEncryption
        {
            get => _rowsAffectedBySpDescribeParameterEncryption;
            set
            {
                if (_rowsAffectedBySpDescribeParameterEncryption == -1)
                {
                    _rowsAffectedBySpDescribeParameterEncryption = value;
                }
                else if (value > 0)
                {
                    _rowsAffectedBySpDescribeParameterEncryption += value;
                }
            }
        }

        internal SqlStatistics Statistics
        {
            get
            {
                if (_activeConnection is not null)
                {
                    bool isStatisticsEnabled = _activeConnection.StatisticsEnabled ||
                                               s_diagnosticListener.IsEnabled(SqlClientCommandAfter.Name);

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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DbParameterCollection/*'/>
        protected override DbParameterCollection DbParameterCollection
        {
            get => Parameters;
        }
        
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DbTransaction/*'/>
        protected override DbTransaction DbTransaction
        {
            get => Transaction;
            set
            {
                // @TODO: Does this need a trace event, we have one in Transaction?
                Transaction = (SqlTransaction)value;
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.Set_DbTransaction | API | " +
                    $"Object Id {ObjectID}, " +
                    $"Client Connection Id {Connection?.ClientConnectionId}");
            }
        }

        // @TODO: This is never null, so we can remove the null checks from usages of it.
        // @TODO: Since we never want it to be null, we could make this a lazy and get rid of the property.
        private AsyncState CachedAsyncState
        {
            get
            {
                _cachedAsyncState ??= new AsyncState();
                return _cachedAsyncState;
            }
        }

        private int DefaultCommandTimeout
        {
            // @TODO: Should use connection? Should DefaultCommandTimeout be defined *in the command object*?
            get => _activeConnection?.CommandTimeout ?? ADP.DefaultCommandTimeout;
        }

        // @TODO: Should be used in more than one place to justify its existence
        private SqlInternalConnectionTds InternalTdsConnection
        {
            // @TODO: Should check for null? Should use Connection?
            get => (SqlInternalConnectionTds)_activeConnection.InnerConnection;
        }

        private bool IsColumnEncryptionEnabled
        {
            get
            {
                bool isEncryptionEnabled =
                    _columnEncryptionSetting is SqlCommandColumnEncryptionSetting.Enabled ||
                    (_columnEncryptionSetting is SqlCommandColumnEncryptionSetting.UseConnectionSetting &&
                     _activeConnection.IsColumnEncryptionSettingEnabled);

                // Order matters here b/c 1) _activeConnection.Parser can throw if the underlying
                // connection is closed, and 2) we do not want to throw in that situation unless
                // the user is using transparent parameter encryption (breaks old behavior).
                return isEncryptionEnabled &&
                       _activeConnection.Parser is not null &&
                       _activeConnection.Parser.IsColumnEncryptionSupported;
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

        private bool IsProviderRetriable => SqlConfigurableRetryFactory.IsRetriable(RetryLogicProvider);

        private bool IsStoredProcedure => CommandType is CommandType.StoredProcedure;

        private bool IsSimpleTextQuery => CommandType is CommandType.Text &&
                                          (_parameters is null || _parameters.Count == 0);

        // @TODO: IsPrepared is part of IsDirty - this is confusing.
        private bool IsUserPrepared => IsPrepared && !_hiddenPrepare && !IsDirty;

        private bool ShouldCacheEncryptionMetadata
        {
            // @TODO: Should we check for null on _activeConnection?
            get => !requiresEnclaveComputations || _activeConnection.Parser.AreEnclaveRetriesSupported;
        }

        private bool ShouldUseEnclaveBasedWorkflow
        {
            // @TODO: I'm pretty sure the or'd condition is used in several places. We could factor that out.
            get => (!string.IsNullOrWhiteSpace(_activeConnection.EnclaveAttestationUrl) ||
                    _activeConnection.AttestationProtocol is SqlConnectionAttestationProtocol.None) &&
                   IsColumnEncryptionEnabled;
        }

        #endregion

        #region Public/Internal Methods

        object ICloneable.Clone() =>
            Clone();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Cancel/*'/>
        public override void Cancel()
        {
            // Cancel is supposed to be multi-thread safe.
            // It doesn't make sense to verify the connection exists or that it is open during
            // cancel because immediately after checking the connection can be closed or removed
            // via another thread.

            using var eventScope = TryEventScope.Create($"SqlCommand.Cancel | API | Object Id {ObjectID}");
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.Cancel | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            SqlStatistics statistics = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                // If we are in reconnect phase simply cancel the waiting task
                var reconnectCompletionSource = _reconnectionCompletionSource;
                if (reconnectCompletionSource is not null && reconnectCompletionSource.TrySetCanceled())
                {
                    return;
                }

                // The pending data flag means that we are awaiting a response or are in the middle
                // of processing a response.
                // * If we have no pending data, then there is nothing to cancel.
                // * If we have pending data, but it is not a result of this command, then we don't
                //   cancel either.
                // Note that this model is implementable because we only allow one active command
                // at any one time. This code will have to change we allow multiple outstanding
                // batches.
                if (_activeConnection?.InnerConnection is not SqlInternalConnectionTds connection)
                {
                    // @TODO: Really this case only applies if the connection is null.
                    // Fail without locking
                    return;
                }

                // The lock here is to protect against the command.cancel / connection.close race
                // condition. The SqlInternalConnectionTds is set to OpenBusy during close, once
                // this happens the cast below will fail and the command will no longer be
                // cancelable. It might be desirable to be able to cancel the close operation, but
                // this is outside the scope of Whidbey RTM. See (SqlConnection::Close) for other lock.
                lock (connection)
                {
                    // Make sure the connection did not get changed getting the connection and
                    // taking the lock. If it has, the connection has been closed.
                    if (connection != _activeConnection.InnerConnection as SqlInternalConnectionTds)
                    {
                        return;
                    }

                    TdsParser parser = connection.Parser;
                    if (parser is null)
                    {
                        return;
                    }

                    if (!_pendingCancel)
                    {
                        // Do nothing if already pending.
                        // Before attempting actual cancel, set the _pendingCancel flag to false.
                        // This denotes to other thread before obtaining stateObject from the
                        // session pool that there is another thread wishing to cancel.
                        // The period in question is between entering the ExecuteAPI and obtaining
                        // a stateObject.
                        _pendingCancel = true;

                        TdsParserStateObject stateObj = _stateObj;
                        if (stateObj is not null)
                        {
                            stateObj.Cancel(this);
                        }
                        else
                        {
                            SqlDataReader reader = connection.FindLiveReader(this);
                            if (reader is not null)
                            {
                                reader.Cancel(this);
                            }
                        }
                    }
                }
                // @TODO: CER Exception Handling was removed here (see GH#3581)
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Clone/*'/>
        public SqlCommand Clone()
        {
            SqlCommand clone = new SqlCommand(this);
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlCommand.Clone | API | " +
                $"Object Id {ObjectID}, " +
                $"Clone Object Id {clone.ObjectID}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}");

            return clone;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CreateParameter/*'/>
        public new SqlParameter CreateParameter() =>
            new SqlParameter();

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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/RegisterColumnEncryptionKeyStoreProvidersOnCommand/*' />
        public void RegisterColumnEncryptionKeyStoreProvidersOnCommand(
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders)
        {
            ValidateCustomProviders(customProviders);

            // Create a temporary dictionary and then add items from the provided dictionary.
            // Dictionary constructor does shallow copying by simply copying the provider name and
            // provider name and provider reference pairs.
            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customColumnEncryptionKeyStoreProviders =
                new(customProviders, StringComparer.OrdinalIgnoreCase);
            _customColumnEncryptionKeyStoreProviders = customColumnEncryptionKeyStoreProviders;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ResetCommandTimeout/*'/>
        public void ResetCommandTimeout()
        {
            if (CommandTimeout != ADP.DefaultCommandTimeout)
            {
                PropertyChanging();
                _commandTimeout = DefaultCommandTimeout;
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// We're being notified that the underlying connection was closed.
        /// </summary>
        internal void OnConnectionClosed() =>
            _stateObj?.OnConnectionClosed();

        internal void OnDoneDescribeParameterEncryptionProc(TdsParserStateObject stateObj)
        {
            // @TODO: Is this not the same stateObj as the currently stored one?

            // Called on RPC batch complete
            if (_batchRPCMode)
            {
                OnDone(
                    stateObj,
                    index: _currentlyExecutingDescribeParameterEncryptionRPC,
                    rpcList: _sqlRPCParameterEncryptionReqArray,
                    _rowsAffected);
                _currentlyExecutingDescribeParameterEncryptionRPC++; // @TODO: Should be interlocked?
            }
        }

        internal void OnDoneProc(TdsParserStateObject stateObject)
        {
            // @TODO: Is this not the same stateObj as the currently stored one?

            // Called on RPC batch complete
            if (_batchRPCMode)
            {
                OnDone(stateObject, _currentlyExecutingBatch, _RPCList, _rowsAffected);
                _currentlyExecutingBatch++; // @TODO: Should be interlocked?

                Debug.Assert(_RPCList.Count >= _currentlyExecutingBatch, "OnDoneProc: Too many DONEPROC events");
            }
        }

        internal void OnReturnStatus(int status)
        {
            // Don't set the return status if this is the status for sp_describe_parameter_encryption
            if (_inPrepare || IsDescribeParameterEncryptionRPCCurrentlyInProgress)
            {
                return;
            }

            // @TODO: Replace with call to GetCurrentParameterCollection
            SqlParameterCollection parameters = _parameters;
            if (_batchRPCMode)
            {
                if (_RPCList.Count > _currentlyExecutingBatch)
                {
                    parameters = _RPCList[_currentlyExecutingBatch].userParams;
                }
                else
                {
                    Debug.Fail("OnReturnStatus: SqlCommand got too many DONEPROC events");
                }
            }

            // See if a return value is bound
            int count = GetParameterCount(parameters);
            for (int i = 0; i < count; i++)
            {
                SqlParameter parameter = parameters[i];

                // @TODO: Invert to reduce nesting :)
                if (parameter.Direction is ParameterDirection.ReturnValue)
                {
                    object value = parameter.Value;

                    // if the user bound a SqlInt32 (the only valid one for status) use it
                    // @TODO: Not sure if this can be converted to a ternary since that forces implicit conversion of status to SqlInt32
                    if (value is SqlInt32)
                    {
                        parameter.Value = new SqlInt32(status);
                    }
                    else
                    {
                        parameter.Value = status;
                    }

                    // If we are not in batch RPC mode, update the query cache with the encryption
                    // metadata. We can do this now if we have distinguished between ReturnValue
                    // and ReturnStatus.
                    // See comments in AddQueryMetadata() for more details.
                    if (!_batchRPCMode && CachingQueryMetadataPostponed && ShouldCacheEncryptionMetadata &&
                        _parameters?.Count > 0)
                    {
                        SqlQueryMetadataCache.GetInstance().AddQueryMetadata(
                            this,
                            ignoreQueriesWithReturnValueParams: false);
                    }
                }
            }
        }

        internal void OnReturnValue(SqlReturnValue returnValue, TdsParserStateObject stateObj)
        {
            // Move the return value to the corresponding output parameter.
            // Return parameters are sent in the order in which they were defined in the procedure.
            // If named, match the parameter name, otherwise fill in based on ordinal position. If
            // the parameter is not bound, then ignore the return value.

            // @TODO: Is stateObj supposed to be different than the currently stored state object?

            if (_inPrepare)
            {
                // Store the returned prepare handle if we are returning from sp_prepare
                if (!returnValue.value.IsNull)
                {
                    _prepareHandle = returnValue.value.Int32;
                }

                _inPrepare = false;
                return;
            }

            SqlParameterCollection parameters = GetCurrentParameterCollection();
            int count = GetParameterCount(parameters);

            // @TODO: Rename to "ReturnParam"
            SqlParameter thisParam = GetParameterForOutputValueExtraction(parameters, returnValue.parameter, count);
            if (thisParam is not null)
            {
                // @TODO: Invert to reduce nesting :)

                // If the parameter's direction is InputOutput, Output, or ReturnValue and it needs
                // to be transparently encrypted/decrypted, then simply decrypt, deserialize, and
                // set the value.
                if (returnValue.cipherMD is not null &&
                    thisParam.CipherMetadata is not null &&
                    (thisParam.Direction == ParameterDirection.Output ||
                     thisParam.Direction == ParameterDirection.InputOutput ||
                     thisParam.Direction == ParameterDirection.ReturnValue))
                {
                    // @TODO: make this a separate method
                    // Validate type of the return value is valid for encryption
                    if (returnValue.tdsType != TdsEnums.SQLBIGVARBINARY)
                    {
                        throw SQL.InvalidDataTypeForEncryptedParameter(
                            thisParam.GetPrefixedParameterName(),
                            returnValue.tdsType,
                            expectedDataType: TdsEnums.SQLBIGVARBINARY);
                    }

                    // Decrypt the cipher text
                    TdsParser parser = _activeConnection.Parser;
                    if (parser is null || parser.State is TdsParserState.Closed or TdsParserState.Broken)
                    {
                        throw ADP.ClosedConnectionError();
                    }

                    if (!returnValue.value.IsNull)
                    {
                        try
                        {
                            Debug.Assert(_activeConnection is not null, @"_activeConnection should not be null");

                            // Get the key information from the parameter and decrypt the value.
                            returnValue.cipherMD.EncryptionInfo = thisParam.CipherMetadata.EncryptionInfo;
                            byte[] unencryptedBytes = SqlSecurityUtility.DecryptWithKey(
                                returnValue.value.ByteArray,
                                returnValue.cipherMD,
                                _activeConnection,
                                this);

                            if (unencryptedBytes is not null)
                            {
                                // Denormalize the value and convert it to the parameter type.
                                SqlBuffer buffer = new SqlBuffer();
                                parser.DeserializeUnencryptedValue(
                                    buffer,
                                    unencryptedBytes,
                                    returnValue,
                                    stateObj,
                                    returnValue.NormalizationRuleVersion);
                                thisParam.SetSqlBuffer(buffer);
                            }
                        }
                        catch (Exception e)
                        {
                            throw SQL.ParamDecryptionFailed(
                                thisParam.GetPrefixedParameterName(),
                                serverName: null,
                                e);
                        }
                    }
                    else
                    {
                        // Create a new SqlBuffer and set it to null
                        // Note: We can't reuse the SqlBuffer in "returnValue" below since it's
                        // already been set (to varbinary) in previous call to
                        // TryProcessReturnValue().
                        // Note 2: We will be coming down this code path only if the Command
                        // Setting is set to use TCE. We pass the command setting as TCE enabled in
                        // the below call for this reason.
                        SqlBuffer buff = new SqlBuffer();
                        // @TODO: uhhh what? can't we just, idk, set it null in the buffer?
                        TdsParser.GetNullSqlValue(
                            buff,
                            returnValue,
                            SqlCommandColumnEncryptionSetting.Enabled,
                            parser.Connection);
                        thisParam.SetSqlBuffer(buff);
                    }
                }
                else
                {
                    // @TODO: This should be a separate method, too
                    // Copy over the data

                    // If the value user has supplied a SqlType class, then just copy over the
                    // SqlType, otherwise convert to the com type.
                    if (thisParam.SqlDbType is SqlDbType.Udt)
                    {
                        try
                        {
                            _activeConnection.CheckGetExtendedUDTInfo(returnValue, fThrow: true);

                            // Extract the byte array from the param value
                            object data = returnValue.value.IsNull
                                ? DBNull.Value
                                : returnValue.value.ByteArray;

                            // Call the connection to instantiate the UDT object
                            thisParam.Value = _activeConnection.GetUdtValue(data, returnValue, returnDBNull: false);
                        }
                        catch (Exception e) when (e is FileNotFoundException or FileLoadException)
                        {
                            // Assign Assembly.Load failure in case where assembly not on client.
                            // This allows execution to complete and failure on SqlParameter.Value.
                            thisParam.SetUdtLoadError(e);
                        }

                        return;
                    }
                    else
                    {
                        thisParam.SetSqlBuffer(returnValue.value);
                    }

                    // @TODO: This seems fishy to me, it seems like it should be part of the SqlReturnValue class
                    MetaType mt = MetaType.GetMetaTypeFromSqlDbType(returnValue.type, isMultiValued: false);

                    if (returnValue.type is SqlDbType.Decimal)
                    {
                        thisParam.ScaleInternal = returnValue.scale;
                        thisParam.PrecisionInternal = returnValue.precision;
                    }
                    else if (mt.IsVarTime)
                    {
                        thisParam.ScaleInternal = returnValue.scale;
                    }
                    else if (returnValue.type is SqlDbType.Xml)
                    {
                        if (thisParam.Value is SqlCachedBuffer cachedBuffer)
                        {
                            thisParam.Value = cachedBuffer.ToString();
                        }
                    }

                    if (returnValue.collation is not null)
                    {
                        Debug.Assert(mt.IsCharType, "Invalid collation structure for non-char type");
                        thisParam.Collation = returnValue.collation;
                    }
                }
            }
        }

        #endregion

        #region Protected Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CreateDbParameter/*'/>
        protected override DbParameter CreateDbParameter() =>
            CreateParameter();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Dispose/*'/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Release managed objects
                _cachedMetaData = null;

                // Reset async cache information to allow a second async execute
                CachedAsyncState?.ResetAsyncState();
            }

            // Release unmanaged objects
            base.Dispose(disposing);
        }

        #endregion

        #region Private Methods

        private static void CancelIgnoreFailureCallback(object state) =>
            ((SqlCommand)state).CancelIgnoreFailure();

        // @TODO: Assess if a parameterized version of this method is necessary or if a property can suffice.
        private static int CountSendableParameters(SqlParameterCollection parameters)
        {
            if (parameters is null)
            {
                return 0;
            }

            int sendableParameters = 0;
            int count = parameters.Count;
            for (int i = 0; i < count; i++)
            {
                if (ShouldSendParameter(parameters[i]))
                {
                    sendableParameters++;
                }
            }

            return sendableParameters;
        }

        /// <summary>
        /// Returns SET option text to turn off format only and key info on and off. When we are
        /// executing as a text command, then we never need to turn off the options since the
        /// command text is executed in the scope of sp_executesql. For a sproc command, however,
        /// we must send over batch sql and then turn off the SET options after we read the data.
        /// </summary>
        private static string GetOptionsSetString(CommandBehavior behavior)
        {
            string s = null;
            if (behavior is CommandBehavior.SchemaOnly or CommandBehavior.KeyInfo)
            {
                // SET FMTONLY ON will cause the server to ignore other SET OPTIONS, so turn if off
                // before we ask for browse mode metadata
                s = TdsEnums.FMTONLY_OFF;

                if (behavior is CommandBehavior.KeyInfo)
                {
                    s += TdsEnums.BROWSE_ON;
                }

                if (behavior is CommandBehavior.SchemaOnly)
                {
                    s += TdsEnums.FMTONLY_ON;
                }
            }

            return s;
        }

        private static string GetOptionsResetString(CommandBehavior behavior)
        {
            string s = null;

            if (behavior is CommandBehavior.SchemaOnly)
            {
                s += TdsEnums.FMTONLY_OFF;
            }

            if (behavior is CommandBehavior.KeyInfo)
            {
                s += TdsEnums.BROWSE_OFF;
            }

            return s;
        }

        // @TODO: Assess if a parameterized version of this method is necessary or if a property can suffice.
        private static int GetParameterCount(SqlParameterCollection parameters) =>
            parameters?.Count ?? 0;

        private static SqlParameter GetParameterForOutputValueExtraction(
            SqlParameterCollection parameters, // @TODO: Is this ever not Parameters?
            string paramName,
            int paramCount) // @TODO: Is this ever not Parameters.Count?
        {
            SqlParameter thisParam;
            if (paramName is null)
            {
                // rec.parameter should only be null for a return value from a function
                for (int i = 0; i < paramCount; i++)
                {
                    thisParam = parameters[i];
                    if (thisParam.Direction is ParameterDirection.ReturnValue)
                    {
                        return thisParam;
                    }
                }
            }
            else
            {
                for (int i = 0; i < paramCount; i++)
                {
                    thisParam = parameters[i];
                    if (thisParam.Direction is not (ParameterDirection.Input or ParameterDirection.ReturnValue) &&
                        SqlParameter.ParameterNamesEqual(paramName, thisParam.ParameterName, StringComparison.Ordinal))
                    {
                        return thisParam;
                    }
                }
            }

            return null;
        }

        private static bool ShouldSendParameter(SqlParameter p, bool includeReturnValue = false)
        {
            switch (p.Direction)
            {
                case ParameterDirection.ReturnValue:
                    // Return value parameters are not sent, except for the parameter list of
                    // sp_describe_parameter_encryption
                    return includeReturnValue;
                case ParameterDirection.Input:
                case ParameterDirection.Output:
                case ParameterDirection.InputOutput:
                    return true;
                default:
                    Debug.Fail("Invalid ParameterDirection!");
                    return false;
            }
        }

        private static void OnDone(TdsParserStateObject stateObj, int index, IList<_SqlRPC> rpcList, int rowsAffected)
        {
            // @TODO: Is the state object not the same as the currently stored one?

            _SqlRPC current = rpcList[index];
            _SqlRPC previous = index > 0 ? rpcList[index - 1] : null;

            // Track the records affected for the just-completed RPC batch.
            // _rowsAffected is cumulative for ExecuteNonQuery across all RPC batches
            current.cumulativeRecordsAffected = rowsAffected;
            current.recordsAffected = previous is not null && rowsAffected >= 0
                ? rowsAffected - Math.Max(previous.cumulativeRecordsAffected, 0)
                : rowsAffected;

            current.batchCommand?.SetRecordAffected(current.recordsAffected.GetValueOrDefault());

            // Track the error collection (not available from TdsParser after ExecuteNonQuery)
            // and which errors are associated with the just-completed RPC batch.
            current.errorsIndexStart = previous?.errorsIndexEnd ?? 0;
            current.errorsIndexEnd = stateObj.ErrorCount;
            current.errors = stateObj._errors;

            // Track the warning collection (not available from TdsParser after ExecuteNonQuery)
            // and which warnings are associated with the just-completed RPC batch.
            current.warningsIndexStart = previous?.warningsIndexEnd ?? 0;
            current.warningsIndexEnd = stateObj.WarningCount;
            current.warnings = stateObj._warnings;
        }

        /// <summary>
        /// Adds quotes to each part of a SQL identifier that may be multi-part, while leaving the
        /// result as a single composite name.
        /// </summary>
        // @TODO: This little utility is either likely duplicated in other places, and likely belongs in some other class.
        private static string ParseAndQuoteIdentifier(string identifier, bool isUdtTypeName) =>
            QuoteIdentifier(SqlParameter.ParseTypeName(identifier, isUdtTypeName));

        // @TODO: This little utility is either likely duplicated in other places, and likely belongs in some other class.
        private static string QuoteIdentifier(ReadOnlySpan<string> strings)
        {
            // Stitching back together is a little tricky. Assume we want to build a full
            // multipart name with all parts except trimming separators for leading empty names
            // (null or empty strings, but not whitespace). Separators in the middle should be
            // added, even if the name part is null/empty, to maintain proper location of the parts
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < strings.Length; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append('.');
                }

                string str = strings[i];
                if (!string.IsNullOrEmpty(str))
                {
                    ADP.AppendQuotedString(builder, "[", "]", str);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Generates a parameter list string for use with sp_executesql, sp_prepare, and sp_prepexec.
        /// </summary>
        // @TODO: How does this compare with BuildStoredProcedureStatementForColumnEncryption
        private string BuildParamList(TdsParser parser, SqlParameterCollection parameters, bool includeReturnValue)
        {
            // @TODO: Rather than manually add separators, is this something that could be done with a string.Join?
            StringBuilder paramList = new StringBuilder();
            bool fAddSeparation = false; // @TODO: Drop f prefix

            int count = parameters.Count;
            for (int i = 0; i < count; i++)
            {
                SqlParameter sqlParam = parameters[i];
                sqlParam.Validate(i, isCommandProc: CommandType is CommandType.StoredProcedure);

                // Skip return value parameters, we never send them to the server
                // @TODO: Is that true if "includeReturnValue" is true?
                if (!ShouldSendParameter(sqlParam, includeReturnValue))
                {
                    continue;
                }

                // Add separator for the ith parameter
                if (fAddSeparation)
                {
                    paramList.Append(',');
                }

                // @TODO: This section could do with a bit of cleanup. --vvv

                SqlParameter.AppendPrefixedParameterName(paramList, sqlParam.ParameterName);

                MetaType mt = sqlParam.InternalMetaType;

                //for UDTs, get the actual type name. Get only the typename, omit catalog and schema names.
                //in TSQL you should only specify the unqualified type name

                // paragraph above doesn't seem to be correct. Server won't find the type
                // if we don't provide a fully qualified name
                // @TODO: So ... what's correct? ---^^^
                paramList.Append(" ");
                if (mt.SqlDbType is SqlDbType.Udt) // @TODO: Switch :)
                {
                    string fullTypeName = sqlParam.UdtTypeName;
                    if (string.IsNullOrEmpty(fullTypeName))
                    {
                        throw SQL.MustSetUdtTypeNameForUdtParams();
                    }

                    paramList.Append(ParseAndQuoteIdentifier(fullTypeName, isUdtTypeName: true));
                }
                else if (mt.SqlDbType is SqlDbType.Structured)
                {
                    string typeName = sqlParam.TypeName;
                    if (string.IsNullOrEmpty(typeName))
                    {
                        throw SQL.MustSetTypeNameForParam(mt.TypeName, sqlParam.GetPrefixedParameterName());
                    }

                    // TVPs currently are the only Structured type and must be read only, so add that keyword
                    paramList.Append(ParseAndQuoteIdentifier(typeName, isUdtTypeName: false));
                    paramList.Append(" READONLY");
                }
                else
                {
                    // Func will change type to that with a 4 byte length if the type has a two
                    // byte length and a parameter length > that expressible in 2 bytes.
                    // @TODO: what func?
                    mt = sqlParam.ValidateTypeLengths();
                    if (!mt.IsPlp && sqlParam.Direction is not ParameterDirection.Output)
                    {
                        sqlParam.FixStreamDataForNonPLP();
                    }

                    paramList.Append(mt.TypeName);
                }

                fAddSeparation = true;

                // @TODO: These seem to be a total hodge-podge of conditions. Can we make a list of categories we're checking and expected behaviors
                if (mt.SqlDbType is SqlDbType.Decimal)
                {
                    byte scale = sqlParam.GetActualScale();
                    byte precision = sqlParam.GetActualPrecision();
                    if (precision == 0)
                    {
                        precision = TdsEnums.DEFAULT_NUMERIC_PRECISION;
                    }

                    paramList.AppendFormat("({0},{1})", precision, scale);
                }
                else if (mt.IsVarTime)
                {
                    byte scale = sqlParam.GetActualScale();
                    paramList.AppendFormat("({0})", scale);
                }
                else if (mt.SqlDbType is SqlDbTypeExtensions.Vector)
                {
                    // The validate function for SqlParameters would have already thrown
                    // InvalidCastException if an incompatible value is specified for vector type.
                    ISqlVector vectorProps = (ISqlVector)sqlParam.Value;
                    paramList.AppendFormat("({0})", vectorProps.Length);
                }
                else if (!mt.IsFixed && !mt.IsLong && mt.SqlDbType is not  SqlDbType.Timestamp
                                                                   and not SqlDbType.Udt
                                                                   and not SqlDbType.Structured)
                {
                    int size = sqlParam.Size;

                    // If using non-unicode types, obtain the actual length in bytes from the
                    // parser, with it's associated code page.
                    if (mt.IsAnsiType)
                    {
                        object value = sqlParam.GetCoercedValue();
                        string s = null;

                        // Deal with the sql types
                        if (value is not null && value != DBNull.Value)
                        {
                            // @TODO: I swear this can be one line in the if statement...
                            s = value as string;
                            if (s is null)
                            {
                                SqlString sval = value is SqlString ? (SqlString)value : SqlString.Null;
                                if (!sval.IsNull)
                                {
                                    s = sval.Value;
                                }
                            }
                        }

                        if (s is not null)
                        {
                            int actualBytes = parser.GetEncodingCharLength(
                                value: s,
                                numChars: sqlParam.GetActualScale(),
                                charOffset: sqlParam.Offset,
                                encoding: null);
                            if (actualBytes > size)
                            {
                                size = actualBytes;
                            }
                        }
                    }

                    // If the user specified a 0-sized parameter for a variable length field, pass
                    // in the maximum size (8000 bytes or 4000 characters for wide types)
                    if (size == 0)
                    {
                        size = mt.IsSizeInCharacters
                            ? TdsEnums.MAXSIZE >> 1
                            : TdsEnums.MAXSIZE;
                    }

                    paramList.AppendFormat("({0})", size);
                }
                else if (mt.IsPlp && mt.SqlDbType is not  SqlDbType.Xml
                                                  and not SqlDbType.Udt
                                                  and not SqlDbTypeExtensions.Json)
                {
                    paramList.Append("(max) "); // @TODO: All caps?
                }

                // Set the output bit for Output/InputOutput parameters
                if (sqlParam.Direction is not ParameterDirection.Input)
                {
                    paramList.Append(" " + TdsEnums.PARAM_OUTPUT);
                }
            }

            return paramList.ToString();
        }

        /// <summary>
        /// This method is used to route CancellationTokens to the Cancel method. Cancellation is a
        /// suggestion, and exceptions should be ignored rather than allowed to be unhandled, as
        /// there is no way to route them to the caller. It would be expected that the error will
        /// be observed anyway from the regular method. An example is cancelling an operation on a
        /// closed connection.
        /// </summary>
        // @TODO: If this is only used in above callback, just combine them together.
        private void CancelIgnoreFailure()
        {
            try
            {
                Cancel();
            }
            catch
            {
                // We are ignoring failure here.
            }
        }

        // @TODO: Rename to match naming convention
        private void CheckThrowSNIException() =>
            _stateObj?.CheckThrowSNIException();

        // @TODO: The naming of this is a bit sketchy, it implies we're just returning CommandText, but we're adding the options string to it. I'd suggest either removing the method entirely or renaming to indicate the
        private string GetCommandText(CommandBehavior behavior)
        {
            // Build the batch string we send over, since we execute within a stored proc
            // (sp_executesql), the SET options never need to be turned off since they are scoped
            // to the sproc.
            Debug.Assert(CommandType is CommandType.Text,
                "invalid call to GetCommandText for stored proc!");
            return GetOptionsSetString(behavior) + CommandText;
        }

        private SqlParameterCollection GetCurrentParameterCollection()
        {
            if (!_batchRPCMode)
            {
                return _parameters;
            }

            if (_RPCList.Count > _currentlyExecutingBatch)
            {
                return _RPCList[_currentlyExecutingBatch].userParams;
            }

            // @TODO: Is there any point to us failing here?
            Debug.Fail("OnReturnValue: SqlCommand got too many DONEPROC events");
            return null;
        }

        // @TODO: Why not *return* it? (update: ok, this is because the intention is to reuse existing RPC objects, but the way it is doing it is confusing)
        // @TODO: Rename to match naming conventions GetRpcObject
        // @TODO: This method would be less confusing if the initialized rpc is always provided (ie, the caller knows which member to grab an existing rpc from), and this method just initializes it.
        private void GetRPCObject(
            int systemParamCount,
            int userParamCount,
            ref _SqlRPC rpc, // @TODO: When is this not null?
            bool forSpDescribeParameterEncryption)
        {
            // @TODO: This method seems like its overoptimizing for no good reason. It basically exists just to allow reuse of an _SqlRPC object.

            // Designed to minimize necessary allocations
            if (rpc is null)
            {
                if (!forSpDescribeParameterEncryption)
                {
                    // @TODO: When the arrayOf1 is used vs the list of RPCs is used is confusing to say the least.
                    if (_rpcArrayOf1 is null)
                    {
                        _rpcArrayOf1 = new _SqlRPC[1];
                        _rpcArrayOf1[0] = new _SqlRPC();
                    }

                    rpc = _rpcArrayOf1[0];
                }
                else
                {
                    _rpcForEncryption ??= new _SqlRPC();
                    rpc = _rpcForEncryption;
                }
            }

            // @TODO: This should be a "clear" or "reset" method on the _SqlRPC object. But reuse of an object like this is dangerous.
            rpc.ProcID = 0;
            rpc.rpcName = null;
            rpc.options = 0;
            rpc.systemParamCount = systemParamCount;
            rpc.needsFetchParameterEncryptionMetadata = false;

            // Make sure there is enough space in the parameters and param options arrays
            int currentSystemParamCount = rpc.systemParams?.Length ?? 0;
            if (currentSystemParamCount < systemParamCount)
            {
                // @TODO: It's especially dangerous because we'll be leaking parameters and data.
                Array.Resize(ref rpc.systemParams, systemParamCount);
                Array.Resize(ref rpc.systemParamOptions, systemParamCount);

                // Initialize new elements in the array
                for (int index = currentSystemParamCount; index < systemParamCount; index++)
                {
                    rpc.systemParams[index] = new SqlParameter();
                }
            }

            for (int i = 0; i < systemParamCount; i++)
            {
                rpc.systemParamOptions[i] = 0;
            }

            int currentUserParamCount = rpc.userParamMap?.Length ?? 0;
            if (currentUserParamCount < userParamCount)
            {
                Array.Resize(ref rpc.userParamMap, userParamCount);
            }
        }

        // @TODO: This method is smelly - it gets the parser state object from the parser and stores it, but also handles the cancelled exception throwing and connection closed exception throwing.
        private void GetStateObject(TdsParser parser = null) // @TODO: Is this ever not null?
        {
            Debug.Assert(_stateObj is null, "StateObject not null on GetStateObject");
            Debug.Assert(_activeConnection is not null, "no active connection?");

            if (_pendingCancel)
            {
                _pendingCancel = false; // Not really needed, but we'll reset anyway.

                // If a pendingCancel exists on the object, we must have had a Cancel() call
                // between the point that we entered an Execute* API and the point in Execute* that
                // we proceeded to call this function and obtain a stateObject. In that case, we
                // now throw a cancelled error.
                throw SQL.OperationCancelled();
            }

            if (parser == null)
            {
                parser = _activeConnection.Parser;
                if (parser == null || parser.State is TdsParserState.Broken or TdsParserState.Closed)
                {
                    // Connection's parser is null as well, therefore we must be closed
                    throw ADP.ClosedConnectionError();
                }
            }

            TdsParserStateObject stateObj = parser.GetSession(this);
            stateObj.StartSession(this);

            _stateObj = stateObj;

            if (_pendingCancel)
            {
                _pendingCancel = false; // Not really needed, but we'll reset anyway.

                // If a pendingCancel exists on the object, we must have had a Cancel() call
                // between the point that we entered this function and the point where we obtained
                // and actually assigned the stateObject to the local member. It is possible that
                // the flag is set as well as a call to stateObj.Cancel - though that would be a
                // no-op. So - throw.
                throw SQL.OperationCancelled();
            }
        }

        // @TODO: Rename PrepareInternal
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

        private void NotifyDependency() =>
            _sqlDep?.StartTimer(Notification);

        private void PropertyChanging()
        {
            IsDirty = true;
        }

        private void PutStateObject()
        {
            TdsParserStateObject stateObject = _stateObj;
            _stateObj = null;

            stateObject?.CloseSession();
        }

        // @TODO: THERE IS NOTHING RELIABLE ABOUT THIS!!! REMOVE!!!
        private void ReliablePutStateObject() =>
            PutStateObject();

        // @TODO Rename to match naming conventions
        private void SetUpRPCParameters(_SqlRPC rpc, bool inSchema, SqlParameterCollection parameters)
        {
            int paramCount = GetParameterCount(parameters);
            int userParamCount = 0;

            for (int index = 0; index < paramCount; index++)
            {
                SqlParameter parameter = parameters[index];
                parameter.Validate(index, isCommandProc: CommandType is CommandType.StoredProcedure);

                // Func will change type to that with a 4 byte length if the type has a 2 byte
                // length and a parameter length > than that expressible in 2 bytes.
                if (!parameter.ValidateTypeLengths().IsPlp && parameter.Direction is not ParameterDirection.Output)
                {
                    parameter.FixStreamDataForNonPLP();
                }

                if (ShouldSendParameter(parameter))
                {
                    byte options = 0;

                    // Set output bit
                    if (parameter.Direction is ParameterDirection.InputOutput or ParameterDirection.Output)
                    {
                        options |= TdsEnums.RPC_PARAM_BYREF;
                    }

                    // Set the encrypted bit if the parameter is to be encrypted
                    if (parameter.CipherMetadata is not null)
                    {
                        options |= TdsEnums.RPC_PARAM_ENCRYPTED;
                    }

                    // Set default value bit
                    if (parameter.Direction is not ParameterDirection.Output)
                    {
                        // Remember that Convert.IsEmpty is null, DBNull.Value is a database null!

                        // Don't assume a default value exists for parameters in the case when the
                        // user is simply requesting schema. TVPs use DEFAULT and do not allow
                        // NULL, even for schema only.
                        if (parameter.Value is null && (!inSchema || parameter.SqlDbType is SqlDbType.Structured))
                        {
                            options |= TdsEnums.RPC_PARAM_DEFAULT;
                        }

                        // Detect incorrectly derived type names unchanged yb the caller and fix
                        if (parameter.IsDerivedParameterTypeName)
                        {
                            string[] parts = MultipartIdentifier.ParseMultipartIdentifier(
                                parameter.TypeName,
                                leftQuote: "[\"",
                                rightQuote: "]\"",
                                property: Strings.SQL_TDSParserTableName,
                                ThrowOnEmptyMultipartName: false);
                            if (parts?.Length == 4)
                            {
                                if (parts[3] is not null && // Name must not be null
                                    parts[2] is not null && // Schema must not be null
                                    parts[1] is not null)   // Server should not be null or we don't need to remove it
                                {
                                    parameter.TypeName = QuoteIdentifier(parts.AsSpan(2, 2));
                                }
                            }
                        }
                    }

                    rpc.userParamMap[userParamCount] = ((long)options << 32) | (long)index;
                    userParamCount++;

                    // Must set parameter option bit for LOB_COOKIE if unfilled LazyMat blob
                }
            }

            rpc.userParamCount = userParamCount;
            rpc.userParams = parameters;
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

        private void WriteBeginExecuteEvent()
        {
            SqlClientEventSource.Log.TryBeginExecuteEvent(
                ObjectID,
                _activeConnection?.DataSource,
                _activeConnection?.Database,
                CommandText,
                _activeConnection?.ClientConnectionId);
        }

        /// <summary>
        /// Writes and end execute event in Event Source.
        /// </summary>
        /// <param name="success">True if SQL command finished successfully, otherwise false.</param>
        /// <param name="sqlExceptionNumber">Number that identifies the type of error.</param>
        /// <param name="isSynchronous">
        /// True if SQL command was executed synchronously, otherwise false.
        /// </param>
        private void WriteEndExecuteEvent(bool success, int? sqlExceptionNumber, bool isSynchronous)
        {
            if (!SqlClientEventSource.Log.IsExecutionTraceEnabled())
            {
                return;
            }

            // SqlEventSource.WriteEvent(int, int, int, int) is faster than provided overload
            // SqlEventSource.WriteEvent(int, object[]). That's why we're trying to fit several
            // booleans in one integer value.

            // Success state is stored the first bit in compositeState 0x01
            int successFlag = success ? 1 : 0;

            // isSqlException is stored in the 2nd bit in compositeState 0x100
            int isSqlExceptionFlag = sqlExceptionNumber.HasValue ? 2 : 0;

            // Synchronous state is stored in the second bit in compositeState 0x10
            int synchronousFlag = isSynchronous ? 4 : 0;

            int compositeState = successFlag | isSqlExceptionFlag | synchronousFlag;

            SqlClientEventSource.Log.TryEndExecuteEvent(
                ObjectID,
                compositeState,
                sqlExceptionNumber.GetValueOrDefault(),
                _activeConnection?.ClientConnectionId);
        }

        #endregion

        private sealed class AsyncState
        {
            // @TODO: Autoproperties
            private int _cachedAsyncCloseCount = -1;    // value of the connection's CloseCount property when the asyncResult was set; tracks when connections are closed after an async operation
            private TaskCompletionSource<object> _cachedAsyncResult = null;
            private SqlConnection _cachedAsyncConnection = null;  // Used to validate that the connection hasn't changed when end the connection;
            private SqlDataReader _cachedAsyncReader = null;
            private RunBehavior _cachedRunBehavior = RunBehavior.ReturnImmediately;
            private string _cachedSetOptions = null;
            private string _cachedEndMethod = null;

            internal AsyncState()
            {
            }

            internal SqlDataReader CachedAsyncReader
            {
                get { return _cachedAsyncReader; }
            }
            internal RunBehavior CachedRunBehavior
            {
                get { return _cachedRunBehavior; }
            }
            internal string CachedSetOptions
            {
                get { return _cachedSetOptions; }
            }
            internal bool PendingAsyncOperation
            {
                get { return _cachedAsyncResult != null; }
            }
            internal string EndMethodName
            {
                get { return _cachedEndMethod; }
            }

            internal bool IsActiveConnectionValid(SqlConnection activeConnection)
            {
                return (_cachedAsyncConnection == activeConnection && _cachedAsyncCloseCount == activeConnection.CloseCount);
            }

            internal void ResetAsyncState()
            {
                SqlClientEventSource.Log.TryTraceEvent("CachedAsyncState.ResetAsyncState | API | ObjectId {0}, Client Connection Id {1}, AsyncCommandInProgress={2}",
                                                       _cachedAsyncConnection?.ObjectID, _cachedAsyncConnection?.ClientConnectionId, _cachedAsyncConnection?.AsyncCommandInProgress);
                _cachedAsyncCloseCount = -1;
                _cachedAsyncResult = null;
                if (_cachedAsyncConnection != null)
                {
                    _cachedAsyncConnection.AsyncCommandInProgress = false;
                    _cachedAsyncConnection = null;
                }
                _cachedAsyncReader = null;
                _cachedRunBehavior = RunBehavior.ReturnImmediately;
                _cachedSetOptions = null;
                _cachedEndMethod = null;
            }

            internal void SetActiveConnectionAndResult(TaskCompletionSource<object> completion, string endMethod, SqlConnection activeConnection)
            {
                Debug.Assert(activeConnection != null, "Unexpected null connection argument on SetActiveConnectionAndResult!");
                TdsParser parser = activeConnection?.Parser;
                SqlClientEventSource.Log.TryTraceEvent("SqlCommand.SetActiveConnectionAndResult | API | ObjectId {0}, Client Connection Id {1}, MARS={2}", activeConnection?.ObjectID, activeConnection?.ClientConnectionId, parser?.MARSOn);
                if ((parser == null) || (parser.State == TdsParserState.Closed) || (parser.State == TdsParserState.Broken))
                {
                    throw ADP.ClosedConnectionError();
                }

                _cachedAsyncCloseCount = activeConnection.CloseCount;
                _cachedAsyncResult = completion;
                if (!parser.MARSOn)
                {
                    if (activeConnection.AsyncCommandInProgress)
                        throw SQL.MARSUnsupportedOnConnection();
                }
                _cachedAsyncConnection = activeConnection;

                // Should only be needed for non-MARS, but set anyways.
                _cachedAsyncConnection.AsyncCommandInProgress = true;
                _cachedEndMethod = endMethod;
            }

            internal void SetAsyncReaderState(SqlDataReader ds, RunBehavior runBehavior, string optionSettings)
            {
                _cachedAsyncReader = ds;
                _cachedRunBehavior = runBehavior;
                _cachedSetOptions = optionSettings;
            }
        }
    }
}
