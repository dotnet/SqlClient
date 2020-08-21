// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading;
using System.Xml;
using Microsoft.Data;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;

// This class is the process wide dependency dispatcher.  It contains all connection listeners for the entire process and 
// receives notifications on those connections to dispatch to the corresponding AppDomain dispatcher to notify the
// appropriate dependencies.

// NOTE - a reference to this class is stored in native code - PROCESS WIDE STATE.

internal class SqlDependencyProcessDispatcher : MarshalByRefObject
{ // MBR since ref'ed by other appdomains.

    // -----------------------------------------------------------------------------------------------
    // Class to contain/store all relevant information about a connection that waits on the SSB queue.
    // -----------------------------------------------------------------------------------------------
    private class SqlConnectionContainer
    {

        private SqlConnection _con;
        private SqlCommand _com;
        private SqlParameter _conversationGuidParam;
        private SqlParameter _timeoutParam;
        private SqlConnectionContainerHashHelper _hashHelper;
        private WindowsIdentity _windowsIdentity;
        private string _queue;
        private string _receiveQuery;
        private string _beginConversationQuery;
        private string _endConversationQuery;
        private string _concatQuery;
        private readonly int _defaultWaitforTimeout = 60000; // Waitfor(Receive) timeout (milleseconds)
        private string _escapedQueueName;
        private string _sprocName;
        private string _dialogHandle;
        private string _cachedServer;
        private string _cachedDatabase;
        private volatile bool _errorState = false;
        private volatile bool _stop = false; // Can probably simplify this slightly - one bool instead of two.
        private volatile bool _stopped = false;
        private volatile bool _serviceQueueCreated = false;
        private int _startCount = 0;     // Each container class is called once per Start() - we refCount 
                                         // to track when we can dispose.
        private Timer _retryTimer = null;
        private Dictionary<string, int> _appDomainKeyHash = null;  // AppDomainKey->Open RefCount

        // -----------
        // BID members
        // -----------

        private readonly int _objectID = System.Threading.Interlocked.Increment(ref _objectTypeCount);
        private static int _objectTypeCount; // EventSource Counter
        internal int ObjectID
        {
            get
            {
                return _objectID;
            }
        }

        // -----------
        // Constructor
        // -----------
        internal SqlConnectionContainer(SqlConnectionContainerHashHelper hashHelper, string appDomainKey, bool useDefaults)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer|DEP> {0}, queue: '{1}'", ObjectID, HashHelper?.Queue);
            bool setupCompleted = false;

