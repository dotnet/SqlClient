// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ApplicationIntent/*'/>
public enum ApplicationIntent
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ReadOnly/*'/>
    ReadOnly = 1,
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/ApplicationIntent.xml' path='docs/members[@name="ApplicationIntent"]/ReadWrite/*'/>
    ReadWrite = 0
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/PoolBlockingPeriod/*'/>
public enum PoolBlockingPeriod
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/Auto/*'/>
    Auto = 0,
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/PoolBlockingPeriod.xml' path='docs/members[@name="PoolBlockingPeriod"]/AlwaysBlock/*'/>
    AlwaysBlock = 1,
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
public abstract class SqlAuthenticationInitializer
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/ctor/*'/>
    protected SqlAuthenticationInitializer() { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationInitializer.xml' path='docs/members[@name="SqlAuthenticationInitializer"]/Initialize/*'/>
    public abstract void Initialize();
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/SqlBulkCopy/*'/>
public sealed class SqlBulkCopy : System.IDisposable
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
public sealed class SqlBulkCopyColumnMapping
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="default"]/*'/>
    public SqlBulkCopyColumnMapping() { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnOrdinalIntegerAndDestinationOrdinalInteger"]/*'/>
    public SqlBulkCopyColumnMapping(int sourceColumnOrdinal, int destinationOrdinal) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnOrdinalIntegerAndDestinationColumnString"]/*'/>
    public SqlBulkCopyColumnMapping(int sourceColumnOrdinal, string destinationColumn) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnStringAndDestinationOrdinalInteger"]/*'/>
    public SqlBulkCopyColumnMapping(string sourceColumn, int destinationOrdinal) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnStringAndDestinationColumnString"]/*'/>
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
public sealed class SqlBulkCopyColumnMappingCollection : System.Collections.CollectionBase
{
    internal SqlBulkCopyColumnMappingCollection() { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Item/*'/>
    public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping this[int index] { get { throw null; } }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="bulkCopyColumnMappingParameter"]/*'/>
    public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping bulkCopyColumnMapping) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnIndexIntegerAndDestinationColumnIndexInteger"]/*'/>
    public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(int sourceColumnIndex, int destinationColumnIndex) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnIndexIntegerAndDestinationColumnString"]/*'/>
    public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(int sourceColumnIndex, string destinationColumn) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnStringAndDestinationColumnIndexInteger"]/*'/>
    public Microsoft.Data.SqlClient.SqlBulkCopyColumnMapping Add(string sourceColumn, int destinationColumnIndex) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMappingCollection.xml' path='docs/members[@name="SqlBulkCopyColumnMappingCollection"]/Add[@name="sourceColumnStringAndDestinationColumnString"]/*'/>
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

#if NETFRAMEWORK
public sealed class SqlClientFactory : System.Data.Common.DbProviderFactory, System.IServiceProvider
#else
public sealed class SqlClientFactory : System.Data.Common.DbProviderFactory
#endif
{
    internal SqlClientFactory() { }

    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/Instance/*'/>
    public static readonly Microsoft.Data.SqlClient.SqlClientFactory Instance;

    #if NET
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CanCreateBatch/*'/>
    public override bool CanCreateBatch { get { throw null; } }
    #endif

    #if NET
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatch/*'/>
    public override System.Data.Common.DbBatch CreateBatch() { throw null; }
    #endif
    #if NET
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatchCommand/*'/>
    public override System.Data.Common.DbBatchCommand CreateBatchCommand() { throw null; }
    #endif
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

    #if NETFRAMEWORK
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreatePermission/*'/>
    public override System.Security.CodeAccessPermission CreatePermission(System.Security.Permissions.PermissionState state) { throw null; }
    #endif

    #if NETFRAMEWORK
    object System.IServiceProvider.GetService(System.Type serviceType) { throw null; }
    #endif
}

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/SqlClientLogger/*'/>
public class SqlClientLogger
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
public static class SqlClientMetaDataCollectionNames
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

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/SqlColumnEncryptionCertificateStoreProvider/*'/>
public class SqlColumnEncryptionCertificateStoreProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
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
public class SqlColumnEncryptionCngProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
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
public class SqlColumnEncryptionCspProvider : Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider
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
public abstract class SqlColumnEncryptionKeyStoreProvider
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
