// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

#nullable enable

namespace Microsoft.Data.SqlClient.Diagnostics
{
    /// <summary>
    /// Provides a scope for emitting diagnostic events related to SQL command and connection operations. Used to track
    /// the start, completion, and error states of database operations for diagnostic listeners.
    /// </summary>
    /// <remarks>
    /// A DiagnosticScope is typically created using the <see cref="SqlDiagnosticListener.CreateCommandScope"/> method
    /// and is intended to be used in a using statement to ensure proper disposal. Disposing the scope emits the
    /// appropriate completion or error event based on whether an exception was set.
    /// </remarks>
    internal ref struct DiagnosticScope : IDisposable
    {
        private const int CommandOperation = 1;
        private const int ConnectionOpenOperation = 2;

        private readonly object _context1;
        private readonly object? _context2;
        private readonly SqlDiagnosticListener _diagnostics;
        private readonly int _operation;
        private readonly Guid _operationId;
        private readonly string _operationName;

        private Exception? _exception;

        private DiagnosticScope(
            SqlDiagnosticListener diagnostics,
            int operation,
            Guid operationId,
            string operationName,
            object context1,
            object? context2)
        {
            _diagnostics = diagnostics;
            _operation = operation;
            _operationId = operationId;
            _operationName = operationName;
            _context1 = context1;
            _context2 = context2;
            _exception = null;
        }

        public static DiagnosticScope CreateCommandScope(
            SqlDiagnosticListener diagnostics,
            SqlCommand command,
            SqlTransaction? transaction,
            [CallerMemberName]
            string operationName = "")
        {
            Guid operationId = diagnostics.WriteCommandBefore(command, transaction, operationName);
            return new DiagnosticScope(diagnostics, CommandOperation, operationId, operationName, command, transaction);
        }

        public readonly void Dispose()
        {
            switch (_operation)
            {
                case CommandOperation:
                    if (_exception is not null)
                    {
                        _diagnostics.WriteCommandError(
                            _operationId,
                            (SqlCommand)_context1,
                            (SqlTransaction?)_context2,
                            _exception,
                            _operationName);
                    }
                    else
                    {
                        _diagnostics.WriteCommandAfter(
                            _operationId,
                            (SqlCommand)_context1,
                            (SqlTransaction?)_context2,
                            _operationName);
                    }
                    break;

                case ConnectionOpenOperation:
                    if (_exception is not null)
                    {
                        _diagnostics.WriteConnectionOpenError(
                            _operationId,
                            (SqlConnection)_context1,
                            _exception,
                            _operationName);
                    }
                    else
                    {
                        _diagnostics.WriteConnectionOpenAfter(
                            _operationId,
                            (SqlConnection)_context1,
                            _operationName);
                    }
                    break;

                    // ConnectionCloseOperation is not implemented because it is conditionally
                    // emitted and that requires manual calls to the write apis
            }
        }

        public void SetException(Exception ex)
        {
            _exception = ex;
        }
    }
}
