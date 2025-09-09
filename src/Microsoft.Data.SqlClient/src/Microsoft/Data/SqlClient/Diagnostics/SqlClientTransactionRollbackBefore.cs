// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace Microsoft.Data.SqlClient.Diagnostics
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/SqlClientTransactionRollbackBefore/*'/>
    public sealed class SqlClientTransactionRollbackBefore : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/SqlClientTransactionRollbackBefore/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackBefore";

        internal SqlClientTransactionRollbackBefore(
            Guid operationId,
            string operation,
            long timestamp,
            IsolationLevel isolationLevel,
            SqlConnection connection,
            long? transactionId,
            string transactionName)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            TransactionName = transactionName;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/TransactionId/*'/>
        public long? TransactionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/TransactionName/*'/>
        public string TransactionName { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
        public int Count => 3 + 4;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
        public KeyValuePair<string, object> this[int index]
        {
            get => index switch
            {
                0 => new KeyValuePair<string, object>(nameof(OperationId), OperationId),
                1 => new KeyValuePair<string, object>(nameof(Operation), Operation),
                2 => new KeyValuePair<string, object>(nameof(Timestamp), Timestamp),
                3 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
                4 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                5 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
                6 => new KeyValuePair<string, object>(nameof(TransactionName), TransactionName),
                _ => throw new IndexOutOfRangeException(nameof(index)),
            };
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetEnumerator/*'/>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            int count = Count;
            for (int index = 0; index < count; index++)
            {
                yield return this[index];
            }
        }
    }
}