            try
            {
                _hashHelper = hashHelper;
                string guid = null;

                // If default, queue name is not present on hashHelper at this point - so we need to 
                // generate one and complete initialization.
                if (useDefaults)
                {
                    guid = Guid.NewGuid().ToString();
                    _queue = SQL.SqlNotificationServiceDefault + "-" + guid;
                    _hashHelper.ConnectionStringBuilder.ApplicationName = _queue; // Used by cleanup sproc.
                }
                else
                {
                    _queue = _hashHelper.Queue;
                }

#if DEBUG
                SqlConnectionString connectionStringOptions = new SqlConnectionString(_hashHelper.ConnectionStringBuilder.ConnectionString);
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer|DEP> Modified connection string: '{0}'", connectionStringOptions.UsersConnectionStringForTrace());
#endif

                // Always use ConnectionStringBuilder since in default case it is different from the 
                // connection string used in the hashHelper.
                _con = new SqlConnection(_hashHelper.ConnectionStringBuilder.ConnectionString); // Create connection and open.

                // Assert permission for this particular connection string since it differs from the user passed string
                // which we have already demanded upon.  
                SqlConnectionString connStringObj = (SqlConnectionString)_con.ConnectionOptions;
                connStringObj.CreatePermissionSet().Assert();
                if (connStringObj.LocalDBInstance != null)
                {
                    // If it is LocalDB, we demanded LocalDB permissions too
                    LocalDBAPI.AssertLocalDBPermissions();
                }
                _con.Open();

                _cachedServer = _con.DataSource; // SQL BU DT 390531.

                if (!_con.IsYukonOrNewer)
                { // After open, verify Yukon or later.
                    throw SQL.NotificationsRequireYukon();
                }

                if (hashHelper.Identity != null)
                {
                    // For now, DbConnectionPoolIdentity does not cache WindowsIdentity.
                    // That means for every container creation, we create a WindowsIdentity twice.
                    // We may want to improve this.
                    _windowsIdentity = DbConnectionPoolIdentity.GetCurrentWindowsIdentity();
                }

                _escapedQueueName = SqlConnection.FixupDatabaseTransactionName(_queue); // Properly escape to prevent SQL Injection.
                _appDomainKeyHash = new Dictionary<string, int>(); // Dictionary stores the Start/Stop refcount per AppDomain for this container.
                _com = new SqlCommand();
                _com.Connection = _con;

                // SQL BU DT 391534 - determine if broker is enabled on current database.
                _com.CommandText = "select is_broker_enabled from sys.databases where database_id=db_id()";

                if (!(bool)_com.ExecuteScalar())
                {
                    throw SQL.SqlDependencyDatabaseBrokerDisabled();
                }

                _conversationGuidParam = new SqlParameter("@p1", SqlDbType.UniqueIdentifier);
                _timeoutParam = new SqlParameter("@p2", SqlDbType.Int);
                _timeoutParam.Value = 0; // Timeout set to 0 for initial sync query.
                _com.Parameters.Add(_timeoutParam);

                setupCompleted = true;
                // connection with the server has been setup - from this point use TearDownAndDispose() in case of error

                // Create standard query.
                _receiveQuery = "WAITFOR(RECEIVE TOP (1) message_type_name, conversation_handle, cast(message_body AS XML) as message_body from " + _escapedQueueName + "), TIMEOUT @p2;";

                // Create queue, service, sync query, and async query on user thread to ensure proper
                // init prior to return.  

                if (useDefaults)
                { // Only create if user did not specify service & database.
                    _sprocName = SqlConnection.FixupDatabaseTransactionName(SQL.SqlNotificationStoredProcedureDefault + "-" + guid);
                    CreateQueueAndService(false); // Fail if we cannot create service, queue, etc.
                }
                else
                {
                    // Continue query setup.
                    _com.CommandText = _receiveQuery;
                    _endConversationQuery = "END CONVERSATION @p1; ";
                    _concatQuery = _endConversationQuery + _receiveQuery;
                }

                bool ignored = false;
                IncrementStartCount(appDomainKey, out ignored);
                // Query synchronously once to ensure everything is working correctly.
                // We want the exception to occur on start to immediately inform caller.
                SynchronouslyQueryServiceBrokerQueue();
                _timeoutParam.Value = _defaultWaitforTimeout; // Sync successful, extend timeout to 60 seconds.
                AsynchronouslyQueryServiceBrokerQueue();
            }
            catch (Exception e)
            {
                if (!ADP.IsCatchableExceptionType(e))
                {
                    throw;
                }

                ADP.TraceExceptionWithoutRethrow(e); // Discard failure, but trace for now.
                if (setupCompleted)
                {
                    // Be sure to drop service & queue.  This may fail if create service & queue failed.
                    // This method will not drop unless we created or service & queue ref-count is 0.
                    TearDownAndDispose();
                }
                else
                {
                    // connection has not been fully setup yet - cannot use TearDownAndDispose();
                    // we have to dispose the command and the connection to avoid connection leaks (until GC collects them).
                    if (_com != null)
                    {
                        _com.Dispose();
                        _com = null;
                    }
                    if (_con != null)
                    {
                        _con.Dispose();
                        _con = null;
                    }

                }
                throw;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        // ----------
        // Properties
        // ----------

        internal string Database
        {
            get
            {
                if (_cachedDatabase == null)
                {
                    _cachedDatabase = _con.Database;
                }
                return _cachedDatabase;
            }
        }

        internal SqlConnectionContainerHashHelper HashHelper
        {
            get
            {
                return _hashHelper;
            }
        }

        internal bool InErrorState
        {
            get
            {
                return _errorState;
            }
        }

        internal string Queue
        {
            get
            {
                return _queue;
            }
        }

        internal string Server
        {
            get
            {
                return _cachedServer;
            }
        }

        // -------
        // Methods
        // -------

        // This function is called by a ThreadPool thread as a result of an AppDomain calling 
        // SqlDependencyProcessDispatcher.QueueAppDomainUnload on AppDomain.Unload.
        internal bool AppDomainUnload(string appDomainKey)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.AppDomainUnload|DEP> {0}, AppDomainKey: '{1}'", ObjectID, appDomainKey);
            try
            {
                Debug.Assert(!ADP.IsEmpty(appDomainKey), "Unexpected empty appDomainKey!");

                // Dictionary used to track how many times start has been called per app domain.
                // For each decrement, subtract from count, and delete if we reach 0.
                lock (_appDomainKeyHash)
                {
                    if (_appDomainKeyHash.ContainsKey(appDomainKey))
                    {
                        // Do nothing if AppDomain did not call Start!
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.AppDomainUnload|DEP> _appDomainKeyHash contained AppDomainKey: '{0}'.", appDomainKey);
                        int value = _appDomainKeyHash[appDomainKey];
                        SqlClientEventSource.Log.TryNotificationTraceEvent("SqlConnectionContainer.AppDomainUnload|DEP> _appDomainKeyHash for AppDomainKey: '{0}' count: '{1}'.", appDomainKey, value);
                        Debug.Assert(value > 0, "Why is value 0 or less?");

                        bool ignored = false;
                        while (value > 0)
                        {
                            Debug.Assert(!_stopped, "We should not yet be stopped!");
                            Stop(appDomainKey, out ignored); // Stop will decrement value and remove if necessary from _appDomainKeyHash.
                            value--;
                        }

                        // Stop will remove key when decremented to 0 for this AppDomain, which should now be the case.
                        Debug.Assert(0 == value, "We did not reach 0 at end of loop in AppDomainUnload!");
                        Debug.Assert(!_appDomainKeyHash.ContainsKey(appDomainKey), "Key not removed after AppDomainUnload!");

                        if (_appDomainKeyHash.ContainsKey(appDomainKey))
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("SqlConnectionContainer.AppDomainUnload|DEP|ERR> ERROR - after the Stop() loop, _appDomainKeyHash for AppDomainKey: '{0}' entry not removed from hash.  Count: {1}'", appDomainKey, _appDomainKeyHash[appDomainKey]);
                        }
                    }
                    else
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("SqlConnectionContainer.AppDomainUnload|DEP> _appDomainKeyHash did not contain AppDomainKey: '{0}'.", appDomainKey);
                    }
                }
                SqlClientEventSource.Log.TryNotificationTraceEvent("SqlConnectionContainer.AppDomainUnload|DEP> Exiting, _stopped: '{0}'.", _stopped);
                return _stopped;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        private void AsynchronouslyQueryServiceBrokerQueue()
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.AsynchronouslyQueryServiceBrokerQueue|DEP> {0}", ObjectID);
            try
            {
                AsyncCallback callback = new AsyncCallback(AsyncResultCallback);
                _com.BeginExecuteReader(callback, null); // NO LOCK NEEDED
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        private void AsyncResultCallback(IAsyncResult asyncResult)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.AsyncResultCallback|DEP> {0}", ObjectID);
            try
            {
                using (SqlDataReader reader = _com.EndExecuteReader(asyncResult))
                {
                    ProcessNotificationResults(reader);
                }

                // Successfull completion of query - no errors.
                if (!_stop)
                {
                    AsynchronouslyQueryServiceBrokerQueue(); // Requeue...
                }
                else
                {
                    TearDownAndDispose();
                }
            }
            catch (Exception e)
            {
                if (!ADP.IsCatchableExceptionType(e))
                {
                    // VSDD 590625: let the waiting thread detect the error and exit (otherwise, the Stop call loops forever)
                    _errorState = true;
                    throw;
                }
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.AsyncResultCallback|DEP> Exception occurred.");

                if (!_stop)
                { // Only assert if not in cancel path.
                    ADP.TraceExceptionWithoutRethrow(e); // Discard failure, but trace for now.
                }

                // Failure - likely due to cancelled command.  Check _stop state.
                if (_stop)
                {
                    TearDownAndDispose();
                }
                else
                {
                    _errorState = true;
                    Restart(null); // Error code path.  Will Invalidate based on server if 1st retry fails.
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        private void CreateQueueAndService(bool restart)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.CreateQueueAndService|DEP> {0}", ObjectID);
            try
            {
                SqlCommand com = new SqlCommand();
                com.Connection = _con;
                SqlTransaction trans = null;

                try
                {
                    trans = _con.BeginTransaction(); // Since we cannot batch proc creation, start transaction.
                    com.Transaction = trans;

                    string nameLiteral = SqlServerEscapeHelper.MakeStringLiteral(_queue);

                    com.CommandText =
                            "CREATE PROCEDURE " + _sprocName + " AS"
                            + " BEGIN"
                                + " BEGIN TRANSACTION;"
                                + " RECEIVE TOP(0) conversation_handle FROM " + _escapedQueueName + ";"
                                + " IF (SELECT COUNT(*) FROM " + _escapedQueueName + " WHERE message_type_name = 'http://schemas.microsoft.com/SQL/ServiceBroker/DialogTimer') > 0"
                                + " BEGIN"
                                    + " if ((SELECT COUNT(*) FROM sys.services WHERE name = " + nameLiteral + ") > 0)"
                                    + "   DROP SERVICE " + _escapedQueueName + ";"
                                    + " if (OBJECT_ID(" + nameLiteral + ", 'SQ') IS NOT NULL)"
                                    + "   DROP QUEUE " + _escapedQueueName + ";"
                                    + " DROP PROCEDURE " + _sprocName + ";" // Don't need conditional because this is self
                                + " END"
                                + " COMMIT TRANSACTION;"
                            + " END";

                    if (!restart)
                    {
                        com.ExecuteNonQuery();
                    }
                    else
                    { // Upon restart, be resilient to the user dropping queue, service, or procedure.
                        try
                        {
                            com.ExecuteNonQuery(); // Cannot add 'IF OBJECT_ID' to create procedure query - wrap and discard failure.
                        }
                        catch (Exception e)
                        {
                            if (!ADP.IsCatchableExceptionType(e))
                            {
                                throw;
                            }
                            ADP.TraceExceptionWithoutRethrow(e);

                            try
                            { // Since the failure will result in a rollback, rollback our object.
                                if (null != trans)
                                {
                                    trans.Rollback();
                                    trans = null;
                                }
                            }
                            catch (Exception f)
                            {
                                if (!ADP.IsCatchableExceptionType(f))
                                {
                                    throw;
                                }
                                ADP.TraceExceptionWithoutRethrow(f); // Discard failure, but trace for now.
                            }
                        }

                        if (null == trans)
                        { // Create a new transaction for next operations.
                            trans = _con.BeginTransaction();
                            com.Transaction = trans;
                        }
                    }


                    com.CommandText =
                             "IF OBJECT_ID(" + nameLiteral + ", 'SQ') IS NULL"
                                + " BEGIN"
                                + " CREATE QUEUE " + _escapedQueueName + " WITH ACTIVATION (PROCEDURE_NAME=" + _sprocName + ", MAX_QUEUE_READERS=1, EXECUTE AS OWNER);"
                                + " END;"
                          + " IF (SELECT COUNT(*) FROM sys.services WHERE NAME=" + nameLiteral + ") = 0"
                                + " BEGIN"
                                + " CREATE SERVICE " + _escapedQueueName + " ON QUEUE " + _escapedQueueName + " ([http://schemas.microsoft.com/SQL/Notifications/PostQueryNotification]);"
                             + " IF (SELECT COUNT(*) FROM sys.database_principals WHERE name='sql_dependency_subscriber' AND type='R') <> 0"
                                  + " BEGIN"
                                  + " GRANT SEND ON SERVICE::" + _escapedQueueName + " TO sql_dependency_subscriber;"
                                  + " END; "
                                + " END;"
                          + " BEGIN DIALOG @dialog_handle FROM SERVICE " + _escapedQueueName + " TO SERVICE " + nameLiteral;

                    SqlParameter param = new SqlParameter();
                    param.ParameterName = "@dialog_handle";
                    param.DbType = DbType.Guid;
                    param.Direction = ParameterDirection.Output;
                    com.Parameters.Add(param);
                    com.ExecuteNonQuery();

                    // Finish setting up queries and state.  For re-start, we need to ensure we begin a new dialog above and reset
                    // our queries to use the new dialogHandle.
                    _dialogHandle = ((Guid)param.Value).ToString();
                    _beginConversationQuery = "BEGIN CONVERSATION TIMER ('" + _dialogHandle + "') TIMEOUT = 120; " + _receiveQuery;
                    _com.CommandText = _beginConversationQuery;
                    _endConversationQuery = "END CONVERSATION @p1; ";
                    _concatQuery = _endConversationQuery + _com.CommandText;

                    trans.Commit();
                    trans = null;
                    _serviceQueueCreated = true;
                }
                finally
                {
                    if (null != trans)
                    {
                        try
                        {
                            trans.Rollback();
                            trans = null;
                        }
                        catch (Exception e)
                        {
                            if (!ADP.IsCatchableExceptionType(e))
                            {
                                throw;
                            }
                            ADP.TraceExceptionWithoutRethrow(e); // Discard failure, but trace for now.
                        }
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        internal void IncrementStartCount(string appDomainKey, out bool appDomainStart)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.IncrementStartCount|DEP> {0}", ObjectID);
            try
            {
                appDomainStart = false; // Reset out param.
                int result = Interlocked.Increment(ref _startCount); // Add to refCount.
                SqlClientEventSource.Log.TryNotificationTraceEvent("SqlConnectionContainer.IncrementStartCount|DEP> {0}, incremented _startCount: {1}", _staticInstance.ObjectID, result);

                // Dictionary used to track how many times start has been called per app domain.
                // For each increment, add to count, and create entry if not present.
                lock (_appDomainKeyHash)
                {
                    if (_appDomainKeyHash.ContainsKey(appDomainKey))
                    {
                        _appDomainKeyHash[appDomainKey] = _appDomainKeyHash[appDomainKey] + 1;
                        SqlClientEventSource.Log.TryNotificationTraceEvent("SqlConnectionContainer.IncrementStartCount|DEP> _appDomainKeyHash contained AppDomainKey: '{0}', incremented count: '{1}'.", appDomainKey, _appDomainKeyHash[appDomainKey]);
                    }
                    else
                    {
                        _appDomainKeyHash[appDomainKey] = 1;
                        appDomainStart = true;
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.IncrementStartCount|DEP> _appDomainKeyHash did not contain AppDomainKey: '{0}', added to hashtable and value set to 1.", appDomainKey);
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        private void ProcessNotificationResults(SqlDataReader reader)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP> {0}", ObjectID);
            try
            {
                Guid handle = Guid.Empty; // Conversation_handle.  Always close this!
                try
                {
                    if (!_stop)
                    {
                        while (reader.Read())
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP> Row read.");
#if DEBUG
                            if (SqlClientEventSource.Log.IsNotificationTraceEnabled())
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    SqlClientEventSource.Log.NotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP> column: {0}, value: {1}", reader.GetName(i), reader.GetValue(i));
                                }
                            }
#endif
                            string msgType = reader.GetString(0);
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP> msgType: '{0}'", msgType);
                            handle = reader.GetGuid(1);

                            // Only process QueryNotification messages.
                            if (0 == String.Compare(msgType, "http://schemas.microsoft.com/SQL/Notifications/QueryNotification", StringComparison.OrdinalIgnoreCase))
                            {
                                SqlXml payload = reader.GetSqlXml(2);
                                if (null != payload)
                                {
                                    SqlNotification notification = SqlNotificationParser.ProcessMessage(payload);
                                    if (null != notification)
                                    {
                                        string key = notification.Key;
                                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP> Key: '{0}'", key);
                                        int index = key.IndexOf(';'); // Our format is simple: "AppDomainKey;commandHash"

                                        if (index >= 0)
                                        { // Ensure ';' present.
                                            string appDomainKey = key.Substring(0, index);
                                            SqlDependencyPerAppDomainDispatcher dispatcher;
                                            lock (_staticInstance._sqlDependencyPerAppDomainDispatchers)
                                            {
                                                dispatcher = _staticInstance._sqlDependencyPerAppDomainDispatchers[appDomainKey];
                                            }
                                            if (null != dispatcher)
                                            {
                                                try
                                                {
                                                    dispatcher.InvalidateCommandID(notification); // CROSS APP-DOMAIN CALL!
                                                }
                                                catch (Exception e)
                                                {
                                                    if (!ADP.IsCatchableExceptionType(e))
                                                    {
                                                        throw;
                                                    }
                                                    ADP.TraceExceptionWithoutRethrow(e); // Discard failure.  User event could throw exception.
                                                }
                                            }
                                            else
                                            {
                                                Debug.Assert(false, "Received notification but do not have an associated PerAppDomainDispatcher!");
                                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP|ERR> Received notification but do not have an associated PerAppDomainDispatcher!");
                                            }
                                        }
                                        else
                                        {
                                            Debug.Assert(false, "Unexpected ID format received!");
                                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP|ERR> Unexpected ID format received!");
                                        }
                                    }
                                    else
                                    {
                                        Debug.Assert(false, "Null notification returned from ProcessMessage!");
                                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP|ERR> Null notification returned from ProcessMessage!");
                                    }
                                }
                                else
                                {
                                    Debug.Assert(false, "Null payload for QN notification type!");
                                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP|ERR> Null payload for QN notification type!");
                                }
                            }
                            else
                            {
                                handle = Guid.Empty;
                                // VSDD 546707: this assert was hit by SQL Notification fuzzing tests, disable it to let these tests run on Debug bits
                                // Debug.Assert(false, "Unexpected message format received!");
                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.ProcessNotificationResults|DEP> Unexpected message format received!");
                            }
                        }
                    }
                }
                finally
                {
                    // Since we do not want to make a separate round trip just for the end conversation call, we need to
                    // batch it with the next command.  
                    if (handle == Guid.Empty)
                    { // This should only happen if failure occurred, or if non-QN format received.
                        _com.CommandText = (null != _beginConversationQuery) ? _beginConversationQuery : _receiveQuery; // If we're doing the initial query, we won't have a conversation Guid to begin yet.
                        if (_com.Parameters.Count > 1)
                        { // Remove conversation param since next execute is only query.
                            _com.Parameters.Remove(_conversationGuidParam);
                        }
                        Debug.Assert(_com.Parameters.Count == 1, "Unexpected number of parameters!");
                    }
                    else
                    {
                        _com.CommandText = _concatQuery; // END query + WAITFOR RECEIVE query.
                        _conversationGuidParam.Value = handle; // Set value for conversation handle.
                        if (_com.Parameters.Count == 1)
                        { // Add parameter if previous execute was only query.
                            _com.Parameters.Add(_conversationGuidParam);
                        }
                        Debug.Assert(_com.Parameters.Count == 2, "Unexpected number of parameters!");
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        // SxS: this method uses WindowsIdentity.Impersonate to impersonate the current thread with the
        // credentials used to create this SqlConnectionContainer.
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        private void Restart(object unused)
        {
            // Unused arg required by TimerCallback.
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.Restart|DEP> {0}", ObjectID);
            try
            {
                try
                {
                    lock (this)
                    {
                        if (!_stop)
                        { // Only execute if we are still in running state.
                            try
                            {
                                _con.Close();
                            }
                            catch (Exception e)
                            {
                                if (!ADP.IsCatchableExceptionType(e))
                                {
                                    throw;
                                }
                                ADP.TraceExceptionWithoutRethrow(e); // Discard close failure, if it occurs.  Only trace it.
                            }
                        }
                    }

                    // Rather than one long lock - take lock 3 times for shorter periods.

                    lock (this)
                    {
                        if (!_stop)
                        {
                            if (null != _hashHelper.Identity)
                            { // Only impersonate if Integrated Security.
                                WindowsImpersonationContext context = null;
                                RuntimeHelpers.PrepareConstrainedRegions(); // CER for context.Undo.
                                try
                                {
                                    context = _windowsIdentity.Impersonate();
                                    _con.Open();
                                }
                                finally
                                {
                                    if (null != context)
                                    {
                                        context.Undo();
                                    }
                                }
                            }
                            else
                            { // Else SQL Authentication.
                                _con.Open();
                            }
                        }
                    }

                    lock (this)
                    {
                        if (!_stop)
                        {
                            if (_serviceQueueCreated)
                            {
                                bool failure = false;

                                try
                                {
                                    CreateQueueAndService(true); // Ensure service, queue, etc is present, if we created it.
                                }
                                catch (Exception e)
                                {
                                    if (!ADP.IsCatchableExceptionType(e))
                                    {
                                        throw;
                                    }
                                    ADP.TraceExceptionWithoutRethrow(e); // Discard failure, but trace for now.
                                    failure = true;
                                }

                                if (failure)
                                {
                                    // If we failed to re-created queue, service, sproc - invalidate!
                                    _staticInstance.Invalidate(Server,
                                                               new SqlNotification(SqlNotificationInfo.Error,
                                                                                   SqlNotificationSource.Client,
                                                                                   SqlNotificationType.Change,
                                                                                   null));

                                }
                            }
                        }
                    }

                    lock (this)
                    {
                        if (!_stop)
                        {
                            _timeoutParam.Value = 0; // Reset timeout to zero - we do not want to block.
                            SynchronouslyQueryServiceBrokerQueue();
                            // If the above succeeds, we are back in success case - requeue for async call.
                            _timeoutParam.Value = _defaultWaitforTimeout; // If success, reset to default for re-queue.
                            AsynchronouslyQueryServiceBrokerQueue();
                            _errorState = false;
                            _retryTimer = null;
                        }
                    }

                    if (_stop)
                    {
                        TearDownAndDispose(); // Function will lock(this).
                    }
                }
                catch (Exception e)
                {
                    if (!ADP.IsCatchableExceptionType(e))
                    {
                        throw;
                    }
                    ADP.TraceExceptionWithoutRethrow(e);

                    try
                    {
                        // If unexpected query or connection failure, invalidate all dependencies against this server.
                        // We may over-notify if only some of the connections to a particular database were affected,
                        // but this should not be frequent enough to be a concern.
                        // NOTE - we invalidate after failure first occurs and then retry fails.  We will then continue
                        // to invalidate every time the retry fails.
                        _staticInstance.Invalidate(Server,
                                                   new SqlNotification(SqlNotificationInfo.Error,
                                                                       SqlNotificationSource.Client,
                                                                       SqlNotificationType.Change,
                                                                       null));
                    }
                    catch (Exception f)
                    {
                        if (!ADP.IsCatchableExceptionType(f))
                        {
                            throw;
                        }
                        ADP.TraceExceptionWithoutRethrow(f); // Discard exception from Invalidate.  User events can throw.
                    }

                    try
                    {
                        _con.Close();
                    }
                    catch (Exception f)
                    {
                        if (!ADP.IsCatchableExceptionType(f))
                        {
                            throw;
                        }
                        ADP.TraceExceptionWithoutRethrow(f); // Discard close failure, if it occurs.  Only trace it.
                    }

                    if (!_stop)
                    {
                        // Create a timer to callback in one minute, retrying the call to Restart().
                        _retryTimer = new Timer(new TimerCallback(Restart), null, _defaultWaitforTimeout, Timeout.Infinite);
                        // We will retry this indefinitely, until success - or Stop();
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        internal bool Stop(string appDomainKey, out bool appDomainStop)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.Stop|DEP> {0}", ObjectID);
            try
            {
                appDomainStop = false;

                // Dictionary used to track how many times start has been called per app domain.
                // For each decrement, subtract from count, and delete if we reach 0.

                // TODO BUG UNDONE - once it's decided we don't need AppDomain.UnloadEvent logic below, this should
                // never be null.
                if (null != appDomainKey)
                {
                    // If null, then this was called from SqlDependencyProcessDispatcher, we ignore appDomainKeyHash.
                    lock (_appDomainKeyHash)
                    {
                        if (_appDomainKeyHash.ContainsKey(appDomainKey))
                        { // Do nothing if AppDomain did not call Start!
                            int value = _appDomainKeyHash[appDomainKey];

                            Debug.Assert(value > 0, "Unexpected count for appDomainKey");
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.Stop|DEP> _appDomainKeyHash contained AppDomainKey: '{0}', pre-decrement Count: '{1}'.", appDomainKey, value);

                            if (value > 0)
                            {
                                _appDomainKeyHash[appDomainKey] = value - 1;
                            }
                            else
                            {
                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.Stop|DEP|ERR> ERROR pre-decremented count <= 0!");
                                Debug.Assert(false, "Unexpected AppDomainKey count in Stop()");
                            }

                            if (1 == value)
                            { // Remove from dictionary if pre-decrement count was one.
                                _appDomainKeyHash.Remove(appDomainKey);
                                appDomainStop = true;
                            }
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.Stop|DEP|ERR> ERROR appDomainKey not null and not found in hash!");
                            Debug.Assert(false, "Unexpected state on Stop() - no AppDomainKey entry in hashtable!");
                        }
                    }
                }

                Debug.Assert(_startCount > 0, "About to decrement _startCount less than 0!");
                int result = Interlocked.Decrement(ref _startCount);

                if (0 == result)
                {
                    // If we've reached refCount 0, destroy.
                    // Lock to ensure Cancel() complete prior to other thread calling TearDown.
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.Stop|DEP> Reached 0 count, cancelling and waiting.");

                    lock (this)
                    {
                        try
                        {
                            // Race condition with executing thread - will throw if connection is closed due to failure.
                            // Rather than fighting the race condition, just call it and discard any potential failure.
                            _com.Cancel(); // Cancel the pending command.  No-op if connection closed.
                        }
                        catch (Exception e)
                        {
                            if (!ADP.IsCatchableExceptionType(e))
                            {
                                throw;
                            }
                            ADP.TraceExceptionWithoutRethrow(e); // Discard failure, if it should occur.
                        }
                        _stop = true;
                    }

                    // Wait until stopped and service & queue are dropped.
                    // TODO: investigate a more appropriate/robust way to ensure this is torn down correctly
                    Stopwatch retryStopwatch = Stopwatch.StartNew();
                    while (true)
                    {
                        lock (this)
                        {
                            if (_stopped)
                            {
                                break;
                            }

                            // If we are in error state (_errorState is true), force a tear down.
                            // Likewise, if we have exceeded the maximum retry period (30 seconds) waiting for cleanup, force a tear down.
                            // In rare cases during app domain unload, the async cleanup performed by AsyncResultCallback
                            // may fail to execute TearDownAndDispose, leaving this method in an infinite loop, see Dev10#923666.  
                            // To avoid the infinite loop, we force the cleanup here after 30 seconds.  Since we have reached 
                            // refcount of 0, either this method call or the thread running AsyncResultCallback is responsible for calling 
                            // TearDownAndDispose when transitioning to the _stopped state.  Failing to call TearDownAndDispose means we leak 
                            // the service broker objects created by this SqlDependency instance, so we make a best effort here to call 
                            // TearDownAndDispose in the maximum retry period case as well as in the _errorState case.
                            if (_errorState || retryStopwatch.Elapsed.Seconds >= 30)
                            {
                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.Stop|DEP|ERR> forcing cleanup. elapsedSeconds: '{0}', _errorState: '{1}'.", retryStopwatch.Elapsed.Seconds, _errorState);
                                Timer retryTimer = _retryTimer;
                                _retryTimer = null;
                                if (retryTimer != null)
                                {
                                    retryTimer.Dispose(); // Dispose timer - stop retry loop!
                                }
                                TearDownAndDispose(); // Will not hit server unless connection open!
                                break;
                            }
                        }

                        // Yield the thread since the stop has not yet completed.
                        // VSDD 590625: To avoid CPU spikes while waiting, yield and wait for at least one millisecond
                        Thread.Sleep(1);
                    }
                }
                else
                {
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlConnectionContainer.Stop|DEP> _startCount not 0 after decrement.  _startCount: '{0}'.", _startCount);
                }

                Debug.Assert(0 <= _startCount, "Invalid start count state");

                return _stopped;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        private void SynchronouslyQueryServiceBrokerQueue()
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.SynchronouslyQueryServiceBrokerQueue|DEP> {0}", ObjectID);

            try
            {
                using (SqlDataReader reader = _com.ExecuteReader())
                {
                    ProcessNotificationResults(reader);
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        [SuppressMessage("Microsoft.Security", "CA2100:ReviewSqlQueriesForSecurityVulnerabilities")]
        private void TearDownAndDispose()
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlConnectionContainer.TearDownAndDispose|DEP> {0}", ObjectID);

            try
            {
                lock (this)
                { // Lock to ensure Stop() (with Cancel()) complete prior to TearDown.
                    try
                    {
                        // Only execute if connection is still up and open.
                        if (ConnectionState.Closed != _con.State && ConnectionState.Broken != _con.State)
                        {
                            if (_com.Parameters.Count > 1)
                            { // Need to close dialog before completing.
                                // In the normal case, the "End Conversation" query is executed before a
                                // receive query and upon return we will clear the state.  However, unless
                                // a non notification query result is returned, we will not clear it.  That
                                // means a query is generally always executing with an "end conversation" on
                                // the wire.  Rather than synchronize for success of the other "end conversation", 
                                // simply re-execute.
                                try
                                {
                                    _com.CommandText = _endConversationQuery;
                                    _com.Parameters.Remove(_timeoutParam);
                                    _com.ExecuteNonQuery();
                                }
                                catch (Exception e)
                                {
                                    if (!ADP.IsCatchableExceptionType(e))
                                    {
                                        throw;
                                    }
                                    ADP.TraceExceptionWithoutRethrow(e); // Discard failure.
                                }
                            }

                            if (_serviceQueueCreated && !_errorState)
                            {
                                /*
                                    BEGIN TRANSACTION;
                                    DROP SERVICE "+_escapedQueueName+";
                                    DROP QUEUE "+_escapedQueueName+";
                                    DROP PROCEDURE "+_sprocName+";
                                    COMMIT TRANSACTION;
                                */
                                _com.CommandText = "BEGIN TRANSACTION; DROP SERVICE " + _escapedQueueName + "; DROP QUEUE " + _escapedQueueName + "; DROP PROCEDURE " + _sprocName + "; COMMIT TRANSACTION;";
                                try
                                {
                                    _com.ExecuteNonQuery();
                                }
                                catch (Exception e)
                                {
                                    if (!ADP.IsCatchableExceptionType(e))
                                    {
                                        throw;
                                    }
                                    ADP.TraceExceptionWithoutRethrow(e); // Discard failure.
                                }
                            }
                        }
                    }
                    finally
                    {
                        _stopped = true;
                        _con.Dispose(); // Close and dispose connection.
                        //dispose windows identity
                        if (_windowsIdentity != null)
                        {
                            _windowsIdentity.Dispose();
                        }
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }
    }
    // -----------------------------------------
    // END SqlConnectionContainer private class.
    // -----------------------------------------



    // -------------------------------------------------------------------
    // Private class encapsulating the notification payload parsing logic.
    // -------------------------------------------------------------------

    // TODO BUG UNDONE - NEED TO REVIEW AND POSSIBLY CORRECT SOME OF BELOW...

    private class SqlNotificationParser
    {
        [Flags]
        private enum MessageAttributes
        {
            None = 0,
            Type = 1,
            Source = 2,
            Info = 4,
            All = Type + Source + Info,
        }

        // node names in the payload
        private const string RootNode = "QueryNotification";
        private const string MessageNode = "Message";

        // attribute names (on the QueryNotification element)
        private const string InfoAttribute = "info";
        private const string SourceAttribute = "source";
        private const string TypeAttribute = "type";

        internal static SqlNotification ProcessMessage(SqlXml xmlMessage)
        {
            using (XmlReader xmlReader = xmlMessage.CreateReader())
            {
                string keyvalue = String.Empty;

                MessageAttributes messageAttributes = MessageAttributes.None;

                SqlNotificationType type = SqlNotificationType.Unknown;
                SqlNotificationInfo info = SqlNotificationInfo.Unknown;
                SqlNotificationSource source = SqlNotificationSource.Unknown;

                string key = string.Empty;

                // Move to main node, expecting "QueryNotification".
                xmlReader.Read();
                if ((XmlNodeType.Element == xmlReader.NodeType) &&
                     (RootNode == xmlReader.LocalName) &&
                     (3 <= xmlReader.AttributeCount))
                {
                    // Loop until we've processed all the attributes.
                    while ((MessageAttributes.All != messageAttributes) && (xmlReader.MoveToNextAttribute()))
                    {
                        try
                        {
                            switch (xmlReader.LocalName)
                            {
                                case TypeAttribute:
                                    try
                                    {
                                        SqlNotificationType temp = (SqlNotificationType)Enum.Parse(typeof(SqlNotificationType), xmlReader.Value, true);
                                        if (Enum.IsDefined(typeof(SqlNotificationType), temp))
                                        {
                                            type = temp;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        if (!ADP.IsCatchableExceptionType(e))
                                        {
                                            throw;
                                        }
                                        ADP.TraceExceptionWithoutRethrow(e); // Discard failure, if it should occur.
                                    }
                                    messageAttributes |= MessageAttributes.Type;
                                    break;
                                case SourceAttribute:
                                    try
                                    {
                                        SqlNotificationSource temp = (SqlNotificationSource)Enum.Parse(typeof(SqlNotificationSource), xmlReader.Value, true);
                                        if (Enum.IsDefined(typeof(SqlNotificationSource), temp))
                                        {
                                            source = temp;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        if (!ADP.IsCatchableExceptionType(e))
                                        {
                                            throw;
                                        }
                                        ADP.TraceExceptionWithoutRethrow(e); // Discard failure, if it should occur.
                                    }
                                    messageAttributes |= MessageAttributes.Source;
                                    break;
                                case InfoAttribute:
                                    try
                                    {
                                        string value = xmlReader.Value;
                                        // SQL BU DT 390529 - 3 of the server info values do not match client values - map.
                                        switch (value)
                                        {
                                            case "set options":
                                                info = SqlNotificationInfo.Options;
                                                break;
                                            case "previous invalid":
                                                info = SqlNotificationInfo.PreviousFire;
                                                break;
                                            case "query template limit":
                                                info = SqlNotificationInfo.TemplateLimit;
                                                break;
                                            default:
                                                SqlNotificationInfo temp = (SqlNotificationInfo)Enum.Parse(typeof(SqlNotificationInfo), value, true);
                                                if (Enum.IsDefined(typeof(SqlNotificationInfo), temp))
                                                {
                                                    info = temp;
                                                }
                                                break;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        if (!ADP.IsCatchableExceptionType(e))
                                        {
                                            throw;
                                        }
                                        ADP.TraceExceptionWithoutRethrow(e); // Discard failure, if it should occur.
                                    }
                                    messageAttributes |= MessageAttributes.Info;
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch (ArgumentException e)
                        {
                            ADP.TraceExceptionWithoutRethrow(e); // Discard failure, but trace.
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> Exception thrown - Enum.Parse failed to parse the value '{0}' of the attribute '{1}'.", xmlReader.Value, xmlReader.LocalName);
                            return null;
                        }
                    }

                    if (MessageAttributes.All != messageAttributes)
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> Not all expected attributes in Message; messageAttributes = '{0}'.", (int)messageAttributes);
                        return null;
                    }

                    // Proceed to the "Message" node.
                    if (!xmlReader.Read())
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<Sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> unexpected Read failure on xml or unexpected structure of xml.");
                        return null;
                    }

                    // Verify state after Read().
                    if ((XmlNodeType.Element != xmlReader.NodeType) || (0 != string.Compare(xmlReader.LocalName, MessageNode, StringComparison.OrdinalIgnoreCase)))
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> unexpected Read failure on xml or unexpected structure of xml.");
                        return null;
                    }

                    // Proceed to the Text Node.
                    if (!xmlReader.Read())
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> unexpected Read failure on xml or unexpected structure of xml.");
                        return null;
                    }

                    // Verify state after Read().
                    if (xmlReader.NodeType != XmlNodeType.Text)
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> unexpected Read failure on xml or unexpected structure of xml.");
                        return null;
                    }

                    // Create a new XmlTextReader on the Message node value. Prohibit DTD processing when dealing with untrusted sources.
                    using (XmlTextReader xmlMessageReader = new XmlTextReader(xmlReader.Value, XmlNodeType.Element, null) { DtdProcessing = DtdProcessing.Prohibit })
                    {
                        // Proceed to the Text Node.
                        if (!xmlMessageReader.Read())
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> unexpected Read failure on xml or unexpected structure of xml.");
                            return null;
                        }

                        if (xmlMessageReader.NodeType == XmlNodeType.Text)
                        {
                            key = xmlMessageReader.Value;
                            xmlMessageReader.Close();
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> unexpected Read failure on xml or unexpected structure of xml.");
                            return null;
                        }
                    }

                    return new SqlNotification(info, source, type, key);
                }
                else
                {
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.ProcessMessage|DEP|ERR> unexpected Read failure on xml or unexpected structure of xml.");
                    return null; // failure
                }
            }
        }
    }
    // ----------------------------------------
    // END SqlNotificationParser private class.
    // ----------------------------------------



    // ------------------------------------------------------------------
    // Private class encapsulating the SqlConnectionContainer hash logic.
    // ------------------------------------------------------------------

    private class SqlConnectionContainerHashHelper
    {
        // For default, queue is computed in SqlConnectionContainer constructor, so queue will be empty and
        // connection string will not include app name based on queue.  As a result, the connection string
        // builder will always contain up to date info, but _connectionString and _queue will not.

        // As a result, we will not use _connectionStringBuilder as part of Equals or GetHashCode.

        private DbConnectionPoolIdentity _identity;
        private string _connectionString;
        private string _queue;
        private SqlConnectionStringBuilder _connectionStringBuilder; // Not to be used for comparison!

        internal SqlConnectionContainerHashHelper(DbConnectionPoolIdentity identity, string connectionString,
                                                  string queue, SqlConnectionStringBuilder connectionStringBuilder)
        {
            _identity = identity;
            _connectionString = connectionString;
            _queue = queue;
            _connectionStringBuilder = connectionStringBuilder;
        }
        /*
                internal string ConnectionString {
                    get {
                        return _connectionString;
                    }
                }
        */
        internal SqlConnectionStringBuilder ConnectionStringBuilder
        { // Not to be used for comparison!
            get
            {
                return _connectionStringBuilder;
            }
        }

        internal DbConnectionPoolIdentity Identity
        {
            get
            {
                return _identity;
            }
        }

        internal string Queue
        {
            get
            {
                return _queue;
            }
        }

        override public bool Equals(object value)
        {
            SqlConnectionContainerHashHelper temp = (SqlConnectionContainerHashHelper)value;

            bool result = false;

            // Ignore SqlConnectionStringBuilder, since it is present largely for debug purposes.

            if (null == temp)
            { // If passed value null - false.
                result = false;
            }
            else if (this == temp)
            { // If instances equal - true.
                result = true;
            }
            else
            {
                if ((_identity != null && temp._identity == null) || // If XOR of null identities false - false.
                     (_identity == null && temp._identity != null))
                {
                    result = false;
                }
                else if (_identity == null && temp._identity == null)
                {
                    if (temp._connectionString == _connectionString &&
                        String.Equals(temp._queue, _queue, StringComparison.OrdinalIgnoreCase))
                    {
                        result = true;
                    }
                    else
                    {
                        result = false;
                    }
                }
                else
                {
                    if (temp._identity.Equals(_identity) &&
                        temp._connectionString == _connectionString &&
                        String.Equals(temp._queue, _queue, StringComparison.OrdinalIgnoreCase))
                    {
                        result = true;
                    }
                    else
                    {
                        result = false;
                    }
                }
            }

            return result;
        }

        override public int GetHashCode()
        {
            int hashValue = 0;

            if (null != _identity)
            {
                hashValue = _identity.GetHashCode();
            }

            if (null != _queue)
            {
                hashValue = unchecked(_connectionString.GetHashCode() + _queue.GetHashCode() + hashValue);
            }
            else
            {
                hashValue = unchecked(_connectionString.GetHashCode() + hashValue);
            }

            return hashValue;
        }
    }
    // ----------------------------------------
    // END SqlConnectionContainerHashHelper private class.
    // ----------------------------------------



    // ---------------------------------------------
    // SqlDependencyProcessDispatcher static members
    // ---------------------------------------------

    private static SqlDependencyProcessDispatcher _staticInstance = new SqlDependencyProcessDispatcher(null);

    // Dictionaries used as maps.
    private Dictionary<SqlConnectionContainerHashHelper, SqlConnectionContainer> _connectionContainers;                 // NT_ID+ConStr+Service->Container
    private Dictionary<string, SqlDependencyPerAppDomainDispatcher> _sqlDependencyPerAppDomainDispatchers; // AppDomainKey->Callback

    // -----------
    // BID members
    // -----------

    private readonly int _objectID = System.Threading.Interlocked.Increment(ref _objectTypeCount);
    private static int _objectTypeCount; // EventSource Counter
    internal int ObjectID
    {
        get
        {
            return _objectID;
        }
    }

    // ------------
    // Constructors
    // ------------

    // Private constructor - only called by public constructor for static initialization.
    private SqlDependencyProcessDispatcher(object dummyVariable)
    {
        Debug.Assert(null == _staticInstance, "Real constructor called with static instance already created!");
        long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependencyProcessDispatcher|DEP> {0}", ObjectID);
        try
        {
#if DEBUG
            // Possibly expensive, limit to debug.
            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher|DEP> {0}, AppDomain.CurrentDomain.FriendlyName: {1}", ObjectID, AppDomain.CurrentDomain.FriendlyName);
#endif
            _connectionContainers = new Dictionary<SqlConnectionContainerHashHelper, SqlConnectionContainer>();
            _sqlDependencyPerAppDomainDispatchers = new Dictionary<string, SqlDependencyPerAppDomainDispatcher>();
        }
        finally
        {
            SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
        }
    }

    // Constructor is only called by remoting.
    // Required to be public, even on internal class, for Remoting infrastructure.
    public SqlDependencyProcessDispatcher()
    {
        long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependencyProcessDispatcher|DEP> {0}", ObjectID);
        try
        {
            // Empty constructor and object - dummy to obtain singleton.
#if DEBUG
            // Possibly expensive, limit to debug.
            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher|DEP> {0}, AppDomain.CurrentDomain.FriendlyName: {1}", ObjectID, AppDomain.CurrentDomain.FriendlyName);
#endif
        }
        finally
        {
            SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
        }
    }

    // ----------
    // Properties
    // ----------

    internal SqlDependencyProcessDispatcher SingletonProcessDispatcher
    {
        get
        {
            return _staticInstance;
        }
    }

    // -----------------------
    // Various private methods
    // -----------------------

    private static SqlConnectionContainerHashHelper GetHashHelper(string connectionString,
                                                                  out SqlConnectionStringBuilder connectionStringBuilder,
                                                                  out DbConnectionPoolIdentity identity,
                                                                  out string user,
                                                                      string queue)
    {
        long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependencyProcessDispatcher.GetHashString|DEP> {0}, queue: {1}", _staticInstance.ObjectID, queue);
        try
        {
            // Force certain connection string properties to be used by SqlDependencyProcessDispatcher.  
            // This logic is done here to enable us to have the complete connection string now to be used
            // for tracing as we flow through the logic.
            connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            connectionStringBuilder.AsynchronousProcessing = true;
            connectionStringBuilder.Pooling = false;
            connectionStringBuilder.Enlist = false;
            connectionStringBuilder.ConnectRetryCount = 0;
            if (null != queue)
            { // User provided!
                connectionStringBuilder.ApplicationName = queue; // ApplicationName will be set to queue name.
            }

            if (connectionStringBuilder.IntegratedSecurity)
            {
                // Use existing identity infrastructure for error cases and proper hash value.
                identity = DbConnectionPoolIdentity.GetCurrent();
                user = null;
            }
            else
            {
                identity = null;
                user = connectionStringBuilder.UserID;
            }

            return new SqlConnectionContainerHashHelper(identity, connectionStringBuilder.ConnectionString,
                                                        queue, connectionStringBuilder);
        }
        finally
        {
            SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
        }
    }

    // Needed for remoting to prevent lifetime issues and default GC cleanup.
    public override object InitializeLifetimeService()
    {
        return null;
    }

    private void Invalidate(string server, SqlNotification sqlNotification)
    {
        long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependencyProcessDispatcher.Invalidate|DEP> {0}, server: {1}", ObjectID, server);
        try
        {
            Debug.Assert(this == _staticInstance, "Instance method called on non _staticInstance instance!");
            lock (_sqlDependencyPerAppDomainDispatchers)
            {

                foreach (KeyValuePair<string, SqlDependencyPerAppDomainDispatcher> entry in _sqlDependencyPerAppDomainDispatchers)
                {
                    SqlDependencyPerAppDomainDispatcher perAppDomainDispatcher = entry.Value;
                    try
                    {
                        perAppDomainDispatcher.InvalidateServer(server, sqlNotification);
                    }
                    catch (Exception f)
                    {
                        // Since we are looping over dependency dispatchers, do not allow one Invalidate
                        // that results in a throw prevent us from invalidating all dependencies
                        // related to this server.
                        // NOTE - SqlDependencyPerAppDomainDispatcher already wraps individual dependency invalidates
                        // with try/catch, but we should be careful and do the same here.
                        if (!ADP.IsCatchableExceptionType(f))
                        {
                            throw;
                        }
                        ADP.TraceExceptionWithoutRethrow(f); // Discard failure, but trace.
                    }
                }
            }
        }
        finally
        {
            SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
        }
    }

    // ----------------------------------------------------
    // Clean-up method initiated by other AppDomain.Unloads
    // ----------------------------------------------------

    // Individual AppDomains upon AppDomain.UnloadEvent will call this method.
    internal void QueueAppDomainUnloading(string appDomainKey)
    {
        ThreadPool.QueueUserWorkItem(new WaitCallback(AppDomainUnloading), appDomainKey);
    }

    // This method is only called by queued work-items from the method above.
    private void AppDomainUnloading(object state)
    {
        long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependencyProcessDispatcher.AppDomainUnloading|DEP> {0}", ObjectID);
        try
        {
            string appDomainKey = (string)state;

            Debug.Assert(this == _staticInstance, "Instance method called on non _staticInstance instance!");
            lock (_connectionContainers)
            {
                List<SqlConnectionContainerHashHelper> containersToRemove = new List<SqlConnectionContainerHashHelper>();

                foreach (KeyValuePair<SqlConnectionContainerHashHelper, SqlConnectionContainer> entry in _connectionContainers)
                {
                    SqlConnectionContainer container = entry.Value;
                    if (container.AppDomainUnload(appDomainKey))
                    { // Perhaps wrap in try catch.
                        containersToRemove.Add(container.HashHelper);
                    }
                }

                foreach (SqlConnectionContainerHashHelper hashHelper in containersToRemove)
                {
                    _connectionContainers.Remove(hashHelper);
                }
            }

            lock (_sqlDependencyPerAppDomainDispatchers)
            { // Remove from global Dictionary.
                _sqlDependencyPerAppDomainDispatchers.Remove(appDomainKey);
            }
        }
        finally
        {
            SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
        }
    }

    // -------------
    // Start methods
    // -------------

    internal bool StartWithDefault(string connectionString,
                                   out string server,
                                   out DbConnectionPoolIdentity identity,
                                   out string user,
                                   out string database,
                                   ref string service,
                                       string appDomainKey,
                                       SqlDependencyPerAppDomainDispatcher dispatcher,
                                   out bool errorOccurred,
                                   out bool appDomainStart)
    {
        Debug.Assert(this == _staticInstance, "Instance method called on non _staticInstance instance!");
        return Start(connectionString,
                     out server,
                     out identity,
                     out user,
                     out database,
                     ref service,
                         appDomainKey,
                         dispatcher,
                     out errorOccurred,
                     out appDomainStart,
                         true);
    }

    internal bool Start(string connectionString,
                        string queue,
                        string appDomainKey,
                        SqlDependencyPerAppDomainDispatcher dispatcher)
    {
        Debug.Assert(this == _staticInstance, "Instance method called on non _staticInstance instance!");
        string dummyValue1 = null;
        bool dummyValue2 = false;
        DbConnectionPoolIdentity dummyValue3 = null;
        return Start(connectionString,
                     out dummyValue1,
                     out dummyValue3,
                     out dummyValue1,
                     out dummyValue1,
                     ref queue,
                         appDomainKey,
                         dispatcher,
                     out dummyValue2,
                     out dummyValue2,
                         false);
    }

    private bool Start(string connectionString,
                       out string server,
                       out DbConnectionPoolIdentity identity,
                       out string user,
                       out string database,
                       ref string queueService,
                           string appDomainKey,
                           SqlDependencyPerAppDomainDispatcher dispatcher,
                       out bool errorOccurred,
                       out bool appDomainStart,
                           bool useDefaults)
    {
        long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependencyProcessDispatcher.Start|DEP> {0}, queue: '{1}', appDomainKey: '{2}', perAppDomainDispatcher ID: '{3}'", ObjectID, queueService, appDomainKey, dispatcher.ObjectID);
        try
        {
            Debug.Assert(this == _staticInstance, "Instance method called on non _staticInstance instance!");
            server = null;  // Reset out params.
            identity = null;
            user = null;
            database = null;
            errorOccurred = false;
            appDomainStart = false;

            lock (_sqlDependencyPerAppDomainDispatchers)
            {
                if (!_sqlDependencyPerAppDomainDispatchers.ContainsKey(appDomainKey))
                {
                    _sqlDependencyPerAppDomainDispatchers[appDomainKey] = dispatcher;
                }
            }

            SqlConnectionStringBuilder connectionStringBuilder = null;
            SqlConnectionContainerHashHelper hashHelper = GetHashHelper(connectionString,
                                                                                     out connectionStringBuilder,
                                                                                     out identity,
                                                                                     out user,
                                                                                         queueService);
#if DEBUG
            SqlConnectionString connectionStringOptions = new SqlConnectionString(connectionStringBuilder.ConnectionString);
            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Start|DEP> Modified connection string: '{0}'", connectionStringOptions.UsersConnectionStringForTrace());
#endif

            bool started = false;

            SqlConnectionContainer container = null;
            lock (_connectionContainers)
            {
                if (!_connectionContainers.ContainsKey(hashHelper))
                {
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Start|DEP> {0}, hashtable miss, creating new container.", ObjectID);
                    container = new SqlConnectionContainer(hashHelper, appDomainKey, useDefaults);
                    _connectionContainers.Add(hashHelper, container);
                    started = true;
                    appDomainStart = true;
                }
                else
                {
                    container = _connectionContainers[hashHelper];
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Start|DEP> {0}, hashtable hit, container: {1}", ObjectID, container.ObjectID);
                    if (container.InErrorState)
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Start|DEP> {0}, container: {1} is in error state!", ObjectID, container.ObjectID);
                        errorOccurred = true; // Set outparam errorOccurred true so we invalidate on Start().
                    }
                    else
                    {
                        container.IncrementStartCount(appDomainKey, out appDomainStart);
                    }
                }
            }

            if (useDefaults && !errorOccurred)
            { // Return server, database, and queue for use by SqlDependency.
                server = container.Server;
                database = container.Database;
                queueService = container.Queue;
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Start|DEP> {0}, default service: '{1}', server: '{2}', database: '{3}'", ObjectID, queueService, server, database);
            }

            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Start|DEP> {0}, started: {1}", ObjectID, started);
            return started;
        }
        finally
        {
            SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
        }
    }

    // ------------
    // Stop methods
    // ------------

    internal bool Stop(string connectionString,
                       out string server,
                       out DbConnectionPoolIdentity identity,
                       out string user,
                       out string database,
                       ref string queueService,
                           string appDomainKey,
                       out bool appDomainStop)
    {
        long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependencyProcessDispatcher.Stop|DEP> {0}, queue: '{1}'", ObjectID, queueService);
        try
        {
            Debug.Assert(this == _staticInstance, "Instance method called on non _staticInstance instance!");
            server = null;  // Reset out param.
            identity = null;
            user = null;
            database = null;
            appDomainStop = false;

            SqlConnectionStringBuilder connectionStringBuilder = null;
            SqlConnectionContainerHashHelper hashHelper = GetHashHelper(connectionString,
                                                                                     out connectionStringBuilder,
                                                                                     out identity,
                                                                                     out user,
                                                                                         queueService);
#if DEBUG
            SqlConnectionString connectionStringOptions = new SqlConnectionString(connectionStringBuilder.ConnectionString);
            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Stop|DEP> Modified connection string: '{0}'", connectionStringOptions.UsersConnectionStringForTrace());
#endif

            bool stopped = false;

            lock (_connectionContainers)
            {
                if (_connectionContainers.ContainsKey(hashHelper))
                {
                    SqlConnectionContainer container = _connectionContainers[hashHelper];
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Stop|DEP> {0}, hashtable hit, container: {1}", ObjectID, container.ObjectID);

                    server = container.Server;   // Return server, database, and queue info for use by calling SqlDependency.
                    database = container.Database;
                    queueService = container.Queue;
                    if (container.Stop(appDomainKey, out appDomainStop))
                    { // Stop can be blocking if refCount == 0 on container.
                        stopped = true;
                        _connectionContainers.Remove(hashHelper); // Remove from collection.
                    }
                }
                else
                {
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<Sc.SqlDependencyProcessDispatcher.Stop|DEP> {0}, hashtable miss.", ObjectID);
                }
            }

            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependencyProcessDispatcher.Stop|DEP> {0}, stopped: {1}", ObjectID, stopped);
            return stopped;
        }
        finally
        {
            SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
        }
    }

    // -----------------------------------------
    // END SqlDependencyProcessDispatcher class.
    // -----------------------------------------
}
