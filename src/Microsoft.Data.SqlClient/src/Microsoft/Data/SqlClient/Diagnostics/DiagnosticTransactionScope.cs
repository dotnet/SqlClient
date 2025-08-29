// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.SqlClient.Diagnostics
{
    internal ref struct DiagnosticTransactionScope //: IDisposable
    {
        public const int TransactionCommit = 1;
        public const int TransactionRollback = 2;

        private readonly SqlConnection _connection;
        private readonly SqlDiagnosticListener _diagnostics;
        private readonly int _operation;
        private readonly Guid _operationId;
        private readonly string _operationName;
        private readonly IsolationLevel _isolationLevel;
        private readonly SqlInternalTransaction _transaction;
        private readonly string _transactionName;

        private Exception _exception;

        public DiagnosticTransactionScope(
            SqlDiagnosticListener diagnostics,
            int operation,
            Guid operationId,
            string operationName,
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            string transactionName)
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

        public static DiagnosticTransactionScope CreateTransactionCommitScope(
            SqlDiagnosticListener diagnostics,
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            [CallerMemberName]
            string operationName = "")
        {
            Guid operationId = diagnostics.WriteTransactionCommitBefore(
                isolationLevel,
                connection,
                transaction,
                operationName);
            return new DiagnosticTransactionScope(
                diagnostics,
                TransactionCommit,
                operationId,
                operationName,
                isolationLevel,
                connection,
                transaction,
                transactionName: null);
        }

        public static DiagnosticTransactionScope CreateTransactionRollbackScope(
            SqlDiagnosticListener diagnostics,
            IsolationLevel isolationLevel,
            SqlConnection connection,
            SqlInternalTransaction transaction,
            string transactionName,
            [CallerMemberName]
            string operationName = "")
        {
            Guid operationId = diagnostics.WriteTransactionRollbackBefore(
                isolationLevel,
                connection,
                transaction,
                transactionName,
                operationName);
            return new DiagnosticTransactionScope(
                diagnostics,
                TransactionCommit,
                operationId,
                operationName,
                isolationLevel,
                connection,
                transaction,
                transactionName);
        }

        // Although ref structs do not allow for inheriting from interfaces (< C#13), but the
        // compiler will know to treat this like an IDisposable (> C# 8)
        public void Dispose()
        {
            switch (_operation)
            {
                case TransactionCommit:
                    if (_exception != null)
                    {
                        _diagnostics.WriteTransactionCommitError(
                            _operationId,
                            _isolationLevel,
                            _connection,
                            _transaction,
                            _exception,
                            _operationName);
                    }
                    else
                    {
                        _diagnostics.WriteTransactionCommitAfter(
                            _operationId,
                            _isolationLevel,
                            _connection,
                            _transaction,
                            _operationName);
                    }
                    break;

                case TransactionRollback:
                    if (_exception != null)
                    {
                        _diagnostics.WriteTransactionRollbackError(
                            _operationId,
                            _isolationLevel,
                            _connection,
                            _transaction,
                            _exception,
                            _transactionName,
                            _operationName);
                    }
                    else
                    {
                        _diagnostics.WriteTransactionRollbackAfter(
                            _operationId,
                            _isolationLevel,
                            _connection,
                            _transaction,
                            _transactionName,
                            _operationName);
                    }
                    break;
            }
        }

        public void SetException(Exception ex)
        {
            _exception = ex;
        }
    }
}
