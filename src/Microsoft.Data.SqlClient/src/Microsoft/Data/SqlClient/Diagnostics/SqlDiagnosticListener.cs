// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET
using System.Reflection;
using System.Runtime.Loader;
#else
using System.Runtime.ConstrainedExecution;
#endif

namespace Microsoft.Data.SqlClient.Diagnostics
{
    internal sealed class SqlDiagnosticListener : DiagnosticListener
    {
        public SqlDiagnosticListener() : base("SqlClientDiagnosticListener")
        {
#if NET
            AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()).Unloading += SqlDiagnosticListener_UnloadingAssemblyLoadContext;
#else
            AppDomain.CurrentDomain.DomainUnload += SqlDiagnosticListener_UnloadingAppDomain;
#endif
        }

        public DiagnosticScope CreateCommandScope(
            SqlCommand command,
            SqlTransaction transaction,
            [CallerMemberName]
            string operationName = "")
        {
            return DiagnosticScope.CreateCommandScope(this, command, transaction, operationName);
        }

        public DiagnosticTransactionScope CreateTransactionCommitScope(
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            [CallerMemberName]
            string operationName = "")
        {
            return DiagnosticTransactionScope.CreateTransactionCommitScope(
                this,
                isolationLevel,
                connection,
                transaction,
                operationName);
        }

        public DiagnosticTransactionScope CreateTransactionRollbackScope(
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            string transactionName,
            [CallerMemberName]
            string operationName = "")
        {
            return DiagnosticTransactionScope.CreateTransactionRollbackScope(
                this,
                isolationLevel,
                connection,
                transaction,
                transactionName,
                operationName);
        }

        public void WriteCommandAfter(
            Guid operationId,
            SqlCommand sqlCommand,
            SqlTransaction transaction,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientCommandAfter.Name))
            {
                return;
            }

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

        public Guid WriteCommandBefore(
            SqlCommand sqlCommand,
            SqlTransaction transaction,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientCommandBefore.Name))
            {
                return Guid.Empty;
            }

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

        public void WriteCommandError(
            Guid operationId,
            SqlCommand sqlCommand,
            SqlTransaction transaction,
            Exception ex,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientCommandError.Name))
            {
                return;
            }

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

        public void WriteConnectionCloseAfter(
            Guid operationId,
            Guid clientConnectionId,
            SqlConnection sqlConnection,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientConnectionCloseAfter.Name))
            {
                return;
            }

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

        public Guid WriteConnectionCloseBefore(SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (!IsEnabled(SqlClientConnectionCloseBefore.Name))
            {
                return Guid.Empty;
            }

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

        public void WriteConnectionCloseError(
            Guid operationId,
            Guid clientConnectionId,
            SqlConnection sqlConnection,
            Exception ex,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientConnectionCloseError.Name))
            {
                return;
            }

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

        public void WriteConnectionOpenAfter(
            Guid operationId,
            SqlConnection sqlConnection,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientConnectionOpenAfter.Name))
            {
                return;
            }

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

        public Guid WriteConnectionOpenBefore(SqlConnection sqlConnection, [CallerMemberName] string operation = "")
        {
            if (!IsEnabled(SqlClientConnectionOpenBefore.Name))
            {
                return Guid.Empty;
            }

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

        public void WriteConnectionOpenError(
            Guid operationId,
            SqlConnection sqlConnection,
            Exception ex,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientConnectionOpenError.Name))
            {
                return;
            }

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

        public void WriteTransactionCommitAfter(
            Guid operationId,
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientTransactionCommitAfter.Name))
            {
                return;
            }

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

        public Guid WriteTransactionCommitBefore(
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientTransactionCommitBefore.Name))
            {
                return Guid.Empty;
            }

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

        public void WriteTransactionCommitError(
            Guid operationId,
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            Exception ex,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientTransactionCommitError.Name))
            {
                return;
            }

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

        public void WriteTransactionRollbackAfter(
            Guid operationId,
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            string transactionName = null,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientTransactionRollbackAfter.Name))
            {
                return;
            }

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

        public Guid WriteTransactionRollbackBefore(
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            string transactionName = null,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientTransactionRollbackBefore.Name))
            {
                return Guid.Empty;
            }

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

        public void WriteTransactionRollbackError(
            Guid operationId,
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            Exception ex,
            string transactionName = null,
            [CallerMemberName]
            string operation = "")
        {
            if (!IsEnabled(SqlClientTransactionRollbackError.Name))
            {
                return;
            }

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

#if NET
        private void SqlDiagnosticListener_UnloadingAssemblyLoadContext(AssemblyLoadContext obj) =>
            Dispose();
#else
        [PrePrepareMethod]
        private void SqlDiagnosticListener_UnloadingAppDomain(object sender, EventArgs e) =>
            Dispose();
#endif
    }
}
