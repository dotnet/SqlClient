// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/SqlBatch/*'/>
    public class SqlBatch :
        #if NET
        DbBatch
        #else
        IDisposable, IAsyncDisposable
        #endif
    {
        private SqlCommand _batchCommand;
        private List<SqlBatchCommand> _commands;
        private SqlBatchCommandCollection _providerCommands;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ctor1/*'/>
        public SqlBatch()
        {
            _batchCommand = new SqlCommand();
        }
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ctor2/*'/>
        public SqlBatch(SqlConnection connection = null, SqlTransaction transaction = null)
            : this()
        {
            Connection = connection;
            Transaction = transaction;
        }

        #if NET
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/DbBatchCommands/*'/>
        protected override DbBatchCommandCollection DbBatchCommands => BatchCommands;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/CreateDbBatchCommand/*'/>
        protected override DbBatchCommand CreateDbBatchCommand() => new SqlBatchCommand();
        #else
        /// <inheritdoc cref="System.IAsyncDisposable.DisposeAsync"/>
        public virtual ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
        #endif

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Timeout/*'/>
        public
        #if NET
        override
        #endif
        int Timeout
        {
            get
            {
                CheckDisposed();
                return _batchCommand.CommandTimeout;
            }
            set
            {
                CheckDisposed();
                _batchCommand.CommandTimeout = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/BatchCommands/*'/>
        public
        #if NET
        new
        #endif
        SqlBatchCommandCollection BatchCommands => _providerCommands != null ? _providerCommands : _providerCommands = new SqlBatchCommandCollection(Commands); // Commands call will check disposed

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/DbConnection/*'/>
        protected
        #if NET
        override
        #else
        virtual
        #endif
        DbConnection DbConnection
        {
            get
            {
                CheckDisposed();
                return Connection;
            }
            set
            {
                CheckDisposed();
                Connection = (SqlConnection)value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/DbTransaction/*'/>
        protected
        #if NET
        override
        #else
        virtual
        #endif
        DbTransaction DbTransaction
        {
            get
            {
                CheckDisposed();
                return Transaction;
            }
            set
            {
                CheckDisposed();
                Transaction = (SqlTransaction)value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Cancel/*'/>
        public
        #if NET
        override
        #endif
        void Cancel()
        {
            CheckDisposed();
            _batchCommand.Cancel();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteNonQuery/*'/>
        public
        #if NET
        override
        #endif
        int ExecuteNonQuery()
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteNonQuery();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteNonQueryAsync/*'/>
        public
        #if NET
        override
        #endif
        Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteScalar/*'/>
        public
        #if NET
        override
        #endif
        object ExecuteScalar()
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteScalar();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteScalarAsync/*'/>
        public
        #if NET
        override
        #endif
        Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteScalarBatchAsync(cancellationToken);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Prepare/*'/>
        public
        #if NET
        override
        #endif
        void Prepare()
        {
            CheckDisposed();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/PrepareAsync/*'/>
        public
        #if NET
        override
        #endif
        Task PrepareAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            return Task.CompletedTask;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Dispose/*'/>
        public
        #if NET
        override
        #endif
        void Dispose()
        {
            _batchCommand?.Dispose();
            _batchCommand = null;
            _commands?.Clear();
            _commands = null;
            #if NET
            base.Dispose();
            #endif
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Commands/*'/>
        public List<SqlBatchCommand> Commands
        {
            get
            {
                CheckDisposed();
                return _commands != null ? _commands : _commands = new List<SqlBatchCommand>();
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Connection/*'/>
        public
        #if NET
        new
        #endif
        SqlConnection Connection
        {
            get
            {
                CheckDisposed();
                return _batchCommand.Connection;
            }
            set
            {
                CheckDisposed();
                _batchCommand.Connection = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/Transaction/*'/>
        public
        #if NET
        new
        #endif
        SqlTransaction Transaction
        {
            get
            {
                CheckDisposed();
                return _batchCommand.Transaction;
            }
            set
            {
                CheckDisposed();
                _batchCommand.Transaction = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteReader/*'/>
        public SqlDataReader ExecuteReader()
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteReader();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteReaderAsync/*'/>
        public
        #if NET
        new
        #endif
        Task<SqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteReaderAsync(cancellationToken);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteDbDataReader/*'/>
        protected
        #if NET
        override
        #else
        virtual
        #endif
        DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => ExecuteReader();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatch.xml' path='docs/members[@name="SqlBatch"]/ExecuteDbDataReaderAsync/*'/>
        protected
        #if NET
        override
        #else
        virtual
        #endif
        Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteReaderAsync(cancellationToken)
                .ContinueWith<DbDataReader>((result) =>
                {
                    if (result.IsFaulted)
                    {
                        throw result.Exception.InnerException;
                    }
                    return result.Result;
                }, 
                CancellationToken.None, 
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled, 
                TaskScheduler.Default
            );
        }

        private void CheckDisposed()
        {
            if (_batchCommand is null)
            {
                throw ADP.ObjectDisposed(this);
            }
        }

        private void SetupBatchCommandExecute()
        {
            SqlConnection connection = Connection;
            if (connection is null)
            {
                throw ADP.ConnectionRequired(nameof(SetupBatchCommandExecute));
            }
            if (_commands is null) 
            {
                throw ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_NoSqlBatchCommandList));
            }
            _batchCommand.Connection = Connection;
            _batchCommand.Transaction = Transaction;
            _batchCommand.SetBatchRPCMode(true, _commands.Count);
            _batchCommand.Parameters.Clear();
            for (int index = 0; index < _commands.Count; index++)
            {
                _batchCommand.AddBatchCommand(_commands[index]);
            }
            _batchCommand.SetBatchRPCModeReadyToExecute();
        }
    }
}
