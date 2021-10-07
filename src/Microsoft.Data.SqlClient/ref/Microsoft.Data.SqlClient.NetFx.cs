// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ActiveDirectoryAuthenticationProvider/*'/>
    public sealed partial class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetIWin32WindowFunc/*'/>
        public void SetIWin32WindowFunc(System.Func<System.Windows.Forms.IWin32Window> iWin32WindowFunc) { }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/SqlClientFactory/*'/>
    public sealed partial class SqlClientFactory : System.Data.Common.DbProviderFactory, System.IServiceProvider
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CanCreateDataSourceEnumerator/*'/>
        public override bool CanCreateDataSourceEnumerator { get { throw null; } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateDataSourceEnumerator/*'/>
        public override System.Data.Common.DbDataSourceEnumerator CreateDataSourceEnumerator() { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreatePermission/*'/>
        public override System.Security.CodeAccessPermission CreatePermission(System.Security.Permissions.PermissionState state) { throw null; }
        object System.IServiceProvider.GetService(System.Type serviceType) { throw null; }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/SqlClientPermission/*'/>
    public sealed partial class SqlClientPermission : System.Data.Common.DBDataPermission
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="default"]/*'/>
        [System.ObsoleteAttribute("SqlClientPermission() has been deprecated.  Use the SqlClientPermission(PermissionState.None) constructor.  http://go.microsoft.com/fwlink/?linkid=14202", true)]
        public SqlClientPermission() : base(default(System.Security.Permissions.PermissionState)) { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="PermissionState"]//*'/>
        public SqlClientPermission(System.Security.Permissions.PermissionState state) : base(default(System.Security.Permissions.PermissionState)) { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="PermissionStateAndallowBlankPasswordBool"]//*'/>
        [System.ObsoleteAttribute("SqlClientPermission(PermissionState state, Boolean allowBlankPassword) has been deprecated.  Use the SqlClientPermission(PermissionState.None) constructor.  http://go.microsoft.com/fwlink/?linkid=14202", true)]
        public SqlClientPermission(System.Security.Permissions.PermissionState state, bool allowBlankPassword) : base(default(System.Security.Permissions.PermissionState)) { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/Add/*'/>
        public override void Add(string connectionString, string restrictions, System.Data.KeyRestrictionBehavior behavior) { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/Copy/*'/>
        public override System.Security.IPermission Copy() { throw null; }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/SqlClientPermissionAttribute/*'/>
    [System.AttributeUsageAttribute(System.AttributeTargets.Method | System.AttributeTargets.Constructor | System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed partial class SqlClientPermissionAttribute : System.Data.Common.DBDataPermissionAttribute
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/ctor/*'/>
        public SqlClientPermissionAttribute(System.Security.Permissions.SecurityAction action) : base(action) { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/CreatePermission/*'/>
        public override System.Security.IPermission CreatePermission() { throw null; }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/SqlCommand/*'/>
    public sealed partial class SqlCommand : System.Data.Common.DbCommand, System.ICloneable
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteNonQuery[@name="default"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteNonQuery() { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteNonQuery[@name="AsyncCallbackAndStateObject"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteNonQuery(System.AsyncCallback callback, object stateObject) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="default"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteReader() { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="AsyncCallbackAndstateObject"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteReader(System.AsyncCallback callback, object stateObject) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="AsyncCallbackAndstateObjectAndCommandBehavior"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteReader(System.AsyncCallback callback, object stateObject, System.Data.CommandBehavior behavior) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="CommandBehavior"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteReader(System.Data.CommandBehavior behavior) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteXmlReader[@name="default"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteXmlReader() { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteXmlReader[@name="AsyncCallbackAndstateObject"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteXmlReader(System.AsyncCallback callback, object stateObject) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/NotificationAutoEnlist/*'/>
        [System.ComponentModel.DefaultValueAttribute(true)]
        public bool NotificationAutoEnlist { get { throw null; } set { } }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/SqlConnection/*'/>
    public sealed partial class SqlConnection : System.Data.Common.DbConnection, System.ICloneable
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/EnlistDistributedTransaction/*'/>
        public void EnlistDistributedTransaction(System.EnterpriseServices.ITransaction transaction) { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/EnlistTransaction/*'/>
        public override void EnlistTransaction(System.Transactions.Transaction transaction) { }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/SqlConnectionStringBuilder/*'/>
    public sealed partial class SqlConnectionStringBuilder : System.Data.Common.DbConnectionStringBuilder
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectionReset/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DisplayNameAttribute("Connection Reset")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ObsoleteAttribute("ConnectionReset has been deprecated.  SqlConnection will ignore the 'connection reset' keyword and always reset the connection.")]
        public bool ConnectionReset { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ContextConnection/*'/>
        [System.ComponentModel.DisplayNameAttribute("Context Connection")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ObsoleteAttribute("ContextConnection has been deprecated.  SqlConnection will ignore the 'Context Connection' keyword.")]
        public bool ContextConnection { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/NetworkLibrary/*'/>
        [System.ComponentModel.DisplayNameAttribute("Network Library")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string NetworkLibrary { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TransparentNetworkIPResolution/*'/>
        [System.ComponentModel.DisplayNameAttribute("Transparent Network IP Resolution")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool TransparentNetworkIPResolution { get { throw null; } set { } }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/SqlDataAdapter/*'/>
    public sealed partial class SqlDataAdapter : System.Data.Common.DbDataAdapter, System.Data.IDataAdapter, System.Data.IDbDataAdapter, System.ICloneable
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/AddToBatch/*'/>
        protected override int AddToBatch(System.Data.IDbCommand command) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ClearBatch/*'/>
        protected override void ClearBatch() { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/CreateRowUpdatedEvent/*'/>
        protected override System.Data.Common.RowUpdatedEventArgs CreateRowUpdatedEvent(System.Data.DataRow dataRow, System.Data.IDbCommand command, System.Data.StatementType statementType, System.Data.Common.DataTableMapping tableMapping) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/CreateRowUpdatingEvent/*'/>
        protected override System.Data.Common.RowUpdatingEventArgs CreateRowUpdatingEvent(System.Data.DataRow dataRow, System.Data.IDbCommand command, System.Data.StatementType statementType, System.Data.Common.DataTableMapping tableMapping) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ExecuteBatch/*'/>
        protected override int ExecuteBatch() { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/GetBatchedParameter/*'/>
        protected override System.Data.IDataParameter GetBatchedParameter(int commandIdentifier, int parameterIndex) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/GetBatchedRecordsAffected/*'/>
        protected override bool GetBatchedRecordsAffected(int commandIdentifier, out int recordsAffected, out System.Exception error) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/InitializeBatching/*'/>
        protected override void InitializeBatching() { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/TerminateBatching/*'/>
        protected override void TerminateBatching() { }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SQLDebugging.xml' path='docs/members[@name="SQLDebugging"]/SQLDebugging/*'/>
    [System.Runtime.InteropServices.ClassInterfaceAttribute(System.Runtime.InteropServices.ClassInterfaceType.None)]
    [System.Runtime.InteropServices.ComVisibleAttribute(true)]
    [System.Runtime.InteropServices.GuidAttribute("afef65ad-4577-447a-a148-83acadd3d4b9")]
    [System.Security.Permissions.PermissionSetAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Name = "FullTrust")]
    public sealed partial class SQLDebugging
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SQLDebugging.xml' path='docs/members[@name="SQLDebugging"]/ctor/*'/>
        public SQLDebugging() { }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/SqlDependency/*'/>
    public sealed partial class SqlDependency
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctor2/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public SqlDependency() { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctorCommand/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public SqlDependency(Microsoft.Data.SqlClient.SqlCommand command) { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctorCommandOptionsTimeout/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public SqlDependency(Microsoft.Data.SqlClient.SqlCommand command, string options, int timeout) { }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StartConnectionString/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public static bool Start(string connectionString) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StartConnectionStringQueue/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public static bool Start(string connectionString, string queue) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StopConnectionString/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public static bool Stop(string connectionString) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StopConnectionStringQueue/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public static bool Stop(string connectionString, string queue) { throw null; }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/SqlException/*'/>
    public sealed partial class SqlException : System.Data.Common.DbException
    {
        private SqlException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SqlParameterCollection/*'/>
    public sealed partial class SqlParameterCollection : System.Data.Common.DbParameterCollection
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameValue/*'/>
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ObsoleteAttribute("Add(String parameterName, Object value) has been deprecated.  Use AddWithValue(String parameterName, Object value).  http://go.microsoft.com/fwlink/?linkid=14202", false)]
        public Microsoft.Data.SqlClient.SqlParameter Add(string parameterName, object value) { throw null; }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient.Server/InvalidUdtException.xml' path='docs/members[@name="InvalidUdtException"]/InvalidUdtException/*'/>
    public sealed partial class InvalidUdtException : System.SystemException
    {
        private InvalidUdtException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
    }
}
