// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Extension methods on the DiagnosticListener class to log SqlCommand data
    /// </summary>
    internal static class SqlClientDiagnosticListenerExtensions
    {
        public const string DiagnosticListenerName = "SqlClientDiagnosticListener";

        public static Guid WriteCommandBefore(this SqlDiagnosticListener @this, SqlCommand sqlCommand, SqlTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientCommandBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlClientCommandBefore.Name,
                    new SqlClientCommandBefore(
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        sqlCommand.Connection?.ClientConnectionId,
                        transaction?.InternalTransaction?.TransactionId,
                        sqlCommand
                    )
                );

                return operationId;
            }
            else
            {
                return Guid.Empty;
            }
        }

        public static void WriteCommandAfter(this SqlDiagnosticListener @this, Guid operationId, SqlCommand sqlCommand, SqlTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientCommandAfter.Name))
            {
                @this.Write(
                    SqlClientCommandAfter.Name,
                    new SqlClientCommandAfter(
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        sqlCommand.Connection?.ClientConnectionId,
                        transaction?.InternalTransaction?.TransactionId,
                        sqlCommand,
                        sqlCommand.Statistics?.GetDictionary()
                    )
                );
            }
        }

        public static void WriteCommandError(this SqlDiagnosticListener @this, Guid operationId, SqlCommand sqlCommand, SqlTransaction transaction, Exception ex, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientCommandError.Name))
            {
                @this.Write(
                    SqlClientCommandError.Name,
                    new SqlClientCommandError
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        sqlCommand.Connection?.ClientConnectionId,
                        transaction?.InternalTransaction?.TransactionId,
                        sqlCommand,
                        ex
                    )
                );
            }
        }

        public static Guid WriteConnectionOpenBefore(this SqlDiagnosticListener @this, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientConnectionOpenBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlClientConnectionOpenBefore.Name,
                    new SqlClientConnectionOpenBefore
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        sqlConnection,
                        ThisAssembly.InformationalVersion
                    )
                );

                return operationId;
            }
            else
            {
                return Guid.Empty;
            }
        }

        public static void WriteConnectionOpenAfter(this SqlDiagnosticListener @this, Guid operationId, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientConnectionOpenAfter.Name))
            {
                @this.Write(
                    SqlClientConnectionOpenAfter.Name,
                    new SqlClientConnectionOpenAfter
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        sqlConnection.ClientConnectionId,
                        sqlConnection,
                        ThisAssembly.InformationalVersion,
                        sqlConnection.Statistics?.GetDictionary()
                    )
                );
            }
        }

        public static void WriteConnectionOpenError(this SqlDiagnosticListener @this, Guid operationId, SqlConnection sqlConnection, Exception ex, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientConnectionOpenError.Name))
            {
                @this.Write(
                    SqlClientConnectionOpenError.Name,
                    new SqlClientConnectionOpenError
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        sqlConnection.ClientConnectionId,
                        sqlConnection,
                        ThisAssembly.InformationalVersion,
                        ex
                    )
                );
            }
        }

        public static Guid WriteConnectionCloseBefore(this SqlDiagnosticListener @this, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientConnectionCloseBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlClientConnectionCloseBefore.Name,
                    new SqlClientConnectionCloseBefore
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        sqlConnection.ClientConnectionId,
                        sqlConnection,
                        sqlConnection.Statistics?.GetDictionary()
                    )
                );

                return operationId;
            }
            else
                return Guid.Empty;
        }

        public static void WriteConnectionCloseAfter(this SqlDiagnosticListener @this, Guid operationId, Guid clientConnectionId, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientConnectionCloseAfter.Name))
            {
                @this.Write(
                    SqlClientConnectionCloseAfter.Name,
                    new SqlClientConnectionCloseAfter
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        clientConnectionId,
                        sqlConnection,
                        sqlConnection.Statistics?.GetDictionary()
                    )
                );
            }
        }

        public static void WriteConnectionCloseError(this SqlDiagnosticListener @this, Guid operationId, Guid clientConnectionId, SqlConnection sqlConnection, Exception ex, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientConnectionCloseError.Name))
            {
                @this.Write(
                    SqlClientConnectionCloseError.Name,
                    new SqlClientConnectionCloseError
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        clientConnectionId,
                        sqlConnection,
                        sqlConnection.Statistics?.GetDictionary(),
                        ex
                    )
                );
            }
        }

        public static Guid WriteTransactionCommitBefore(this SqlDiagnosticListener @this, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientTransactionCommitBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlClientTransactionCommitBefore.Name,
                    new SqlClientTransactionCommitBefore
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        isolationLevel,
                        connection,
                        transaction?.TransactionId
                    )
                );

                return operationId;
            }
            else
            {
                return Guid.Empty;
            }
        }

        public static void WriteTransactionCommitAfter(this SqlDiagnosticListener @this, Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientTransactionCommitAfter.Name))
            {
                @this.Write(
                    SqlClientTransactionCommitAfter.Name,
                    new SqlClientTransactionCommitAfter
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        isolationLevel,
                        connection,
                        transaction?.TransactionId
                    )
                );
            }
        }

        public static void WriteTransactionCommitError(this SqlDiagnosticListener @this, Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, Exception ex, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientTransactionCommitError.Name))
            {
                @this.Write(
                    SqlClientTransactionCommitError.Name,
                    new SqlClientTransactionCommitError
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        isolationLevel,
                        connection,
                        transaction?.TransactionId,
                        ex
                    )
               );
            }
        }

        public static Guid WriteTransactionRollbackBefore(this SqlDiagnosticListener @this, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientTransactionRollbackBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlClientTransactionRollbackBefore.Name,
                    new SqlClientTransactionRollbackBefore
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        isolationLevel,
                        connection,
                        transaction?.TransactionId,
                        transactionName
                    )
                );

                return operationId;
            }
            else
            {
                return Guid.Empty;
            }
        }

        public static void WriteTransactionRollbackAfter(this SqlDiagnosticListener @this, Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientTransactionRollbackAfter.Name))
            {
                @this.Write(
                    SqlClientTransactionRollbackAfter.Name,
                    new SqlClientTransactionRollbackAfter
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        isolationLevel,
                        connection,
                        transaction?.TransactionId,
                        transactionName
                    )
                );
            }
        }

        public static void WriteTransactionRollbackError(this SqlDiagnosticListener @this, Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, Exception ex, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (@this.IsEnabled(SqlClientTransactionRollbackError.Name))
            {
                @this.Write(
                    SqlClientTransactionRollbackError.Name,
                    new SqlClientTransactionRollbackError
                    (
                        operationId,
                        operation,
                        Stopwatch.GetTimestamp(),
                        isolationLevel,
                        connection,
                        transaction?.TransactionId,
                        transactionName,
                        ex
                    )
                );
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
