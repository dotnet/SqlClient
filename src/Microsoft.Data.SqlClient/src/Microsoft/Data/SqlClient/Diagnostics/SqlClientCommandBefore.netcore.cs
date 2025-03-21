// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.Diagnostics
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/SqlClientCommandBefore/*'/>
    public sealed class SqlClientCommandBefore : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/SqlClientCommandBefore/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteCommandBefore";

        internal SqlClientCommandBefore(
            Guid operationId,
            string operation,
            long timestamp,
            Guid? connectionId,
            long? transactionId,
            SqlCommand command)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/TransactionId/*'/>
        public long? TransactionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/Command/*'/>
        public SqlCommand Command { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
        public int Count => 3 + 3;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
        public KeyValuePair<string, object> this[int index]
        {
            get => index switch
            {
                0 => new KeyValuePair<string, object>(nameof(OperationId), OperationId),
                1 => new KeyValuePair<string, object>(nameof(Operation), Operation),
                2 => new KeyValuePair<string, object>(nameof(Timestamp), Timestamp),
                3 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
                4 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
                5 => new KeyValuePair<string, object>(nameof(Command), Command),
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

#endif
