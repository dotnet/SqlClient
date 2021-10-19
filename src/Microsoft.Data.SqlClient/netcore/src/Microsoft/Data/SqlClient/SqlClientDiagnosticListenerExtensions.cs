// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Extension methods on the DiagnosticListener class to log SqlCommand data
    /// </summary>
    internal static class SqlClientDiagnosticListenerExtensions
    {
        public const string DiagnosticListenerName = "SqlClientDiagnosticListener";

        private const string SqlClientPrefix = "Microsoft.Data.SqlClient.";

        public const string SqlBeforeExecuteCommand = SqlClientPrefix + nameof(WriteCommandBefore);
        public const string SqlAfterExecuteCommand = SqlClientPrefix + nameof(WriteCommandAfter);
        public const string SqlErrorExecuteCommand = SqlClientPrefix + nameof(WriteCommandError);

        public const string SqlBeforeOpenConnection = SqlClientPrefix + nameof(WriteConnectionOpenBefore);
        public const string SqlAfterOpenConnection = SqlClientPrefix + nameof(WriteConnectionOpenAfter);
        public const string SqlErrorOpenConnection = SqlClientPrefix + nameof(WriteConnectionOpenError);

        public const string SqlBeforeCloseConnection = SqlClientPrefix + nameof(WriteConnectionCloseBefore);
        public const string SqlAfterCloseConnection = SqlClientPrefix + nameof(WriteConnectionCloseAfter);
        public const string SqlErrorCloseConnection = SqlClientPrefix + nameof(WriteConnectionCloseError);

        public const string SqlBeforeCommitTransaction = SqlClientPrefix + nameof(WriteTransactionCommitBefore);
        public const string SqlAfterCommitTransaction = SqlClientPrefix + nameof(WriteTransactionCommitAfter);
        public const string SqlErrorCommitTransaction = SqlClientPrefix + nameof(WriteTransactionCommitError);

        public const string SqlBeforeRollbackTransaction = SqlClientPrefix + nameof(WriteTransactionRollbackBefore);
        public const string SqlAfterRollbackTransaction = SqlClientPrefix + nameof(WriteTransactionRollbackAfter);
        public const string SqlErrorRollbackTransaction = SqlClientPrefix + nameof(WriteTransactionRollbackError);

        public static Guid WriteCommandBefore(this SqlDiagnosticListener @this, SqlCommand sqlCommand, SqlTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlBeforeExecuteCommand))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeExecuteCommand,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        ConnectionId = sqlCommand.Connection?.ClientConnectionId,
                        Command = sqlCommand,
                        transaction?.InternalTransaction?.TransactionId,
                        Timestamp = Stopwatch.GetTimestamp()
                    });

                return operationId;
            }
            else
                return Guid.Empty;
        }

        public static void WriteCommandAfter(this SqlDiagnosticListener @this, Guid operationId, SqlCommand sqlCommand, SqlTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlAfterExecuteCommand))
            {
                @this.Write(
                    SqlAfterExecuteCommand,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        ConnectionId = sqlCommand.Connection?.ClientConnectionId,
                        Command = sqlCommand,
                        transaction?.InternalTransaction?.TransactionId,
                        Statistics = sqlCommand.Statistics?.GetDictionary(),
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static void WriteCommandError(this SqlDiagnosticListener @this, Guid operationId, SqlCommand sqlCommand, SqlTransaction transaction, Exception ex, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlErrorExecuteCommand))
            {
                @this.Write(
                    SqlErrorExecuteCommand,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        ConnectionId = sqlCommand.Connection?.ClientConnectionId,
                        Command = sqlCommand,
                        transaction?.InternalTransaction?.TransactionId,
                        Exception = ex,
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static Guid WriteConnectionOpenBefore(this SqlDiagnosticListener @this, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlBeforeOpenConnection))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeOpenConnection,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        Connection = sqlConnection,
                        ClientVersion = ThisAssembly.InformationalVersion,
                        Timestamp = Stopwatch.GetTimestamp()
                    });

                return operationId;
            }
            else
                return Guid.Empty;
        }

        public static void WriteConnectionOpenAfter(this SqlDiagnosticListener @this, Guid operationId, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlAfterOpenConnection))
            {
                @this.Write(
                    SqlAfterOpenConnection,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        ConnectionId = sqlConnection.ClientConnectionId,
                        Connection = sqlConnection,
                        ClientVersion = ThisAssembly.InformationalVersion,
                        Statistics = sqlConnection.Statistics?.GetDictionary(),
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static void WriteConnectionOpenError(this SqlDiagnosticListener @this, Guid operationId, SqlConnection sqlConnection, Exception ex, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlErrorOpenConnection))
            {
                @this.Write(
                    SqlErrorOpenConnection,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        ConnectionId = sqlConnection.ClientConnectionId,
                        Connection = sqlConnection,
                        ClientVersion = ThisAssembly.InformationalVersion,
                        Exception = ex,
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static Guid WriteConnectionCloseBefore(this SqlDiagnosticListener @this, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlBeforeCloseConnection))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeCloseConnection,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        ConnectionId = sqlConnection.ClientConnectionId,
                        Connection = sqlConnection,
                        Statistics = sqlConnection.Statistics?.GetDictionary(),
                        Timestamp = Stopwatch.GetTimestamp()
                    });

                return operationId;
            }
            else
                return Guid.Empty;
        }

        public static void WriteConnectionCloseAfter(this SqlDiagnosticListener @this, Guid operationId, Guid clientConnectionId, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlAfterCloseConnection))
            {
                @this.Write(
                    SqlAfterCloseConnection,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        ConnectionId = clientConnectionId,
                        Connection = sqlConnection,
                        Statistics = sqlConnection.Statistics?.GetDictionary(),
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static void WriteConnectionCloseError(this SqlDiagnosticListener @this, Guid operationId, Guid clientConnectionId, SqlConnection sqlConnection, Exception ex, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlErrorCloseConnection))
            {
                @this.Write(
                    SqlErrorCloseConnection,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        ConnectionId = clientConnectionId,
                        Connection = sqlConnection,
                        Statistics = sqlConnection.Statistics?.GetDictionary(),
                        Exception = ex,
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static Guid WriteTransactionCommitBefore(this SqlDiagnosticListener @this, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlBeforeCommitTransaction))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeCommitTransaction,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        IsolationLevel = isolationLevel,
                        Connection = connection,
                        transaction?.TransactionId,
                        Timestamp = Stopwatch.GetTimestamp()
                    });

                return operationId;
            }
            else
                return Guid.Empty;
        }

        public static void WriteTransactionCommitAfter(this SqlDiagnosticListener @this, Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlAfterCommitTransaction))
            {
                @this.Write(
                    SqlAfterCommitTransaction,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        IsolationLevel = isolationLevel,
                        Connection = connection,
                        transaction?.TransactionId,
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static void WriteTransactionCommitError(this SqlDiagnosticListener @this, Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, Exception ex, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlErrorCommitTransaction))
            {
                @this.Write(
                    SqlErrorCommitTransaction,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        IsolationLevel = isolationLevel,
                        Connection = connection,
                        transaction?.TransactionId,
                        Exception = ex,
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static Guid WriteTransactionRollbackBefore(this SqlDiagnosticListener @this, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlBeforeRollbackTransaction))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeRollbackTransaction,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        IsolationLevel = isolationLevel,
                        Connection = connection,
                        transaction?.TransactionId,
                        TransactionName = transactionName,
                        Timestamp = Stopwatch.GetTimestamp()
                    });

                return operationId;
            }
            else
                return Guid.Empty;
        }

        public static void WriteTransactionRollbackAfter(this SqlDiagnosticListener @this, Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlAfterRollbackTransaction))
            {
                @this.Write(
                    SqlAfterRollbackTransaction,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        IsolationLevel = isolationLevel,
                        Connection = connection,
                        transaction?.TransactionId,
                        TransactionName = transactionName,
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static void WriteTransactionRollbackError(this SqlDiagnosticListener @this, Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, Exception ex, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlErrorRollbackTransaction))
            {
                @this.Write(
                    SqlErrorRollbackTransaction,
                    new
                    {
                        OperationId = operationId,
                        Operation = operation,
                        IsolationLevel = isolationLevel,
                        Connection = connection,
                        transaction?.TransactionId,
                        TransactionName = transactionName,
                        Exception = ex,
                        Timestamp = Stopwatch.GetTimestamp()
                    });
            }
        }

        public static DiagnosticScope CreateCommandScope(this SqlDiagnosticListener @this, SqlCommand command, SqlTransaction transaction, [CallerMemberName] string operationName = "")
        {
            return DiagnosticScope.CreateCommandScope(@this, command, transaction, operationName);
        }

        public static DiagnosticTransactionScope CreateTransactionCommitScope(this SqlDiagnosticListener @this, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operationName = "")
        {
            return DiagnosticTransactionScope.CreateTransactionCommitScope(@this, isolationLevel, connection, transaction, operationName);
        }

        public static DiagnosticTransactionScope CreateTransactionRollbackScope(this SqlDiagnosticListener @this, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName, [CallerMemberName] string operationName = "")
        {
            return DiagnosticTransactionScope.CreateTransactionRollbackScope(@this, isolationLevel, connection, transaction, transactionName, operationName);
        }
    }

    internal ref struct DiagnosticScope //: IDisposable //ref structs cannot implement interfaces but the compiler will use pattern matching
    {
        private const int CommandOperation = 1;
        private const int ConnectionOpenOperation = 2;

        private readonly SqlDiagnosticListener _diagnostics;
        private readonly int _operation;
        private readonly string _operationName;
        private readonly Guid _operationId;
        private readonly object _context1;
        private readonly object _context2;
        private Exception _exception;

        private DiagnosticScope(SqlDiagnosticListener diagnostics, int operation, Guid operationsId, string operationName, object context1, object context2)
        {
            _diagnostics = diagnostics;
            _operation = operation;
            _operationId = operationsId;
            _operationName = operationName;
            _context1 = context1;
            _context2 = context2;
            _exception = null;
        }

        public void Dispose()
        {
            switch (_operation)
            {
                case CommandOperation:
                    if (_exception != null)
                    {
                        _diagnostics.WriteCommandError(_operationId, (SqlCommand)_context1, (SqlTransaction)_context2, _exception, _operationName);
                    }
                    else
                    {
                        _diagnostics.WriteCommandAfter(_operationId, (SqlCommand)_context1, (SqlTransaction)_context2, _operationName);
                    }
                    break;

                case ConnectionOpenOperation:
                    if (_exception != null)
                    {
                        _diagnostics.WriteConnectionOpenError(_operationId, (SqlConnection)_context1, _exception, _operationName);
                    }
                    else
                    {
                        _diagnostics.WriteConnectionOpenAfter(_operationId, (SqlConnection)_context1, _operationName);
                    }
                    break;

                    // ConnectionCloseOperation is not implemented because it is conditionally emitted and that requires manual calls to the write apis
            }
        }

        public void SetException(Exception ex)
        {
            _exception = ex;
        }

        public static DiagnosticScope CreateCommandScope(SqlDiagnosticListener diagnostics, SqlCommand command, SqlTransaction transaction, [CallerMemberName] string operationName = "")
        {
            Guid operationId = diagnostics.WriteCommandBefore(command, transaction, operationName);
            return new DiagnosticScope(diagnostics, CommandOperation, operationId, operationName, command, transaction);
        }
    }

    internal ref struct DiagnosticTransactionScope //: IDisposable //ref structs cannot implement interfaces but the compiler will use pattern matching
    {
        public const int TransactionCommit = 1;
        public const int TransactionRollback = 2;

        private readonly SqlDiagnosticListener _diagnostics;
        private readonly int _operation;
        private readonly Guid _operationId;
        private readonly string _operationName;
        private readonly IsolationLevel _isolationLevel;
        private readonly SqlConnection _connection;
        private readonly SqlInternalTransaction _transaction;
        private readonly string _transactionName;
        private Exception _exception;

        public DiagnosticTransactionScope(SqlDiagnosticListener diagnostics, int operation, Guid operationId, string operationName, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName)
        {
            _diagnostics = diagnostics;
            _operation = operation;
            _operationId = operationId;
            _operationName = operationName;
            _isolationLevel = isolationLevel;
            _connection = connection;
            _transaction = transaction;
            _transactionName = transactionName;
            _exception = null;
        }

        public void Dispose()
        {
            switch (_operation)
            {
                case TransactionCommit:
                    if (_exception != null)
                    {
                        _diagnostics.WriteTransactionCommitError(_operationId, _isolationLevel, _connection, _transaction, _exception, _operationName);
                    }
                    else
                    {
                        _diagnostics.WriteTransactionCommitAfter(_operationId, _isolationLevel, _connection, _transaction, _operationName);
                    }
                    break;

                case TransactionRollback:
                    if (_exception != null)
                    {
                        _diagnostics.WriteTransactionRollbackError(_operationId, _isolationLevel, _connection, _transaction, _exception, _transactionName, _operationName);
                    }
                    else
                    {
                        _diagnostics.WriteTransactionRollbackAfter(_operationId, _isolationLevel, _connection, _transaction, _transactionName, _operationName);
                    }
                    break;
            }
        }

        public void SetException(Exception ex)
        {
            _exception = ex;
        }

        public static DiagnosticTransactionScope CreateTransactionCommitScope(SqlDiagnosticListener diagnostics, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operationName = "")
        {
            Guid operationId = diagnostics.WriteTransactionCommitBefore(isolationLevel, connection, transaction, operationName);
            return new DiagnosticTransactionScope(diagnostics, TransactionCommit, operationId, operationName, isolationLevel, connection, transaction, null);
        }

        public static DiagnosticTransactionScope CreateTransactionRollbackScope(SqlDiagnosticListener diagnostics, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName, [CallerMemberName] string operationName = "")
        {
            Guid operationId = diagnostics.WriteTransactionRollbackBefore(isolationLevel, connection, transaction, transactionName, operationName);
            return new DiagnosticTransactionScope(diagnostics, TransactionCommit, operationId, operationName, isolationLevel, connection, transaction, transactionName);
        }
    }
}
