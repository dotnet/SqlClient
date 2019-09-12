// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data
{
    public sealed partial class OperationAbortedException : System.SystemException
    {
        internal OperationAbortedException() { }
    }
}
namespace Microsoft.Data.Sql
{
    public sealed partial class SqlNotificationRequest
    {
        public SqlNotificationRequest() { }
        public SqlNotificationRequest(string userData, string options, int timeout) { }
        public string Options { get { throw null; } set { } }
        public int Timeout { get { throw null; } set { } }
        public string UserData { get { throw null; } set { } }
    }
}
namespace Microsoft.Data.SqlClient
{
    public enum ApplicationIntent
    {
        ReadOnly = 1,
        ReadWrite = 0
    }
    public delegate void OnChangeEventHandler(object sender, Microsoft.Data.SqlClient.SqlNotificationEventArgs e);
    public enum SortOrder
    {
        Ascending = 0,
        Descending = 1,
        Unspecified = -1
    }
    public abstract partial class SqlAuthenticationInitializer
    {
        protected SqlAuthenticationInitializer() { }
        public abstract void Initialize();
    }
    public enum SqlAuthenticationMethod
    {
        ActiveDirectoryIntegrated = 3,
        ActiveDirectoryInteractive = 4,
        ActiveDirectoryPassword = 2,
        NotSpecified = 0,
        SqlPassword = 1
    }
    public partial class SqlAuthenticationParameters
    {
        protected SqlAuthenticationParameters(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod, string serverName, string databaseName, string resource, string authority, string userId, string password, System.Guid connectionId) { }
        public Microsoft.Data.SqlClient.SqlAuthenticationMethod AuthenticationMethod { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string Authority { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.Guid ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string DatabaseName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string Password { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string Resource { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string ServerName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public abstract partial class SqlAuthenticationProvider
    {
        protected SqlAuthenticationProvider() { }
        public abstract System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlAuthenticationToken> AcquireTokenAsync(Microsoft.Data.SqlClient.SqlAuthenticationParameters parameters);
        public virtual void BeforeLoad(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod) { }
        public virtual void BeforeUnload(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod) { }
        public static Microsoft.Data.SqlClient.SqlAuthenticationProvider GetProvider(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod) { throw null; }
        public abstract bool IsSupported(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod);
        public static bool SetProvider(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod, Microsoft.Data.SqlClient.SqlAuthenticationProvider provider) { throw null; }
    }
    public partial class SqlAuthenticationToken
    {
        public SqlAuthenticationToken(string accessToken, System.DateTimeOffset expiresOn) { }
        public string AccessToken { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.DateTimeOffset ExpiresOn { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public sealed partial class SqlBulkCopy : System.IDisposable
    {
        public SqlBulkCopy(Microsoft.Data.SqlClient.SqlConnection connection) { }
        public SqlBulkCopy(Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlBulkCopyOptions copyOptions, Microsoft.Data.SqlClient.SqlTransaction externalTransaction) { }
        public SqlBulkCopy(string connectionString) { }
        public SqlBulkCopy(string connectionString, Microsoft.Data.SqlClient.SqlBulkCopyOptions copyOptions) { }
        public int BatchSize { get { throw null; } set { } }
        public int BulkCopyTimeout { get { throw null; } set { } }
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMappingCollection ColumnMappings { get { throw null; } }
        public string DestinationTableName { get { throw null; } set { } }
        public bool EnableStreaming { get { throw null; } set { } }
        public int NotifyAfter { get { throw null; } set { } }
        public event Microsoft.Data.SqlClient.SqlRowsCopiedEventHandler SqlRowsCopied { add { } remove { } }
        public void Close() { }
        void System.IDisposable.Dispose() { }
        public void WriteToServer(System.Data.Common.DbDataReader reader) { }
        public void WriteToServer(System.Data.DataTable table) { }
        public void WriteToServer(System.Data.DataTable table, System.Data.DataRowState rowState) { }
        public void WriteToServer(System.Data.DataRow[] rows) { }
        public void WriteToServer(System.Data.IDataReader reader) { }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.Common.DbDataReader reader) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.Common.DbDataReader reader, System.Threading.CancellationToken cancellationToken) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataRow[] rows) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataRow[] rows, System.Threading.CancellationToken cancellationToken) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataTable table) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataTable table, System.Data.DataRowState rowState) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataTable table, System.Data.DataRowState rowState, System.Threading.CancellationToken cancellationToken) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataTable table, System.Threading.CancellationToken cancellationToken) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.IDataReader reader) { throw null; }
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.IDataReader reader, System.Threading.CancellationToken cancellationToken) { throw null; }
    }
    public sealed partial class SqlBulkCopyColumnMapping
    {
        public SqlBulkCopyColumnMapping() { }
        public SqlBulkCopyColumnMapping(int sourceColumnOrdinal, int destinationOrdinal) { }
        public SqlBulkCopyColumnMapping(int sourceColumnOrdinal, string destinationColumn) { }
        public SqlBulkCopyColumnMapping(string sourceColumn, int destinationOrdinal) { }
        public SqlBulkCopyColumnMapping(string sourceColumn, string destinationColumn) { }
        public string DestinationColumn { get { throw null; } set { } }
        public int DestinationOrdinal { get { throw null; } set { } }
        public string SourceColumn { get { throw null; } set { } }
        public int SourceOrdinal { get { throw null; } set { } }
    }
    public sealed partial class SqlBulkCopyColumnMappingCollection : System.Collections.CollectionBase
    {
        internal SqlBulkCopyColumnMappingCollection() { }
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping this[int index] { get { throw null; } }
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping bulkCopyColumnMapping) { throw null; }
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(int sourceColumnIndex, int destinationColumnIndex) { throw null; }
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(int sourceColumnIndex, string destinationColumn) { throw null; }
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(string sourceColumn, int destinationColumnIndex) { throw null; }
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(string sourceColumn, string destinationColumn) { throw null; }
        public new void Clear() { }
        public bool Contains(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping value) { throw null; }
        public void CopyTo(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping[] array, int index) { }
        public int IndexOf(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping value) { throw null; }
        public void Insert(int index, Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping value) { }
        public void Remove(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping value) { }
        public new void RemoveAt(int index) { }
    }
    [System.FlagsAttribute]
    public enum SqlBulkCopyOptions
    {
        AllowEncryptedValueModifications = 64,
        CheckConstraints = 2,
        Default = 0,
        FireTriggers = 16,
        KeepIdentity = 1,
        KeepNulls = 8,
        TableLock = 4,
        UseInternalTransaction = 32,
    }
    public sealed partial class SqlClientFactory : System.Data.Common.DbProviderFactory
    {
        internal SqlClientFactory() { }
        public static readonly Microsoft.Data.SqlClient.SqlClientFactory Instance;
        public override System.Data.Common.DbCommand CreateCommand() { throw null; }
        public override System.Data.Common.DbCommandBuilder CreateCommandBuilder() { throw null; }
        public override System.Data.Common.DbConnection CreateConnection() { throw null; }
        public override System.Data.Common.DbConnectionStringBuilder CreateConnectionStringBuilder() { throw null; }
        public override System.Data.Common.DbDataAdapter CreateDataAdapter() { throw null; }
        public override System.Data.Common.DbParameter CreateParameter() { throw null; }
    }
    public static partial class SqlClientMetaDataCollectionNames
    {
        public static readonly string Columns;
        public static readonly string Databases;
        public static readonly string ForeignKeys;
        public static readonly string IndexColumns;
        public static readonly string Indexes;
        public static readonly string Parameters;
        public static readonly string ProcedureColumns;
        public static readonly string Procedures;
        public static readonly string Tables;
        public static readonly string UserDefinedTypes;
        public static readonly string Users;
        public static readonly string ViewColumns;
        public static readonly string Views;
    }
    [System.ComponentModel.DefaultEventAttribute("RecordsAffected")]
    [System.ComponentModel.DesignerAttribute("Microsoft.VSDesigner.Data.VS.SqlCommandDesigner, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    [System.ComponentModel.ToolboxItemAttribute(true)]
    public sealed partial class SqlCommand : System.Data.Common.DbCommand, System.ICloneable
    {
        public SqlCommand() { }
        public SqlCommand(string cmdText) { }
        public SqlCommand(string cmdText, Microsoft.Data.SqlClient.SqlConnection connection) { }
        public SqlCommand(string cmdText, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction) { }
        [System.ComponentModel.DefaultValueAttribute("")]
        [System.ComponentModel.EditorAttribute("Microsoft.VSDesigner.Data.SQL.Design.SqlCommandTextEditor, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public override string CommandText { get { throw null; } set { } }
        public override int CommandTimeout { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute(1)]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public override System.Data.CommandType CommandType { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute(null)]
        [System.ComponentModel.EditorAttribute("Microsoft.VSDesigner.Data.Design.DbConnectionEditor, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public new Microsoft.Data.SqlClient.SqlConnection Connection { get { throw null; } set { } }
        protected override System.Data.Common.DbConnection DbConnection { get { throw null; } set { } }
        protected override System.Data.Common.DbParameterCollection DbParameterCollection { get { throw null; } }
        protected override System.Data.Common.DbTransaction DbTransaction { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DefaultValueAttribute(true)]
        [System.ComponentModel.DesignOnlyAttribute(true)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override bool DesignTimeVisible { get { throw null; } set { } }
        public new Microsoft.Data.SqlClient.SqlParameterCollection Parameters { get { throw null; } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public new Microsoft.Data.SqlClient.SqlTransaction Transaction { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute(3)]
        public override System.Data.UpdateRowSource UpdatedRowSource { get { throw null; } set { } }
        public event System.Data.StatementCompletedEventHandler StatementCompleted { add { } remove { } }
        public System.IAsyncResult BeginExecuteNonQuery() { throw null; }
        public System.IAsyncResult BeginExecuteNonQuery(System.AsyncCallback callback, object stateObject) { throw null; }
        public System.IAsyncResult BeginExecuteReader() { throw null; }
        public System.IAsyncResult BeginExecuteReader(System.AsyncCallback callback, object stateObject) { throw null; }
        public System.IAsyncResult BeginExecuteReader(System.AsyncCallback callback, object stateObject, System.Data.CommandBehavior behavior) { throw null; }
        public System.IAsyncResult BeginExecuteReader(System.Data.CommandBehavior behavior) { throw null; }
        public System.IAsyncResult BeginExecuteXmlReader() { throw null; }
        public System.IAsyncResult BeginExecuteXmlReader(System.AsyncCallback callback, object stateObject) { throw null; }
        public override void Cancel() { }
        object System.ICloneable.Clone() { throw null; }
        public Microsoft.Data.SqlClient.SqlCommand Clone() { throw null; }
        protected override System.Data.Common.DbParameter CreateDbParameter() { throw null; }
        public new Microsoft.Data.SqlClient.SqlParameter CreateParameter() { throw null; }
        public int EndExecuteNonQuery(System.IAsyncResult asyncResult) { throw null; }
        public Microsoft.Data.SqlClient.SqlDataReader EndExecuteReader(System.IAsyncResult asyncResult) { throw null; }
        public System.Xml.XmlReader EndExecuteXmlReader(System.IAsyncResult asyncResult) { throw null; }
        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) { throw null; }
        protected override System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteDbDataReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken) { throw null; }
        public override int ExecuteNonQuery() { throw null; }
        public override System.Threading.Tasks.Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        public new Microsoft.Data.SqlClient.SqlDataReader ExecuteReader() { throw null; }
        public new Microsoft.Data.SqlClient.SqlDataReader ExecuteReader(System.Data.CommandBehavior behavior) { throw null; }
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync() { throw null; }
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync(System.Data.CommandBehavior behavior) { throw null; }
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken) { throw null; }
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        public override object ExecuteScalar() { throw null; }
        public override System.Threading.Tasks.Task<object> ExecuteScalarAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        public System.Xml.XmlReader ExecuteXmlReader() { throw null; }
        public System.Threading.Tasks.Task<System.Xml.XmlReader> ExecuteXmlReaderAsync() { throw null; }
        public System.Threading.Tasks.Task<System.Xml.XmlReader> ExecuteXmlReaderAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        public override void Prepare() { }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public Microsoft.Data.Sql.SqlNotificationRequest Notification { get { throw null; } set { } }
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Content)]
        public void ResetCommandTimeout() { }
    }
    public sealed class SqlCommandBuilder : System.Data.Common.DbCommandBuilder
    {
        public SqlCommandBuilder() { }
        public SqlCommandBuilder(Microsoft.Data.SqlClient.SqlDataAdapter adapter) { }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override System.Data.Common.CatalogLocation CatalogLocation { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override string CatalogSeparator { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute(null)]
        public new Microsoft.Data.SqlClient.SqlDataAdapter DataAdapter { get { throw null; } set { } }
        public static void DeriveParameters(Microsoft.Data.SqlClient.SqlCommand command) { }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override string QuotePrefix { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override string QuoteSuffix { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override string SchemaSeparator { get { throw null; } set { } }
        protected override void ApplyParameterInfo(System.Data.Common.DbParameter parameter, System.Data.DataRow datarow, System.Data.StatementType statementType, bool whereClause) { }
        public new Microsoft.Data.SqlClient.SqlCommand GetDeleteCommand() { throw null; }
        public new Microsoft.Data.SqlClient.SqlCommand GetDeleteCommand(bool useColumnsForParameterNames) { throw null; }
        public new Microsoft.Data.SqlClient.SqlCommand GetInsertCommand() { throw null; }
        public new Microsoft.Data.SqlClient.SqlCommand GetInsertCommand(bool useColumnsForParameterNames) { throw null; }
        protected override string GetParameterName(int parameterOrdinal) { throw null; }
        protected override string GetParameterName(string parameterName) { throw null; }
        protected override string GetParameterPlaceholder(int parameterOrdinal) { throw null; }
        protected override System.Data.DataTable GetSchemaTable(System.Data.Common.DbCommand srcCommand) { throw null; }
        public new Microsoft.Data.SqlClient.SqlCommand GetUpdateCommand() { throw null; }
        public new Microsoft.Data.SqlClient.SqlCommand GetUpdateCommand(bool useColumnsForParameterNames) { throw null; }
        protected override System.Data.Common.DbCommand InitializeCommand(System.Data.Common.DbCommand command) { throw null; }
        public override string QuoteIdentifier(string unquotedIdentifier) { throw null; }
        protected override void SetRowUpdatingHandler(System.Data.Common.DbDataAdapter adapter) { }
        public override string UnquoteIdentifier(string quotedIdentifier) { throw null; }
    }
    [System.ComponentModel.DefaultEventAttribute("InfoMessage")]
    public sealed partial class SqlConnection : System.Data.Common.DbConnection, System.ICloneable
    {
        public SqlConnection() { }
        public SqlConnection(string connectionString) { }
        public SqlConnection(string connectionString, Microsoft.Data.SqlClient.SqlCredential credential) { }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public string AccessToken { get { throw null; } set { } }
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public System.Guid ClientConnectionId { get { throw null; } }
        object System.ICloneable.Clone() { throw null; }
        [System.ComponentModel.DefaultValueAttribute("")]
        [System.ComponentModel.EditorAttribute("Microsoft.VSDesigner.Data.SQL.Design.SqlConnectionStringEditor, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ComponentModel.SettingsBindableAttribute(true)]
        public override string ConnectionString { get { throw null; } set { } }
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override int ConnectionTimeout { get { throw null; } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public Microsoft.Data.SqlClient.SqlCredential Credential { get { throw null; } set { } }
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override string Database { get { throw null; } }
        [System.ComponentModel.BrowsableAttribute(true)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override string DataSource { get { throw null; } }
        public bool FireInfoMessageEventOnUserErrors { get { throw null; } set { } }
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public int PacketSize { get { throw null; } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override string ServerVersion { get { throw null; } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override System.Data.ConnectionState State { get { throw null; } }
        [System.ComponentModel.DefaultValueAttribute(false)]
        public bool StatisticsEnabled { get { throw null; } set { } }
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public string WorkstationId { get { throw null; } }
        public event Microsoft.Data.SqlClient.SqlInfoMessageEventHandler InfoMessage { add { } remove { } }
        protected override System.Data.Common.DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) { throw null; }
        public new Microsoft.Data.SqlClient.SqlTransaction BeginTransaction() { throw null; }
        public new Microsoft.Data.SqlClient.SqlTransaction BeginTransaction(System.Data.IsolationLevel iso) { throw null; }
        public Microsoft.Data.SqlClient.SqlTransaction BeginTransaction(System.Data.IsolationLevel iso, string transactionName) { throw null; }
        public Microsoft.Data.SqlClient.SqlTransaction BeginTransaction(string transactionName) { throw null; }
        public override void ChangeDatabase(string database) { throw null; }
        public static void ChangePassword(string connectionString, Microsoft.Data.SqlClient.SqlCredential credential, System.Security.SecureString newSecurePassword) { throw null; }
        public static void ChangePassword(string connectionString, string newPassword) { throw null; }
        public static void ClearAllPools() { throw null; }
        public static void ClearPool(Microsoft.Data.SqlClient.SqlConnection connection) { throw null; }
        public override void Close() { throw null; }
        public new Microsoft.Data.SqlClient.SqlCommand CreateCommand() { throw null; }
        protected override System.Data.Common.DbCommand CreateDbCommand() { throw null; }
        public override System.Data.DataTable GetSchema() { throw null; }
        public override System.Data.DataTable GetSchema(string collectionName) { throw null; }
        public override System.Data.DataTable GetSchema(string collectionName, string[] restrictionValues) { throw null; }
        public override void Open() { }
        public override System.Threading.Tasks.Task OpenAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        public void ResetStatistics() { }
        public System.Collections.IDictionary RetrieveStatistics() { throw null; }
    }
    [System.ComponentModel.DefaultPropertyAttribute("DataSource")]
    public sealed partial class SqlConnectionStringBuilder : System.Data.Common.DbConnectionStringBuilder
    {
        public SqlConnectionStringBuilder() { }
        public SqlConnectionStringBuilder(string connectionString) { }
        [System.ComponentModel.DisplayNameAttribute("ApplicationIntent")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public Microsoft.Data.SqlClient.ApplicationIntent ApplicationIntent { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Application Name")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string ApplicationName { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("AttachDbFilename")]
        [System.ComponentModel.EditorAttribute("System.Windows.Forms.Design.FileNameEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string AttachDBFilename { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Authentication")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public Microsoft.Data.SqlClient.SqlAuthenticationMethod Authentication { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("ConnectRetryCount")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int ConnectRetryCount { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("ConnectRetryInterval")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int ConnectRetryInterval { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Connect Timeout")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int ConnectTimeout { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Current Language")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string CurrentLanguage { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Data Source")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string DataSource { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Encrypt")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool Encrypt { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Enlist")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool Enlist { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Failover Partner")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string FailoverPartner { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Initial Catalog")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string InitialCatalog { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Integrated Security")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool IntegratedSecurity { get { throw null; } set { } }
        public override bool IsFixedSize { get { throw null; } }
        public override object this[string keyword] { get { throw null; } set { } }
        public override System.Collections.ICollection Keys { get { throw null; } }
        [System.ComponentModel.DisplayNameAttribute("Load Balance Timeout")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int LoadBalanceTimeout { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Max Pool Size")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int MaxPoolSize { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Min Pool Size")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int MinPoolSize { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("MultipleActiveResultSets")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool MultipleActiveResultSets { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("MultiSubnetFailover")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool MultiSubnetFailover { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Packet Size")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int PacketSize { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Password")]
        [System.ComponentModel.PasswordPropertyTextAttribute(true)]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string Password { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Persist Security Info")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool PersistSecurityInfo { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Pooling")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool Pooling { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Replication")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool Replication { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Transaction Binding")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string TransactionBinding { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("TrustServerCertificate")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool TrustServerCertificate { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("Type System Version")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string TypeSystemVersion { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("User ID")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string UserID { get { throw null; } set { } }
        [System.ComponentModel.DisplayNameAttribute("User Instance")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool UserInstance { get { throw null; } set { } }
        public override System.Collections.ICollection Values { get { throw null; } }
        [System.ComponentModel.DisplayNameAttribute("Workstation ID")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string WorkstationID { get { throw null; } set { } }
        public override void Clear() { }
        public override bool ContainsKey(string keyword) { throw null; }
        public override bool Remove(string keyword) { throw null; }
        public override bool ShouldSerialize(string keyword) { throw null; }
        public override bool TryGetValue(string keyword, out object value) { throw null; }
    }
    public sealed class SqlCredential
    {
        public SqlCredential(string userId, System.Security.SecureString password) { }
        public System.Security.SecureString Password { get { throw null; } }
        public string UserId { get { throw null; } }
    }
    [System.ComponentModel.DefaultEventAttribute("RowUpdated")]
    [System.ComponentModel.DesignerAttribute("Microsoft.VSDesigner.Data.VS.SqlDataAdapterDesigner, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    [System.ComponentModel.ToolboxItemAttribute("Microsoft.VSDesigner.Data.VS.SqlDataAdapterToolboxItem, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    public sealed partial class SqlDataAdapter : System.Data.Common.DbDataAdapter, System.Data.IDataAdapter, System.Data.IDbDataAdapter, System.ICloneable
    {
        public SqlDataAdapter() { }
        public SqlDataAdapter(Microsoft.Data.SqlClient.SqlCommand selectCommand) { }
        public SqlDataAdapter(string selectCommandText, Microsoft.Data.SqlClient.SqlConnection selectConnection) { }
        public SqlDataAdapter(string selectCommandText, string selectConnectionString) { }
        [System.ComponentModel.DefaultValueAttribute(null)]
        [System.ComponentModel.EditorAttribute("Microsoft.VSDesigner.Data.Design.DBCommandEditor, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public new Microsoft.Data.SqlClient.SqlCommand DeleteCommand { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute(null)]
        [System.ComponentModel.EditorAttribute("Microsoft.VSDesigner.Data.Design.DBCommandEditor, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public new Microsoft.Data.SqlClient.SqlCommand InsertCommand { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute(null)]
        [System.ComponentModel.EditorAttribute("Microsoft.VSDesigner.Data.Design.DBCommandEditor, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public new Microsoft.Data.SqlClient.SqlCommand SelectCommand { get { throw null; } set { } }
        System.Data.IDbCommand System.Data.IDbDataAdapter.DeleteCommand { get { throw null; } set { } }
        System.Data.IDbCommand System.Data.IDbDataAdapter.InsertCommand { get { throw null; } set { } }
        System.Data.IDbCommand System.Data.IDbDataAdapter.SelectCommand { get { throw null; } set { } }
        System.Data.IDbCommand System.Data.IDbDataAdapter.UpdateCommand { get { throw null; } set { } }
        public override int UpdateBatchSize { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute(null)]
        [System.ComponentModel.EditorAttribute("Microsoft.VSDesigner.Data.Design.DBCommandEditor, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public new Microsoft.Data.SqlClient.SqlCommand UpdateCommand { get { throw null; } set { } }
        public event Microsoft.Data.SqlClient.SqlRowUpdatedEventHandler RowUpdated { add { } remove { } }
        public event Microsoft.Data.SqlClient.SqlRowUpdatingEventHandler RowUpdating { add { } remove { } }
        protected override void OnRowUpdated(System.Data.Common.RowUpdatedEventArgs value) { }
        protected override void OnRowUpdating(System.Data.Common.RowUpdatingEventArgs value) { }
        object System.ICloneable.Clone() { throw null; }
    }
    public partial class SqlDataReader : System.Data.Common.DbDataReader, System.Data.IDataReader, System.IDisposable
    {
        internal SqlDataReader() { }
        protected Microsoft.Data.SqlClient.SqlConnection Connection { get { throw null; } }
        public override int Depth { get { throw null; } }
        public override int FieldCount { get { throw null; } }
        public override bool HasRows { get { throw null; } }
        public override bool IsClosed { get { throw null; } }
        public override object this[int i] { get { throw null; } }
        public override object this[string name] { get { throw null; } }
        public override int RecordsAffected { get { throw null; } }
        public Microsoft.Data.SqlClient.DataClassification.SensitivityClassification SensitivityClassification { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public override int VisibleFieldCount { get { throw null; } }
        public override void Close() { }
        public override bool GetBoolean(int i) { throw null; }
        public override byte GetByte(int i) { throw null; }
        public override long GetBytes(int i, long dataIndex, byte[] buffer, int bufferIndex, int length) { throw null; }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override char GetChar(int i) { throw null; }
        public override long GetChars(int i, long dataIndex, char[] buffer, int bufferIndex, int length) { throw null; }
        public override string GetDataTypeName(int i) { throw null; }
        public override System.DateTime GetDateTime(int i) { throw null; }
        public virtual System.DateTimeOffset GetDateTimeOffset(int i) { throw null; }
        public override decimal GetDecimal(int i) { throw null; }
        public override double GetDouble(int i) { throw null; }
        public override System.Collections.IEnumerator GetEnumerator() { throw null; }
        public override System.Type GetFieldType(int i) { throw null; }
        public override T GetFieldValue<T>(int i) { throw null; }
        public override System.Threading.Tasks.Task<T> GetFieldValueAsync<T>(int i, System.Threading.CancellationToken cancellationToken) { throw null; }
        public override float GetFloat(int i) { throw null; }
        public override System.Guid GetGuid(int i) { throw null; }
        public override short GetInt16(int i) { throw null; }
        public override int GetInt32(int i) { throw null; }
        public override long GetInt64(int i) { throw null; }
        public override string GetName(int i) { throw null; }
        public override int GetOrdinal(string name) { throw null; }
        public override System.Type GetProviderSpecificFieldType(int i) { throw null; }
        public override object GetProviderSpecificValue(int i) { throw null; }
        public override int GetProviderSpecificValues(object[] values) { throw null; }
        public virtual System.Data.SqlTypes.SqlBinary GetSqlBinary(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlBoolean GetSqlBoolean(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlByte GetSqlByte(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlBytes GetSqlBytes(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlChars GetSqlChars(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlDateTime GetSqlDateTime(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlDecimal GetSqlDecimal(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlDouble GetSqlDouble(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlGuid GetSqlGuid(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlInt16 GetSqlInt16(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlInt32 GetSqlInt32(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlInt64 GetSqlInt64(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlMoney GetSqlMoney(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlSingle GetSqlSingle(int i) { throw null; }
        public virtual System.Data.SqlTypes.SqlString GetSqlString(int i) { throw null; }
        public virtual object GetSqlValue(int i) { throw null; }
        public virtual int GetSqlValues(object[] values) { throw null; }
        public virtual System.Data.SqlTypes.SqlXml GetSqlXml(int i) { throw null; }
        public override System.Data.DataTable GetSchemaTable() { throw null; }
        public override System.IO.Stream GetStream(int i) { throw null; }
        public override string GetString(int i) { throw null; }
        public override System.IO.TextReader GetTextReader(int i) { throw null; }
        public virtual System.TimeSpan GetTimeSpan(int i) { throw null; }
        public override object GetValue(int i) { throw null; }
        public override int GetValues(object[] values) { throw null; }
        public virtual System.Xml.XmlReader GetXmlReader(int i) { throw null; }
        protected internal bool IsCommandBehavior(System.Data.CommandBehavior condition) { throw null; }
        public override bool IsDBNull(int i) { throw null; }
        public override System.Threading.Tasks.Task<bool> IsDBNullAsync(int i, System.Threading.CancellationToken cancellationToken) { throw null; }
        public override bool NextResult() { throw null; }
        public override System.Threading.Tasks.Task<bool> NextResultAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        public override bool Read() { throw null; }
        public override System.Threading.Tasks.Task<bool> ReadAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        System.Data.IDataReader System.Data.IDataRecord.GetData(int i) { throw null; }
    }
    public sealed partial class SqlDependency
    {
        public SqlDependency() { }
        public SqlDependency(Microsoft.Data.SqlClient.SqlCommand command) { }
        public SqlDependency(Microsoft.Data.SqlClient.SqlCommand command, string options, int timeout) { }
        public bool HasChanges { get { throw null; } }
        public string Id { get { throw null; } }
        public event Microsoft.Data.SqlClient.OnChangeEventHandler OnChange { add { } remove { } }
        public void AddCommandDependency(Microsoft.Data.SqlClient.SqlCommand command) { }
        public static bool Start(string connectionString) { throw null; }
        public static bool Start(string connectionString, string queue) { throw null; }
        public static bool Stop(string connectionString) { throw null; }
        public static bool Stop(string connectionString, string queue) { throw null; }
    }
    public sealed partial class SqlError
    {
        internal SqlError() { }
        public byte Class { get { throw null; } }
        public int LineNumber { get { throw null; } }
        public string Message { get { throw null; } }
        public int Number { get { throw null; } }
        public string Procedure { get { throw null; } }
        public string Server { get { throw null; } }
        public string Source { get { throw null; } }
        public byte State { get { throw null; } }
        public override string ToString() { throw null; }
    }
    [System.ComponentModel.ListBindableAttribute(false)]
    public sealed partial class SqlErrorCollection : System.Collections.ICollection, System.Collections.IEnumerable
    {
        internal SqlErrorCollection() { }
        public int Count { get { throw null; } }
        public Microsoft.Data.SqlClient.SqlError this[int index] { get { throw null; } }
        bool System.Collections.ICollection.IsSynchronized { get { throw null; } }
        object System.Collections.ICollection.SyncRoot { get { throw null; } }
        public void CopyTo(System.Array array, int index) { }
        public void CopyTo(Microsoft.Data.SqlClient.SqlError[] array, int index) { }
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
    }
    public sealed partial class SqlException : System.Data.Common.DbException
    {
        internal SqlException() { }
        public byte Class { get { throw null; } }
        public System.Guid ClientConnectionId { get { throw null; } }
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Content)]
        public Microsoft.Data.SqlClient.SqlErrorCollection Errors { get { throw null; } }
        public int LineNumber { get { throw null; } }
        public int Number { get { throw null; } }
        public string Procedure { get { throw null; } }
        public string Server { get { throw null; } }
        public override string Source { get { throw null; } }
        public byte State { get { throw null; } }
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Flags = System.Security.Permissions.SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo si, System.Runtime.Serialization.StreamingContext context) { }
        public override string ToString() { throw null; }
    }
    public sealed partial class SqlInfoMessageEventArgs : System.EventArgs
    {
        internal SqlInfoMessageEventArgs() { }
        public Microsoft.Data.SqlClient.SqlErrorCollection Errors { get { throw null; } }
        public string Message { get { throw null; } }
        public string Source { get { throw null; } }
        public override string ToString() { throw null; }
    }
    public delegate void SqlInfoMessageEventHandler(object sender, Microsoft.Data.SqlClient.SqlInfoMessageEventArgs e);
    public partial class SqlNotificationEventArgs : System.EventArgs
    {
        public SqlNotificationEventArgs(Microsoft.Data.SqlClient.SqlNotificationType type, Microsoft.Data.SqlClient.SqlNotificationInfo info, Microsoft.Data.SqlClient.SqlNotificationSource source) { }
        public Microsoft.Data.SqlClient.SqlNotificationInfo Info { get { throw null; } }
        public Microsoft.Data.SqlClient.SqlNotificationSource Source { get { throw null; } }
        public Microsoft.Data.SqlClient.SqlNotificationType Type { get { throw null; } }
    }
    public enum SqlNotificationInfo
    {
        AlreadyChanged = -2,
        Alter = 5,
        Delete = 3,
        Drop = 4,
        Error = 7,
        Expired = 12,
        Insert = 1,
        Invalid = 9,
        Isolation = 11,
        Merge = 16,
        Options = 10,
        PreviousFire = 14,
        Query = 8,
        Resource = 13,
        Restart = 6,
        TemplateLimit = 15,
        Truncate = 0,
        Unknown = -1,
        Update = 2
    }
    public enum SqlNotificationSource
    {
        Client = -2,
        Data = 0,
        Database = 3,
        Environment = 6,
        Execution = 7,
        Object = 2,
        Owner = 8,
        Statement = 5,
        System = 4,
        Timeout = 1,
        Unknown = -1
    }
    public enum SqlNotificationType
    {
        Change = 0,
        Subscribe = 1,
        Unknown = -1
    }
    public sealed partial class SqlParameter : System.Data.Common.DbParameter, System.Data.IDataParameter, System.Data.IDbDataParameter, System.ICloneable
    {
        public SqlParameter() { }
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType) { }
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType, int size) { }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType, int size, System.Data.ParameterDirection direction, bool isNullable, byte precision, byte scale, string sourceColumn, System.Data.DataRowVersion sourceVersion, object value) { }
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType, int size, System.Data.ParameterDirection direction, byte precision, byte scale, string sourceColumn, System.Data.DataRowVersion sourceVersion, bool sourceColumnNullMapping, object value, string xmlSchemaCollectionDatabase, string xmlSchemaCollectionOwningSchema, string xmlSchemaCollectionName) { }
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType, int size, string sourceColumn) { }
        public SqlParameter(string parameterName, object value) { }
        object System.ICloneable.Clone() { throw null; }
        [System.ComponentModel.BrowsableAttribute(false)]
        public System.Data.SqlTypes.SqlCompareOptions CompareInfo { get { throw null; } set { } }
        public override System.Data.DbType DbType { get { throw null; } set { } }
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public override System.Data.ParameterDirection Direction { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute(false)]
        public override bool IsNullable { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        public int LocaleId { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        public int Offset { get { throw null; } set { } }
        public override string ParameterName { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute((byte)0)]
        public new byte Precision { get { throw null; } set { } }
        [System.ComponentModel.DefaultValueAttribute((byte)0)]
        public new byte Scale { get { throw null; } set { } }
        public override int Size { get { throw null; } set { } }
        public override string SourceColumn { get { throw null; } set { } }
        public override bool SourceColumnNullMapping { get { throw null; } set { } }
        public override System.Data.DataRowVersion SourceVersion { get { throw null; } set { } }
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.Data.Common.DbProviderSpecificTypePropertyAttribute(true)]
        public System.Data.SqlDbType SqlDbType { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public object SqlValue { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        public string TypeName { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        public string UdtTypeName { get { throw null; } set { } }
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ComponentModel.TypeConverterAttribute(typeof(System.ComponentModel.StringConverter))]
        public override object Value { get { throw null; } set { } }
        public string XmlSchemaCollectionDatabase { get { throw null; } set { } }
        public string XmlSchemaCollectionName { get { throw null; } set { } }
        public string XmlSchemaCollectionOwningSchema { get { throw null; } set { } }
        public override void ResetDbType() { }
        public void ResetSqlDbType() { }
        public override string ToString() { throw null; }
    }
    [System.ComponentModel.EditorAttribute("Microsoft.VSDesigner.Data.Design.DBParametersEditor, Microsoft.VSDesigner, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    [System.ComponentModel.ListBindableAttribute(false)]
    public sealed partial class SqlParameterCollection : System.Data.Common.DbParameterCollection
    {
        internal SqlParameterCollection() { }
        public override int Count { get { throw null; } }
        public override bool IsFixedSize { get { throw null; } }
        public override bool IsReadOnly { get { throw null; } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public new Microsoft.Data.SqlClient.SqlParameter this[int index] { get { throw null; } set { } }
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public new Microsoft.Data.SqlClient.SqlParameter this[string parameterName] { get { throw null; } set { } }
        public override object SyncRoot { get { throw null; } }
        public Microsoft.Data.SqlClient.SqlParameter Add(Microsoft.Data.SqlClient.SqlParameter value) { throw null; }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override int Add(object value) { throw null; }
        public Microsoft.Data.SqlClient.SqlParameter Add(string parameterName, System.Data.SqlDbType sqlDbType) { throw null; }
        public Microsoft.Data.SqlClient.SqlParameter Add(string parameterName, System.Data.SqlDbType sqlDbType, int size) { throw null; }
        public Microsoft.Data.SqlClient.SqlParameter Add(string parameterName, System.Data.SqlDbType sqlDbType, int size, string sourceColumn) { throw null; }
        public void AddRange(Microsoft.Data.SqlClient.SqlParameter[] values) { }
        public override void AddRange(System.Array values) { }
        public Microsoft.Data.SqlClient.SqlParameter AddWithValue(string parameterName, object value) { throw null; }
        public override void Clear() { }
        public bool Contains(Microsoft.Data.SqlClient.SqlParameter value) { throw null; }
        public override bool Contains(object value) { throw null; }
        public override bool Contains(string value) { throw null; }
        public override void CopyTo(System.Array array, int index) { }
        public void CopyTo(Microsoft.Data.SqlClient.SqlParameter[] array, int index) { }
        public override System.Collections.IEnumerator GetEnumerator() { throw null; }
        protected override System.Data.Common.DbParameter GetParameter(int index) { throw null; }
        protected override System.Data.Common.DbParameter GetParameter(string parameterName) { throw null; }
        public int IndexOf(Microsoft.Data.SqlClient.SqlParameter value) { throw null; }
        public override int IndexOf(object value) { throw null; }
        public override int IndexOf(string parameterName) { throw null; }
        public void Insert(int index, Microsoft.Data.SqlClient.SqlParameter value) { }
        public override void Insert(int index, object value) { }
        public void Remove(Microsoft.Data.SqlClient.SqlParameter value) { }
        public override void Remove(object value) { }
        public override void RemoveAt(int index) { }
        public override void RemoveAt(string parameterName) { }
        protected override void SetParameter(int index, System.Data.Common.DbParameter value) { }
        protected override void SetParameter(string parameterName, System.Data.Common.DbParameter value) { }
    }
    public partial class SqlRowsCopiedEventArgs : System.EventArgs
    {
        public SqlRowsCopiedEventArgs(long rowsCopied) { }
        public bool Abort { get { throw null; } set { } }
        public long RowsCopied { get { throw null; } }
    }
    public delegate void SqlRowsCopiedEventHandler(object sender, Microsoft.Data.SqlClient.SqlRowsCopiedEventArgs e);
    public sealed partial class SqlRowUpdatedEventArgs : System.Data.Common.RowUpdatedEventArgs
    {
        public SqlRowUpdatedEventArgs(System.Data.DataRow row, System.Data.IDbCommand command, System.Data.StatementType statementType, System.Data.Common.DataTableMapping tableMapping)
            : base(row, command, statementType, tableMapping) { }
        public new Microsoft.Data.SqlClient.SqlCommand Command { get { throw null; } }
    }
    public delegate void SqlRowUpdatedEventHandler(object sender, Microsoft.Data.SqlClient.SqlRowUpdatedEventArgs e);
    public sealed partial class SqlRowUpdatingEventArgs : System.Data.Common.RowUpdatingEventArgs
    {
        public SqlRowUpdatingEventArgs(System.Data.DataRow row, System.Data.IDbCommand command, System.Data.StatementType statementType, System.Data.Common.DataTableMapping tableMapping)
            : base(row, command, statementType, tableMapping) { }
        protected override System.Data.IDbCommand BaseCommand { get { throw null; } set { } }
        public new Microsoft.Data.SqlClient.SqlCommand Command { get { throw null; } set { } }
    }
    public delegate void SqlRowUpdatingEventHandler(object sender, Microsoft.Data.SqlClient.SqlRowUpdatingEventArgs e);
    public sealed partial class SqlTransaction : System.Data.Common.DbTransaction
    {
        internal SqlTransaction() { }
        public new Microsoft.Data.SqlClient.SqlConnection Connection { get { throw null; } }
        protected override System.Data.Common.DbConnection DbConnection { get { throw null; } }
        public override System.Data.IsolationLevel IsolationLevel { get { throw null; } }
        public override void Commit() { }
        protected override void Dispose(bool disposing) { }
        public override void Rollback() { }
        public void Rollback(string transactionName) { }
        public void Save(string savePointName) { }
    }
}
namespace Microsoft.Data.SqlClient.Server
{
    public enum DataAccessKind
    {
        None = 0,
        Read = 1
    }
    public enum Format
    {
        Unknown = 0,
        Native = 1,
        UserDefined = 2
    }
    public interface IBinarySerialize
    {
        void Read(System.IO.BinaryReader r);
        void Write(System.IO.BinaryWriter w);
    }
    public sealed partial class InvalidUdtException : System.SystemException
    {
        internal InvalidUdtException() { }
    }
    public partial class SqlDataRecord : System.Data.IDataRecord
    {
        public SqlDataRecord(params Microsoft.Data.SqlClient.Server.SqlMetaData[] metaData) { }
        public virtual int FieldCount { get { throw null; } }
        public virtual object this[int ordinal] { get { throw null; } }
        public virtual object this[string name] { get { throw null; } }
        public virtual bool GetBoolean(int ordinal) { throw null; }
        public virtual byte GetByte(int ordinal) { throw null; }
        public virtual long GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) { throw null; }
        public virtual char GetChar(int ordinal) { throw null; }
        public virtual long GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) { throw null; }
        System.Data.IDataReader System.Data.IDataRecord.GetData(int ordinal) { throw null; }
        public virtual string GetDataTypeName(int ordinal) { throw null; }
        public virtual System.DateTime GetDateTime(int ordinal) { throw null; }
        public virtual System.DateTimeOffset GetDateTimeOffset(int ordinal) { throw null; }
        public virtual decimal GetDecimal(int ordinal) { throw null; }
        public virtual double GetDouble(int ordinal) { throw null; }
        public virtual System.Type GetFieldType(int ordinal) { throw null; }
        public virtual float GetFloat(int ordinal) { throw null; }
        public virtual System.Guid GetGuid(int ordinal) { throw null; }
        public virtual short GetInt16(int ordinal) { throw null; }
        public virtual int GetInt32(int ordinal) { throw null; }
        public virtual long GetInt64(int ordinal) { throw null; }
        public virtual string GetName(int ordinal) { throw null; }
        public virtual int GetOrdinal(string name) { throw null; }
        public virtual System.Data.SqlTypes.SqlBinary GetSqlBinary(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlBoolean GetSqlBoolean(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlByte GetSqlByte(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlBytes GetSqlBytes(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlChars GetSqlChars(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlDateTime GetSqlDateTime(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlDecimal GetSqlDecimal(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlDouble GetSqlDouble(int ordinal) { throw null; }
        public virtual System.Type GetSqlFieldType(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlGuid GetSqlGuid(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlInt16 GetSqlInt16(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlInt32 GetSqlInt32(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlInt64 GetSqlInt64(int ordinal) { throw null; }
        public virtual Microsoft.Data.SqlClient.Server.SqlMetaData GetSqlMetaData(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlMoney GetSqlMoney(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlSingle GetSqlSingle(int ordinal) { throw null; }
        public virtual System.Data.SqlTypes.SqlString GetSqlString(int ordinal) { throw null; }
        public virtual object GetSqlValue(int ordinal) { throw null; }
        public virtual int GetSqlValues(object[] values) { throw null; }
        public virtual System.Data.SqlTypes.SqlXml GetSqlXml(int ordinal) { throw null; }
        public virtual string GetString(int ordinal) { throw null; }
        public virtual System.TimeSpan GetTimeSpan(int ordinal) { throw null; }
        public virtual object GetValue(int ordinal) { throw null; }
        public virtual int GetValues(object[] values) { throw null; }
        public virtual bool IsDBNull(int ordinal) { throw null; }
        public virtual void SetBoolean(int ordinal, bool value) { }
        public virtual void SetByte(int ordinal, byte value) { }
        public virtual void SetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) { }
        public virtual void SetChar(int ordinal, char value) { }
        public virtual void SetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) { }
        public virtual void SetDateTime(int ordinal, System.DateTime value) { }
        public virtual void SetDateTimeOffset(int ordinal, System.DateTimeOffset value) { }
        public virtual void SetDBNull(int ordinal) { }
        public virtual void SetDecimal(int ordinal, decimal value) { }
        public virtual void SetDouble(int ordinal, double value) { }
        public virtual void SetFloat(int ordinal, float value) { }
        public virtual void SetGuid(int ordinal, System.Guid value) { }
        public virtual void SetInt16(int ordinal, short value) { }
        public virtual void SetInt32(int ordinal, int value) { }
        public virtual void SetInt64(int ordinal, long value) { }
        public virtual void SetSqlBinary(int ordinal, System.Data.SqlTypes.SqlBinary value) { }
        public virtual void SetSqlBoolean(int ordinal, System.Data.SqlTypes.SqlBoolean value) { }
        public virtual void SetSqlByte(int ordinal, System.Data.SqlTypes.SqlByte value) { }
        public virtual void SetSqlBytes(int ordinal, System.Data.SqlTypes.SqlBytes value) { }
        public virtual void SetSqlChars(int ordinal, System.Data.SqlTypes.SqlChars value) { }
        public virtual void SetSqlDateTime(int ordinal, System.Data.SqlTypes.SqlDateTime value) { }
        public virtual void SetSqlDecimal(int ordinal, System.Data.SqlTypes.SqlDecimal value) { }
        public virtual void SetSqlDouble(int ordinal, System.Data.SqlTypes.SqlDouble value) { }
        public virtual void SetSqlGuid(int ordinal, System.Data.SqlTypes.SqlGuid value) { }
        public virtual void SetSqlInt16(int ordinal, System.Data.SqlTypes.SqlInt16 value) { }
        public virtual void SetSqlInt32(int ordinal, System.Data.SqlTypes.SqlInt32 value) { }
        public virtual void SetSqlInt64(int ordinal, System.Data.SqlTypes.SqlInt64 value) { }
        public virtual void SetSqlMoney(int ordinal, System.Data.SqlTypes.SqlMoney value) { }
        public virtual void SetSqlSingle(int ordinal, System.Data.SqlTypes.SqlSingle value) { }
        public virtual void SetSqlString(int ordinal, System.Data.SqlTypes.SqlString value) { }
        public virtual void SetSqlXml(int ordinal, System.Data.SqlTypes.SqlXml value) { }
        public virtual void SetString(int ordinal, string value) { }
        public virtual void SetTimeSpan(int ordinal, System.TimeSpan value) { }
        public virtual void SetValue(int ordinal, object value) { }
        public virtual int SetValues(params object[] values) { throw null; }
    }
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false), System.SerializableAttribute]
    public partial class SqlFunctionAttribute : System.Attribute
    {
        public SqlFunctionAttribute() { }
        public bool IsDeterministic { get { throw null; } set { } }
        public DataAccessKind DataAccess { get { throw null; } set { } }
        public SystemDataAccessKind SystemDataAccess { get { throw null; } set { } }
        public bool IsPrecise { get { throw null; } set { } }
        public string Name { get { throw null; } set { } }
        public string TableDefinition { get { throw null; } set { } }
        public string FillRowMethodName { get { throw null; } set { } }
    }
    public sealed partial class SqlMetaData
    {
        public SqlMetaData(string name, System.Data.SqlDbType dbType) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, byte precision, byte scale) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, byte precision, byte scale, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, byte precision, byte scale, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions, System.Type userDefinedType) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, byte precision, byte scale, long localeId, System.Data.SqlTypes.SqlCompareOptions compareOptions, System.Type userDefinedType, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, string database, string owningSchema, string objectName) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, string database, string owningSchema, string objectName, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType, string serverTypeName) { }
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType, string serverTypeName, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }

        public System.Data.SqlTypes.SqlCompareOptions CompareOptions { get { throw null; } }
        public System.Data.DbType DbType { get { throw null; } }
        public bool IsUniqueKey { get { throw null; } }
        public long LocaleId { get { throw null; } }
        public static long Max { get { throw null; } }
        public long MaxLength { get { throw null; } }
        public string Name { get { throw null; } }
        public byte Precision { get { throw null; } }
        public byte Scale { get { throw null; } }
        public Microsoft.Data.SqlClient.SortOrder SortOrder { get { throw null; } }
        public int SortOrdinal { get { throw null; } }
        public System.Data.SqlDbType SqlDbType { get { throw null; } }
        public System.Type Type { get { throw null; } }
        public string TypeName { get { throw null; } }
        public bool UseServerDefault { get { throw null; } }
        public string XmlSchemaCollectionDatabase { get { throw null; } }
        public string XmlSchemaCollectionName { get { throw null; } }
        public string XmlSchemaCollectionOwningSchema { get { throw null; } }
        public bool Adjust(bool value) { throw null; }
        public byte Adjust(byte value) { throw null; }
        public byte[] Adjust(byte[] value) { throw null; }
        public char Adjust(char value) { throw null; }
        public char[] Adjust(char[] value) { throw null; }
        public System.Data.SqlTypes.SqlBinary Adjust(System.Data.SqlTypes.SqlBinary value) { throw null; }
        public System.Data.SqlTypes.SqlBoolean Adjust(System.Data.SqlTypes.SqlBoolean value) { throw null; }
        public System.Data.SqlTypes.SqlByte Adjust(System.Data.SqlTypes.SqlByte value) { throw null; }
        public System.Data.SqlTypes.SqlBytes Adjust(System.Data.SqlTypes.SqlBytes value) { throw null; }
        public System.Data.SqlTypes.SqlChars Adjust(System.Data.SqlTypes.SqlChars value) { throw null; }
        public System.Data.SqlTypes.SqlDateTime Adjust(System.Data.SqlTypes.SqlDateTime value) { throw null; }
        public System.Data.SqlTypes.SqlDecimal Adjust(System.Data.SqlTypes.SqlDecimal value) { throw null; }
        public System.Data.SqlTypes.SqlDouble Adjust(System.Data.SqlTypes.SqlDouble value) { throw null; }
        public System.Data.SqlTypes.SqlGuid Adjust(System.Data.SqlTypes.SqlGuid value) { throw null; }
        public System.Data.SqlTypes.SqlInt16 Adjust(System.Data.SqlTypes.SqlInt16 value) { throw null; }
        public System.Data.SqlTypes.SqlInt32 Adjust(System.Data.SqlTypes.SqlInt32 value) { throw null; }
        public System.Data.SqlTypes.SqlInt64 Adjust(System.Data.SqlTypes.SqlInt64 value) { throw null; }
        public System.Data.SqlTypes.SqlMoney Adjust(System.Data.SqlTypes.SqlMoney value) { throw null; }
        public System.Data.SqlTypes.SqlSingle Adjust(System.Data.SqlTypes.SqlSingle value) { throw null; }
        public System.Data.SqlTypes.SqlString Adjust(System.Data.SqlTypes.SqlString value) { throw null; }
        public System.Data.SqlTypes.SqlXml Adjust(System.Data.SqlTypes.SqlXml value) { throw null; }
        public System.DateTime Adjust(System.DateTime value) { throw null; }
        public System.DateTimeOffset Adjust(System.DateTimeOffset value) { throw null; }
        public decimal Adjust(decimal value) { throw null; }
        public double Adjust(double value) { throw null; }
        public System.Guid Adjust(System.Guid value) { throw null; }
        public short Adjust(short value) { throw null; }
        public int Adjust(int value) { throw null; }
        public long Adjust(long value) { throw null; }
        public object Adjust(object value) { throw null; }
        public float Adjust(float value) { throw null; }
        public string Adjust(string value) { throw null; }
        public System.TimeSpan Adjust(System.TimeSpan value) { throw null; }
        public static Microsoft.Data.SqlClient.Server.SqlMetaData InferFromValue(object value, string name) { throw null; }
    }
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false), System.SerializableAttribute]
    public sealed partial class SqlMethodAttribute : SqlFunctionAttribute
    {
        public SqlMethodAttribute() { }
        public bool OnNullCall { get { throw null; } set { } }
        public bool IsMutator { get { throw null; } set { } }
        public bool InvokeIfReceiverIsNull { get { throw null; } set { } }
    }
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed partial class SqlUserDefinedAggregateAttribute : System.Attribute
    {
        public const int MaxByteSizeValue = 8000;
        public SqlUserDefinedAggregateAttribute(Format format) { }
        public int MaxByteSize { get { throw null; } set { } }
        public bool IsInvariantToDuplicates { get { throw null; } set { } }
        public bool IsInvariantToNulls { get { throw null; } set { } }
        public bool IsInvariantToOrder { get { throw null; } set { } }
        public bool IsNullIfEmpty { get { throw null; } set { } }
        public Format Format { get { throw null; } }
        public string Name { get { throw null; } set { } }
    }
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public sealed partial class SqlUserDefinedTypeAttribute : System.Attribute
    {
        public SqlUserDefinedTypeAttribute(Format format) { }
        public int MaxByteSize { get { throw null; } set { } }
        public bool IsFixedLength { get { throw null; } set { } }
        public bool IsByteOrdered { get { throw null; } set { } }
        public Format Format { get { throw null; } }
        public string ValidationMethodName { get { throw null; } set { } }
        public string Name { get { throw null; } set { } }
    }
    public enum SystemDataAccessKind
    {
        None = 0,
        Read = 1
    }
}
namespace Microsoft.Data.SqlClient.DataClassification
{
    public partial class ColumnSensitivity
    {
        public ColumnSensitivity(System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.SensitivityProperty> sensitivityProperties) { }
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.SensitivityProperty> SensitivityProperties { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public partial class InformationType
    {
        public InformationType(string name, string id) { }
        public string Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public partial class Label
    {
        public Label(string name, string id) { }
        public string Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public partial class SensitivityClassification
    {
        public SensitivityClassification(System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.Label> labels, System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.InformationType> informationTypes, System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.ColumnSensitivity> columnSensitivity) { }
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.ColumnSensitivity> ColumnSensitivities { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.InformationType> InformationTypes { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.Label> Labels { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public partial class SensitivityProperty
    {
        public SensitivityProperty(Microsoft.Data.SqlClient.DataClassification.Label label, Microsoft.Data.SqlClient.DataClassification.InformationType informationType) { }
        public Microsoft.Data.SqlClient.DataClassification.InformationType InformationType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public Microsoft.Data.SqlClient.DataClassification.Label Label { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
}
