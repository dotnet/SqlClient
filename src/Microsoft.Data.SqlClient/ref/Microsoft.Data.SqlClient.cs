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

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/SqlCommand/*'/>
[System.ComponentModel.DefaultEventAttribute("RecordsAffected")]
[System.ComponentModel.ToolboxItemAttribute(true)]
public sealed class SqlCommand : System.Data.Common.DbCommand, System.ICloneable
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
    #if NETFRAMEWORK
    [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
    #endif
    public System.IAsyncResult BeginExecuteNonQuery() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteNonQuery[@name="AsyncCallbackAndStateObject"]/*'/>
    #if NETFRAMEWORK
    [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
    #endif
    public System.IAsyncResult BeginExecuteNonQuery(System.AsyncCallback callback, object stateObject) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="default"]/*'/>
    #if NETFRAMEWORK
    [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
    #endif
    public System.IAsyncResult BeginExecuteReader() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="AsyncCallbackAndStateObject"]/*'/>
    #if NETFRAMEWORK
    [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
    #endif
    public System.IAsyncResult BeginExecuteReader(System.AsyncCallback callback, object stateObject) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="AsyncCallbackAndStateObjectAndCommandBehavior"]/*'/>
    #if NETFRAMEWORK
    [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
    #endif
    public System.IAsyncResult BeginExecuteReader(System.AsyncCallback callback, object stateObject, System.Data.CommandBehavior behavior) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="CommandBehavior"]/*'/>
    #if NETFRAMEWORK
    [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
    #endif
    public System.IAsyncResult BeginExecuteReader(System.Data.CommandBehavior behavior) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteXmlReader[@name="default"]/*'/>
    #if NETFRAMEWORK
    [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
    #endif
    public System.IAsyncResult BeginExecuteXmlReader() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteXmlReader[@name="AsyncCallbackAndstateObject"]/*'/>
    #if NETFRAMEWORK
    [System.Security.Permissions.HostProtectionAttribute(System.Security.Permissions.SecurityAction.LinkDemand, ExternalThreading = true)]
    #endif
    public System.IAsyncResult BeginExecuteXmlReader(System.AsyncCallback callback, object stateObject) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Cancel/*'/>
    public override void Cancel() { }
    // @TODO: Document?
    object System.ICloneable.Clone() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Clone/*'/>
    public Microsoft.Data.SqlClient.SqlCommand Clone() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CreateDbParameter/*'/>
    protected override System.Data.Common.DbParameter CreateDbParameter() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CreateParameter/*'/>
    public new Microsoft.Data.SqlClient.SqlParameter CreateParameter() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EndExecuteNonQuery[@name="IAsyncResult"]/*'/>
    public int EndExecuteNonQuery(System.IAsyncResult asyncResult) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EndExecuteReader[@name="IAsyncResult"]/*'/>
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
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/RegisterColumnEncryptionKeyStoreProvidersOnCommand/*' />
    public void RegisterColumnEncryptionKeyStoreProvidersOnCommand(System.Collections.Generic.IDictionary<string, Microsoft.Data.SqlClient.SqlColumnEncryptionKeyStoreProvider> customProviders) { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ResetCommandTimeout/*'/>
    [System.ComponentModel.DesignerSerializationVisibilityAttribute(System.ComponentModel.DesignerSerializationVisibility.Content)]
    public void ResetCommandTimeout() { }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/RetryLogicProvider/*' />
    public SqlRetryLogicBaseProvider RetryLogicProvider { get { throw null; } set { } }

    #if NETFRAMEWORK
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Dispose/*'/>
    protected override void Dispose(bool disposing) { }
    #endif

    #if NETFRAMEWORK
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/NotificationAutoEnlist/*'/>
    [System.ComponentModel.DefaultValueAttribute(true)]
    public bool NotificationAutoEnlist { get { throw null; } set { } }
    #endif
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

/// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/SqlConnection/*'/>
[System.ComponentModel.DefaultEventAttribute("InfoMessage")]
public sealed class SqlConnection : System.Data.Common.DbConnection, System.ICloneable
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
    #if NETFRAMEWORK
    [System.ComponentModel.DefaultValueAttribute(null)]
    #endif
    public static System.TimeSpan ColumnEncryptionKeyCacheTtl { get { throw null; } set { } }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionQueryMetadataCacheEnabled/*'/>
    #if NETFRAMEWORK
    [System.ComponentModel.DefaultValueAttribute(null)]
    #endif
    public static bool ColumnEncryptionQueryMetadataCacheEnabled { get { throw null; } set { } }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionTrustedMasterKeyPaths/*'/>
    #if NETFRAMEWORK
    [System.ComponentModel.DefaultValueAttribute(null)]
    #endif
    public static System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<string>> ColumnEncryptionTrustedMasterKeyPaths { get { throw null; } }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CommandTimeout/*' />
    [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
    public int CommandTimeout { get { throw null; } }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ConnectionString/*'/>
    [System.ComponentModel.DefaultValueAttribute("")]
    #pragma warning disable CS0618
    [System.ComponentModel.RecommendedAsConfigurableAttribute(true)]
    #pragma warning restore CS0618
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
    public override void ChangeDatabase(string database) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangePasswordConnectionStringCredentialNewSecurePassword/*'/>
    public static void ChangePassword(string connectionString, Microsoft.Data.SqlClient.SqlCredential credential, System.Security.SecureString newSecurePassword) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangePasswordConnectionStringNewPassword/*'/>
    public static void ChangePassword(string connectionString, string newPassword) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClearAllPools/*'/>
    public static void ClearAllPools() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClearPool/*'/>
    public static void ClearPool(Microsoft.Data.SqlClient.SqlConnection connection) { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Close/*'/>
    public override void Close() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CreateCommand/*'/>
    public new Microsoft.Data.SqlClient.SqlCommand CreateCommand() { throw null; }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CreateDbCommand/*'/>
    protected override System.Data.Common.DbCommand CreateDbCommand() { throw null; }
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

    #if NETFRAMEWORK
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/DbProviderFactory/*'/>
    protected override System.Data.Common.DbProviderFactory DbProviderFactory { get { throw null; } }
    #endif

    #if NETFRAMEWORK
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Dispose/*'/>
    protected override void Dispose(bool disposing) { }
    #endif

    #if NETFRAMEWORK
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/EnlistDistributedTransaction/*'/>
    public void EnlistDistributedTransaction(System.EnterpriseServices.ITransaction transaction) { }
    #endif

    #if NETFRAMEWORK
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/EnlistTransaction/*'/>
    public override void EnlistTransaction(System.Transactions.Transaction transaction) { }
    #endif

    #if NET
    ///
    /// for internal test only
    ///
    [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
    internal string SQLDNSCachingSupportedState { get { throw null; } }
    #endif

    #if NET
    ///
    /// for internal test only
    ///
    [System.ComponentModel.DesignerSerializationVisibilityAttribute(0)]
    internal string SQLDNSCachingSupportedStateBeforeRedirect { get { throw null; } }
    #endif
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
