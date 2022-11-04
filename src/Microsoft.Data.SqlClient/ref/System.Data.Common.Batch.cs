// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace System.Data.Common
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract partial class DbBatch : System.IDisposable
    {
        public System.Data.Common.DbBatchCommandCollection BatchCommands { get { throw null!; } }
        protected abstract System.Data.Common.DbBatchCommandCollection DbBatchCommands { get; }
        public abstract int Timeout { get; set; }
        public System.Data.Common.DbConnection? Connection { get; set; }
        protected abstract System.Data.Common.DbConnection? DbConnection { get; set; }
        public System.Data.Common.DbTransaction? Transaction { get; set; }
        protected abstract System.Data.Common.DbTransaction? DbTransaction { get; set; }
        public System.Data.Common.DbDataReader ExecuteReader(System.Data.CommandBehavior behavior = System.Data.CommandBehavior.Default) { throw null!; }
        protected abstract System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior);
        public System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteReaderAsync(System.Threading.CancellationToken cancellationToken = default) { throw null!; }
        public System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken = default) { throw null!; }
        protected abstract System.Threading.Tasks.Task<System.Data.Common.DbDataReader> ExecuteDbDataReaderAsync(System.Data.CommandBehavior behavior, System.Threading.CancellationToken cancellationToken);
        public abstract int ExecuteNonQuery();
        public abstract System.Threading.Tasks.Task<int> ExecuteNonQueryAsync(System.Threading.CancellationToken cancellationToken = default);
        public abstract object? ExecuteScalar();
        public abstract System.Threading.Tasks.Task<object?> ExecuteScalarAsync(System.Threading.CancellationToken cancellationToken = default);
        public abstract void Prepare();
        public abstract System.Threading.Tasks.Task PrepareAsync(System.Threading.CancellationToken cancellationToken = default);
        public abstract void Cancel();
        public System.Data.Common.DbBatchCommand CreateBatchCommand() { throw null!; }
        protected abstract System.Data.Common.DbBatchCommand CreateDbBatchCommand();
        public virtual void Dispose() { throw null!; }
    }
    public abstract class DbBatchCommand
    {
        public abstract string CommandText { get; set; }
        public abstract System.Data.CommandType CommandType { get; set; }
        public abstract int RecordsAffected { get; }
        public System.Data.Common.DbParameterCollection Parameters { get { throw null!; } }
        protected abstract System.Data.Common.DbParameterCollection DbParameterCollection { get; }
    }

    public abstract class DbBatchCommandCollection : System.Collections.Generic.IList<DbBatchCommand>
    {
        public abstract System.Collections.Generic.IEnumerator<System.Data.Common.DbBatchCommand> GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { throw null!; }
        public abstract void Add(System.Data.Common.DbBatchCommand item);
        public abstract void Clear();
        public abstract bool Contains(System.Data.Common.DbBatchCommand item);
        public abstract void CopyTo(System.Data.Common.DbBatchCommand[] array, int arrayIndex);
        public abstract bool Remove(System.Data.Common.DbBatchCommand item);
        public abstract int Count { get; }
        public abstract bool IsReadOnly { get; }
        public abstract int IndexOf(DbBatchCommand item);
        public abstract void Insert(int index, DbBatchCommand item);
        public abstract void RemoveAt(int index);
        public System.Data.Common.DbBatchCommand this[int index] { get { throw null!; } set { throw null!; } }
        protected abstract System.Data.Common.DbBatchCommand GetBatchCommand(int index);
        protected abstract void SetBatchCommand(int index, System.Data.Common.DbBatchCommand batchCommand);
    }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
