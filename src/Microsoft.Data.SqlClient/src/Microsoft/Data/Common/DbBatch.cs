// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.Common
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/DbBatch/*' />
    public abstract partial class DbBatch : IDisposable
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/BatchCommands/*'/>
        public DbBatchCommandCollection BatchCommands => DbBatchCommands;
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/DbBatchCommands/*'/>
        protected abstract DbBatchCommandCollection DbBatchCommands { get; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/Timeout/*'/>
        public abstract int Timeout { get; set; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/Connection/*'/>
        public DbConnection Connection
        {
            get => DbConnection;
            set => DbConnection = value;
        }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/DbConnection/*'/>
        protected abstract DbConnection DbConnection { get; set; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/Transaction/*'/>
        public DbTransaction Transaction
        {
            get => DbTransaction;
            set => DbTransaction = value;
        }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/DbTransaction/*'/>
        protected abstract DbTransaction DbTransaction { get; set; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteReader/*'/>
        public DbDataReader ExecuteReader(CommandBehavior behavior = CommandBehavior.Default)
            => ExecuteDbDataReader(behavior);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteDbDataReader/*'/>
        protected abstract DbDataReader ExecuteDbDataReader(CommandBehavior behavior);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteReaderAsync/*'/>
        public Task<DbDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default)
            => ExecuteDbDataReaderAsync(CommandBehavior.Default, cancellationToken);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteReaderAsync/*'/>
        public Task<DbDataReader> ExecuteReaderAsync(CommandBehavior behavior,CancellationToken cancellationToken = default)
            => ExecuteDbDataReaderAsync(behavior, cancellationToken);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteDbDataReaderAsync/*'/>
        protected abstract Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,CancellationToken cancellationToken);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteNonQuery/*'/>
        public abstract int ExecuteNonQuery();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteNonQueryAsync/*'/>
        public abstract Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteScalar/*'/>
        public abstract object ExecuteScalar();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/ExecuteScalarAsync/*'/>
        public abstract Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/Prepare/*'/>
        public abstract void Prepare();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/PrepareAsync/*'/>
        public abstract Task PrepareAsync(CancellationToken cancellationToken = default);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/Cancel/*'/>
        public abstract void Cancel();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/CreateDbBatchCommand/*'/>
        public DbBatchCommand CreateBatchCommand() => CreateDbBatchCommand();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/CreateDbBatchCommand/*'/>
        protected abstract DbBatchCommand CreateDbBatchCommand();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatch.xml' path='docs/members[@name="DbBatch"]/Dispose/*'/>
        public virtual void Dispose() { }
    }
}
