namespace System.Data.Common
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract partial class DbBatch : System.IDisposable
    {
        protected class DbBatchCommandList<TCommand> : System.Collections.Generic.IList<DbBatchCommand> where TCommand : DbBatchCommand
        {
            public DbBatchCommandList(System.Collections.Generic.List<TCommand> batchCommands) => throw null;

            public void Add(DbBatchCommand item) => throw null;

            public void Clear() => throw null;

            public bool Contains(DbBatchCommand item) => throw null;

            public System.Collections.Generic.IEnumerator<DbBatchCommand> GetEnumerator() => throw null;

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw null;

            public bool Remove(DbBatchCommand item) => throw null;

            public int Count => throw null;

            public bool IsReadOnly => throw null;

            public int IndexOf(DbBatchCommand item) => throw null;

            public void Insert(int index, DbBatchCommand item) => throw null;

            public void RemoveAt(int index) => throw null;

            public DbBatchCommand this[int index]
            {
                get => throw null;
                set => throw null;
            }

            public void CopyTo(DbBatchCommand[] array, int arrayIndex) => throw null;
        }
        public System.Collections.Generic.IList<DbBatchCommand> BatchCommands { get { throw null; } }
        protected abstract System.Collections.Generic.IList<DbBatchCommand> DbBatchCommands { get; }
        public DbDataReader ExecuteDbReader() { throw null; }
        protected abstract DbDataReader ExecuteDbDataReader();
        public Threading.Tasks.Task<DbDataReader> ExecuteReaderAsync(Threading.CancellationToken cancellationToken = default) { throw null; }
        protected abstract Threading.Tasks.Task<DbDataReader> ExecuteDbDataReaderAsync(Threading.CancellationToken cancellationToken);
        public abstract int ExecuteNonQuery();
        public abstract Threading.Tasks.Task<int> ExecuteNonQueryAsync(Threading.CancellationToken cancellationToken = default);
        public abstract object ExecuteScalar();
        public abstract Threading.Tasks.Task<object> ExecuteScalarAsync(Threading.CancellationToken cancellationToken = default);
        public abstract int Timeout { get; set; }
        public DbConnection Connection { get { throw null; } set { throw null; } }
        protected abstract DbConnection DbConnection { get; set; }
        public DbTransaction Transaction { get { throw null; } set { throw null; } }
        protected abstract DbTransaction DbTransaction { get; set; }
        public abstract void Prepare();
        public abstract Threading.Tasks.Task PrepareAsync(Threading.CancellationToken cancellationToken = default);
        public abstract void Cancel();
        public void Dispose() { throw null; }
        protected virtual void Dispose(bool disposing) { throw null; }
    }

    public abstract class DbBatchCommand
    {
        public abstract string CommandText { get; set; }
        public abstract CommandType CommandType { get; set; }
        public abstract CommandBehavior CommandBehavior { get; set; }
        public abstract int RecordsAffected { get; set; }

        public DbParameterCollection Parameters { get { throw null; } }
        protected abstract DbParameterCollection DbParameterCollection { get; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
