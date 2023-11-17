// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
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

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/SqlClientDiagnostic/*'/>
    public abstract class SqlClientDiagnostic : IReadOnlyList<KeyValuePair<string, object>>
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/CommonPropertyCount/*'/>
        protected const int CommonPropertyCount = 3;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/ctor/*'/>
        protected SqlClientDiagnostic(Guid operationId, string operation, long timestamp)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/OperationId/*'/>
        public Guid OperationId { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Operation/*'/>
        public string Operation { get; }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/Timestamp/*'/>
        public long Timestamp { get; }

        /// <inheritdoc/>>
        public int Count => CommonPropertyCount + GetDerivedCount();

        /// <inheritdoc/>>
        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                if (TryGetCommonProperty(index, out KeyValuePair<string, object> commonProperty))
                {
                    return commonProperty;
                }
                else
                {
                    return GetDerivedProperty(index - CommonPropertyCount);
                }
            }
        }

        /// <inheritdoc/>>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            int count = Count;
            for (var index = 0; index < count; index++)
            {
                yield return this[index];
            }
        }

        /// <inheritdoc/>>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/TryGetCommonProperty/*'/>
        protected bool TryGetCommonProperty(int index, out KeyValuePair<string, object> property)
        {
            switch (index)
            {
                case 0:
                    property = new KeyValuePair<string, object>(nameof(OperationId), OperationId);
                    return true;
                case 1:
                    property = new KeyValuePair<string, object>(nameof(Operation), Operation);
                    return true;
                case 2:
                    property = new KeyValuePair<string, object>(nameof(Timestamp), Timestamp);
                    return true;
                default:
                    property = default;
                    return false;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetDerivedCount/*'/>
        protected abstract int GetDerivedCount();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientDiagnostic"]/GetDerivedProperty/*'/>
        protected abstract KeyValuePair<string, object> GetDerivedProperty(int index);
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/SqlClientCommandBefore/*'/>
    public sealed class SqlClientCommandBefore : SqlClientDiagnostic
    {    
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/SqlClientCommandBefore/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteCommandBefore";

        internal SqlClientCommandBefore(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/TransactionId/*'/>
        public long? TransactionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandBefore"]/Command/*'/>
        public SqlCommand Command { get; }


        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 3;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            2 => new KeyValuePair<string, object>(nameof(Command), Command),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/SqlClientCommandAfter/*'/>
    public sealed class SqlClientCommandAfter : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteCommandAfter";

        internal SqlClientCommandAfter(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command, IDictionary statistics)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
            Statistics = statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/TransactionId/*'/>
        public long? TransactionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Command/*'/>
        public SqlCommand Command { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandAfter"]/Statistics/*'/>
        public IDictionary Statistics { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 4;
        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            2 => new KeyValuePair<string, object>(nameof(Command), Command),
            3 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/SqlClientCommandError/*'/>
    public sealed class SqlClientCommandError : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/SqlClientCommandError/*'/>

        public const string Name = "Microsoft.Data.SqlClient.WriteCommandError";

        internal SqlClientCommandError(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command, Exception exception)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
            Exception = exception;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/TransactionId/*'/>
        public long? TransactionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/Command/*'/>
        public SqlCommand Command { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientCommandError"]/Exception/*'/>
        public Exception Exception { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 4;
        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            2 => new KeyValuePair<string, object>(nameof(Command), Command),
            3 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/SqlClientConnectionOpenBefore/*'/>
    public sealed class SqlClientConnectionOpenBefore : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenBefore";

        internal SqlClientConnectionOpenBefore(Guid operationId, string operation, long timestamp, SqlConnection connection, string clientVersion)
            : base(operationId, operation, timestamp)
        {
            Connection = connection;
            ClientVersion = clientVersion;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenBefore"]/ClientVersion/*'/>
        public string ClientVersion { get; }

        /// <inheritdoc/>>
        protected override int GetDerivedCount() => 2;
        /// <inheritdoc/>>
        protected override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            1 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/SqlClientConnectionOpenAfter/*'/>
    public sealed class SqlClientConnectionOpenAfter : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenAfter";

        internal SqlClientConnectionOpenAfter(Guid operationId, string operation, long timestamp, Guid connectionId, SqlConnection connection, string clientVersion, IDictionary statistics)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            ClientVersion = clientVersion;
            Statistics = statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/ConnectionId/*'/>
        public Guid ConnectionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/ClientVersion/*'/>
        public string ClientVersion { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenAfter"]/Statistics/*'/>
        public IDictionary Statistics { get; }

        /// <inheritdoc/>>
        protected override int GetDerivedCount() => 4;

        /// <inheritdoc/>>
        protected override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
            3 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/SqlClientConnectionOpenError/*'/>
    public sealed class SqlClientConnectionOpenError : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Name/*'/>

        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionOpenError";

        internal SqlClientConnectionOpenError(Guid operationId, string operation, long timestamp, Guid connectionId, SqlConnection connection, string clientVersion, Exception exception)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            ClientVersion = clientVersion;
            Exception = exception;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/ConnectionId/*'/>
        public Guid ConnectionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/ClientVersion/*'/>
        public string ClientVersion { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionOpenError"]/Exception/*'/>
        public Exception Exception { get; }

        /// <inheritdoc/>>
        protected override int GetDerivedCount() => 4;
        /// <inheritdoc/>>
        protected override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
            3 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/SqlClientConnectionCloseBefore/*'/>
    public sealed class SqlClientConnectionCloseBefore : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]//*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseBefore";

        internal SqlClientConnectionCloseBefore(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseBefore"]/Statistics/*'/>
        public IDictionary Statistics { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 3;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/SqlClientConnectionCloseAfter/*'/>
    public sealed class SqlClientConnectionCloseAfter : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseAfter";

        internal SqlClientConnectionCloseAfter(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseAfter"]/Statistics/*'/>
        public IDictionary Statistics { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 3;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/SqlClientConnectionCloseError/*'/>
    public sealed class SqlClientConnectionCloseError : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteConnectionCloseError";

        internal SqlClientConnectionCloseError(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics, Exception ex)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
            Exception = ex;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/ConnectionId/*'/>
        public Guid? ConnectionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Connection/*'/>

        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Statistics/*'/>
        public IDictionary Statistics { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientConnectionCloseError"]/Exception/*'/>
        public Exception Exception { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 4;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            3 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/SqlClientTransactionCommitBefore/*'/>
    public sealed class SqlClientTransactionCommitBefore : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitBefore";

        internal SqlClientTransactionCommitBefore(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitBefore"]/TransactionId/*'/>
        public long? TransactionId { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 3;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/SqlClientTransactionCommitAfter/*'/>
    public sealed class SqlClientTransactionCommitAfter : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitAfter";

        internal SqlClientTransactionCommitAfter(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitAfter"]/TransactionId/*'/>
        public long? TransactionId { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 3;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/SqlClientTransactionCommitError/*'/>
    public sealed class SqlClientTransactionCommitError : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionCommitError";

        internal SqlClientTransactionCommitError(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, Exception ex)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            Exception = ex;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/TransactionId/*'/>
        public long? TransactionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionCommitError"]/Exception/*'/>
        public Exception Exception { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 4;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            3 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/SqlClientTransactionRollbackBefore/*'/>
    public sealed class SqlClientTransactionRollbackBefore : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/SqlClientTransactionRollbackBefore/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackBefore";

        internal SqlClientTransactionRollbackBefore(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            TransactionName = transactionName;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/TransactionId/*'/>
        public long? TransactionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackBefore"]/TransactionName/*'/>
        public string TransactionName { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 4;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            3 => new KeyValuePair<string, object>(nameof(TransactionName), TransactionName),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/SqlClientTransactionRollbackAfter/*'/>
    public sealed class SqlClientTransactionRollbackAfter : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackAfter";

        internal SqlClientTransactionRollbackAfter(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            TransactionName = transactionName;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/IsolationLevel/*'/>
        public IsolationLevel IsolationLevel { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/Connection/*'/>
        public SqlConnection Connection { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/TransactionId/*'/>
        public long? TransactionId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackAfter"]/TransactionName/*'/>
        public string TransactionName { get; }

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 4;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            3 => new KeyValuePair<string, object>(nameof(TransactionName), TransactionName),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/SqlClientTransactionRollbackError/*'/>
    public sealed class SqlClientTransactionRollbackError : SqlClientDiagnostic
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientDiagnostic.xml' path='docs/members[@name="SqlClientTransactionRollbackError"]/Name/*'/>
        public const string Name = "Microsoft.Data.SqlClient.WriteTransactionRollbackError";

        internal SqlClientTransactionRollbackError(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName, Exception ex)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            TransactionName = transactionName;
            Exception = ex;
        }

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

        /// <inheritdoc/>>
        protected sealed override int GetDerivedCount() => 5;

        /// <inheritdoc/>>
        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            3 => new KeyValuePair<string, object>(nameof(TransactionName), TransactionName),
            4 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
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
