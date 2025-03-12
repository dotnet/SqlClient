﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.Data.SqlClient.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlDiagnosticListener : DiagnosticListener
    {
        public const string DiagnosticListenerName = "SqlClientDiagnosticListener";

        public SqlDiagnosticListener(string name) : base(name)
        {
            AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()).Unloading += SqlDiagnosticListener_Unloading;
        }

        public DiagnosticScope CreateCommandScope(SqlCommand command, SqlTransaction transaction, [CallerMemberName] string operationName = "")
        {
            return DiagnosticScope.CreateCommandScope(this, command, transaction, operationName);
        }

        public DiagnosticTransactionScope CreateTransactionCommitScope(IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operationName = "")
        {
            return DiagnosticTransactionScope.CreateTransactionCommitScope(this, isolationLevel, connection, transaction, operationName);
        }

        public DiagnosticTransactionScope CreateTransactionRollbackScope(IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName, [CallerMemberName] string operationName = "")
        {
            return DiagnosticTransactionScope.CreateTransactionRollbackScope(this, isolationLevel, connection, transaction, transactionName, operationName);
        }

        public void WriteCommandAfter(Guid operationId, SqlCommand sqlCommand, SqlTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientCommandAfter.Name))
            {
                Write(
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

        public Guid WriteCommandBefore(SqlCommand sqlCommand, SqlTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientCommandBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                Write(
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

        public void WriteCommandError(Guid operationId, SqlCommand sqlCommand, SqlTransaction transaction, Exception ex, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientCommandError.Name))
            {
                Write(
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

        public void WriteConnectionCloseAfter(Guid operationId, Guid clientConnectionId, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientConnectionCloseAfter.Name))
            {
                Write(
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

        public Guid WriteConnectionCloseBefore(SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientConnectionCloseBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                Write(
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

        public void WriteConnectionCloseError(Guid operationId, Guid clientConnectionId, SqlConnection sqlConnection, Exception ex, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientConnectionCloseError.Name))
            {
                Write(
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

        public void WriteConnectionOpenAfter(Guid operationId, SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientConnectionOpenAfter.Name))
            {
                Write(
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

        public Guid WriteConnectionOpenBefore(SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientConnectionOpenBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                Write(
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

        public void WriteConnectionOpenError(Guid operationId, SqlConnection sqlConnection, Exception ex, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientConnectionOpenError.Name))
            {
                Write(
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

        public void WriteTransactionCommitAfter(Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientTransactionCommitAfter.Name))
            {
                Write(
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

        public Guid WriteTransactionCommitBefore(IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientTransactionCommitBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                Write(
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

        public void WriteTransactionCommitError(Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, Exception ex, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientTransactionCommitError.Name))
            {
                Write(
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

        public void WriteTransactionRollbackAfter(Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientTransactionRollbackAfter.Name))
            {
                Write(
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

        public Guid WriteTransactionRollbackBefore(IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientTransactionRollbackBefore.Name))
            {
                Guid operationId = Guid.NewGuid();

                Write(
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

        public void WriteTransactionRollbackError(Guid operationId, IsolationLevel isolationLevel, SqlConnection connection, SqlInternalTransaction transaction, Exception ex, string transactionName = null, [CallerMemberName] string operation = "")
        {
            if (IsEnabled(SqlClientTransactionRollbackError.Name))
            {
                Write(
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

        private void SqlDiagnosticListener_Unloading(AssemblyLoadContext obj)
        {
            Dispose();
        }
    }
}

#endif
