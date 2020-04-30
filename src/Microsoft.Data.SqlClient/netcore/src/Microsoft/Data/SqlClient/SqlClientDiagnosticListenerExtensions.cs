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
    }
}
