// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// NOTE: The current Microsoft.VSDesigner editor attributes are implemented for System.Data.SqlClient, and are not publicly available.
// New attributes that are designed to work with Microsoft.Data.SqlClient and are publicly documented should be included in future.

[assembly: System.CLSCompliant(true)]
namespace Microsoft.Data
{
    /// <include file='../../../../doc/snippets/Microsoft.Data/OperationAbortedException.xml' path='docs/members[@name="OperationAbortedException"]/OperationAbortedException/*' />
    [System.Serializable]
    public sealed partial class OperationAbortedException : System.SystemException
    {
        internal OperationAbortedException() { }

        private OperationAbortedException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }

    }

    /// <include file='../../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlDbTypeExtensions/*' />
    public static class SqlDbTypeExtensions
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data/SqlDbTypeExtensions.xml' path='docs/members[@name="SqlDbTypeExtensions"]/SqlJson[@name="default"]' />
        public const System.Data.SqlDbType Json = (System.Data.SqlDbType)35;
    }
}

namespace Microsoft.Data.Sql
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/SqlNotificationRequest/*' />
    public sealed partial class SqlNotificationRequest
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/ctor1/*' />
        public SqlNotificationRequest() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/ctor2/*' />
        public SqlNotificationRequest(string userData, string options, int timeout) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/Options/*' />
        public string Options { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/Timeout/*' />
        public int Timeout { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlNotificationRequest.xml' path='docs/members[@name="SqlNotificationRequest"]/UserData/*' />
        public string UserData { get { throw null; } set { } }
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/SqlDataSourceEnumerator/*' />
    public sealed class SqlDataSourceEnumerator : System.Data.Common.DbDataSourceEnumerator
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/Instance/*' />  
        public static SqlDataSourceEnumerator Instance {get;}
        /// <include file='../../../../doc/snippets/Microsoft.Data.Sql/SqlDataSourceEnumerator.xml' path='docs/members[@name="SqlDataSourceEnumerator"]/GetDataSources/*' />      
        public override System.Data.DataTable GetDataSources(){ throw null; }
    }
}

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ActiveDirectoryAuthenticationProvider/*'/>
    public sealed class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor/*'/>
        public ActiveDirectoryAuthenticationProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor2/*'/>
        public ActiveDirectoryAuthenticationProvider(string applicationClientId) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ClearUserTokenCache/*'/>
        public static void ClearUserTokenCache() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor3/*'/>
        public ActiveDirectoryAuthenticationProvider(System.Func<Microsoft.Identity.Client.DeviceCodeResult, System.Threading.Tasks.Task> deviceCodeFlowCallbackMethod, string applicationClientId = null) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/AcquireTokenAsync/*'/>
        public override System.Threading.Tasks.Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetDeviceCodeFlowCallback/*'/>
        public void SetDeviceCodeFlowCallback(System.Func<Microsoft.Identity.Client.DeviceCodeResult, System.Threading.Tasks.Task> deviceCodeFlowCallbackMethod) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetAcquireAuthorizationCodeAsyncCallback/*'/>
        public void SetAcquireAuthorizationCodeAsyncCallback(System.Func<System.Uri, System.Uri, System.Threading.CancellationToken, System.Threading.Tasks.Task<System.Uri>> acquireAuthorizationCodeAsyncCallback) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetIWin32WindowFunc/*'/>
        public void SetIWin32WindowFunc(System.Func<System.Windows.Forms.IWin32Window> iWin32WindowFunc) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/IsSupported/*'/>
        public override bool IsSupported(SqlAuthenticationMethod authentication) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/BeforeLoad/*'/>
        public override void BeforeLoad(SqlAuthenticationMethod authentication) { }
        /// <include file='../../../..//doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/BeforeUnload/*'/>
        public override void BeforeUnload(SqlAuthenticationMethod authentication) { }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ApplicationIntent/*'/>
    public enum ApplicationIntent
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ReadOnly/*'/>
        ReadOnly = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ReadWrite/*'/>
        ReadWrite = 0
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/OnChangeEventHandler.xml' path='docs/members[@name="OnChangeEventHandler"]/*'/>
    public delegate void OnChangeEventHandler(object sender, Microsoft.Data.SqlClient.SqlNotificationEventArgs e);
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/PoolBlockingPeriod/*'/>
    public enum PoolBlockingPeriod
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/AlwaysBlock/*'/>
        AlwaysBlock = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/Auto/*'/>
        Auto = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/NeverBlock/*'/>
        NeverBlock = 2,
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SortOrder.xml' path='docs/members[@name="SortOrder"]/SortOrder/*'/>
    public enum SortOrder
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SortOrder.xml' path='docs/members[@name="SortOrder"]/Unspecified/*'/>
        Unspecified = -1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SortOrder.xml' path='docs/members[@name="SortOrder"]/Ascending/*'/>
        Ascending = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SortOrder.xml' path='docs/members[@name="SortOrder"]/Descending/*'/>
        Descending = 1
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/SqlAuthenticationInitializer/*'/>
    public abstract partial class SqlAuthenticationInitializer
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/ctor/*'/>
        protected SqlAuthenticationInitializer() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/Initialize/*'/>
        public abstract void Initialize();
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/SqlAuthenticationMethod/*'/>
    public enum SqlAuthenticationMethod
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/NotSpecified/*'/>
        NotSpecified = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/SqlPassword/*'/>
        SqlPassword = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryPassword/*'/>
        ActiveDirectoryPassword = 2,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryIntegrated/*'/>
        ActiveDirectoryIntegrated = 3,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryInteractive/*'/>
        ActiveDirectoryInteractive = 4,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryServicePrincipal/*'/>
        ActiveDirectoryServicePrincipal = 5,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryDeviceCodeFlow/*'/>
        ActiveDirectoryDeviceCodeFlow = 6,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryManagedIdentity/*'/>
        ActiveDirectoryManagedIdentity = 7,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryMSI/*'/>
        ActiveDirectoryMSI = 8,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryDefault/*'/>
        ActiveDirectoryDefault = 9,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationMethod.xml' path='docs/members[@name="SqlAuthenticationMethod"]/ActiveDirectoryWorkloadIdentity/*'/>
        ActiveDirectoryWorkloadIdentity = 10
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/SqlAuthenticationParameters/*'/>
    public class SqlAuthenticationParameters
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ctor/*'/>
        protected SqlAuthenticationParameters(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod, string serverName, string databaseName, string resource, string authority, string userId, string password, System.Guid connectionId, int connectionTimeout) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/AuthenticationMethod/*'/>
        public Microsoft.Data.SqlClient.SqlAuthenticationMethod AuthenticationMethod { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Authority/*'/>
        public string Authority { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ConnectionId/*'/>
        public System.Guid ConnectionId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/DatabaseName/*'/>
        public string DatabaseName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Password/*'/>
        public string Password { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/Resource/*'/>
        public string Resource { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ServerName/*'/>
        public string ServerName { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/UserId/*'/>
        public string UserId { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationParameters.xml' path='docs/members[@name="SqlAuthenticationParameters"]/ConnectionTimeout/*'/>
        public int ConnectionTimeout { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SqlAuthenticationProvider/*'/>
    public abstract partial class SqlAuthenticationProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/ctor/*'/>
        protected SqlAuthenticationProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/AcquireTokenAsync/*'/>
        public abstract System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlAuthenticationToken> AcquireTokenAsync(Microsoft.Data.SqlClient.SqlAuthenticationParameters parameters);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/BeforeLoad/*'/>
        public virtual void BeforeLoad(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/BeforeUnload/*'/>
        public virtual void BeforeUnload(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/GetProvider/*'/>
        public static Microsoft.Data.SqlClient.SqlAuthenticationProvider GetProvider(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/IsSupported/*'/>
        public abstract bool IsSupported(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SetProvider/*'/>
        public static bool SetProvider(Microsoft.Data.SqlClient.SqlAuthenticationMethod authenticationMethod, Microsoft.Data.SqlClient.SqlAuthenticationProvider provider) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/SqlAuthenticationToken/*'/>
    public partial class SqlAuthenticationToken
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ctor/*'/>
        public SqlAuthenticationToken(string accessToken, System.DateTimeOffset expiresOn) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/AccessToken/*'/>
        public string AccessToken { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ExpiresOn/*'/>
        public System.DateTimeOffset ExpiresOn { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/SqlBulkCopy/*'/>
    public sealed partial class SqlBulkCopy : System.IDisposable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/ctor[@name="SqlConnectionParameter"]/*'/>
        public SqlBulkCopy(Microsoft.Data.SqlClient.SqlConnection connection) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/ctor[@name="SqlConnectionAndSqlBulkCopyOptionAndSqlTransactionParameters"]/*'/>
        public SqlBulkCopy(Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlBulkCopyOptions copyOptions, Microsoft.Data.SqlClient.SqlTransaction externalTransaction) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/ctor[@name="ConnectionStringParameter"]/*'/>
        public SqlBulkCopy(string connectionString) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/ctor[@name="ConnectionStringAndSqlBulkCopyOptionsParameters"]/*'/>
        public SqlBulkCopy(string connectionString, Microsoft.Data.SqlClient.SqlBulkCopyOptions copyOptions) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/BatchSize/*'/>
        public int BatchSize { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/BulkCopyTimeout/*'/>
        public int BulkCopyTimeout { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/ColumnMappings/*'/>
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMappingCollection ColumnMappings { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/ColumnOrderHints/*'/>
        public SqlBulkCopyColumnOrderHintCollection ColumnOrderHints { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/DestinationTableName/*'/>
        public string DestinationTableName { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/EnableStreaming/*'/>
        public bool EnableStreaming { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/NotifyAfter/*'/>
        public int NotifyAfter { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/RowsCopied/*'/>
        public int RowsCopied { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/RowsCopied64/*'/>
        public long RowsCopied64 { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/SqlRowsCopied/*'/>
        public event Microsoft.Data.SqlClient.SqlRowsCopiedEventHandler SqlRowsCopied { add { } remove { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/Close/*'/>
        public void Close() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/System.IDisposable.Dispose/*'/>
        void System.IDisposable.Dispose() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServer[@name="DbDataReaderParameter"]/*'/>
        public void WriteToServer(System.Data.Common.DbDataReader reader) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServer[@name="DataTableParameter"]/*'/>
        public void WriteToServer(System.Data.DataTable table) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServer[@name="DataTableAndRowStateParameters"]/*'/>
        public void WriteToServer(System.Data.DataTable table, System.Data.DataRowState rowState) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServer[@name="DataRowParameter"]/*'/>
        public void WriteToServer(System.Data.DataRow[] rows) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServer[@name="IDataReaderParameter"]/*'/>
        public void WriteToServer(System.Data.IDataReader reader) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DbDataReaderParameter"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.Common.DbDataReader reader) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DbDataReaderAndCancellationTokenParameters"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.Common.DbDataReader reader, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataRowParameter"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataRow[] rows) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataRowAndCancellationTokenParameters"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataRow[] rows, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataTableParameter"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataTable table) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataTableAndDataRowStateParameter"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataTable table, System.Data.DataRowState rowState) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataTableAndDataRowStateAndCancellationTokenParameters"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataTable table, System.Data.DataRowState rowState, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataTableAndCancellationTokenParameters"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.DataTable table, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="IDataReaderParameter"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.IDataReader reader) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="IDataReaderAndCancellationTokenParameters"]/*'/>
        public System.Threading.Tasks.Task WriteToServerAsync(System.Data.IDataReader reader, System.Threading.CancellationToken cancellationToken) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/SqlBulkCopyColumnMapping/*'/>
    public sealed partial class SqlBulkCopyColumnMapping
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="default"]/*'/>
        public SqlBulkCopyColumnMapping() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnOrdinalIntegerAnddestinationOrdinalInteger"]/*'/>
        public SqlBulkCopyColumnMapping(int sourceColumnOrdinal, int destinationOrdinal) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnOrdinalIntegerAnddestinationColumnString"]/*'/>
        public SqlBulkCopyColumnMapping(int sourceColumnOrdinal, string destinationColumn) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnStringAnddestinationOrdinalInteger"]/*'/>
        public SqlBulkCopyColumnMapping(string sourceColumn, int destinationOrdinal) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnStringAnddestinationColumnString"]/*'/>
        public SqlBulkCopyColumnMapping(string sourceColumn, string destinationColumn) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/DestinationColumn/*'/>
        public string DestinationColumn { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/DestinationOrdinal/*'/>
        public int DestinationOrdinal { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/SourceColumn/*'/>
        public string SourceColumn { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/SourceOrdinal/*'/>
        public int SourceOrdinal { get { throw null; } set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/SqlBulkCopyColumnMappingCollection/*'/>
    public sealed partial class SqlBulkCopyColumnMappingCollection : System.Collections.CollectionBase
    {
        internal SqlBulkCopyColumnMappingCollection() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Item/*'/>
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping this[int index] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="bulkCopyColumnMappingParameter"]/*'/>
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping bulkCopyColumnMapping) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnIndexIntegerAnddestinationColumnIndexInteger"]/*'/>
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(int sourceColumnIndex, int destinationColumnIndex) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnIndexIntegerAnddestinationColumnString"]/*'/>
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(int sourceColumnIndex, string destinationColumn) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnStringAnddestinationColumnIndexInteger"]/*'/>
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(string sourceColumn, int destinationColumnIndex) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnStringAnddestinationColumnString"]/*'/>
        public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(string sourceColumn, string destinationColumn) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Clear/*'/>
        public new void Clear() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Contains/*'/>
        public bool Contains(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/CopyTo/*'/>
        public void CopyTo(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping[] array, int index) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/IndexOf/*'/>
        public int IndexOf(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Insert/*'/>
        public void Insert(int index, Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Remove/*'/>
        public void Remove(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/RemoveAt/*'/>
        public new void RemoveAt(int index) { }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHint.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHint"]/SqlBulkCopyColumnOrderHint/*'/>
    public sealed class SqlBulkCopyColumnOrderHint
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHint.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHint"]/ctor[@name="columnStringAndsortOrderSortOrder"]/*'/>
        public SqlBulkCopyColumnOrderHint(string column, SortOrder sortOrder) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHint.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHint"]/Column/*'/>
        public string Column { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHint.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHint"]/SortOrder/*'/>
        public SortOrder SortOrder { get { throw null; } set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/SqlBulkCopyColumnOrderHintCollection/*'/>
    public sealed class SqlBulkCopyColumnOrderHintCollection : System.Collections.CollectionBase
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Item/*'/>
        public SqlBulkCopyColumnOrderHint this[int index] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Add[@name="columnOrderHintParameter"]/*'/>
        public SqlBulkCopyColumnOrderHint Add(SqlBulkCopyColumnOrderHint columnOrderHint) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Add[@name="columnStringAndsortOrderSortOrder"]/*'/>
        public SqlBulkCopyColumnOrderHint Add(string column, SortOrder sortOrder) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Clear/*'/>
        public new void Clear() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Contains/*'/>
        public bool Contains(SqlBulkCopyColumnOrderHint value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/CopyTo/*'/>
        public void CopyTo(SqlBulkCopyColumnOrderHint[] array, int index) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/IndexOf/*'/>
        public int IndexOf(SqlBulkCopyColumnOrderHint value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Insert/*'/>
        public void Insert(int index, SqlBulkCopyColumnOrderHint columnOrderHint) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/Remove/*'/>
        public void Remove(SqlBulkCopyColumnOrderHint columnOrderHint) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnOrderHintCollection.xml' path='docs/members[@name="SqlBulkCopyColumnOrderHintCollection"]/RemoveAt/*'/>
        public new void RemoveAt(int index) { }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/SqlBulkCopyOptions/*'/>
    [System.FlagsAttribute]
    public enum SqlBulkCopyOptions
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/AllowEncryptedValueModifications/*'/>
        AllowEncryptedValueModifications = 64,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/CheckConstraints/*'/>
        CheckConstraints = 2,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/Default/*'/>
        Default = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/FireTriggers/*'/>
        FireTriggers = 16,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/KeepIdentity/*'/>
        KeepIdentity = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/KeepNulls/*'/>
        KeepNulls = 8,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/TableLock/*'/>
        TableLock = 4,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyOptions.xml' path='docs/members[@name="SqlBulkCopyOptions"]/UseInternalTransaction/*'/>
        UseInternalTransaction = 32,
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/SqlClientFactory/*'/>
    public sealed partial class SqlClientFactory : System.Data.Common.DbProviderFactory, System.IServiceProvider
    {
        internal SqlClientFactory() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/Instance/*'/>
        public static readonly Microsoft.Data.SqlClient.SqlClientFactory Instance;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CanCreateDataSourceEnumerator/*'/>
        public override bool CanCreateDataSourceEnumerator { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateCommand/*'/>
        public override System.Data.Common.DbCommand CreateCommand() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateCommandBuilder/*'/>
        public override System.Data.Common.DbCommandBuilder CreateCommandBuilder() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateConnection/*'/>
        public override System.Data.Common.DbConnection CreateConnection() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateConnectionStringBuilder/*'/>
        public override System.Data.Common.DbConnectionStringBuilder CreateConnectionStringBuilder() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateDataAdapter/*'/>
        public override System.Data.Common.DbDataAdapter CreateDataAdapter() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateDataSourceEnumerator/*'/>
        public override System.Data.Common.DbDataSourceEnumerator CreateDataSourceEnumerator() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateParameter/*'/>
        public override System.Data.Common.DbParameter CreateParameter() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreatePermission/*'/>
        public override System.Security.CodeAccessPermission CreatePermission(System.Security.Permissions.PermissionState state) { throw null; }
        object System.IServiceProvider.GetService(System.Type serviceType) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/SqlClientLogger/*'/>
    public partial class SqlClientLogger
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/ctor/*'/>
        public SqlClientLogger() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/IsLoggingEnabled/*'/>
        public bool IsLoggingEnabled { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/LogWarning/*'/>
        public void LogWarning(string type, string method, string message) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/LogAssert/*'/>
        public bool LogAssert(bool value, string type, string method, string message) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/LogError/*'/>
        public void LogError(string type, string method, string message) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/LogInfo/*'/>
        public void LogInfo(string type, string method, string message) { }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/SqlClientMetaDataCollectionNames/*'/>
    public static partial class SqlClientMetaDataCollectionNames
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/Columns/*'/>
        public static readonly string Columns;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/Databases/*'/>
        public static readonly string Databases;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/ForeignKeys/*'/>
        public static readonly string ForeignKeys;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/IndexColumns/*'/>
        public static readonly string IndexColumns;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/Indexes/*'/>
        public static readonly string Indexes;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/ProcedureParameters/*'/>
        public static readonly string ProcedureParameters;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/Procedures/*'/>
        public static readonly string Procedures;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/Tables/*'/>
        public static readonly string Tables;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/UserDefinedTypes/*'/>
        public static readonly string UserDefinedTypes;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/Users/*'/>
        public static readonly string Users;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/ViewColumns/*'/>
        public static readonly string ViewColumns;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/Views/*'/>
        public static readonly string Views;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/AllColumns/*'/>
        public static readonly string AllColumns;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/ColumnSetColumns/*'/>
        public static readonly string ColumnSetColumns;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientMetaDataCollectionNames.xml' path='docs/members[@name="SqlClientMetaDataCollectionNames"]/StructuredTypeMembers/*' />
        public static readonly string StructuredTypeMembers;
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/SqlClientPermission/*'/>
    public sealed partial class SqlClientPermission : System.Data.Common.DBDataPermission
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="default"]/*'/>
        [System.ObsoleteAttribute("SqlClientPermission() has been deprecated.  Use the SqlClientPermission(PermissionState.None) constructor.  http://go.microsoft.com/fwlink/?linkid=14202", true)]
        public SqlClientPermission() : base(default(System.Security.Permissions.PermissionState)) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="PermissionState"]//*'/>
        public SqlClientPermission(System.Security.Permissions.PermissionState state) : base(default(System.Security.Permissions.PermissionState)) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/ctor[@name="PermissionStateAndAllowBlankPasswordBool"]//*'/>
        [System.ObsoleteAttribute("SqlClientPermission(PermissionState state, Boolean allowBlankPassword) has been deprecated.  Use the SqlClientPermission(PermissionState.None) constructor.  http://go.microsoft.com/fwlink/?linkid=14202", true)]
        public SqlClientPermission(System.Security.Permissions.PermissionState state, bool allowBlankPassword) : base(default(System.Security.Permissions.PermissionState)) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/Add/*'/>
        public override void Add(string connectionString, string restrictions, System.Data.KeyRestrictionBehavior behavior) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermission.xml' path='docs/members[@name="SqlClientPermission"]/Copy/*'/>
        public override System.Security.IPermission Copy() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/SqlClientPermissionAttribute/*'/>
    [System.AttributeUsageAttribute(System.AttributeTargets.Method | System.AttributeTargets.Constructor | System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed partial class SqlClientPermissionAttribute : System.Data.Common.DBDataPermissionAttribute
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/ctor/*'/>
        public SqlClientPermissionAttribute(System.Security.Permissions.SecurityAction action) : base(action) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientPermissionAttribute.xml' path='docs/members[@name="SqlClientPermissionAttribute"]/CreatePermission/*'/>
        public override System.Security.IPermission CreatePermission() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/SqlColumnEncryptionCertificateStoreProvider/*'/>
    public partial class SqlColumnEncryptionCertificateStoreProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/ProviderName/*'/>
        public const string ProviderName = "MSSQL_CERTIFICATE_STORE";
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/ctor/*'/>
        public SqlColumnEncryptionCertificateStoreProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/DecryptColumnEncryptionKey/*'/>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/EncryptColumnEncryptionKey/*'/>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/SignColumnMasterKeyMetadata/*'/>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SqlColumnEncryptionCngProvider/*'/>
    public partial class SqlColumnEncryptionCngProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/ProviderName/*'/>
        public const string ProviderName = "MSSQL_CNG_STORE";
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/ctor/*'/>
        public SqlColumnEncryptionCngProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/DecryptColumnEncryptionKey/*'/>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/EncryptColumnEncryptionKey/*'/>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SignColumnMasterKeyMetadata/*'/>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/SqlColumnEncryptionCspProvider/*'/>
    public partial class SqlColumnEncryptionCspProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/ProviderName/*'/>
        public const string ProviderName = "MSSQL_CSP_PROVIDER";
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/ctor/*'/>
        public SqlColumnEncryptionCspProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/DecryptColumnEncryptionKey/*'/>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/EncryptColumnEncryptionKey/*'/>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/SignColumnMasterKeyMetadata/*'/>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/SqlColumnEncryptionKeyStoreProvider/*'/>
    public abstract partial class SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/ctor/*'/>
        protected SqlColumnEncryptionKeyStoreProvider() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/DecryptColumnEncryptionKey/*'/>
        public abstract byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/EncryptColumnEncryptionKey/*'/>
        public abstract byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/SignColumnMasterKeyMetadata/*'/>
        public virtual byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public virtual bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/ColumnEncryptionKeyCacheTtl/*'/>
        public virtual System.TimeSpan? ColumnEncryptionKeyCacheTtl { get { throw null; } set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/SqlCommand/*'/>
    [System.ComponentModel.DefaultEventAttribute("RecordsAffected")]
    [System.ComponentModel.ToolboxItemAttribute(true)]
    // TODO: Add designer attribute when Microsoft.VSDesigner.Data.VS.SqlCommandDesigner uses Microsoft.Data.SqlClient
    public sealed partial class SqlCommand : System.Data.Common.DbCommand, System.ICloneable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="default"]/*'/>
        public SqlCommand() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextString"]/*'/>
        public SqlCommand(string cmdText) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextStringAndSqlConnection"]/*'/>
        public SqlCommand(string cmdText, Microsoft.Data.SqlClient.SqlConnection connection) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextStringAndSqlConnectionAndSqlTransaction"]/*'/>
        public SqlCommand(string cmdText, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ctor[@name="cmdTextStringAndSqlConnectionAndSqlTransactionAndSqlCommandColumnEncryptionSetting"]/*'/>
        public SqlCommand(string cmdText, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction, Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting columnEncryptionSetting) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ColumnEncryptionSetting/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting ColumnEncryptionSetting { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CommandText/*'/>
        [System.ComponentModel.DefaultValueAttribute("")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public override string CommandText { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CommandTimeout/*'/>
        public override int CommandTimeout { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CommandType/*'/>
        [System.ComponentModel.DefaultValueAttribute(1)]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public override System.Data.CommandType CommandType { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Connection/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public new Microsoft.Data.SqlClient.SqlConnection Connection { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DbConnection/*'/>
        protected override System.Data.Common.DbConnection DbConnection { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DbParameterCollection/*'/>
        protected override System.Data.Common.DbParameterCollection DbParameterCollection { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DbTransaction/*'/>
        protected override System.Data.Common.DbTransaction DbTransaction { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/DesignTimeVisible/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DefaultValueAttribute(true)]
        [System.ComponentModel.DesignOnlyAttribute(true)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override bool DesignTimeVisible { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EnableOptimizedParameterBinding/*'/>
        public bool EnableOptimizedParameterBinding { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Parameters/*'/>
        public new Microsoft.Data.SqlClient.SqlParameterCollection Parameters { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Transaction/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public new Microsoft.Data.SqlClient.SqlTransaction Transaction { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/UpdatedRowSource/*'/>
        [System.ComponentModel.DefaultValueAttribute(3)]
        public override System.Data.UpdateRowSource UpdatedRowSource { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/StatementCompleted/*'/>
        public event System.Data.StatementCompletedEventHandler StatementCompleted { add { } remove { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteNonQuery[@name="default"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteNonQuery() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteNonQuery[@name="AsyncCallbackAndStateObject"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteNonQuery(System.AsyncCallback callback, object stateObject) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="default"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteReader() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="AsyncCallbackAndstateObject"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteReader(System.AsyncCallback callback, object stateObject) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="AsyncCallbackAndstateObjectAndCommandBehavior"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteReader(System.AsyncCallback callback, object stateObject, System.Data.CommandBehavior behavior) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="CommandBehavior"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteReader(System.Data.CommandBehavior behavior) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteXmlReader[@name="default"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteXmlReader() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteXmlReader[@name="AsyncCallbackAndstateObject"]/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public System.IAsyncResult BeginExecuteXmlReader(System.AsyncCallback callback, object stateObject) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Cancel/*'/>
        public override void Cancel() { }
        object System.ICloneable.Clone() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Clone/*'/>
        public Microsoft.Data.SqlClient.SqlCommand Clone() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CreateDbParameter/*'/>
        protected override System.Data.Common.DbParameter CreateDbParameter() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CreateParameter/*'/>
        public new Microsoft.Data.SqlClient.SqlParameter CreateParameter() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Dispose/*'/>
        protected override void Dispose(bool disposing) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EndExecuteNonQuery[@name="IAsyncResult"]/*'/>
        public int EndExecuteNonQuery(System.IAsyncResult asyncResult) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EndExecuteReader[@name="IAsyncResult2"]/*'/>
        public Microsoft.Data.SqlClient.SqlDataReader EndExecuteReader(System.IAsyncResult asyncResult) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EndExecuteXmlReader[@name="IAsyncResult"]/*'/>
        public System.Xml.XmlReader EndExecuteXmlReader(System.IAsyncResult asyncResult) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteDbDataReader[@name="CommandBehavior"]/*'/>
        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteDbDataReaderAsync/*'/>
        protected override System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteDbDataReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteNonQuery[@name="default"]/*'/>
        public override int ExecuteNonQuery() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteNonQueryAsync[@name="CancellationToken"]/*'/>
        public override System.Threading.Tasks.Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReader[@name="default"]/*'/>
        public new Microsoft.Data.SqlClient.SqlDataReader ExecuteReader() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReader[@name="CommandBehavior"]/*'/>
        public new Microsoft.Data.SqlClient.SqlDataReader ExecuteReader(System.Data.CommandBehavior behavior) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReaderAsync[@name="default"]/*'/>
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReaderAsync[@name="CommandBehavior"]/*'/>
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync(System.Data.CommandBehavior behavior) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReaderAsync[@name="commandBehaviorAndCancellationToken"]/*'/>
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReaderAsync[@name="CancellationToken"]/*'/>
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteScalar/*'/>
        public override object ExecuteScalar() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteScalarAsync[@name="CancellationToken"]/*'/>
        public override System.Threading.Tasks.Task<object> ExecuteScalarAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReader[@name="default"]/*'/>
        public System.Xml.XmlReader ExecuteXmlReader() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReaderAsync[@name="default"]/*'/>
        public System.Threading.Tasks.Task<System.Xml.XmlReader> ExecuteXmlReaderAsync() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReaderAsync[@name="CancellationToken"]/*'/>
        public System.Threading.Tasks.Task<System.Xml.XmlReader> ExecuteXmlReaderAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Prepare/*'/>
        public override void Prepare() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Notification/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public Microsoft.Data.Sql.SqlNotificationRequest Notification { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/NotificationAutoEnlist/*'/>
        [System.ComponentModel.DefaultValueAttribute(true)]
        public bool NotificationAutoEnlist { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/RegisterColumnEncryptionKeyStoreProvidersOnCommand/*' />
        public void RegisterColumnEncryptionKeyStoreProvidersOnCommand(System.Collections.Generic.IDictionary<string, Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider> customProviders) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ResetCommandTimeout/*'/>
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Content)]
        public void ResetCommandTimeout() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/RetryLogicProvider/*' />
        public SqlRetryLogicBaseProvider RetryLogicProvider { get { throw null; } set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/SqlCommandBuilder/*'/>
    public sealed class SqlCommandBuilder : System.Data.Common.DbCommandBuilder
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/ctor1/*'/>
        public SqlCommandBuilder() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/ctor2/*'/>
        public SqlCommandBuilder(Microsoft.Data.SqlClient.SqlDataAdapter adapter) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/CatalogLocation/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override System.Data.Common.CatalogLocation CatalogLocation { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/CatalogSeparator/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override string CatalogSeparator { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/DataAdapter/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public new Microsoft.Data.SqlClient.SqlDataAdapter DataAdapter { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/DeriveParameters/*'/>
        public static void DeriveParameters(Microsoft.Data.SqlClient.SqlCommand command) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuotePrefix/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override string QuotePrefix { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuoteSuffix/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override string QuoteSuffix { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/SchemaSeparator/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override string SchemaSeparator { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/ApplyParameterInfo/*'/>
        protected override void ApplyParameterInfo(System.Data.Common.DbParameter parameter, System.Data.DataRow datarow, System.Data.StatementType statementType, bool whereClause) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetDeleteCommand2/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand GetDeleteCommand() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetDeleteCommand3/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand GetDeleteCommand(bool useColumnsForParameterNames) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetInsertCommand2/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand GetInsertCommand() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetInsertCommand3/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand GetInsertCommand(bool useColumnsForParameterNames) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetParameterName1/*'/>
        protected override string GetParameterName(int parameterOrdinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetParameterName2/*'/>
        protected override string GetParameterName(string parameterName) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetParameterPlaceholder/*'/>
        protected override string GetParameterPlaceholder(int parameterOrdinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetSchemaTable/*'/>
        protected override System.Data.DataTable GetSchemaTable(System.Data.Common.DbCommand srcCommand) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetUpdateCommand2/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand GetUpdateCommand() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetUpdateCommand3/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand GetUpdateCommand(bool useColumnsForParameterNames) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/InitializeCommand/*'/>
        protected override System.Data.Common.DbCommand InitializeCommand(System.Data.Common.DbCommand command) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuoteIdentifier/*'/>
        public override string QuoteIdentifier(string unquotedIdentifier) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/SetRowUpdatingHandler/*'/>
        protected override void SetRowUpdatingHandler(System.Data.Common.DbDataAdapter adapter) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/UnquoteIdentifier/*'/>
        public override string UnquoteIdentifier(string quotedIdentifier) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/SqlCommandColumnEncryptionSetting/*'/>
    public enum SqlCommandColumnEncryptionSetting
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/Disabled/*'/>
        Disabled = 3,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/Enabled/*'/>
        Enabled = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/ResultSetOnly/*'/>
        ResultSetOnly = 2,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandColumnEncryptionSetting.xml' path='docs/members[@name="SqlCommandColumnEncryptionSetting"]/UseConnectionSetting/*'/>
        UseConnectionSetting = 0,
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/SqlConnection/*'/>
    [System.ComponentModel.DefaultEventAttribute("InfoMessage")]
    public sealed partial class SqlConnection : System.Data.Common.DbConnection, System.ICloneable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ctor2/*'/>
        public SqlConnection() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ctorConnectionString/*'/>
        public SqlConnection(string connectionString) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ctorConnectionStringCredential/*'/>
        public SqlConnection(string connectionString, Microsoft.Data.SqlClient.SqlCredential credential) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/AccessToken/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public string AccessToken { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/AccessTokenCallback/*' />
        public System.Func<SqlAuthenticationParameters, System.Threading.CancellationToken, System.Threading.Tasks.Task<SqlAuthenticationToken>> AccessTokenCallback { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClientConnectionId/*'/>
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public System.Guid ClientConnectionId { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/System.ICloneable.Clone/*' />
        object System.ICloneable.Clone() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionKeyCacheTtl/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public static System.TimeSpan ColumnEncryptionKeyCacheTtl { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionQueryMetadataCacheEnabled/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public static bool ColumnEncryptionQueryMetadataCacheEnabled { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionTrustedMasterKeyPaths/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public static System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<string>> ColumnEncryptionTrustedMasterKeyPaths { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CommandTimeout/*'/>
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public int CommandTimeout { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ConnectionString/*'/>
        [System.ComponentModel.DefaultValueAttribute("")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ComponentModel.SettingsBindableAttribute(true)]
        public override string ConnectionString { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ConnectionTimeout/*'/>
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override int ConnectionTimeout { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Credential/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public Microsoft.Data.SqlClient.SqlCredential Credential { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Database/*'/>
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override string Database { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/DataSource/*'/>
        [System.ComponentModel.BrowsableAttribute(true)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override string DataSource { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/DbProviderFactory/*'/>
        protected override System.Data.Common.DbProviderFactory DbProviderFactory { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/FireInfoMessageEventOnUserErrors/*'/>
        public bool FireInfoMessageEventOnUserErrors { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/PacketSize/*'/>
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public int PacketSize { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ServerVersion/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override string ServerVersion { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ServerProcessId/*'/>
        public int ServerProcessId { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/State/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public override System.Data.ConnectionState State { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/StatisticsEnabled/*'/>
        [System.ComponentModel.DefaultValueAttribute(false)]
        public bool StatisticsEnabled { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/WorkstationId/*'/>
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public string WorkstationId { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/InfoMessage/*'/>
        public event Microsoft.Data.SqlClient.SqlInfoMessageEventHandler InfoMessage { add { } remove { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginDbTransaction/*'/>
        protected override System.Data.Common.DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransaction2/*'/>
        public new Microsoft.Data.SqlClient.SqlTransaction BeginTransaction() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionIso/*'/>
        public new Microsoft.Data.SqlClient.SqlTransaction BeginTransaction(System.Data.IsolationLevel iso) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionIsoTransactionName/*'/>
        public Microsoft.Data.SqlClient.SqlTransaction BeginTransaction(System.Data.IsolationLevel iso, string transactionName) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionTransactionName/*'/>
        public Microsoft.Data.SqlClient.SqlTransaction BeginTransaction(string transactionName) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangeDatabase/*'/>
        public override void ChangeDatabase(string database) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangePasswordConnectionStringCredentialNewSecurePassword/*'/>
        public static void ChangePassword(string connectionString, Microsoft.Data.SqlClient.SqlCredential credential, System.Security.SecureString newSecurePassword) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangePasswordConnectionStringNewPassword/*'/>
        public static void ChangePassword(string connectionString, string newPassword) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClearAllPools/*'/>
        public static void ClearAllPools() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClearPool/*'/>
        public static void ClearPool(Microsoft.Data.SqlClient.SqlConnection connection) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Close/*'/>
        public override void Close() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CreateCommand/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand CreateCommand() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CreateDbCommand/*'/>
        protected override System.Data.Common.DbCommand CreateDbCommand() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Dispose/*'/>
        protected override void Dispose(bool disposing) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/EnlistDistributedTransaction/*'/>
        public void EnlistDistributedTransaction(System.EnterpriseServices.ITransaction transaction) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/EnlistTransaction/*'/>
        public override void EnlistTransaction(System.Transactions.Transaction transaction) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/GetSchema2/*'/>
        public override System.Data.DataTable GetSchema() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/GetSchemaCollectionName/*'/>
        public override System.Data.DataTable GetSchema(string collectionName) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/GetSchemaCollectionNameRestrictionValues/*'/>
        public override System.Data.DataTable GetSchema(string collectionName, string[] restrictionValues) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Open/*'/>
        public override void Open() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/OpenWithOverrides/*'/>
        public void Open(SqlConnectionOverrides overrides) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/OpenAsync/*'/>
        public override System.Threading.Tasks.Task OpenAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/OpenAsyncWithOverrides/*'/>
        public System.Threading.Tasks.Task OpenAsync(Microsoft.Data.SqlClient.SqlConnectionOverrides overrides, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RegisterColumnEncryptionKeyStoreProviders/*'/>
        public static void RegisterColumnEncryptionKeyStoreProviders(System.Collections.Generic.IDictionary<string, Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider> customProviders) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RegisterColumnEncryptionKeyStoreProvidersOnConnection/*' />
        public void RegisterColumnEncryptionKeyStoreProvidersOnConnection(System.Collections.Generic.IDictionary<string, Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider> customProviders) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ResetStatistics/*'/>
        public void ResetStatistics() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RetrieveStatistics/*'/>
        public System.Collections.IDictionary RetrieveStatistics() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RetrieveInternalInfo/*'/>
        public System.Collections.Generic.IDictionary<string, object> RetrieveInternalInfo() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RetryLogicProvider/*' />
        public SqlRetryLogicBaseProvider RetryLogicProvider { get { throw null; } set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionColumnEncryptionSetting.xml' path='docs/members[@name="SqlConnectionColumnEncryptionSetting"]/SqlConnectionColumnEncryptionSetting/*'/>
    public enum SqlConnectionColumnEncryptionSetting
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionColumnEncryptionSetting.xml' path='docs/members[@name="SqlConnectionColumnEncryptionSetting"]/Disabled/*'/>
        Disabled = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionColumnEncryptionSetting.xml' path='docs/members[@name="SqlConnectionColumnEncryptionSetting"]/Enabled/*'/>
        Enabled = 1,
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/SqlConnectionAttestationProtocol/*' />
    public enum SqlConnectionAttestationProtocol
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/NotSpecified/*' />
        NotSpecified = 0,

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/AAS/*' />
        AAS = 1,

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/None/*' />
        None = 2,

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionAttestationProtocol.xml' path='docs/members[@name="SqlConnectionAttestationProtocol"]/HGS/*' />
        HGS = 3
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionIPAddressPreference.xml' path='docs/members[@name="SqlConnectionIPAddressPreference"]/SqlConnectionIPAddressPreferenceNetfx/*' />
    public enum SqlConnectionIPAddressPreference
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionIPAddressPreference.xml' path='docs/members[@name="SqlConnectionIPAddressPreference"]/IPv4First/*' />
        IPv4First = 0,  // default

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionIPAddressPreference.xml' path='docs/members[@name="SqlConnectionIPAddressPreference"]/IPv6First/*' />
        IPv6First = 1,

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionIPAddressPreference.xml' path='docs/members[@name="SqlConnectionIPAddressPreference"]/UsePlatformDefault/*' />
        UsePlatformDefault = 2
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/SqlConnectionEncryptOption/*'/>
    public sealed class SqlConnectionEncryptOption
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Parse/*' />
        public static SqlConnectionEncryptOption Parse(string value) => throw null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/TryParse/*' />
        public static bool TryParse(string value, out SqlConnectionEncryptOption result) => throw null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Optional/*' />
        public static SqlConnectionEncryptOption Optional => throw null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Mandatory/*' />
        public static SqlConnectionEncryptOption Mandatory => throw null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Strict/*' />
        public static SqlConnectionEncryptOption Strict => throw null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/BoolToOption/*' />
        public static implicit operator SqlConnectionEncryptOption(bool value) => throw null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/OptionToBool/*' />
        public static implicit operator bool(SqlConnectionEncryptOption value) => throw null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/ToString/*' />
        public override string ToString() { throw null; }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/Equals/*' />
        public override bool Equals(object obj) { throw null; }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptOption.xml' path='docs/members[@name="SqlConnectionEncryptOption"]/GetHashCode/*' />
        public override int GetHashCode() { throw null; }
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionOverrides.xml' path='docs/members[@name="SqlConnectionOverrides"]/SqlConnectionOverrides/*' />
    public enum SqlConnectionOverrides
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionOverrides.xml' path='docs/members[@name="SqlConnectionOverrides"]/None/*' />
        None = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionOverrides.xml' path='docs/members[@name="SqlConnectionOverrides"]/OpenWithoutRetry/*' />
        OpenWithoutRetry = 1,
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/SqlConnectionStringBuilder/*'/>
    [System.ComponentModel.DefaultPropertyAttribute("DataSource")]
    public sealed partial class SqlConnectionStringBuilder : System.Data.Common.DbConnectionStringBuilder
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ctor2/*'/>
        public SqlConnectionStringBuilder() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ctorConnectionString/*'/>
        public SqlConnectionStringBuilder(string connectionString) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ApplicationIntent/*'/>
        [System.ComponentModel.DisplayNameAttribute("Application Intent")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public Microsoft.Data.SqlClient.ApplicationIntent ApplicationIntent { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ApplicationName/*'/>
        [System.ComponentModel.DisplayNameAttribute("Application Name")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string ApplicationName { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/AttachDBFilename/*'/>
        [System.ComponentModel.DisplayNameAttribute("AttachDbFilename")]
        [System.ComponentModel.EditorAttribute("System.Windows.Forms.Design.FileNameEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string AttachDBFilename { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Authentication/*'/>
        [System.ComponentModel.DisplayNameAttribute("Authentication")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public Microsoft.Data.SqlClient.SqlAuthenticationMethod Authentication { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ColumnEncryptionSetting/*'/>
        [System.ComponentModel.DisplayNameAttribute("Column Encryption Setting")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public Microsoft.Data.SqlClient.SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/CommandTimeout/*'/>
        [System.ComponentModel.DisplayNameAttribute("Command Timeout")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int CommandTimeout { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectionReset/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DisplayNameAttribute("Connection Reset")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ObsoleteAttribute("ConnectionReset has been deprecated.  SqlConnection will ignore the 'connection reset' keyword and always reset the connection.")]
        public bool ConnectionReset { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectRetryCount/*'/>
        [System.ComponentModel.DisplayNameAttribute("Connect Retry Count")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int ConnectRetryCount { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectRetryInterval/*'/>
        [System.ComponentModel.DisplayNameAttribute("Connect Retry Interval")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int ConnectRetryInterval { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectTimeout/*'/>
        [System.ComponentModel.DisplayNameAttribute("Connect Timeout")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int ConnectTimeout { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/CurrentLanguage/*'/>
        [System.ComponentModel.DisplayNameAttribute("Current Language")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string CurrentLanguage { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/DataSource/*'/>
        [System.ComponentModel.DisplayNameAttribute("Data Source")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ComponentModel.TypeConverter("Microsoft.Data.SqlClient.SqlConnectionStringBuilder+SqlDataSourceConverter, Microsoft.Data.SqlClient")]
        public string DataSource { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/EnclaveAttestationUrl/*'/>
        [System.ComponentModel.DisplayNameAttribute("Enclave Attestation Url")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string EnclaveAttestationUrl { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/AttestationProtocol/*'/>
        [System.ComponentModel.DisplayNameAttribute("Attestation Protocol")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public Microsoft.Data.SqlClient.SqlConnectionAttestationProtocol AttestationProtocol { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/IPAddressPreference/*'/>
        [System.ComponentModel.DisplayNameAttribute("IP Address Preference")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public Microsoft.Data.SqlClient.SqlConnectionIPAddressPreference IPAddressPreference { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Encrypt/*'/>
        [System.ComponentModel.DisplayNameAttribute("Encrypt")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public SqlConnectionEncryptOption Encrypt { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/HostNameInCertificate/*'/>
        [System.ComponentModel.DisplayNameAttribute("Host Name In Certificate")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string HostNameInCertificate { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ServerCertificate/*'/>
        [System.ComponentModel.DisplayNameAttribute("Server Certificate")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string ServerCertificate { get { throw null; } set { } }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Enlist/*'/>
        [System.ComponentModel.DisplayNameAttribute("Enlist")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool Enlist { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/FailoverPartner/*'/>
        [System.ComponentModel.DisplayNameAttribute("Failover Partner")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ComponentModel.TypeConverter("Microsoft.Data.SqlClient.SqlConnectionStringBuilder+SqlDataSourceConverter, Microsoft.Data.SqlClient")]
        public string FailoverPartner { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/FailoverPartnerSPN/*'/>
        [System.ComponentModel.DisplayNameAttribute("Failover Partner SPN")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string FailoverPartnerSPN { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/InitialCatalog/*'/>
        [System.ComponentModel.DisplayNameAttribute("Initial Catalog")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string InitialCatalog { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/IntegratedSecurity/*'/>
        [System.ComponentModel.DisplayNameAttribute("Integrated Security")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool IntegratedSecurity { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/IsFixedSize/*'/>
        public override bool IsFixedSize { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Item/*'/>
        public override object this[string keyword] { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Keys/*'/>
        public override System.Collections.ICollection Keys { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/LoadBalanceTimeout/*'/>
        [System.ComponentModel.DisplayNameAttribute("Load Balance Timeout")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int LoadBalanceTimeout { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MaxPoolSize/*'/>
        [System.ComponentModel.DisplayNameAttribute("Max Pool Size")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int MaxPoolSize { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MinPoolSize/*'/>
        [System.ComponentModel.DisplayNameAttribute("Min Pool Size")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int MinPoolSize { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MultipleActiveResultSets/*'/>
        [System.ComponentModel.DisplayNameAttribute("Multiple Active Result Sets")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool MultipleActiveResultSets { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MultiSubnetFailover/*'/>
        [System.ComponentModel.DisplayNameAttribute("Multi Subnet Failover")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool MultiSubnetFailover { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/NetworkLibrary/*'/>
        [System.ComponentModel.DisplayNameAttribute("Network Library")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string NetworkLibrary { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PacketSize/*'/>
        [System.ComponentModel.DisplayNameAttribute("Packet Size")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public int PacketSize { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Password/*'/>
        [System.ComponentModel.DisplayNameAttribute("Password")]
        [System.ComponentModel.PasswordPropertyTextAttribute(true)]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string Password { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PersistSecurityInfo/*'/>
        [System.ComponentModel.DisplayNameAttribute("Persist Security Info")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool PersistSecurityInfo { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PoolBlockingPeriod/*'/>
        [System.ComponentModel.DisplayNameAttribute("Pool Blocking Period")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public Microsoft.Data.SqlClient.PoolBlockingPeriod PoolBlockingPeriod { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Pooling/*'/>
        [System.ComponentModel.DisplayNameAttribute("Pooling")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool Pooling { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Replication/*'/>
        [System.ComponentModel.DisplayNameAttribute("Replication")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool Replication { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ServerSPN/*'/>
        [System.ComponentModel.DisplayNameAttribute("Server SPN")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string ServerSPN { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TransactionBinding/*'/>
        [System.ComponentModel.DisplayNameAttribute("Transaction Binding")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string TransactionBinding { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TransparentNetworkIPResolution/*'/>
        [System.ComponentModel.DisplayNameAttribute("Transparent Network IP Resolution")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool TransparentNetworkIPResolution { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TrustServerCertificate/*'/>
        [System.ComponentModel.DisplayNameAttribute("Trust Server Certificate")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool TrustServerCertificate { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TypeSystemVersion/*'/>
        [System.ComponentModel.DisplayNameAttribute("Type System Version")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string TypeSystemVersion { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/UserID/*'/>
        [System.ComponentModel.DisplayNameAttribute("User ID")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string UserID { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/UserInstance/*'/>
        [System.ComponentModel.DisplayNameAttribute("User Instance")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public bool UserInstance { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Values/*'/>
        public override System.Collections.ICollection Values { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/WorkstationID/*'/>
        [System.ComponentModel.DisplayNameAttribute("Workstation ID")]
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public string WorkstationID { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Clear/*'/>
        public override void Clear() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ContainsKey/*'/>
        public override bool ContainsKey(string keyword) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Remove/*'/>
        public override bool Remove(string keyword) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ShouldSerialize/*'/>
        public override bool ShouldSerialize(string keyword) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TryGetValue/*'/>
        public override bool TryGetValue(string keyword, out object value) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCredential.xml' path='docs/members[@name="SqlCredential"]/SqlCredential/*'/>
    public sealed class SqlCredential
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCredential.xml' path='docs/members[@name="SqlCredential"]/ctor/*'/>
        public SqlCredential(string userId, System.Security.SecureString password) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCredential.xml' path='docs/members[@name="SqlCredential"]/Password/*'/>
        public System.Security.SecureString Password { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCredential.xml' path='docs/members[@name="SqlCredential"]/UserId/*'/>
        public string UserId { get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/SqlDataAdapter/*'/>
    [System.ComponentModel.DefaultEventAttribute("RowUpdated")]
    // TODO: Add designer and toolbox attribute when Microsoft.VSDesigner.Data.VS.SqlDataAdapterDesigner uses Microsoft.Data.SqlClient
    public sealed partial class SqlDataAdapter : System.Data.Common.DbDataAdapter, System.Data.IDataAdapter, System.Data.IDbDataAdapter, System.ICloneable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctor2/*'/>
        public SqlDataAdapter() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommand/*'/>
        public SqlDataAdapter(Microsoft.Data.SqlClient.SqlCommand selectCommand) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommandTextSelectConnection/*'/>
        public SqlDataAdapter(string selectCommandText, Microsoft.Data.SqlClient.SqlConnection selectConnection) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommandTextSelectConnectionString/*'/>
        public SqlDataAdapter(string selectCommandText, string selectConnectionString) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/DeleteCommand/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public new Microsoft.Data.SqlClient.SqlCommand DeleteCommand { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/InsertCommand/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public new Microsoft.Data.SqlClient.SqlCommand InsertCommand { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/SelectCommand/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public new Microsoft.Data.SqlClient.SqlCommand SelectCommand { get { throw null; } set { } }
        System.Data.IDbCommand System.Data.IDbDataAdapter.DeleteCommand { get { throw null; } set { } }
        System.Data.IDbCommand System.Data.IDbDataAdapter.InsertCommand { get { throw null; } set { } }
        System.Data.IDbCommand System.Data.IDbDataAdapter.SelectCommand { get { throw null; } set { } }
        System.Data.IDbCommand System.Data.IDbDataAdapter.UpdateCommand { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/UpdateBatchSize/*'/>
        public override int UpdateBatchSize { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/UpdateCommand/*'/>
        [System.ComponentModel.DefaultValueAttribute(null)]
        public new Microsoft.Data.SqlClient.SqlCommand UpdateCommand { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/RowUpdated/*'/>
        public event Microsoft.Data.SqlClient.SqlRowUpdatedEventHandler RowUpdated { add { } remove { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/RowUpdating/*'/>
        public event Microsoft.Data.SqlClient.SqlRowUpdatingEventHandler RowUpdating { add { } remove { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/AddToBatch/*'/>
        protected override int AddToBatch(System.Data.IDbCommand command) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ClearBatch/*'/>
        protected override void ClearBatch() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/CreateRowUpdatedEvent/*'/>
        protected override System.Data.Common.RowUpdatedEventArgs CreateRowUpdatedEvent(System.Data.DataRow dataRow, System.Data.IDbCommand command, System.Data.StatementType statementType, System.Data.Common.DataTableMapping tableMapping) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/CreateRowUpdatingEvent/*'/>
        protected override System.Data.Common.RowUpdatingEventArgs CreateRowUpdatingEvent(System.Data.DataRow dataRow, System.Data.IDbCommand command, System.Data.StatementType statementType, System.Data.Common.DataTableMapping tableMapping) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ExecuteBatch/*'/>
        protected override int ExecuteBatch() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/GetBatchedParameter/*'/>
        protected override System.Data.IDataParameter GetBatchedParameter(int commandIdentifier, int parameterIndex) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/GetBatchedRecordsAffected/*'/>
        protected override bool GetBatchedRecordsAffected(int commandIdentifier, out int recordsAffected, out System.Exception error) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/InitializeBatching/*'/>
        protected override void InitializeBatching() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/OnRowUpdated/*'/>
        protected override void OnRowUpdated(System.Data.Common.RowUpdatedEventArgs value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/OnRowUpdating/*'/>
        protected override void OnRowUpdating(System.Data.Common.RowUpdatingEventArgs value) { }
        object System.ICloneable.Clone() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/TerminateBatching/*'/>
        protected override void TerminateBatching() { }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/SqlDataReader/*'/>
    public partial class SqlDataReader : System.Data.Common.DbDataReader, System.Data.IDataReader, System.Data.Common.IDbColumnSchemaGenerator, System.IDisposable
    {
        internal SqlDataReader() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/Connection/*'/>
        protected Microsoft.Data.SqlClient.SqlConnection Connection { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/Depth/*'/>
        public override int Depth { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/FieldCount/*'/>
        public override int FieldCount { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/HasRows/*'/>
        public override bool HasRows { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/IsClosed/*'/>
        public override bool IsClosed { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/ItemI/*'/>
        public override object this[int i] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/ItemName/*'/>
        public override object this[string name] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/RecordsAffected/*'/>
        public override int RecordsAffected { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/SensitivityClassification/*'/>
        public Microsoft.Data.SqlClient.DataClassification.SensitivityClassification SensitivityClassification { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/VisibleFieldCount/*'/>
        public override int VisibleFieldCount { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/Close/*'/>
        public override void Close() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetBoolean/*'/>
        public override bool GetBoolean(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetByte/*'/>
        public override byte GetByte(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetBytes/*'/>
        public override long GetBytes(int i, long dataIndex, byte[] buffer, int bufferIndex, int length) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetChar/*'/>
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override char GetChar(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetChars/*'/>
        public override long GetChars(int i, long dataIndex, char[] buffer, int bufferIndex, int length) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetColumnSchema/*'/>
        public System.Collections.ObjectModel.ReadOnlyCollection<System.Data.Common.DbColumn> GetColumnSchema() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetDataTypeName/*'/>
        public override string GetDataTypeName(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetDateTime/*'/>
        public override System.DateTime GetDateTime(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetDateTimeOffset/*'/>
        public virtual System.DateTimeOffset GetDateTimeOffset(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetDecimal/*'/>
        public override decimal GetDecimal(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetDouble/*'/>
        public override double GetDouble(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetEnumerator/*'/>
        public override System.Collections.IEnumerator GetEnumerator() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetFieldType/*'/>
        public override System.Type GetFieldType(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetFieldValue/*'/>
        public override T GetFieldValue<T>(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetFieldValueAsync/*'/>
        public override System.Threading.Tasks.Task<T> GetFieldValueAsync<T>(int i, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetFloat/*'/>
        public override float GetFloat(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetGuid/*'/>
        public override System.Guid GetGuid(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetInt16/*'/>
        public override short GetInt16(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetInt32/*'/>
        public override int GetInt32(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetInt64/*'/>
        public override long GetInt64(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetName/*'/>
        public override string GetName(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetOrdinal/*'/>
        public override int GetOrdinal(string name) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetProviderSpecificFieldType/*'/>
        public override System.Type GetProviderSpecificFieldType(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetProviderSpecificValue/*'/>
        public override object GetProviderSpecificValue(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetProviderSpecificValues/*'/>
        public override int GetProviderSpecificValues(object[] values) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlBinary/*'/>
        public virtual System.Data.SqlTypes.SqlBinary GetSqlBinary(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlBoolean/*'/>
        public virtual System.Data.SqlTypes.SqlBoolean GetSqlBoolean(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlByte/*'/>
        public virtual System.Data.SqlTypes.SqlByte GetSqlByte(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlBytes/*'/>
        public virtual System.Data.SqlTypes.SqlBytes GetSqlBytes(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlChars/*'/>
        public virtual System.Data.SqlTypes.SqlChars GetSqlChars(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlDateTime/*'/>
        public virtual System.Data.SqlTypes.SqlDateTime GetSqlDateTime(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlDecimal/*'/>
        public virtual System.Data.SqlTypes.SqlDecimal GetSqlDecimal(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlDouble/*'/>
        public virtual System.Data.SqlTypes.SqlDouble GetSqlDouble(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlGuid/*'/>
        public virtual System.Data.SqlTypes.SqlGuid GetSqlGuid(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlInt16/*'/>
        public virtual System.Data.SqlTypes.SqlInt16 GetSqlInt16(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlInt32/*'/>
        public virtual System.Data.SqlTypes.SqlInt32 GetSqlInt32(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlInt64/*'/>
        public virtual System.Data.SqlTypes.SqlInt64 GetSqlInt64(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlJson/*' />
        virtual public Microsoft.Data.SqlTypes.SqlJson GetSqlJson(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlMoney/*'/>
        public virtual System.Data.SqlTypes.SqlMoney GetSqlMoney(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlSingle/*'/>
        public virtual System.Data.SqlTypes.SqlSingle GetSqlSingle(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlString/*'/>
        public virtual System.Data.SqlTypes.SqlString GetSqlString(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlValue/*'/>
        public virtual object GetSqlValue(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlValues/*'/>
        public virtual int GetSqlValues(object[] values) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSqlXml/*'/>
        public virtual System.Data.SqlTypes.SqlXml GetSqlXml(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetSchemaTable/*'/>
        public override System.Data.DataTable GetSchemaTable() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetStream/*'/>
        public override System.IO.Stream GetStream(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetString/*'/>
        public override string GetString(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetTextReader/*'/>
        public override System.IO.TextReader GetTextReader(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetTimeSpan/*'/>
        public virtual System.TimeSpan GetTimeSpan(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetValue/*'/>
        public override object GetValue(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetValues/*'/>
        public override int GetValues(object[] values) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/GetXmlReader/*'/>
        public virtual System.Xml.XmlReader GetXmlReader(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/IsCommandBehavior/*'/>
        protected internal bool IsCommandBehavior(System.Data.CommandBehavior condition) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/IsDBNull/*'/>
        public override bool IsDBNull(int i) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/IsDBNullAsync/*'/>
        public override System.Threading.Tasks.Task<bool> IsDBNullAsync(int i, System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/NextResult/*'/>
        public override bool NextResult() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/NextResultAsync/*'/>
        public override System.Threading.Tasks.Task<bool> NextResultAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/Read/*'/>
        public override bool Read() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataReader.xml' path='docs/members[@name="SqlDataReader"]/ReadAsync/*'/>
        public override System.Threading.Tasks.Task<bool> ReadAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        System.Data.IDataReader System.Data.IDataRecord.GetData(int i) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/SqlDependency/*'/>
    public sealed partial class SqlDependency
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctor2/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public SqlDependency() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctorCommand/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public SqlDependency(Microsoft.Data.SqlClient.SqlCommand command) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/ctorCommandOptionsTimeout/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public SqlDependency(Microsoft.Data.SqlClient.SqlCommand command, string options, int timeout) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/HasChanges/*'/>
        public bool HasChanges { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/Id/*'/>
        public string Id { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/OnChange/*'/>
        public event Microsoft.Data.SqlClient.OnChangeEventHandler OnChange { add { } remove { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/AddCommandDependency/*'/>
        public void AddCommandDependency(Microsoft.Data.SqlClient.SqlCommand command) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StartConnectionString/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public static bool Start(string connectionString) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StartConnectionStringQueue/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public static bool Start(string connectionString, string queue) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StopConnectionString/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public static bool Stop(string connectionString) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDependency.xml' path='docs/members[@name="SqlDependency"]/StopConnectionStringQueue/*'/>
        [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
        public static bool Stop(string connectionString, string queue) { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/SqlError/*'/>
    public sealed partial class SqlError
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/ctor/*'/>
        internal SqlError() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Class/*'/>
        public byte Class { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/LineNumber/*'/>
        public int LineNumber { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Message/*'/>
        public string Message { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Number/*'/>
        public int Number { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Procedure/*'/>
        public string Procedure { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Server/*'/>
        public string Server { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/Source/*'/>
        public string Source { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/State/*'/>
        public byte State { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlError.xml' path='docs/members[@name="SqlError"]/ToString/*'/>
        public override string ToString() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/SqlErrorCollection/*'/>
    [System.ComponentModel.ListBindableAttribute(false)]
    public sealed partial class SqlErrorCollection : System.Collections.ICollection, System.Collections.IEnumerable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/ctor/*'/>
        internal SqlErrorCollection() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/Count/*'/>
        public int Count { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/Item/*'/>
        public Microsoft.Data.SqlClient.SqlError this[int index] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/System.Collections.ICollection.IsSynchronized/*'/>
        bool System.Collections.ICollection.IsSynchronized { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/System.Collections.ICollection.SyncRoot/*'/>
        object System.Collections.ICollection.SyncRoot { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/CopyToArrayIndex1/*'/>
        public void CopyTo(System.Array array, int index) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/CopyToArrayIndex2/*'/>
        public void CopyTo(Microsoft.Data.SqlClient.SqlError[] array, int index) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/GetEnumerator/*'/>
        public System.Collections.IEnumerator GetEnumerator() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/SqlException/*'/>
    [System.Serializable]
    public sealed partial class SqlException : System.Data.Common.DbException
    {
        internal SqlException() { }
        private SqlException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/Class/*'/>
        public byte Class { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/ClientConnectionId/*'/>
        public System.Guid ClientConnectionId { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/Errors/*'/>
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Content)]
        public Microsoft.Data.SqlClient.SqlErrorCollection Errors { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/LineNumber/*'/>
        public int LineNumber { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/Number/*'/>
        public int Number { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/Procedure/*'/>
        public string Procedure { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/Server/*'/>
        public string Server { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/Source/*'/>
        public override string Source { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/State/*'/>
        public byte State { get { throw null; } }
        /// <inheritdoc cref="System.Exception.GetObjectData" />
        [System.Security.Permissions.SecurityPermissionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Flags = System.Security.Permissions.SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo si, System.Runtime.Serialization.StreamingContext context) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/ToString/*'/>
        public override string ToString() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/SqlInfoMessageEventArgs/*'/>
    public sealed partial class SqlInfoMessageEventArgs : System.EventArgs
    {
        internal SqlInfoMessageEventArgs() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Errors/*'/>
        public Microsoft.Data.SqlClient.SqlErrorCollection Errors { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Message/*'/>
        public string Message { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/Source/*'/>
        public string Source { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventArgs.xml' path='docs/members[@name="SqlInfoMessageEventArgs"]/ToString/*'/>
        public override string ToString() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlInfoMessageEventHandler.xml' path='docs/members[@name="SqlInfoMessageEventHandler"]/SqlInfoMessageEventHandler/*'/>
    public delegate void SqlInfoMessageEventHandler(object sender, Microsoft.Data.SqlClient.SqlInfoMessageEventArgs e);
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationEventArgs.xml' path='docs/members[@name="SqlNotificationEventArgs"]/SqlNotificationEventArgs/*'/>
    public partial class SqlNotificationEventArgs : System.EventArgs
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationEventArgs.xml' path='docs/members[@name="SqlNotificationEventArgs"]/ctor/*'/>
        public SqlNotificationEventArgs(Microsoft.Data.SqlClient.SqlNotificationType type, Microsoft.Data.SqlClient.SqlNotificationInfo info, Microsoft.Data.SqlClient.SqlNotificationSource source) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationEventArgs.xml' path='docs/members[@name="SqlNotificationEventArgs"]/Info/*'/>
        public Microsoft.Data.SqlClient.SqlNotificationInfo Info { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationEventArgs.xml' path='docs/members[@name="SqlNotificationEventArgs"]/Source/*'/>
        public Microsoft.Data.SqlClient.SqlNotificationSource Source { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationEventArgs.xml' path='docs/members[@name="SqlNotificationEventArgs"]/Type/*'/>
        public Microsoft.Data.SqlClient.SqlNotificationType Type { get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/SqlNotificationInfo/*'/>
    public enum SqlNotificationInfo
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/AlreadyChanged/*'/>
        AlreadyChanged = -2,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Alter/*'/>
        Alter = 5,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Delete/*'/>
        Delete = 3,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Drop/*'/>
        Drop = 4,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Error/*'/>
        Error = 7,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Expired/*'/>
        Expired = 12,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Insert/*'/>
        Insert = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Invalid/*'/>
        Invalid = 9,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Isolation/*'/>
        Isolation = 11,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Merge/*'/>
        Merge = 16,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Options/*'/>
        Options = 10,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/PreviousFire/*'/>
        PreviousFire = 14,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Query/*'/>
        Query = 8,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Resource/*'/>
        Resource = 13,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Restart/*'/>
        Restart = 6,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/TemplateLimit/*'/>
        TemplateLimit = 15,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Truncate/*'/>
        Truncate = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Unknown/*'/>
        Unknown = -1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationInfo.xml' path='docs/members[@name="SqlNotificationInfo"]/Update/*'/>
        Update = 2
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/SqlNotificationSource/*'/>
    public enum SqlNotificationSource
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Client/*'/>
        Client = -2,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Data/*'/>
        Data = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Database/*'/>
        Database = 3,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Environment/*'/>
        Environment = 6,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Execution/*'/>
        Execution = 7,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Object/*'/>
        Object = 2,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Owner/*'/>
        Owner = 8,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Statement/*'/>
        Statement = 5,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/System/*'/>
        System = 4,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Timeout/*'/>
        Timeout = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationSource.xml' path='docs/members[@name="SqlNotificationSource"]/Unknown/*'/>
        Unknown = -1
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationType.xml' path='docs/members[@name="SqlNotificationType"]/SqlNotificationType/*'/>
    public enum SqlNotificationType
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationType.xml' path='docs/members[@name="SqlNotificationType"]/Change/*'/>
        Change = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationType.xml' path='docs/members[@name="SqlNotificationType"]/Subscribe/*'/>
        Subscribe = 1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlNotificationType.xml' path='docs/members[@name="SqlNotificationType"]/Unknown/*'/>
        Unknown = -1
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SqlParameter/*'/>
    public sealed partial class SqlParameter : System.Data.Common.DbParameter, System.Data.IDataParameter, System.Data.IDbDataParameter, System.ICloneable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctor2/*'/>
        public SqlParameter() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbType/*'/>
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbTypeSize/*'/>
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType, int size) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbTypeSizeDirectionIsNullablePrecisionScaleSourceColumnSourceVersionValue/*'/>
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType, int size, System.Data.ParameterDirection direction, bool isNullable, byte precision, byte scale, string sourceColumn, System.Data.DataRowVersion sourceVersion, object value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbTypeSizeDirectionPrecisionScaleSourceColumnSourceVersionSourceColumnNullMappingValue/*'/>
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType, int size, System.Data.ParameterDirection direction, byte precision, byte scale, string sourceColumn, System.Data.DataRowVersion sourceVersion, bool sourceColumnNullMapping, object value, string xmlSchemaCollectionDatabase, string xmlSchemaCollectionOwningSchema, string xmlSchemaCollectionName) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbTypeSizeSourceColumn/*'/>
        public SqlParameter(string parameterName, System.Data.SqlDbType dbType, int size, string sourceColumn) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameValue/*'/>
        public SqlParameter(string parameterName, object value) { }
        object System.ICloneable.Clone() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/CompareInfo/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        public System.Data.SqlTypes.SqlCompareOptions CompareInfo { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/DbType/*'/>
        public override System.Data.DbType DbType { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Direction/*'/>
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        public override System.Data.ParameterDirection Direction { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ForceColumnEncryption/*'/>
        [System.ComponentModel.DefaultValueAttribute(false)]
        public bool ForceColumnEncryption { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/IsNullable/*'/>
        public override bool IsNullable { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/LocaleId/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        public int LocaleId { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Offset/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        public int Offset { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ParameterName/*'/>
        public override string ParameterName { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Precision/*'/>
        [System.ComponentModel.DefaultValueAttribute((byte)0)]
        public new byte Precision { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Scale/*'/>
        [System.ComponentModel.DefaultValueAttribute((byte)0)]
        public new byte Scale { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Size/*'/>
        public override int Size { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SourceColumn/*'/>
        public override string SourceColumn { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SourceColumnNullMapping/*'/>
        public override bool SourceColumnNullMapping { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SourceVersion/*'/>
        public override System.Data.DataRowVersion SourceVersion { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SqlDbType/*'/>
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.Data.Common.DbProviderSpecificTypePropertyAttribute(true)]
        public System.Data.SqlDbType SqlDbType { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SqlValue/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public object SqlValue { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/TypeName/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        public string TypeName { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/UdtTypeName/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        public string UdtTypeName { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Value/*'/>
        [System.ComponentModel.RefreshPropertiesAttribute(System.ComponentModel.RefreshProperties.All)]
        [System.ComponentModel.TypeConverterAttribute(typeof(System.ComponentModel.StringConverter))]
        public override object Value { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/XmlSchemaCollectionDatabase/*'/>
        public string XmlSchemaCollectionDatabase { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/XmlSchemaCollectionName/*'/>
        public string XmlSchemaCollectionName { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/XmlSchemaCollectionOwningSchema/*'/>
        public string XmlSchemaCollectionOwningSchema { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ResetDbType/*'/>
        public override void ResetDbType() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ResetSqlDbType/*'/>
        public void ResetSqlDbType() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ToString/*'/>
        public override string ToString() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SqlParameterCollection/*'/>
    [System.ComponentModel.ListBindableAttribute(false)]
    public sealed partial class SqlParameterCollection : System.Data.Common.DbParameterCollection
    {
        internal SqlParameterCollection() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/Count/*'/>
        public override int Count { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsFixedSize/*'/>
        public override bool IsFixedSize { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsReadOnly/*'/>
        public override bool IsReadOnly { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IsSynchronized/*'/>
        public override bool IsSynchronized { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ItemIndex/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public new Microsoft.Data.SqlClient.SqlParameter this[int index] { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ItemParameterName/*'/>
        [System.ComponentModel.BrowsableAttribute(false)]
        [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
        public new Microsoft.Data.SqlClient.SqlParameter this[string parameterName] { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SyncRoot/*'/>
        public override object SyncRoot { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddValue1/*'/>
        public Microsoft.Data.SqlClient.SqlParameter Add(Microsoft.Data.SqlClient.SqlParameter value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddValue2/*'/>
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        public override int Add(object value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbType/*'/>
        public Microsoft.Data.SqlClient.SqlParameter Add(string parameterName, System.Data.SqlDbType sqlDbType) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbTypeSize/*'/>
        public Microsoft.Data.SqlClient.SqlParameter Add(string parameterName, System.Data.SqlDbType sqlDbType, int size) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameSqlDbTypeSizeSourceColumn/*'/>
        public Microsoft.Data.SqlClient.SqlParameter Add(string parameterName, System.Data.SqlDbType sqlDbType, int size, string sourceColumn) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddParameterNameValue/*'/>
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ObsoleteAttribute("Add(String parameterName, Object value) has been deprecated.  Use AddWithValue(String parameterName, Object value).  http://go.microsoft.com/fwlink/?linkid=14202", false)]
        public Microsoft.Data.SqlClient.SqlParameter Add(string parameterName, object value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddRangeValues1/*'/>
        public void AddRange(Microsoft.Data.SqlClient.SqlParameter[] values) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddRangeValues2/*'/>
        public override void AddRange(System.Array values) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/AddWithValue/*'/>
        public Microsoft.Data.SqlClient.SqlParameter AddWithValue(string parameterName, object value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/Clear/*'/>
        public override void Clear() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue1/*'/>
        public bool Contains(Microsoft.Data.SqlClient.SqlParameter value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue2/*'/>
        public override bool Contains(object value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/ContainsValue3/*'/>
        public override bool Contains(string value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/CopyToArrayIndex1/*'/>
        public override void CopyTo(System.Array array, int index) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/CopyToArrayIndex2/*'/>
        public void CopyTo(Microsoft.Data.SqlClient.SqlParameter[] array, int index) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetEnumerator/*'/>
        public override System.Collections.IEnumerator GetEnumerator() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetParameterIndex/*'/>
        protected override System.Data.Common.DbParameter GetParameter(int index) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/GetParameterParameterName/*'/>
        protected override System.Data.Common.DbParameter GetParameter(string parameterName) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfValue1/*'/>
        public int IndexOf(Microsoft.Data.SqlClient.SqlParameter value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfValue2/*'/>
        public override int IndexOf(object value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/IndexOfParameterName/*'/>
        public override int IndexOf(string parameterName) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/InsertIndexValue1/*'/>
        public void Insert(int index, Microsoft.Data.SqlClient.SqlParameter value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/InsertIndexValue2/*'/>
        public override void Insert(int index, object value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveValue1/*'/>
        public void Remove(Microsoft.Data.SqlClient.SqlParameter value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveValue2/*'/>
        public override void Remove(object value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveAtIndex/*'/>
        public override void RemoveAt(int index) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/RemoveAtParameterName/*'/>
        public override void RemoveAt(string parameterName) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SetParameterIndexValue/*'/>
        protected override void SetParameter(int index, System.Data.Common.DbParameter value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameterCollection.xml' path='docs/members[@name="SqlParameterCollection"]/SetParameterParameterNameValue/*'/>
        protected override void SetParameter(string parameterName, System.Data.Common.DbParameter value) { }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventArgs.xml' path='docs/members[@name="SqlRowsCopiedEventArgs"]/SqlRowsCopiedEventArgs/*'/>
    public partial class SqlRowsCopiedEventArgs : System.EventArgs
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventArgs.xml' path='docs/members[@name="SqlRowsCopiedEventArgs"]/ctor/*'/>
        public SqlRowsCopiedEventArgs(long rowsCopied) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventArgs.xml' path='docs/members[@name="SqlRowsCopiedEventArgs"]/Abort/*'/>
        public bool Abort { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventArgs.xml' path='docs/members[@name="SqlRowsCopiedEventArgs"]/RowsCopied/*'/>
        public long RowsCopied { get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowsCopiedEventHandler.xml' path='docs/members[@name="SqlRowsCopiedEventHandler"]/SqlRowsCopiedEventHandler/*'/>
    public delegate void SqlRowsCopiedEventHandler(object sender, Microsoft.Data.SqlClient.SqlRowsCopiedEventArgs e);
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatedEventArgs.xml' path='docs/members[@name="SqlRowUpdatedEventArgs"]/SqlRowUpdatedEventArgs/*'/>
    public sealed partial class SqlRowUpdatedEventArgs : System.Data.Common.RowUpdatedEventArgs
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatedEventArgs.xml' path='docs/members[@name="SqlRowUpdatedEventArgs"]/ctor/*'/>
        public SqlRowUpdatedEventArgs(System.Data.DataRow row, System.Data.IDbCommand command, System.Data.StatementType statementType, System.Data.Common.DataTableMapping tableMapping)
            : base(row, command, statementType, tableMapping) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatedEventArgs.xml' path='docs/members[@name="SqlRowUpdatedEventArgs"]/Command/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand Command { get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatedEventHandler.xml' path='docs/members[@name="SqlRowUpdatedEventHandler"]/SqlRowUpdatedEventHandler/*'/>
    public delegate void SqlRowUpdatedEventHandler(object sender, Microsoft.Data.SqlClient.SqlRowUpdatedEventArgs e);
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventArgs.xml' path='docs/members[@name="SqlRowUpdatingEventArgs"]/SqlRowUpdatingEventArgs/*'/>
    public sealed partial class SqlRowUpdatingEventArgs : System.Data.Common.RowUpdatingEventArgs
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventArgs.xml' path='docs/members[@name="SqlRowUpdatingEventArgs"]/ctor/*'/>
        public SqlRowUpdatingEventArgs(System.Data.DataRow row, System.Data.IDbCommand command, System.Data.StatementType statementType, System.Data.Common.DataTableMapping tableMapping)
            : base(row, command, statementType, tableMapping) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventArgs.xml' path='docs/members[@name="SqlRowUpdatingEventArgs"]/BaseCommand/*'/>
        protected override System.Data.IDbCommand BaseCommand { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventArgs.xml' path='docs/members[@name="SqlRowUpdatingEventArgs"]/Command/*'/>
        public new Microsoft.Data.SqlClient.SqlCommand Command { get { throw null; } set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRowUpdatingEventHandler.xml' path='docs/members[@name="SqlRowUpdatingEventHandler"]/SqlRowUpdatingEventHandler/*'/>
    public delegate void SqlRowUpdatingEventHandler(object sender, Microsoft.Data.SqlClient.SqlRowUpdatingEventArgs e);
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/SqlTransaction/*'/>
    public sealed partial class SqlTransaction : System.Data.Common.DbTransaction
    {
        internal SqlTransaction() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Connection/*'/>
        public new Microsoft.Data.SqlClient.SqlConnection Connection { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/DbConnection/*'/>
        protected override System.Data.Common.DbConnection DbConnection { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/IsolationLevel/*'/>
        public override System.Data.IsolationLevel IsolationLevel { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Commit/*'/>
        public override void Commit() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Dispose/*'/>
        protected override void Dispose(bool disposing) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Rollback1/*'/>
        public override void Rollback() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Rollback2/*'/>
        public void Rollback(string transactionName) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlTransaction.xml' path='docs/members[@name="SqlTransaction"]/Save/*'/>
        public void Save(string savePointName) { }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/SqlRetryingEventArgs/*' />
    public sealed class SqlRetryingEventArgs : System.EventArgs
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/ctor/*' />
        public SqlRetryingEventArgs(int retryCount, System.TimeSpan delay, System.Collections.Generic.IList<System.Exception> exceptions) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/RetryCount/*' />
        public int RetryCount { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/Delay/*' />
        public System.TimeSpan Delay { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/Cancel/*' />
        public bool Cancel { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/Exceptions/*' />
        public System.Collections.Generic.IList<System.Exception> Exceptions { get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/SqlRetryIntervalBaseEnumerator/*' />
    public abstract class SqlRetryIntervalBaseEnumerator : System.Collections.Generic.IEnumerator<System.TimeSpan>, System.ICloneable
    {
        private readonly System.TimeSpan _minValue = System.TimeSpan.Zero;
        private readonly System.TimeSpan _maxValue = System.TimeSpan.FromSeconds(120);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/GapTimeInterval/*' />
        public System.TimeSpan GapTimeInterval { get { throw null; } protected set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/MaxTimeInterval/*' />
        public System.TimeSpan MaxTimeInterval { get { throw null; } protected set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/MinTimeInterval/*' />
        public System.TimeSpan MinTimeInterval { get { throw null; } protected set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Current/*' />
        public System.TimeSpan Current { get { throw null; } protected set { } }
        object System.Collections.IEnumerator.Current { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/ctor1/*' />
        public SqlRetryIntervalBaseEnumerator() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/ctor2/*' />
        public SqlRetryIntervalBaseEnumerator(System.TimeSpan timeInterval, System.TimeSpan maxTime, System.TimeSpan minTime) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Reset/*' />
        public virtual void Reset() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Validate/*' />
        protected virtual void Validate(System.TimeSpan timeInterval, System.TimeSpan maxTimeInterval, System.TimeSpan minTimeInterval) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/GetNextInterval/*' />
        protected abstract System.TimeSpan GetNextInterval();
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/MoveNext/*' />
        public virtual bool MoveNext() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Dispose/*' />
        public virtual void Dispose() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Clone/*' />
        public virtual object Clone() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/SqlRetryLogicBase/*' />
    public abstract class SqlRetryLogicBase : System.ICloneable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/NumberOfTries/*' />
        public int NumberOfTries { get { throw null; } protected set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/Current/*' />
        public int Current { get { throw null; } protected set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/RetryIntervalEnumerator/*' />
        public SqlRetryIntervalBaseEnumerator RetryIntervalEnumerator { get { throw null; } protected set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/TransientPredicate/*' />
        public System.Predicate<System.Exception> TransientPredicate { get { throw null; } protected set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/RetryCondition/*' />
        public virtual bool RetryCondition(object sender) { throw null; }
        ///<include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/TryNextInterval/*' />
        public abstract bool TryNextInterval(out System.TimeSpan intervalTime);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/Reset/*' />
        public abstract void Reset();
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBase.xml' path='docs/members[@name="SqlRetryLogicBase"]/Clone/*' />
        public virtual object Clone() { throw null; }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/SqlRetryLogicBaseProvider/*' />
    public abstract class SqlRetryLogicBaseProvider
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/Retrying/*' />
        public System.EventHandler<SqlRetryingEventArgs> Retrying { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/RetryLogic/*' />
        public SqlRetryLogicBase RetryLogic { get { throw null; } protected set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/Execute/*' />
        public abstract TResult Execute<TResult>(object sender, System.Func<TResult> function);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/ExecuteAsync1/*' />
        public abstract System.Threading.Tasks.Task<TResult> ExecuteAsync<TResult>(object sender, System.Func<System.Threading.Tasks.Task<TResult>> function, System.Threading.CancellationToken cancellationToken = default);
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/ExecuteAsync2/*' />
        public abstract System.Threading.Tasks.Task ExecuteAsync(object sender, System.Func<System.Threading.Tasks.Task> function, System.Threading.CancellationToken cancellationToken = default);
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/SqlRetryLogicOption/*' />
    public sealed class SqlRetryLogicOption
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/NumberOfTries/*' />
        public int NumberOfTries { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/DeltaTime/*' />
        public System.TimeSpan DeltaTime { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/MinTimeInterval/*' />
        public System.TimeSpan MinTimeInterval { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/MaxTimeInterval/*' />
        public System.TimeSpan MaxTimeInterval { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/TransientErrors/*' />
        public System.Collections.Generic.IEnumerable<int> TransientErrors { get { throw null; } set { } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/AuthorizedSqlCondition/*' />
        public System.Predicate<string> AuthorizedSqlCondition { get { throw null; } set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/SqlConfigurableRetryFactory/*' />
    public sealed class SqlConfigurableRetryFactory
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/CreateExponentialRetryProvider/*' />
        public static SqlRetryLogicBaseProvider CreateExponentialRetryProvider(SqlRetryLogicOption retryLogicOption) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/CreateIncrementalRetryProvider/*' />
        public static SqlRetryLogicBaseProvider CreateIncrementalRetryProvider(SqlRetryLogicOption retryLogicOption) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/CreateFixedRetryProvider/*' />
        public static SqlRetryLogicBaseProvider CreateFixedRetryProvider(SqlRetryLogicOption retryLogicOption) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/CreateNoneRetryProvider/*' />
        public static SqlRetryLogicBaseProvider CreateNoneRetryProvider() { throw null; }
    }
}
namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SqlDataRecord/*'/>
    public partial class SqlDataRecord : System.Data.IDataRecord
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ctor/*'/>
        public SqlDataRecord(params Microsoft.Data.SqlClient.Server.SqlMetaData[] metaData) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/FieldCount/*'/>
        public virtual int FieldCount { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ItemOrdinal/*'/>
        public virtual object this[int ordinal] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/ItemName/*'/>
        public virtual object this[string name] { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetBoolean/*'/>
        public virtual bool GetBoolean(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetByte/*'/>
        public virtual byte GetByte(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetBytes/*'/>
        public virtual long GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetChar/*'/>
        public virtual char GetChar(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetChars/*'/>
        public virtual long GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetData/*'/>
        System.Data.IDataReader System.Data.IDataRecord.GetData(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDataTypeName/*'/>
        public virtual string GetDataTypeName(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDateTime/*'/>
        public virtual System.DateTime GetDateTime(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDateTimeOffset/*'/>
        public virtual System.DateTimeOffset GetDateTimeOffset(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDecimal/*'/>
        public virtual decimal GetDecimal(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetDouble/*'/>
        public virtual double GetDouble(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetFieldType/*'/>
        public virtual System.Type GetFieldType(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetFloat/*'/>
        public virtual float GetFloat(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetGuid/*'/>
        public virtual System.Guid GetGuid(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt16/*'/>
        public virtual short GetInt16(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt32/*'/>
        public virtual int GetInt32(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetInt64/*'/>
        public virtual long GetInt64(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetName/*'/>
        public virtual string GetName(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetOrdinal/*'/>
        public virtual int GetOrdinal(string name) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBinary/*'/>
        public virtual System.Data.SqlTypes.SqlBinary GetSqlBinary(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBoolean/*'/>
        public virtual System.Data.SqlTypes.SqlBoolean GetSqlBoolean(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlByte/*'/>
        public virtual System.Data.SqlTypes.SqlByte GetSqlByte(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlBytes/*'/>
        public virtual System.Data.SqlTypes.SqlBytes GetSqlBytes(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlChars/*'/>
        public virtual System.Data.SqlTypes.SqlChars GetSqlChars(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDateTime/*'/>
        public virtual System.Data.SqlTypes.SqlDateTime GetSqlDateTime(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDecimal/*'/>
        public virtual System.Data.SqlTypes.SqlDecimal GetSqlDecimal(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlDouble/*'/>
        public virtual System.Data.SqlTypes.SqlDouble GetSqlDouble(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlFieldType/*'/>
        public virtual System.Type GetSqlFieldType(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlGuid/*'/>
        public virtual System.Data.SqlTypes.SqlGuid GetSqlGuid(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt16/*'/>
        public virtual System.Data.SqlTypes.SqlInt16 GetSqlInt16(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt32/*'/>
        public virtual System.Data.SqlTypes.SqlInt32 GetSqlInt32(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlInt64/*'/>
        public virtual System.Data.SqlTypes.SqlInt64 GetSqlInt64(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlMetaData/*'/>
        public virtual Microsoft.Data.SqlClient.Server.SqlMetaData GetSqlMetaData(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlMoney/*'/>
        public virtual System.Data.SqlTypes.SqlMoney GetSqlMoney(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlSingle/*'/>
        public virtual System.Data.SqlTypes.SqlSingle GetSqlSingle(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlString/*'/>
        public virtual System.Data.SqlTypes.SqlString GetSqlString(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlValue/*'/>
        public virtual object GetSqlValue(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlValues/*'/>
        public virtual int GetSqlValues(object[] values) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetSqlXml/*'/>
        public virtual System.Data.SqlTypes.SqlXml GetSqlXml(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetString/*'/>
        public virtual string GetString(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetTimeSpan/*'/>
        public virtual System.TimeSpan GetTimeSpan(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetValue/*'/>
        public virtual object GetValue(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/GetValues/*'/>
        public virtual int GetValues(object[] values) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/IsDBNull/*'/>
        public virtual bool IsDBNull(int ordinal) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetBoolean/*'/>
        public virtual void SetBoolean(int ordinal, bool value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetByte/*'/>
        public virtual void SetByte(int ordinal, byte value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetBytes/*'/>
        public virtual void SetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetChar/*'/>
        public virtual void SetChar(int ordinal, char value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetChars/*'/>
        public virtual void SetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDateTime/*'/>
        public virtual void SetDateTime(int ordinal, System.DateTime value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDateTimeOffset/*'/>
        public virtual void SetDateTimeOffset(int ordinal, System.DateTimeOffset value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDBNull/*'/>
        public virtual void SetDBNull(int ordinal) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDecimal/*'/>
        public virtual void SetDecimal(int ordinal, decimal value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetDouble/*'/>
        public virtual void SetDouble(int ordinal, double value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetFloat/*'/>
        public virtual void SetFloat(int ordinal, float value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetGuid/*'/>
        public virtual void SetGuid(int ordinal, System.Guid value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt16/*'/>
        public virtual void SetInt16(int ordinal, short value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt32/*'/>
        public virtual void SetInt32(int ordinal, int value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetInt64/*'/>
        public virtual void SetInt64(int ordinal, long value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBinary/*'/>
        public virtual void SetSqlBinary(int ordinal, System.Data.SqlTypes.SqlBinary value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBoolean/*'/>
        public virtual void SetSqlBoolean(int ordinal, System.Data.SqlTypes.SqlBoolean value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlByte/*'/>
        public virtual void SetSqlByte(int ordinal, System.Data.SqlTypes.SqlByte value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlBytes/*'/>
        public virtual void SetSqlBytes(int ordinal, System.Data.SqlTypes.SqlBytes value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlChars/*'/>
        public virtual void SetSqlChars(int ordinal, System.Data.SqlTypes.SqlChars value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDateTime/*'/>
        public virtual void SetSqlDateTime(int ordinal, System.Data.SqlTypes.SqlDateTime value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDecimal/*'/>
        public virtual void SetSqlDecimal(int ordinal, System.Data.SqlTypes.SqlDecimal value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlDouble/*'/>
        public virtual void SetSqlDouble(int ordinal, System.Data.SqlTypes.SqlDouble value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlGuid/*'/>
        public virtual void SetSqlGuid(int ordinal, System.Data.SqlTypes.SqlGuid value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt16/*'/>
        public virtual void SetSqlInt16(int ordinal, System.Data.SqlTypes.SqlInt16 value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt32/*'/>
        public virtual void SetSqlInt32(int ordinal, System.Data.SqlTypes.SqlInt32 value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlInt64/*'/>
        public virtual void SetSqlInt64(int ordinal, System.Data.SqlTypes.SqlInt64 value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlMoney/*'/>
        public virtual void SetSqlMoney(int ordinal, System.Data.SqlTypes.SqlMoney value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlSingle/*'/>
        public virtual void SetSqlSingle(int ordinal, System.Data.SqlTypes.SqlSingle value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlString/*'/>
        public virtual void SetSqlString(int ordinal, System.Data.SqlTypes.SqlString value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetSqlXml/*'/>
        public virtual void SetSqlXml(int ordinal, System.Data.SqlTypes.SqlXml value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetString/*'/>
        public virtual void SetString(int ordinal, string value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetTimeSpan/*'/>
        public virtual void SetTimeSpan(int ordinal, System.TimeSpan value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetValue/*'/>
        public virtual void SetValue(int ordinal, object value) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlDataRecord.xml' path='docs/members[@name="SqlDataRecord"]/SetValues/*'/>
        public virtual int SetValues(params object[] values) { throw null; }
    }
    /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SqlMetaData/*' />
    public sealed partial class SqlMetaData
    {
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbType/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypePrecisionScale/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, byte precision, byte scale) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypePrecisionScaleUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, byte precision, byte scale, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLength/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthPrecisionScaleLocaleCompareOptionsUserDefinedType/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, byte precision, byte scale, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions, System.Type userDefinedType) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthPrecisionScaleLocaleCompareOptionsUserDefinedTypeUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, byte precision, byte scale, long localeId, System.Data.SqlTypes.SqlCompareOptions compareOptions, System.Type userDefinedType, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthLocaleCompareOptions/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeMaxLengthLocaleCompareOptionsUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, long maxLength, long locale, System.Data.SqlTypes.SqlCompareOptions compareOptions, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeDatabaseOwningSchemaObjectName/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, string database, string owningSchema, string objectName) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeDatabaseOwningSchemaObjectNameUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, string database, string owningSchema, string objectName, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedType/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedTypeServerTypeName/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType, string serverTypeName) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/ctorNameDbTypeUserDefinedTypeServerTypeNameUseServerDefaultIsUniqueKeyColumnSortOrderSortOrdinal/*' />
        public SqlMetaData(string name, System.Data.SqlDbType dbType, System.Type userDefinedType, string serverTypeName, bool useServerDefault, bool isUniqueKey, Microsoft.Data.SqlClient.SortOrder columnSortOrder, int sortOrdinal) { }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/CompareOptions/*' />
        public System.Data.SqlTypes.SqlCompareOptions CompareOptions { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/DbType/*' />
        public System.Data.DbType DbType { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/IsUniqueKey/*' />
        public bool IsUniqueKey { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/LocaleId/*' />
        public long LocaleId { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Max/*' />
        public static long Max { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/MaxLength/*' />
        public long MaxLength { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Name/*' />
        public string Name { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Precision/*' />
        public byte Precision { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Scale/*' />
        public byte Scale { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SortOrder/*' />
        public Microsoft.Data.SqlClient.SortOrder SortOrder { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SortOrdinal/*' />
        public int SortOrdinal { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/SqlDbType/*' />
        public System.Data.SqlDbType SqlDbType { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/Type/*' />
        public System.Type Type { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/TypeName/*' />
        public string TypeName { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/UseServerDefault/*' />
        public bool UseServerDefault { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionDatabase/*' />
        public string XmlSchemaCollectionDatabase { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionName/*' />
        public string XmlSchemaCollectionName { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/XmlSchemaCollectionOwningSchema/*' />
        public string XmlSchemaCollectionOwningSchema { get { throw null; } }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue1/*' />
        public bool Adjust(bool value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue2/*' />
        public byte Adjust(byte value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue3/*' />
        public byte[] Adjust(byte[] value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue4/*' />
        public char Adjust(char value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue5/*' />
        public char[] Adjust(char[] value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue6/*' />
        public System.Data.SqlTypes.SqlBinary Adjust(System.Data.SqlTypes.SqlBinary value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue7/*' />
        public System.Data.SqlTypes.SqlBoolean Adjust(System.Data.SqlTypes.SqlBoolean value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue8/*' />
        public System.Data.SqlTypes.SqlByte Adjust(System.Data.SqlTypes.SqlByte value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue9/*' />
        public System.Data.SqlTypes.SqlBytes Adjust(System.Data.SqlTypes.SqlBytes value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue10/*' />
        public System.Data.SqlTypes.SqlChars Adjust(System.Data.SqlTypes.SqlChars value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue11/*' />
        public System.Data.SqlTypes.SqlDateTime Adjust(System.Data.SqlTypes.SqlDateTime value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue12/*' />
        public System.Data.SqlTypes.SqlDecimal Adjust(System.Data.SqlTypes.SqlDecimal value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue13/*' />
        public System.Data.SqlTypes.SqlDouble Adjust(System.Data.SqlTypes.SqlDouble value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue14/*' />
        public System.Data.SqlTypes.SqlGuid Adjust(System.Data.SqlTypes.SqlGuid value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue15/*' />
        public System.Data.SqlTypes.SqlInt16 Adjust(System.Data.SqlTypes.SqlInt16 value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue16/*' />
        public System.Data.SqlTypes.SqlInt32 Adjust(System.Data.SqlTypes.SqlInt32 value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue17/*' />
        public System.Data.SqlTypes.SqlInt64 Adjust(System.Data.SqlTypes.SqlInt64 value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue18/*' />
        public System.Data.SqlTypes.SqlMoney Adjust(System.Data.SqlTypes.SqlMoney value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue19/*' />
        public System.Data.SqlTypes.SqlSingle Adjust(System.Data.SqlTypes.SqlSingle value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue20/*' />
        public System.Data.SqlTypes.SqlString Adjust(System.Data.SqlTypes.SqlString value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue21/*' />
        public System.Data.SqlTypes.SqlXml Adjust(System.Data.SqlTypes.SqlXml value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue22/*' />
        public System.DateTime Adjust(System.DateTime value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue23/*' />
        public System.DateTimeOffset Adjust(System.DateTimeOffset value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue24/*' />
        public decimal Adjust(decimal value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue25/*' />
        public double Adjust(double value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue26/*' />
        public System.Guid Adjust(System.Guid value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue27/*' />
        public short Adjust(short value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue28/*' />
        public int Adjust(int value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue29/*' />
        public long Adjust(long value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue30/*' />
        public object Adjust(object value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue31/*' />
        public float Adjust(float value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue32/*' />
        public string Adjust(string value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/AdjustValue33/*' />
        public System.TimeSpan Adjust(System.TimeSpan value) { throw null; }
        /// <include file='./../../../../doc/snippets/Microsoft.Data.SqlClient.Server/SqlMetaData.xml' path='docs/members[@name="SqlMetaData"]/InferFromValue/*' />
        public static Microsoft.Data.SqlClient.Server.SqlMetaData InferFromValue(object value, string name) { throw null; }
    }
}
namespace Microsoft.Data.SqlClient.DataClassification
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ColumnSensitivity/*' />
    public partial class ColumnSensitivity
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ctor/*' />
        public ColumnSensitivity(System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.SensitivityProperty> sensitivityProperties) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/GetSensitivityProperties/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.SensitivityProperty> SensitivityProperties { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/InformationType/*' />
    public partial class InformationType
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/ctor/*' />
        public InformationType(string name, string id) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/Id/*' />
        public string Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/Name/*' />
        public string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Label/*' />
    public partial class Label
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/ctor/*' />
        public Label(string name, string id) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Id/*' />
        public string Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Name/*' />
        public string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/SensitivityRank/*' />
    public enum SensitivityRank
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/NotDefined/*' />
        NOT_DEFINED = -1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/None/*' />
        NONE = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Low/*' />
        LOW = 10,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Medium/*' />
        MEDIUM = 20,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/High/*' />
        HIGH = 30,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Critical/*' />
        CRITICAL = 40
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityClassification/*' />
    public partial class SensitivityClassification
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ctor/*' />
        public SensitivityClassification(System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.Label> labels, System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.InformationType> informationTypes, System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.ColumnSensitivity> columnSensitivity, SensitivityRank sensitivityRank) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ColumnSensitivities/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.ColumnSensitivity> ColumnSensitivities { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/InformationTypes/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.InformationType> InformationTypes { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/Labels/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.Label> Labels { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityRank/*' />
        public SensitivityRank SensitivityRank { get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityProperty/*' />
    public partial class SensitivityProperty
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/ctor/*' />
        public SensitivityProperty(Microsoft.Data.SqlClient.DataClassification.Label label, Microsoft.Data.SqlClient.DataClassification.InformationType informationType, SensitivityRank sensitivityRank) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/InformationType/*' />
        public Microsoft.Data.SqlClient.DataClassification.InformationType InformationType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/Label/*' />
        public Microsoft.Data.SqlClient.DataClassification.Label Label { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityRank/*' />
        public SensitivityRank SensitivityRank { get { throw null; } }
    }
}
namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SqlFileStream/*' />
    public sealed partial class SqlFileStream : System.IO.Stream
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor1/*' />
        public SqlFileStream(string path, byte[] transactionContext, System.IO.FileAccess access) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor2/*' />
        public SqlFileStream(string path, byte[] transactionContext, System.IO.FileAccess access, System.IO.FileOptions options, System.Int64 allocationSize) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Name/*' />
        public string Name { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/TransactionContext/*' />
        public byte[] TransactionContext { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanRead/*' />
        public override bool CanRead { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanSeek/*' />
        public override bool CanSeek { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanTimeout/*' />
        public override bool CanTimeout { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanWrite/*' />
        public override bool CanWrite { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Length/*' />
        public override long Length { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Position/*' />
        public override long Position { get { throw null; } set { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadTimeout/*' />
        public override int ReadTimeout { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteTimeout/*' />
        public override int WriteTimeout { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Flush/*' />
        public override void Flush() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginRead/*' />
        public override System.IAsyncResult BeginRead(byte[] buffer, int offset, int count, System.AsyncCallback callback, object state) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndRead/*' />
        public override int EndRead(System.IAsyncResult asyncResult) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginWrite/*' />
        public override System.IAsyncResult BeginWrite(byte[] buffer, int offset, int count, System.AsyncCallback callback, System.Object state) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndWrite/*' />
        public override void EndWrite(System.IAsyncResult asyncResult) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Seek/*' />
        public override long Seek(long offset, System.IO.SeekOrigin origin) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SetLength/*' />
        public override void SetLength(long value) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Read/*' />
        public override int Read(byte[] buffer, int offset, int count) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadByte/*' />
        public override int ReadByte() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Write/*' />
        public override void Write(byte[] buffer, int offset, int count) { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteByte/*' />
        public override void WriteByte(byte value) { }
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/SqlJson/*' />
    public class SqlJson : System.Data.SqlTypes.INullable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor1/*' />
        public SqlJson() { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor2/*' />
        public SqlJson(string jsonString) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor3/*' />
        public SqlJson(System.Text.Json.JsonDocument jsonDoc) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/IsNull/*' />
        public bool IsNull => throw null; 
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/Null/*' />
        public static SqlJson Null => throw null;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/Value/*' />
        public string Value { get { throw null; } }
    }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/SqlVectorFloat32/*' />
    public class SqlVectorFloat32 : System.Data.SqlTypes.INullable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ctor1/*' />
        public SqlVectorFloat32(int length) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ctor2/*' />
        public SqlVectorFloat32(float[] values) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/IsNull/*' />
        public bool IsNull => throw null;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Null/*' />
        public SqlVectorFloat32 Null => throw null;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Length/*' />
        public int Length { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Values/*' />
        public float[] Values { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ToString/*' />
        public override string ToString() { throw null; }
    }
}
