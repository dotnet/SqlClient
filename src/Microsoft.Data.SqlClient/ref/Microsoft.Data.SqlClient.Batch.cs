// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/SqlBatch/*'/>
    public class SqlBatch :
        #if NET
        System.Data.Common.DbBatch
        #else
        System.IDisposable, System.IAsyncDisposable
        #endif
    {
        #if NET
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Timeout/*'/>
        public override int Timeout { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Connection/*'/>
        public new Microsoft.Data.SqlClient.SqlConnection Connection { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Transaction/*'/>
        public new Microsoft.Data.SqlClient.SqlTransaction Transaction { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/BatchCommands/*'/>
        public new Microsoft.Data.SqlClient.SqlBatchCommandCollection BatchCommands { get => throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/DbBatchCommands/*'/>
        protected override System.Data.Common.DbBatchCommandCollection DbBatchCommands { get => throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/DbConnection/*'/>
        protected override System.Data.Common.DbConnection DbConnection { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/DbTransaction/*'/>
        protected override System.Data.Common.DbTransaction DbTransaction { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Cancel/*'/>
        public override void Cancel() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/CreateDbBatchCommand/*'/>
        protected override System.Data.Common.DbBatchCommand CreateDbBatchCommand() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Dispose/*'/>
        public override void Dispose() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteDbDataReader/*'/>
        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteDbDataReaderAsync/*'/>
        protected override System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteDbDataReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteNonQuery/*'/>
        public override int ExecuteNonQuery() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteNonQueryAsync/*'/>
        public override System.Threading.Tasks.Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteReaderAsync/*'/>
        public new System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteScalar/*'/>
        public override object ExecuteScalar() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteScalarAsync/*'/>
        public override System.Threading.Tasks.Task<object> ExecuteScalarAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Prepare/*'/>
        public override void Prepare() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/PrepareAsync/*'/>
        public override System.Threading.Tasks.Task PrepareAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
#else
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Timeout/*'/>
        public int Timeout { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Connection/*'/>
        public Microsoft.Data.SqlClient.SqlConnection Connection { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Transaction/*'/>
        public Microsoft.Data.SqlClient.SqlTransaction Transaction { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/BatchCommands/*'/>
        public Microsoft.Data.SqlClient.SqlBatchCommandCollection BatchCommands { get => throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/DbConnection/*'/>
        protected virtual System.Data.Common.DbConnection DbConnection { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/DbTransaction/*'/>
        protected virtual System.Data.Common.DbTransaction DbTransaction { get => throw null; set { } }        
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Cancel/*'/>
        public void Cancel() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Dispose/*'/>
        public void Dispose() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteDbDataReader/*'/>
        protected virtual System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteDbDataReaderAsync/*'/>
        protected virtual System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteDbDataReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteNonQuery/*'/>
        public int ExecuteNonQuery() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteNonQueryAsync/*'/>
        public System.Threading.Tasks.Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteReaderAsync/*'/>
        public System.Threading.Tasks.Task<Microsoft.Data.SqlClient.SqlDataReader> ExecuteReaderAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteScalar/*'/>
        public object ExecuteScalar() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteScalarAsync/*'/>
        public System.Threading.Tasks.Task<object> ExecuteScalarAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Prepare/*'/>
        public void Prepare() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/PrepareAsync/*'/>
        public System.Threading.Tasks.Task PrepareAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <inheritdoc cref="System.IAsyncDisposable.DisposeAsync"/>
        public virtual System.Threading.Tasks.ValueTask DisposeAsync() => throw null;
#endif
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ctor1/*'/>
        public SqlBatch() { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ctor2/*'/>
        public SqlBatch(Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction = null) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Commands/*'/>
        public System.Collections.Generic.List<Microsoft.Data.SqlClient.SqlBatchCommand> Commands { get { throw null; } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteReader/*'/>
        public Microsoft.Data.SqlClient.SqlDataReader ExecuteReader() => throw null;       
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/SqlBatchCommand/*'/>
    public class SqlBatchCommand
        #if NET
        : System.Data.Common.DbBatchCommand
        #endif
    {
        #if NET
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/Parameters/*'/>
        public new Microsoft.Data.SqlClient.SqlParameterCollection Parameters { get { throw null; } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CanCreateParameter/*'/>
        public override bool CanCreateParameter => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandText/*'/>
        public override string CommandText { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandType/*'/>
        public override System.Data.CommandType CommandType { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/RecordsAffected/*'/>
        public override int RecordsAffected { get { throw null; } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/DbParameterCollection/*'/>
        protected override System.Data.Common.DbParameterCollection DbParameterCollection => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CreateParameter/*'/>
        public override System.Data.Common.DbParameter CreateParameter() => throw null;
        #else
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/Parameters/*'/>
        public Microsoft.Data.SqlClient.SqlParameterCollection Parameters { get { throw null; } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CanCreateParameter/*'/>
        public bool CanCreateParameter => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandText/*'/>
        public string CommandText { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandType/*'/>
        public System.Data.CommandType CommandType { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/RecordsAffected/*'/>
        public int RecordsAffected { get { throw null; } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/DbParameterCollection/*'/>
        protected virtual System.Data.Common.DbParameterCollection DbParameterCollection => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CreateParameter/*'/>
        public System.Data.Common.DbParameter CreateParameter() => throw null;
        #endif
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/ctor1/*'/>
        public SqlBatchCommand() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/ctor2/*'/>
        public SqlBatchCommand(string commandText, System.Data.CommandType commandType = System.Data.CommandType.Text, System.Collections.Generic.IEnumerable<Microsoft.Data.SqlClient.SqlParameter> parameters = null, Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting columnEncryptionSetting = Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting.UseConnectionSetting) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/ColumnEncryptionSetting/*'/>
        public Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting ColumnEncryptionSetting { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandBehavior/*'/>
        public System.Data.CommandBehavior CommandBehavior { get { throw null; } set { } }
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/SqlBatchCommandCollection/*'/>
    public class SqlBatchCommandCollection :
        #if NET
        System.Data.Common.DbBatchCommandCollection,
        #endif
        System.Collections.Generic.IList<Microsoft.Data.SqlClient.SqlBatchCommand>
    {
        #if NET
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Count/*'/>
        public override int Count => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IsReadOnly/*'/>
        public override bool IsReadOnly => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/GetEnumerator/*'/>
        public override System.Collections.Generic.IEnumerator<System.Data.Common.DbBatchCommand> GetEnumerator() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Add2/*'/>
        public override void Add(System.Data.Common.DbBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Clear/*'/>
        public override void Clear() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Contains2/*'/>
        public override bool Contains(System.Data.Common.DbBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/CopyTo2/*'/>
        public override void CopyTo(System.Data.Common.DbBatchCommand[] array, int arrayIndex) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IndexOf2/*'/>
        public override int IndexOf(System.Data.Common.DbBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Insert2/*'/>
        public override void Insert(int index, System.Data.Common.DbBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Remove2/*'/>
        public override bool Remove(System.Data.Common.DbBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/RemoveAt/*'/>
        public override void RemoveAt(int index) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/this2/*'/>
        public new Microsoft.Data.SqlClient.SqlBatchCommand this[int index] { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/GetBatchCommand/*'/>
        protected override System.Data.Common.DbBatchCommand GetBatchCommand(int index) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/SetBatchCommand/*'/>
        protected override void SetBatchCommand(int index, System.Data.Common.DbBatchCommand batchCommand) => throw null;
        #else
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Count/*'/>
        public int Count => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IsReadOnly/*'/>
        public bool IsReadOnly => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Clear/*'/>
        public void Clear() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/RemoveAt/*'/>
        public void RemoveAt(int index) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/this2/*'/>
        public Microsoft.Data.SqlClient.SqlBatchCommand this[int index] { get => throw null; set { } }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw null;
        #endif
        System.Collections.Generic.IEnumerator<Microsoft.Data.SqlClient.SqlBatchCommand> System.Collections.Generic.IEnumerable<Microsoft.Data.SqlClient.SqlBatchCommand>.GetEnumerator() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Add1/*'/>
        public void Add(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Contains1/*'/>
        public bool Contains(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/CopyTo1/*'/>
        public void CopyTo(Microsoft.Data.SqlClient.SqlBatchCommand[] array, int arrayIndex) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IndexOf1/*'/>
        public int IndexOf(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Insert1/*'/>
        public void Insert(int index, Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Remove1/*'/>
        public bool Remove(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/this1/*'/>
        Microsoft.Data.SqlClient.SqlBatchCommand System.Collections.Generic.IList<SqlBatchCommand>.this[int index] { get => throw null; set { } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/SqlClientFactory/*'/>
    public sealed partial class SqlClientFactory
    {
        #if NET
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CanCreateBatch/*'/>
        public override bool CanCreateBatch { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatch/*'/>
        public override System.Data.Common.DbBatch CreateBatch() { throw null; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientFactory.xml' path='docs/members[@name="SqlClientFactory"]/CreateBatchCommand/*'/>
        public override System.Data.Common.DbBatchCommand CreateBatchCommand() { throw null; }
        #endif
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/SqlConnection/*'/>
    public sealed partial class SqlConnection
    {
        #if NET
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CanCreateBatch/*'/>
        public override bool CanCreateBatch { get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CreateDbBatch/*'/>
        protected override System.Data.Common.DbBatch CreateDbBatch() => throw null;
        #endif
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/SqlException/*'/>
    public sealed partial class SqlException
    {
        #if NET
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/BatchCommand/*' />
        public new Microsoft.Data.SqlClient.SqlBatchCommand BatchCommand { get { throw null; } }
        #else
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/BatchCommand/*' />
        public Microsoft.Data.SqlClient.SqlBatchCommand BatchCommand { get { throw null; } }
        #endif
    }
}
