// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace Microsoft.Data.SqlClient.Diagnostics
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/SqlClientCommandBefore/*'/>
    public sealed class SqlClientCommandBefore : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/SqlClientCommandBefore/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteCommandBefore";

        internal SqlClientCommandBefore(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command)
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/SqlClientCommandAfter/*'/>
    public sealed class SqlClientCommandAfter : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteCommandAfter";

        internal SqlClientCommandAfter(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command, IDictionary statistics)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
            Statistics = statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/TransactionId/*'/>
        public long? TransactionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Command/*'/>
        public SqlCommand Command { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Statistics/*'/>
        public IDictionary Statistics { get; }

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
                3 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
                4 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
                5 => new KeyValuePair<string, object>(nameof(Command), Command),
                6 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/SqlClientCommandError/*'/>
    public sealed class SqlClientCommandError : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/SqlClientCommandError/*'/>

        public const string Name = "Microsoft.Data.SqlClient.WriteCommandError";

        internal SqlClientCommandError(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command, Exception exception)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
            Exception = exception;
        }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/TransactionId/*'/>
        public long? TransactionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/Command/*'/>
        public SqlCommand Command { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/Exception/*'/>
        public Exception Exception { get; }

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
                3 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
                4 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
                5 => new KeyValuePair<string, object>(nameof(Command), Command),
                6 => new KeyValuePair<string, object>(nameof(Exception), Exception),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/SqlClientConnectionOpenBefore/*'/>
    public sealed class SqlClientConnectionOpenBefore : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenBefore";

        internal SqlClientConnectionOpenBefore(Guid operationId, string operation, long timestamp, SqlConnection connection, string clientVersion)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            Connection = connection;
            ClientVersion = clientVersion;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/ClientVersion/*'/>
        public string ClientVersion { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
        public int Count => 3 + 2;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Item1/*'/>
        public KeyValuePair<string, object> this[int index]
        {
            get => index switch
            {
                0 => new KeyValuePair<string, object>(nameof(OperationId), OperationId),
                1 => new KeyValuePair<string, object>(nameof(Operation), Operation),
                2 => new KeyValuePair<string, object>(nameof(Timestamp), Timestamp),
                3 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                4 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/SqlClientConnectionOpenAfter/*'/>
    public sealed class SqlClientConnectionOpenAfter : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenAfter";

        internal SqlClientConnectionOpenAfter(Guid operationId, string operation, long timestamp, Guid connectionId, SqlConnection connection, string clientVersion, IDictionary statistics)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            ConnectionId = connectionId;
            Connection = connection;
            ClientVersion = clientVersion;
            Statistics = statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/ConnectionId/*'/>
        public Guid ConnectionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/ClientVersion/*'/>
        public string ClientVersion { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Statistics/*'/>
        public IDictionary Statistics { get; }

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
                3 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
                4 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                5 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
                6 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/SqlClientConnectionOpenError/*'/>
    public sealed class SqlClientConnectionOpenError : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Name/*'/>

        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenError";

        internal SqlClientConnectionOpenError(Guid operationId, string operation, long timestamp, Guid connectionId, SqlConnection connection, string clientVersion, Exception exception)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            ConnectionId = connectionId;
            Connection = connection;
            ClientVersion = clientVersion;
            Exception = exception;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/ConnectionId/*'/>
        public Guid ConnectionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/ClientVersion/*'/>
        public string ClientVersion { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Exception/*'/>
        public Exception Exception { get; }

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
                3 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
                4 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                5 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
                6 => new KeyValuePair<string, object>(nameof(Exception), Exception),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/SqlClientConnectionCloseBefore/*'/>
    public sealed class SqlClientConnectionCloseBefore : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]//*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseBefore";

        internal SqlClientConnectionCloseBefore(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/Statistics/*'/>
        public IDictionary Statistics { get; }

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
                4 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                5 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/SqlClientConnectionCloseAfter/*'/>
    public sealed class SqlClientConnectionCloseAfter : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseAfter";

        internal SqlClientConnectionCloseAfter(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Statistics/*'/>
        public IDictionary Statistics { get; }

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
                4 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                5 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/SqlClientConnectionCloseError/*'/>
    public sealed class SqlClientConnectionCloseError : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseError";

        internal SqlClientConnectionCloseError(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics, Exception ex)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
            Exception = ex;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Statistics/*'/>
        public IDictionary Statistics { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Exception/*'/>
        public Exception Exception { get; }

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
                3 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
                4 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                5 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
                6 => new KeyValuePair<string, object>(nameof(Exception), Exception),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/SqlClientTransactionCommitBefore/*'/>
    public sealed class SqlClientTransactionCommitBefore : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitBefore";

        internal SqlClientTransactionCommitBefore(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/TransactionId/*'/>
        public long? TransactionId { get; }

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
                3 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
                4 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                5 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/SqlClientTransactionCommitAfter/*'/>
    public sealed class SqlClientTransactionCommitAfter : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitAfter";

        internal SqlClientTransactionCommitAfter(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/TransactionId/*'/>
        public long? TransactionId { get; }

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
                3 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
                4 => new KeyValuePair<string, object>(nameof(Connection), Connection),
                5 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/SqlClientTransactionCommitError/*'/>
    public sealed class SqlClientTransactionCommitError : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitError";

        internal SqlClientTransactionCommitError(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, Exception ex)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            Exception = ex;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/TransactionId/*'/>
        public long? TransactionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Exception/*'/>
        public Exception Exception { get; }

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
                6 => new KeyValuePair<string, object>(nameof(Exception), Exception),
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/SqlClientTransactionRollbackBefore/*'/>
    public sealed class SqlClientTransactionRollbackBefore : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/SqlClientTransactionRollbackBefore/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackBefore";

        internal SqlClientTransactionRollbackBefore(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName)
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/SqlClientTransactionRollbackAfter/*'/>
    public sealed class SqlClientTransactionRollbackAfter : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackAfter";

        internal SqlClientTransactionRollbackAfter(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName)
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
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/TransactionId/*'/>
        public long? TransactionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/TransactionName/*'/>
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/SqlClientTransactionRollbackError/*'/>
    public sealed class SqlClientTransactionRollbackError : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackError";

        internal SqlClientTransactionRollbackError(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName, Exception ex)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            TransactionName = transactionName;
            Exception = ex;
        }


        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/Connection/*'/>
        public SqlConnection Connection { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/TransactionId/*'/>
        public long? TransactionId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/TransactionName/*'/>
        public string TransactionName { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/Exception/*'/>
        public Exception Exception { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Count/*'/>
        public int Count => 3 + 5;

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
                7 => new KeyValuePair<string, object>(nameof(Exception), Exception),
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
