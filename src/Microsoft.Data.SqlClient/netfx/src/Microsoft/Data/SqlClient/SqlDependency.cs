// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Versioning;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.Sql;

namespace Microsoft.Data.SqlClient
{
    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/SqlDependency/*' />
    public sealed class SqlDependency
    {

        // ---------------------------------------------------------------------------------------------------------
        // Private class encapsulating the user/identity information - either SQL Auth username or Windows identity.
        // ---------------------------------------------------------------------------------------------------------

        internal class IdentityUserNamePair
        {
            private DbConnectionPoolIdentity _identity;
            private string _userName;

            internal IdentityUserNamePair(DbConnectionPoolIdentity identity, string userName)
            {
                Debug.Assert((identity == null && userName != null) ||
                              (identity != null && userName == null), "Unexpected arguments!");
                _identity = identity;
                _userName = userName;
            }

            internal DbConnectionPoolIdentity Identity
            {
                get
                {
                    return _identity;
                }
            }

            internal string UserName
            {
                get
                {
                    return _userName;
                }
            }

            override public bool Equals(object value)
            {
                IdentityUserNamePair temp = (IdentityUserNamePair)value;

                bool result = false;

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
                    if (_identity != null)
                    {
                        if (_identity.Equals(temp._identity))
                        {
                            result = true;
                        }
                    }
                    else if (_userName == temp._userName)
                    {
                        result = true;
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
                else
                {
                    hashValue = _userName.GetHashCode();
                }

                return hashValue;
            }
        }
        // ----------------------------------------
        // END IdentityHashHelper private class.
        // ----------------------------------------

        // ----------------------------------------------------------------------
        // Private class encapsulating the database, service info and hash logic.
        // ----------------------------------------------------------------------

        private class DatabaseServicePair
        {
            private string _database;
            private string _service; // Store the value, but don't use for equality or hashcode!

            internal DatabaseServicePair(string database, string service)
            {
                Debug.Assert(database != null, "Unexpected argument!");
                _database = database;
                _service = service;
            }

            internal string Database
            {
                get
                {
                    return _database;
                }
            }

            internal string Service
            {
                get
                {
                    return _service;
                }
            }

            override public bool Equals(object value)
            {
                DatabaseServicePair temp = (DatabaseServicePair)value;

                bool result = false;

                if (null == temp)
                { // If passed value null - false.
                    result = false;
                }
                else if (this == temp)
                { // If instances equal - true.
                    result = true;
                }
                else if (_database == temp._database)
                {
                    result = true;
                }

                return result;
            }

            override public int GetHashCode()
            {
                return _database.GetHashCode();
            }
        }
        // ----------------------------------------
        // END IdentityHashHelper private class.
        // ----------------------------------------

        // ----------------------------------------------------------------------------
        // Private class encapsulating the event and it's registered execution context.
        // ----------------------------------------------------------------------------

        internal class EventContextPair
        {
            private OnChangeEventHandler _eventHandler;
            private ExecutionContext _context;
            private SqlDependency _dependency;
            private SqlNotificationEventArgs _args;

            static private ContextCallback _contextCallback = new ContextCallback(InvokeCallback);

            internal EventContextPair(OnChangeEventHandler eventHandler, SqlDependency dependency)
            {
                Debug.Assert(eventHandler != null && dependency != null, "Unexpected arguments!");
                _eventHandler = eventHandler;
                _context = ExecutionContext.Capture();
                _dependency = dependency;
            }

            override public bool Equals(object value)
            {
                EventContextPair temp = (EventContextPair)value;

                bool result = false;

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
                    if (_eventHandler == temp._eventHandler)
                    { // Handler for same delegates are reference equivalent.
                        result = true;
                    }
                }

                return result;
            }

            override public int GetHashCode()
            {
                return _eventHandler.GetHashCode();
            }

            internal void Invoke(SqlNotificationEventArgs args)
            {
                _args = args;
                ExecutionContext.Run(_context, _contextCallback, this);
            }

            private static void InvokeCallback(object eventContextPair)
            {
                EventContextPair pair = (EventContextPair)eventContextPair;
                pair._eventHandler(pair._dependency, (SqlNotificationEventArgs)pair._args);
            }
        }
        // ----------------------------------------
        // END EventContextPair private class.
        // ----------------------------------------

        // ----------------------------------------
        // Private class for restricting allowed types from deserialization.
        // ----------------------------------------

        private class SqlDependencyProcessDispatcherSerializationBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                // Deserializing an unexpected type can inject objects with malicious side effects.
                // If the type is unexpected, throw an exception to stop deserialization.
                if (typeName == nameof(SqlDependencyProcessDispatcher))
                {
                    return typeof(SqlDependencyProcessDispatcher);
                }
                else
                {
                    throw new ArgumentException("Unexpected type", nameof(typeName));
                }
            }
        }
        // ----------------------------------------
        // END SqlDependencyProcessDispatcherSerializationBinder private class.
        // ----------------------------------------

        // ----------------
        // Instance members
        // ----------------

        // SqlNotificationRequest required state members

        // Only used for SqlDependency.Id.
        private readonly string _id = Guid.NewGuid().ToString() + ";" + _appDomainKey;
        private string _options; // Concat of service & db, in the form "service=x;local database=y".
        private int _timeout;

        // Various SqlDependency required members
        private bool _dependencyFired = false;
        // SQL BU DT 382314 - we are required to implement our own event collection to preserve ExecutionContext on callback.
        private List<EventContextPair> _eventList = new List<EventContextPair>();
        private object _eventHandlerLock = new object(); // Lock for event serialization.
        // Track the time that this dependency should time out. If the server didn't send a change
        // notification or a time-out before this point then the client will perform a client-side 
        // timeout.
        private DateTime _expirationTime = DateTime.MaxValue;
        // Used for invalidation of dependencies based on which servers they rely upon.
        // It's possible we will over invalidate if unexpected server failure occurs (but not server down).
        private List<string> _serverList = new List<string>();

        // --------------
        // Static members
        // --------------

        private static object _startStopLock = new object();
        private static readonly string _appDomainKey = Guid.NewGuid().ToString();
        // Hashtable containing all information to match from a server, user, database triple to the service started for that 
        // triple.  For each server, there can be N users.  For each user, there can be N databases.  For each server, user, 
        // database, there can only be one service.
        private static Dictionary<string, Dictionary<IdentityUserNamePair, List<DatabaseServicePair>>> _serverUserHash =
                   new Dictionary<string, Dictionary<IdentityUserNamePair, List<DatabaseServicePair>>>(StringComparer.OrdinalIgnoreCase);
        private static SqlDependencyProcessDispatcher _processDispatcher = null;
        // The following two strings are used for AppDomain.CreateInstance.
        private static readonly string _assemblyName = (typeof(SqlDependencyProcessDispatcher)).Assembly.FullName;
        private static readonly string _typeName = (typeof(SqlDependencyProcessDispatcher)).FullName;

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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctor2/*' />
        // ------------
        // Constructors
        // ------------ 
        [System.Security.Permissions.HostProtectionAttribute(ExternalThreading = true)]
        public SqlDependency() : this(null, null, SQL.SqlDependencyTimeoutDefault)
        {
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctorCommand/*' />
        [System.Security.Permissions.HostProtectionAttribute(ExternalThreading = true)]
        public SqlDependency(SqlCommand command) : this(command, null, SQL.SqlDependencyTimeoutDefault)
        {
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctorCommandOptionsTimeout/*' />
        [System.Security.Permissions.HostProtectionAttribute(ExternalThreading = true)]
        public SqlDependency(SqlCommand command, string options, int timeout)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency|DEP> {0}, options: '{1}', timeout: '{2}'", ObjectID, options, timeout);
            try
            {
                if (InOutOfProcHelper.InProc)
                {
                    throw SQL.SqlDepCannotBeCreatedInProc();
                }
                if (timeout < 0)
                {
                    throw SQL.InvalidSqlDependencyTimeout("timeout");
                }
                _timeout = timeout;

                if (null != options)
                { // Ignore null value - will force to default.
                    _options = options;
                }

                AddCommandInternal(command);
                SqlDependencyPerAppDomainDispatcher.SingletonInstance.AddDependencyEntry(this); // Add dep to hashtable with Id.
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        // -----------------
        // Public Properties
        // -----------------
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/HasChanges/*' />
        [
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlDependency_HasChanges)
        ]
        public bool HasChanges
        {
            get
            {
                return _dependencyFired;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/Id/*' />
        [
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlDependency_Id)
        ]
        public string Id
        {
            get
            {
                return _id;
            }
        }

        // -------------------
        // Internal Properties
        // -------------------

        internal static string AppDomainKey
        {
            get
            {
                return _appDomainKey;
            }
        }

        internal DateTime ExpirationTime
        {
            get
            {
                return _expirationTime;
            }
        }

        internal string Options
        {
            get
            {
                string result = null;

                if (null != _options)
                {
                    result = _options;
                }

                return result;
            }
        }

        internal static SqlDependencyProcessDispatcher ProcessDispatcher
        {
            get
            {
                return _processDispatcher;
            }
        }

        internal int Timeout
        {
            get
            {
                return _timeout;
            }
        }

        // ------
        // Events
        // ------
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/OnChange/*' />
        [
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlDependency_OnChange)
        ]
        public event OnChangeEventHandler OnChange
        {
            // EventHandlers to be fired when dependency is notified.
            add
            {
                long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.OnChange-Add|DEP> {0}", ObjectID);

                try
                {
                    if (null != value)
                    {
                        SqlNotificationEventArgs sqlNotificationEvent = null;

                        lock (_eventHandlerLock)
                        {
                            if (_dependencyFired)
                            {
                                // If fired, fire the new event immediately.
                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.OnChange-Add|DEP> Dependency already fired, firing new event.");
                                sqlNotificationEvent = new SqlNotificationEventArgs(SqlNotificationType.Subscribe, SqlNotificationInfo.AlreadyChanged, SqlNotificationSource.Client);
                            }
                            else
                            {
                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.OnChange-Add|DEP> Dependency has not fired, adding new event.");
                                EventContextPair pair = new EventContextPair(value, this);
                                if (!_eventList.Contains(pair))
                                {
                                    _eventList.Add(pair);
                                }
                                else
                                {
                                    throw SQL.SqlDependencyEventNoDuplicate(); // SQL BU DT 382314
                                }
                            }
                        }

                        if (null != sqlNotificationEvent)
                        { // Delay firing the event until outside of lock.
                            value(this, sqlNotificationEvent);
                        }
                    }
                }
                finally
                {
                    SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
                }
            }
            remove
            {
                long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.OnChange-Remove|DEP> {0}", ObjectID);
                try
                {
                    if (null != value)
                    {
                        EventContextPair pair = new EventContextPair(value, this);
                        lock (_eventHandlerLock)
                        {
                            int index = _eventList.IndexOf(pair);
                            if (0 <= index)
                            {
                                _eventList.RemoveAt(index);
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

        // --------------
        // Public Methods
        // --------------
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/AddCommandDependency/*' />
        [
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlDependency_AddCommandDependency)
        ]
        public void AddCommandDependency(SqlCommand command)
        {
            // Adds command to dependency collection so we automatically create the SqlNotificationsRequest object
            // and listen for a notification for the added commands.
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.AddCommandDependency|DEP> {0}", ObjectID);
            try
            {
                if (command == null)
                {
                    throw ADP.ArgumentNull("command");
                }

                AddCommandInternal(command);
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        [System.Security.Permissions.ReflectionPermission(System.Security.Permissions.SecurityAction.Assert, MemberAccess = true)]
        private static ObjectHandle CreateProcessDispatcher(_AppDomain masterDomain)
        {
            return masterDomain.CreateInstance(_assemblyName, _typeName);
        }

        // ----------------------------------
        // Static Methods - public & internal
        // ----------------------------------

        // Method to obtain AppDomain reference and then obtain the reference to the process wide dispatcher for
        // Start() and Stop() method calls on the individual SqlDependency instances.
        // SxS: this method retrieves the primary AppDomain stored in native library. Since each System.Data.dll has its own copy of native
        // library, this call is safe in SxS
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        private static void ObtainProcessDispatcher()
        {
            byte[] nativeStorage = SNINativeMethodWrapper.GetData();

            if (nativeStorage == null)
            {
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ObtainProcessDispatcher|DEP> nativeStorage null, obtaining dispatcher AppDomain and creating ProcessDispatcher.");

#if DEBUG       // Possibly expensive, limit to debug.
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ObtainProcessDispatcher|DEP> AppDomain.CurrentDomain.FriendlyName: {0}", AppDomain.CurrentDomain.FriendlyName);

#endif
                _AppDomain masterDomain = SNINativeMethodWrapper.GetDefaultAppDomain();

                if (null != masterDomain)
                {
                    ObjectHandle handle = CreateProcessDispatcher(masterDomain);

                    if (null != handle)
                    {
                        SqlDependencyProcessDispatcher dependency = (SqlDependencyProcessDispatcher)handle.Unwrap();

                        if (null != dependency)
                        {
                            _processDispatcher = dependency.SingletonProcessDispatcher; // Set to static instance.

                            // Serialize and set in native.
                            ObjRef objRef = GetObjRef(_processDispatcher);
                            BinaryFormatter formatter = new BinaryFormatter();
                            MemoryStream stream = new MemoryStream();
                            GetSerializedObject(objRef, formatter, stream);
                            SNINativeMethodWrapper.SetData(stream.GetBuffer()); // Native will be forced to synchronize and not overwrite.
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ObtainProcessDispatcher|DEP|ERR> ERROR - ObjectHandle.Unwrap returned null!");
                            throw ADP.InternalError(ADP.InternalErrorCode.SqlDependencyObtainProcessDispatcherFailureObjectHandle);
                        }
                    }
                    else
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ObtainProcessDispatcher|DEP|ERR> ERROR - AppDomain.CreateInstance returned null!");
                        throw ADP.InternalError(ADP.InternalErrorCode.SqlDependencyProcessDispatcherFailureCreateInstance);
                    }
                }
                else
                {
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ObtainProcessDispatcher|DEP|ERR> ERROR - unable to obtain default AppDomain!");
                    throw ADP.InternalError(ADP.InternalErrorCode.SqlDependencyProcessDispatcherFailureAppDomain);
                }
            }
            else
            {
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ObtainProcessDispatcher|DEP> nativeStorage not null, obtaining existing dispatcher AppDomain and ProcessDispatcher.");

#if DEBUG       // Possibly expensive, limit to debug.
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ObtainProcessDispatcher|DEP> AppDomain.CurrentDomain.FriendlyName: {0}", AppDomain.CurrentDomain.FriendlyName);
#endif
                BinaryFormatter formatter = new BinaryFormatter();
                MemoryStream stream = new MemoryStream(nativeStorage);
                _processDispatcher = GetDeserializedObject(formatter, stream); // Deserialize and set for appdomain.
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ObtainProcessDispatcher|DEP> processDispatcher obtained, ID: {0}", _processDispatcher.ObjectID);
            }
        }

        // ---------------------------------------------------------
        // Static security asserted methods - limit scope of assert.
        // ---------------------------------------------------------

        [SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.RemotingConfiguration)]
        private static ObjRef GetObjRef(SqlDependencyProcessDispatcher _processDispatcher)
        {
            return RemotingServices.Marshal(_processDispatcher);
        }

        [SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.SerializationFormatter)]
        private static void GetSerializedObject(ObjRef objRef, BinaryFormatter formatter, MemoryStream stream)
        {
            formatter.Serialize(stream, objRef);
        }

        [SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.SerializationFormatter)]
        private static SqlDependencyProcessDispatcher GetDeserializedObject(BinaryFormatter formatter, MemoryStream stream)
        {
            // Use a custom SerializationBinder to restrict deserialized types to SqlDependencyProcessDispatcher.
            formatter.Binder = new SqlDependencyProcessDispatcherSerializationBinder();
            object result = formatter.Deserialize(stream);
            Debug.Assert(result.GetType() == typeof(SqlDependencyProcessDispatcher), "Unexpected type stored in native!");
            return (SqlDependencyProcessDispatcher)result;
        }

        // -------------------------
        // Static Start/Stop methods
        // -------------------------
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StartConnectionString/*' />
        [System.Security.Permissions.HostProtectionAttribute(ExternalThreading = true)]
        public static bool Start(string connectionString)
        {
            return Start(connectionString, null, true);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StartConnectionStringQueue/*' />
        [System.Security.Permissions.HostProtectionAttribute(ExternalThreading = true)]
        public static bool Start(string connectionString, string queue)
        {
            return Start(connectionString, queue, false);
        }

        internal static bool Start(string connectionString, string queue, bool useDefaults)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.Start|DEP> AppDomainKey: '{0}', queue: '{1}'", AppDomainKey, queue);
            try
            {
                // The following code exists in Stop as well.  It exists here to demand permissions as high in the stack
                // as possible.
                if (InOutOfProcHelper.InProc)
                {
                    throw SQL.SqlDepCannotBeCreatedInProc();
                }

                if (ADP.IsEmpty(connectionString))
                {
                    if (null == connectionString)
                    {
                        throw ADP.ArgumentNull("connectionString");
                    }
                    else
                    {
                        throw ADP.Argument("connectionString");
                    }
                }

                if (!useDefaults && ADP.IsEmpty(queue))
                { // If specified but null or empty, use defaults.
                    useDefaults = true;
                    queue = null; // Force to null - for proper hashtable comparison for default case.
                }

                // Create new connection options for demand on their connection string.  We modify the connection string
                // and assert on our modified string when we create the container.
                SqlConnectionString connectionStringObject = new SqlConnectionString(connectionString);
                connectionStringObject.DemandPermission();
                if (connectionStringObject.LocalDBInstance != null)
                {
                    LocalDBAPI.DemandLocalDBPermissions();
                }
                // End duplicate Start/Stop logic.

                bool errorOccurred = false;
                bool result = false;

                lock (_startStopLock)
                {
                    try
                    {
                        if (null == _processDispatcher)
                        { // Ensure _processDispatcher reference is present - inside lock.
                            ObtainProcessDispatcher();
                        }

                        if (useDefaults)
                        { // Default listener.
                            string server = null;
                            DbConnectionPoolIdentity identity = null;
                            string user = null;
                            string database = null;
                            string service = null;
                            bool appDomainStart = false;

                            RuntimeHelpers.PrepareConstrainedRegions();
                            try
                            { // CER to ensure that if Start succeeds we add to hash completing setup.
                                // Start using process wide default service/queue & database from connection string.
                                result = _processDispatcher.StartWithDefault(connectionString,
                                                                             out server,
                                                                             out identity,
                                                                             out user,
                                                                             out database,
                                                                             ref service,
                                                                                 _appDomainKey,
                                                                                 SqlDependencyPerAppDomainDispatcher.SingletonInstance,
                                                                             out errorOccurred,
                                                                             out appDomainStart);

                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.Start|DEP> Start (defaults) returned: '{0}', with service: '{1}', server: '{2}', database: '{3}'", result, service, server, database);
                            }
                            finally
                            {
                                if (appDomainStart && !errorOccurred)
                                { // If success, add to hashtable.
                                    IdentityUserNamePair identityUser = new IdentityUserNamePair(identity, user);
                                    DatabaseServicePair databaseService = new DatabaseServicePair(database, service);
                                    if (!AddToServerUserHash(server, identityUser, databaseService))
                                    {
                                        try
                                        {
                                            Stop(connectionString, queue, useDefaults, true);
                                        }
                                        catch (Exception e)
                                        { // Discard stop failure!
                                            if (!ADP.IsCatchableExceptionType(e))
                                            {
                                                throw;
                                            }

                                            ADP.TraceExceptionWithoutRethrow(e); // Discard failure, but trace for now.
                                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.Start|DEP|ERR> Exception occurred from Stop() after duplicate was found on Start().");
                                        }
                                        throw SQL.SqlDependencyDuplicateStart();
                                    }
                                }
                            }
                        }
                        else
                        { // Start with specified service/queue & database.
                            result = _processDispatcher.Start(connectionString,
                                                              queue,
                                                              _appDomainKey,
                                                              SqlDependencyPerAppDomainDispatcher.SingletonInstance);
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.Start|DEP> Start (user provided queue) returned: '{0}'", result);

                            // No need to call AddToServerDatabaseHash since if not using default queue user is required
                            // to provide options themselves.
                        }
                    }
                    catch (Exception e)
                    {
                        if (!ADP.IsCatchableExceptionType(e))
                        {
                            throw;
                        }

                        ADP.TraceExceptionWithoutRethrow(e); // Discard failure, but trace for now.

                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.Start|DEP|ERR> Exception occurred from _processDispatcher.Start(...), calling Invalidate(...).");
                        throw;
                    }
                }

                return result;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StopConnectionString/*' />
        [System.Security.Permissions.HostProtectionAttribute(ExternalThreading = true)]
        public static bool Stop(string connectionString)
        {
            return Stop(connectionString, null, true, false);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StopConnectionStringQueue/*' />
        [System.Security.Permissions.HostProtectionAttribute(ExternalThreading = true)]
        public static bool Stop(string connectionString, string queue)
        {
            return Stop(connectionString, queue, false, false);
        }

        internal static bool Stop(string connectionString, string queue, bool useDefaults, bool startFailed)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.Stop|DEP> AppDomainKey: '{0}', queue: '{1}'", AppDomainKey, queue);
            try
            {
                // The following code exists in Stop as well.  It exists here to demand permissions as high in the stack
                // as possible.
                if (InOutOfProcHelper.InProc)
                {
                    throw SQL.SqlDepCannotBeCreatedInProc();
                }

                if (ADP.IsEmpty(connectionString))
                {
                    if (null == connectionString)
                    {
                        throw ADP.ArgumentNull("connectionString");
                    }
                    else
                    {
                        throw ADP.Argument("connectionString");
                    }
                }

                if (!useDefaults && ADP.IsEmpty(queue))
                { // If specified but null or empty, use defaults.
                    useDefaults = true;
                    queue = null; // Force to null - for proper hashtable comparison for default case.
                }

                // Create new connection options for demand on their connection string.  We modify the connection string
                // and assert on our modified string when we create the container.
                SqlConnectionString connectionStringObject = new SqlConnectionString(connectionString);
                connectionStringObject.DemandPermission();
                if (connectionStringObject.LocalDBInstance != null)
                {
                    LocalDBAPI.DemandLocalDBPermissions();
                }
                // End duplicate Start/Stop logic.

                bool result = false;

                lock (_startStopLock)
                {
                    if (null != _processDispatcher)
                    { // If _processDispatcher null, no Start has been called.
                        try
                        {
                            string server = null;
                            DbConnectionPoolIdentity identity = null;
                            string user = null;
                            string database = null;
                            string service = null;

                            if (useDefaults)
                            {
                                bool appDomainStop = false;

                                RuntimeHelpers.PrepareConstrainedRegions();
                                try
                                { // CER to ensure that if Stop succeeds we remove from hash completing teardown.
                                    // Start using process wide default service/queue & database from connection string.
                                    result = _processDispatcher.Stop(connectionString,
                                                                     out server,
                                                                     out identity,
                                                                     out user,
                                                                     out database,
                                                                     ref service,
                                                                         _appDomainKey,
                                                                     out appDomainStop);
                                }
                                finally
                                {
                                    if (appDomainStop && !startFailed)
                                    { // If success, remove from hashtable.
                                        Debug.Assert(!ADP.IsEmpty(server) && !ADP.IsEmpty(database), "Server or Database null/Empty upon successful Stop()!");
                                        IdentityUserNamePair identityUser = new IdentityUserNamePair(identity, user);
                                        DatabaseServicePair databaseService = new DatabaseServicePair(database, service);
                                        RemoveFromServerUserHash(server, identityUser, databaseService);
                                    }
                                }
                            }
                            else
                            {
                                bool ignored = false;
                                result = _processDispatcher.Stop(connectionString,
                                                                 out server,
                                                                 out identity,
                                                                 out user,
                                                                 out database,
                                                                 ref queue,
                                                                     _appDomainKey,
                                                                 out ignored);
                                // No need to call RemoveFromServerDatabaseHash since if not using default queue user is required
                                // to provide options themselves.
                            }
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
                return result;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        // --------------------------------
        // General static utility functions
        // --------------------------------

        private static bool AddToServerUserHash(string server, IdentityUserNamePair identityUser, DatabaseServicePair databaseService)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.AddToServerUserHash|DEP> server: '{0}', database: '{1}', service: '{2}'", server, databaseService.Database, databaseService.Service);
            try
            {
                bool result = false;

                lock (_serverUserHash)
                {
                    Dictionary<IdentityUserNamePair, List<DatabaseServicePair>> identityDatabaseHash;

                    if (!_serverUserHash.ContainsKey(server))
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.AddToServerUserHash|DEP> Hash did not contain server, adding.");
                        identityDatabaseHash = new Dictionary<IdentityUserNamePair, List<DatabaseServicePair>>();
                        _serverUserHash.Add(server, identityDatabaseHash);
                    }
                    else
                    {
                        identityDatabaseHash = _serverUserHash[server];
                    }

                    List<DatabaseServicePair> databaseServiceList;

                    if (!identityDatabaseHash.ContainsKey(identityUser))
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.AddToServerUserHash|DEP> Hash contained server but not user, adding user.");
                        databaseServiceList = new List<DatabaseServicePair>();
                        identityDatabaseHash.Add(identityUser, databaseServiceList);
                    }
                    else
                    {
                        databaseServiceList = identityDatabaseHash[identityUser];
                    }

                    if (!databaseServiceList.Contains(databaseService))
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.AddToServerUserHash|DEP> Adding database.");
                        databaseServiceList.Add(databaseService);
                        result = true;
                    }
                    else
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.AddToServerUserHash|DEP|ERR> ERROR - hash already contained server, user, and database - we will throw!.");
                    }
                }

                return result;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        private static void RemoveFromServerUserHash(string server, IdentityUserNamePair identityUser, DatabaseServicePair databaseService)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.RemoveFromServerUserHash|DEP> server: '{0}', database: '{1}', service: '{2}'", server, databaseService.Database, databaseService.Service);
            try
            {
                lock (_serverUserHash)
                {
                    Dictionary<IdentityUserNamePair, List<DatabaseServicePair>> identityDatabaseHash;

                    if (_serverUserHash.ContainsKey(server))
                    {
                        identityDatabaseHash = _serverUserHash[server];

                        List<DatabaseServicePair> databaseServiceList;

                        if (identityDatabaseHash.ContainsKey(identityUser))
                        {
                            databaseServiceList = identityDatabaseHash[identityUser];

                            int index = databaseServiceList.IndexOf(databaseService);
                            if (index >= 0)
                            {
                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.RemoveFromServerUserHash|DEP> Hash contained server, user, and database - removing database.");
                                databaseServiceList.RemoveAt(index);

                                if (databaseServiceList.Count == 0)
                                {
                                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.RemoveFromServerUserHash|DEP> databaseServiceList count 0, removing the list for this server and user.");
                                    identityDatabaseHash.Remove(identityUser);

                                    if (identityDatabaseHash.Count == 0)
                                    {
                                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.RemoveFromServerUserHash|DEP> identityDatabaseHash count 0, removing the hash for this server.");
                                        _serverUserHash.Remove(server);
                                    }
                                }
                            }
                            else
                            {
                                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.RemoveFromServerUserHash|DEP|ERR> ERROR - hash contained server and user but not database!");
                                Debug.Assert(false, "Unexpected state - hash did not contain database!");
                            }
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.RemoveFromServerUserHash|DEP|ERR> ERROR - hash contained server but not user!");
                            Debug.Assert(false, "Unexpected state - hash did not contain user!");
                        }
                    }
                    else
                    {
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.RemoveFromServerUserHash|DEP|ERR> ERROR - hash did not contain server!");
                        Debug.Assert(false, "Unexpected state - hash did not contain server!");
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        internal static string GetDefaultComposedOptions(string server, string failoverServer, IdentityUserNamePair identityUser, string database)
        {
            // Server must be an exact match, but user and database only needs to match exactly if there is more than one 
            // for the given user or database passed.  That is ambiguious and we must fail.
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.GetDefaultComposedOptions|DEP> server: '{0}', failoverServer: '{1}', database: '{2}'", server, failoverServer, database);

            try
            {
                string result;

                lock (_serverUserHash)
                {
                    if (!_serverUserHash.ContainsKey(server))
                    {
                        if (0 == _serverUserHash.Count)
                        {
                            // Special error for no calls to start.
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.GetDefaultComposedOptions|DEP|ERR> ERROR - no start calls have been made, about to throw.");
                            throw SQL.SqlDepDefaultOptionsButNoStart();
                        }
                        else if (!ADP.IsEmpty(failoverServer) && _serverUserHash.ContainsKey(failoverServer))
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.GetDefaultComposedOptions|DEP> using failover server instead\n");
                            server = failoverServer;
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.GetDefaultComposedOptions|DEP|ERR> ERROR - not listening to this server, about to throw.");
                            throw SQL.SqlDependencyNoMatchingServerStart();
                        }
                    }

                    Dictionary<IdentityUserNamePair, List<DatabaseServicePair>> identityDatabaseHash = _serverUserHash[server];

                    List<DatabaseServicePair> databaseList = null;

                    if (!identityDatabaseHash.ContainsKey(identityUser))
                    {
                        if (identityDatabaseHash.Count > 1)
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.GetDefaultComposedOptions|DEP|ERR> ERROR - not listening for this user, " +
                               "but listening to more than one other user, about to throw.");
                            throw SQL.SqlDependencyNoMatchingServerStart();
                        }
                        else
                        {
                            // Since only one user, - use that.
                            // Foreach - but only one value present.
                            foreach (KeyValuePair<IdentityUserNamePair, List<DatabaseServicePair>> entry in identityDatabaseHash)
                            {
                                databaseList = entry.Value;
                                break; // Only iterate once.
                            }
                        }
                    }
                    else
                    {
                        databaseList = identityDatabaseHash[identityUser];
                    }

                    DatabaseServicePair pair = new DatabaseServicePair(database, null);
                    DatabaseServicePair resultingPair = null;
                    int index = databaseList.IndexOf(pair);
                    if (index != -1)
                    { // Exact match found, use it.
                        resultingPair = databaseList[index];
                    }

                    if (null != resultingPair)
                    { // Exact database match.
                        database = FixupServiceOrDatabaseName(resultingPair.Database); // Fixup in place.
                        string quotedService = FixupServiceOrDatabaseName(resultingPair.Service);
                        result = "Service=" + quotedService + ";Local Database=" + database;
                    }
                    else
                    { // No exact database match found.
                        if (databaseList.Count == 1)
                        { // If only one database for this server/user, use it.
                            object[] temp = databaseList.ToArray(); // Must copy, no other choice but foreach.
                            resultingPair = (DatabaseServicePair)temp[0];
                            Debug.Assert(temp.Length == 1, "If databaseList.Count==1, why does copied array have length other than 1?");
                            string quotedDatabase = FixupServiceOrDatabaseName(resultingPair.Database);
                            string quotedService = FixupServiceOrDatabaseName(resultingPair.Service);
                            result = "Service=" + quotedService + ";Local Database=" + quotedDatabase;
                        }
                        else
                        {
                            // More than one database for given server, ambiguous - fail the default case!
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.GetDefaultComposedOptions|DEP|ERR> ERROR - SqlDependency.Start called multiple times for this server/user, but no matching database.");
                            throw SQL.SqlDependencyNoMatchingServerDatabaseStart();
                        }
                    }
                }

                Debug.Assert(!ADP.IsEmpty(result), "GetDefaultComposedOptions should never return null or empty string!");
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.GetDefaultComposedOptions|DEP> resulting options: '{0}'.", result);
                return result;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        // ----------------
        // Internal Methods
        // ----------------

        // Called by SqlCommand upon execution of a SqlNotificationRequest class created by this dependency.  We 
        // use this list for a reverse lookup based on server.
        internal void AddToServerList(string server)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.AddToServerList|DEP> {0}, server: '{1}'", ObjectID, server);
            try
            {
                lock (_serverList)
                {
                    int index = _serverList.BinarySearch(server, StringComparer.OrdinalIgnoreCase);
                    if (0 > index)
                    {
                        // If less than 0, item was not found in list.
                        SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.AddToServerList|DEP> Server not present in hashtable, adding server: '{0}'.", server);
                        index = ~index; // BinarySearch returns the 2's compliment of where the item should be inserted to preserver a sorted list after insertion.
                        _serverList.Insert(index, server);
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        internal bool ContainsServer(string server)
        {
            lock (_serverList)
            {
                return _serverList.Contains(server);
            }
        }

        internal string ComputeHashAndAddToDispatcher(SqlCommand command)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.ComputeHashAndAddToDispatcher|DEP> {0}, SqlCommand: {1}", ObjectID, command.ObjectID);
            try
            {
                // Create a string representing the concatenation of the connection string, command text and .ToString on all parameter values.
                // This string will then be mapped to unique notification ID (new GUID).  We add the guid and the hash to the app domain
                // dispatcher to be able to map back to the dependency that needs to be fired for a notification of this
                // command.

                // VSTS 59821: add Connection string to prevent redundant notifications when same command is running against different databases or SQL servers
                // TODO: this solution only mitigates the majority of cases. We do not solve the case where the Connection string and the Command text are the same, 
                // but Database is different (default DB is assigned to user). In this case, users will still get redundant notifications of each other and the only 
                // workaround would be changing the command text to have some unique user information.
                string commandHash = ComputeCommandHash(command.Connection.ConnectionString, command); // calculate the string representation of command

                string idString = SqlDependencyPerAppDomainDispatcher.SingletonInstance.AddCommandEntry(commandHash, this); // Add to map.
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ComputeHashAndAddToDispatcher|DEP> computed id string: '{0}'.", idString);
                return idString;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        internal void Invalidate(SqlNotificationType type, SqlNotificationInfo info, SqlNotificationSource source)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.Invalidate|DEP> {0}", ObjectID);
            try
            {
                List<EventContextPair> eventList = null;

                lock (_eventHandlerLock)
                {
                    if (_dependencyFired &&
                        SqlNotificationInfo.AlreadyChanged != info &&
                        SqlNotificationSource.Client != source)
                    {

                        if (ExpirationTime < DateTime.UtcNow)
                        {
                            // There is a small window in which SqlDependencyPerAppDomainDispatcher.TimeoutTimerCallback
                            // raises Timeout event but before removing this event from the list. If notification is received from
                            // server in this case, we will hit this code path.
                            // It is safe to ignore this race condition because no event is sent to user and no leak happens.
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.Invalidate|DEP> ignore notification received after timeout!");
                        }
                        else
                        {
                            Debug.Assert(false, "Received notification twice - we should never enter this state!");
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.Invalidate|DEP|ERR> ERROR - notification received twice - we should never enter this state!");
                        }
                    }
                    else
                    {
                        // It is the invalidators responsibility to remove this dependency from the app domain static hash.
                        _dependencyFired = true;
                        eventList = _eventList;
                        _eventList = new List<EventContextPair>(); // Since we are firing the events, null so we do not fire again.
                    }
                }

                if (eventList != null)
                {
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.Invalidate|DEP> Firing events.");
                    foreach (EventContextPair pair in eventList)
                    {
                        pair.Invoke(new SqlNotificationEventArgs(type, info, source));
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        // This method is used by SqlCommand.
        internal void StartTimer(SqlNotificationRequest notificationRequest)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.StartTimer|DEP> {0}", ObjectID);
            try
            {
                if (_expirationTime == DateTime.MaxValue)
                {
                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.StartTimer|DEP> We've timed out, executing logic.");
                    int seconds = SQL.SqlDependencyServerTimeout;
                    if (0 != _timeout)
                    {
                        seconds = _timeout;
                    }
                    if (notificationRequest != null && notificationRequest.Timeout < seconds && notificationRequest.Timeout != 0)
                    {
                        seconds = notificationRequest.Timeout;
                    }

                    // VSDD 563926: we use UTC to check if SqlDependency is expired, need to use it here as well.
                    _expirationTime = DateTime.UtcNow.AddSeconds(seconds);
                    SqlDependencyPerAppDomainDispatcher.SingletonInstance.StartTimer(this);
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        // ---------------
        // Private Methods
        // ---------------

        private void AddCommandInternal(SqlCommand cmd)
        {
            if (cmd != null)
            {
                // Don't bother with BID if command null.
                long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.AddCommandInternal|DEP> {0}, SqlCommand: {1}", ObjectID, cmd.ObjectID);
                try
                {
                    SqlConnection connection = cmd.Connection;

                    if (cmd.Notification != null)
                    {
                        // Fail if cmd has notification that is not already associated with this dependency.
                        if (cmd._sqlDep == null || cmd._sqlDep != this)
                        {
                            SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.AddCommandInternal|DEP|ERR> ERROR - throwing command has existing SqlNotificationRequest exception.");
                            throw SQL.SqlCommandHasExistingSqlNotificationRequest();
                        }
                    }
                    else
                    {
                        bool needToInvalidate = false;

                        lock (_eventHandlerLock)
                        {
                            if (!_dependencyFired)
                            {
                                cmd.Notification = new SqlNotificationRequest();
                                cmd.Notification.Timeout = _timeout;

                                // Add the command - A dependancy should always map to a set of commands which haven't fired.
                                if (null != _options)
                                { // Assign options if user provided.
                                    cmd.Notification.Options = _options;
                                }

                                cmd._sqlDep = this;
                            }
                            else
                            {
                                // We should never be able to enter this state, since if we've fired our event list is cleared
                                // and the event method will immediately fire if a new event is added.  So, we should never have
                                // an event to fire in the event list once we've fired.
                                Debug.Assert(0 == _eventList.Count, "How can we have an event at this point?");
                                if (0 == _eventList.Count)
                                {
                                    // Keep logic just in case.
                                    SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.AddCommandInternal|DEP|ERR> ERROR - firing events, though it is unexpected we have events at this point.");
                                    needToInvalidate = true; // Delay invalidation until outside of lock.
                                }
                            }
                        }

                        if (needToInvalidate)
                        {
                            Invalidate(SqlNotificationType.Subscribe, SqlNotificationInfo.AlreadyChanged, SqlNotificationSource.Client);
                        }
                    }
                }
                finally
                {
                    SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
                }
            }
        }

        private string ComputeCommandHash(string connectionString, SqlCommand command)
        {
            long scopeID = SqlClientEventSource.Log.TryNotificationScopeEnterEvent("<sc.SqlDependency.ComputeCommandHash|DEP> {0}, SqlCommand: {1}", ObjectID, command.ObjectID);
            try
            {
                // Create a string representing the concatenation of the connection string, the command text and .ToString on all its parameter values.
                // This string will then be mapped to the notification ID.

                // All types should properly support a .ToString for the values except
                // byte[], char[], and XmlReader.

                // NOTE - I hope this doesn't come back to bite us.  :(
                StringBuilder builder = new StringBuilder();

                // add the Connection string and the Command text
                builder.AppendFormat("{0};{1}", connectionString, command.CommandText);

                // append params
                for (int i = 0; i < command.Parameters.Count; i++)
                {
                    object value = command.Parameters[i].Value;

                    if (value == null || value == DBNull.Value)
                    {
                        builder.Append("; NULL");
                    }
                    else
                    {
                        Type type = value.GetType();

                        if (type == typeof(Byte[]))
                        {
                            builder.Append(";");
                            byte[] temp = (byte[])value;
                            for (int j = 0; j < temp.Length; j++)
                            {
                                builder.Append(temp[j].ToString("x2", CultureInfo.InvariantCulture));
                            }
                        }
                        else if (type == typeof(Char[]))
                        {
                            builder.Append((char[])value);
                        }
                        else if (type == typeof(XmlReader))
                        {
                            builder.Append(";");
                            // Cannot .ToString XmlReader - just allocate GUID.
                            // This means if XmlReader is used, we will not reuse IDs.
                            builder.Append(Guid.NewGuid().ToString());
                        }
                        else
                        {
                            builder.Append(";");
                            builder.Append(value.ToString());
                        }
                    }
                }

                string result = builder.ToString();
                SqlClientEventSource.Log.TryNotificationTraceEvent("<sc.SqlDependency.ComputeCommandHash|DEP> ComputeCommandHash result: '{0}'.", result);
                return result;
            }
            finally
            {
                SqlClientEventSource.Log.TryNotificationScopeLeaveEvent(scopeID);
            }
        }

        // Basic copy of function in SqlConnection.cs for ChangeDatabase and similar functionality.  Since this will
        // only be used for default service and database provided by server, we do not need to worry about an already
        // quoted value.
        static internal string FixupServiceOrDatabaseName(string name)
        {
            if (!ADP.IsEmpty(name))
            {
                return "\"" + name.Replace("\"", "\"\"") + "\"";
            }
            else
            {
                return name;
            }
        }
    }
}
