// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SqlBatch : DbBatch
    {
        private SqlCommand _batchCommand;
        private List<SqlBatchCommand> _commands;
        private SqlBatchCommandCollection _providerCommands;

        public SqlBatch()
        {
            _batchCommand = new SqlCommand();
        }

        public SqlBatch(SqlConnection connection = null, SqlTransaction transaction = null)
            : this()
        {
            Connection = connection;
            Transaction = transaction;
        }

        public override int Timeout
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

        protected override DbBatchCommandCollection DbBatchCommands => BatchCommands;

        public new SqlBatchCommandCollection BatchCommands => _providerCommands != null ? _providerCommands : _providerCommands = new SqlBatchCommandCollection(Commands); // Commands call will check disposed

        protected override DbConnection DbConnection
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
        protected override DbTransaction DbTransaction
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

        public override void Cancel()
        {
            CheckDisposed();
            _batchCommand.Cancel();
        }

        public override int ExecuteNonQuery()
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteNonQuery();
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        public override object ExecuteScalar()
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteScalar();
        }

        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteScalarBatchAsync(cancellationToken);
        }

        public override void Prepare()
        {
            CheckDisposed();
        }

        public override Task PrepareAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _batchCommand?.Dispose();
            _batchCommand = null;
            _commands?.Clear();
            _commands = null;
            base.Dispose();
        }

        public List<SqlBatchCommand> Commands
        {
            get
            {
                CheckDisposed();
                return _commands != null ? _commands : _commands = new List<SqlBatchCommand>();
            }
        }

        public new SqlConnection Connection
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

        public new SqlTransaction Transaction
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

        public SqlDataReader ExecuteReader()
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteReader();
        }

        public new Task<SqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            SetupBatchCommandExecute();
            return _batchCommand.ExecuteReaderAsync(cancellationToken);
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => ExecuteReader();

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
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

        protected override DbBatchCommand CreateDbBatchCommand()
        {
            return new SqlBatchCommand();
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
