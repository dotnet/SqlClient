// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/SqlBatch/*'/>
    public class SqlBatch : System.Data.Common.DbBatch
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ctor1/*'/>
        public SqlBatch() { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ctor2/*'/>
        public SqlBatch(Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction = null) { throw null; }
        /// <inheritdoc />
        public override int Timeout { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Timeout/*'/>
        public new Microsoft.Data.SqlClient.SqlConnection Connection { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Connection/*'/>
        public new Microsoft.Data.SqlClient.SqlTransaction Transaction { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Transaction/*'/>
        public new SqlBatchCommandCollection BatchCommands { get => throw null; }
        /// <inheritdoc />
        protected override System.Data.Common.DbBatchCommandCollection DbBatchCommands { get => throw null; }
        /// <inheritdoc />
        protected override System.Data.Common.DbConnection DbConnection { get => throw null; set { } }
        /// <inheritdoc />
        protected override System.Data.Common.DbTransaction DbTransaction { get => throw null; set { } }
        /// <inheritdoc />
        public override void Cancel() => throw null;
        /// <inheritdoc />
        public override int ExecuteNonQuery() => throw null;
        /// <inheritdoc />
        public override System.Threading.Tasks.Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <inheritdoc />
        public override object ExecuteScalar() => throw null;
        /// <inheritdoc />
        public override System.Threading.Tasks.Task<object> ExecuteScalarAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <inheritdoc />
        public override void Prepare() => throw null;
        /// <inheritdoc />
        public override System.Threading.Tasks.Task PrepareAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        /// <inheritdoc />
        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) => throw null;
        /// <inheritdoc />
        protected override System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteDbDataReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Commands/*'/>
        public System.Collections.Generic.List<Microsoft.Data.SqlClient.SqlBatchCommand> Commands { get { throw null; } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteReader/*'/>
        public Microsoft.Data.SqlClient.SqlDataReader ExecuteReader() => throw null;
        /// <inheritdoc />
        protected override System.Data.Common.DbBatchCommand CreateDbBatchCommand() => throw null;
    }
    /// <inheritdoc />
    public partial class SqlBatchCommand : System.Data.Common.DbBatchCommand
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/cto1/*'/>
        public SqlBatchCommand() => throw null;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/ctor2/*'/>
        public SqlBatchCommand(string commandText, System.Data.CommandType commandType = System.Data.CommandType.Text, System.Collections.Generic.IEnumerable<Microsoft.Data.SqlClient.SqlParameter> parameters = null, Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting columnEncryptionSetting = Microsoft.Data.SqlClient.SqlCommandColumnEncryptionSetting.UseConnectionSetting) { throw null; }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/Parameters/*'/>
        public new Microsoft.Data.SqlClient.SqlParameterCollection Parameters { get { throw null; } }
        /// <inheritdoc />
        public override string CommandText { get { throw null; } set { } }
        /// <inheritdoc />
        public override System.Data.CommandType CommandType { get { throw null; } set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommand.xml' path='docs/members[@name="SqlBatchCommand"]/CommandBehavior/*'/>
        public System.Data.CommandBehavior CommandBehavior { get { throw null; } set { } }
        /// <inheritdoc />
        public override int RecordsAffected { get { throw null; } }
        /// <inheritdoc />
        protected override System.Data.Common.DbParameterCollection DbParameterCollection => throw null;
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/SqlBatchCommandCollection/*'/>
    public class SqlBatchCommandCollection : System.Data.Common.DbBatchCommandCollection, System.Collections.Generic.IList<SqlBatchCommand>
    {
        /// <inheritdoc />
        public override int Count => throw null;
        /// <inheritdoc />
        public override bool IsReadOnly => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/GetEnumerator/*'/>
        System.Collections.Generic.IEnumerator<Microsoft.Data.SqlClient.SqlBatchCommand> System.Collections.Generic.IEnumerable<Microsoft.Data.SqlClient.SqlBatchCommand>.GetEnumerator() => throw null;
        /// <inheritdoc />
        public override System.Collections.Generic.IEnumerator<System.Data.Common.DbBatchCommand> GetEnumerator() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Add/*'/>
        public void Add(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <inheritdoc />
        public override void Add(System.Data.Common.DbBatchCommand item) => throw null;
        /// <inheritdoc />
        public override void Clear() => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Contains/*'/>
        public bool Contains(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <inheritdoc />
        public override bool Contains(System.Data.Common.DbBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/CopyTo/*'/>
        public void CopyTo(Microsoft.Data.SqlClient.SqlBatchCommand[] array, int arrayIndex) => throw null;
        /// <inheritdoc />
        public override void CopyTo(System.Data.Common.DbBatchCommand[] array, int arrayIndex) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IndexOf/*'/>
        public int IndexOf(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <inheritdoc />
        public override int IndexOf(System.Data.Common.DbBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Insert/*'/>
        public void Insert(int index, Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <inheritdoc />
        public override void Insert(int index, System.Data.Common.DbBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Remove/*'/>
        public bool Remove(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Remove/*'/>
        public override bool Remove(System.Data.Common.DbBatchCommand item) => throw null;
        /// <inheritdoc />
        public override void RemoveAt(int index) => throw null;
        Microsoft.Data.SqlClient.SqlBatchCommand System.Collections.Generic.IList<SqlBatchCommand>.this[int index] { get => throw null; set { } }
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/this/*'/>
        public new Microsoft.Data.SqlClient.SqlBatchCommand this[int index] { get => throw null; set { } }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw null;
        /// <inheritdoc />
        protected override System.Data.Common.DbBatchCommand GetBatchCommand(int index) => throw null;
        /// <inheritdoc />
        protected override void SetBatchCommand(int index, System.Data.Common.DbBatchCommand batchCommand) => throw null;
    }
    /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/SqlException/*'/>
    public sealed partial class SqlException
    {
        /// <include file='../../../doc/snippets/Microsoft.Data.SqlClient/SqlException.xml' path='docs/members[@name="SqlException"]/BatchCommand/*' />
        public new Microsoft.Data.SqlClient.SqlBatchCommand BatchCommand { get { throw null; } }
    }
}
