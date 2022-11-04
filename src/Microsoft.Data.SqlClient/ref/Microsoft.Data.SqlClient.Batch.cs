// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SqlBatch : System.Data.Common.DbBatch
    {
        public SqlBatch() { throw null; }
        public SqlBatch(Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction = null) { throw null; }
        public override int Timeout { get => throw null; set { } }
        public new Microsoft.Data.SqlClient.SqlConnection Connection { get => throw null; set { } }
        public new Microsoft.Data.SqlClient.SqlTransaction Transaction { get => throw null; set { } }
        public new SqlBatchCommandCollection BatchCommands { get => throw null; }
        protected override System.Data.Common.DbBatchCommandCollection DbBatchCommands { get => throw null; }
        protected override System.Data.Common.DbConnection DbConnection { get => throw null; set { } }
        protected override System.Data.Common.DbTransaction DbTransaction { get => throw null; set { } }
        public override void Cancel() => throw null;
        public override int ExecuteNonQuery() => throw null;
        public override System.Threading.Tasks.Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        public override object ExecuteScalar() => throw null;
        public override System.Threading.Tasks.Task<object> ExecuteScalarAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        public override void Prepare() => throw null;
        public override System.Threading.Tasks.Task PrepareAsync(System.Threading.CancellationToken cancellationToken = default) => throw null;
        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) => throw null;
        protected override System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteDbDataReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken) => throw null;
        public System.Collections.Generic.List<Microsoft.Data.SqlClient.SqlBatchCommand> Commands { get { throw null; } }
        public Microsoft.Data.SqlClient.SqlDataReader ExecuteReader() => throw null;
        protected override System.Data.Common.DbBatchCommand CreateDbBatchCommand() => throw null;
    }
    public partial class SqlBatchCommand : System.Data.Common.DbBatchCommand
    {
        public SqlBatchCommand() => throw null;
        public SqlBatchCommand(string commandText, System.Data.CommandType commandType = System.Data.CommandType.Text, System.Collections.Generic.IEnumerable<Microsoft.Data.SqlClient.SqlParameter> parameters = null) { throw null; }
        public new Microsoft.Data.SqlClient.SqlParameterCollection Parameters { get { throw null; } }
        public override string CommandText { get { throw null; } set { } }
        public override System.Data.CommandType CommandType { get { throw null; } set { } }
        public System.Data.CommandBehavior CommandBehavior { get { throw null; } set { } }
        public override int RecordsAffected { get { throw null; } }
        protected override System.Data.Common.DbParameterCollection DbParameterCollection => throw null;
    }
    public class SqlBatchCommandCollection : System.Data.Common.DbBatchCommandCollection, System.Collections.Generic.IList<SqlBatchCommand>
    {
        public override int Count => throw null;
        public override bool IsReadOnly => throw null;
        System.Collections.Generic.IEnumerator<Microsoft.Data.SqlClient.SqlBatchCommand> System.Collections.Generic.IEnumerable<Microsoft.Data.SqlClient.SqlBatchCommand>.GetEnumerator() => throw null;
        public override System.Collections.Generic.IEnumerator<System.Data.Common.DbBatchCommand> GetEnumerator() => throw null;
        public void Add(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        public override void Add(System.Data.Common.DbBatchCommand item) => throw null;
        public override void Clear() => throw null;
        public bool Contains(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        public override bool Contains(System.Data.Common.DbBatchCommand item) => throw null;
        public void CopyTo(Microsoft.Data.SqlClient.SqlBatchCommand[] array, int arrayIndex) => throw null;
        public override void CopyTo(System.Data.Common.DbBatchCommand[] array, int arrayIndex) => throw null;
        public int IndexOf(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        public override int IndexOf(System.Data.Common.DbBatchCommand item) => throw null;
        public void Insert(int index, Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        public override void Insert(int index, System.Data.Common.DbBatchCommand item) => throw null;
        public bool Remove(Microsoft.Data.SqlClient.SqlBatchCommand item) => throw null;
        public override bool Remove(System.Data.Common.DbBatchCommand item) => throw null;
        public override void RemoveAt(int index) => throw null;
        Microsoft.Data.SqlClient.SqlBatchCommand System.Collections.Generic.IList<SqlBatchCommand>.this[int index] { get => throw null; set { } }
        public new Microsoft.Data.SqlClient.SqlBatchCommand this[int index] { get => throw null; set { } }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw null;
        protected override System.Data.Common.DbBatchCommand GetBatchCommand(int index) => throw null;
        protected override void SetBatchCommand(int index, System.Data.Common.DbBatchCommand batchCommand) => throw null;
    }
    public sealed partial class SqlException
    {
        public
#if NET6_0_OR_GREATER
        new
#endif 
        Microsoft.Data.SqlClient.SqlBatchCommand BatchCommand { get { throw null; } }

    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
