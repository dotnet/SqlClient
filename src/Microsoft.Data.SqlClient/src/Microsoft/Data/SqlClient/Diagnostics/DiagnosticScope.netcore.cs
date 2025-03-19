// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.SqlClient.Diagnostics
{
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
}

#endif
