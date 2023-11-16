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
            if (@this.IsEnabled(SqlAfterExecuteCommand))
            {
                @this.Write(
                    SqlAfterExecuteCommand,
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
            if (@this.IsEnabled(SqlErrorExecuteCommand))
            {
                @this.Write(
                    SqlErrorExecuteCommand,
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
            if (@this.IsEnabled(SqlBeforeOpenConnection))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeOpenConnection,
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
            if (@this.IsEnabled(SqlAfterOpenConnection))
            {
                @this.Write(
                    SqlAfterOpenConnection,
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
            if (@this.IsEnabled(SqlErrorOpenConnection))
            {
                @this.Write(
                    SqlErrorOpenConnection,
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
            if (@this.IsEnabled(SqlBeforeCloseConnection))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeCloseConnection,
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
            if (@this.IsEnabled(SqlAfterCloseConnection))
            {
                @this.Write(
                    SqlAfterCloseConnection,
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
            if (@this.IsEnabled(SqlErrorCloseConnection))
            {
                @this.Write(
                    SqlErrorCloseConnection,
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
            if (@this.IsEnabled(SqlBeforeCommitTransaction))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeCommitTransaction,
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
            if (@this.IsEnabled(SqlAfterCommitTransaction))
            {
                @this.Write(
                    SqlAfterCommitTransaction,
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
            if (@this.IsEnabled(SqlErrorCommitTransaction))
            {
                @this.Write(
                    SqlErrorCommitTransaction,
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
            if (@this.IsEnabled(SqlBeforeRollbackTransaction))
            {
                Guid operationId = Guid.NewGuid();

                @this.Write(
                    SqlBeforeRollbackTransaction,
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
            if (@this.IsEnabled(SqlAfterRollbackTransaction))
            {
                @this.Write(
                    SqlAfterRollbackTransaction,
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
            if (@this.IsEnabled(SqlErrorRollbackTransaction))
            {
                @this.Write(
                    SqlErrorRollbackTransaction,
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract class SqlClientDiagnostic : IReadOnlyList<KeyValuePair<string, object>>
    {
        protected const int CommonPropertyCount = 3;

        protected SqlClientDiagnostic(Guid operationId, string operation, long timestamp)
        {
            OperationId = operationId;
            Operation = operation;
            Timestamp = timestamp;
        }

        public Guid OperationId { get; }
        public string Operation { get; }
        public long Timestamp { get; }

        public int Count => CommonPropertyCount + GetDerivedCount();

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

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            var count = Count;
            for (var i = 0; i < count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

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

        protected abstract int GetDerivedCount();

        protected abstract KeyValuePair<string, object> GetDerivedProperty(int index);
    }

    public sealed class SqlClientCommandBefore : SqlClientDiagnostic
    {
        public SqlClientCommandBefore(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
        }

        public Guid? ConnectionId { get; }
        public long? TransactionId { get; }
        public SqlCommand Command { get; }

        protected sealed override int GetDerivedCount() => 3;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            2 => new KeyValuePair<string, object>(nameof(Command), Command),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientCommandAfter : SqlClientDiagnostic
    {
        public SqlClientCommandAfter(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command, IDictionary statistics)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
            Statistics = statistics;
        }

        public Guid? ConnectionId { get; }
        public long? TransactionId { get; }
        public SqlCommand Command { get; }
        public IDictionary Statistics { get; }

        protected sealed override int GetDerivedCount() => 4;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            2 => new KeyValuePair<string, object>(nameof(Command), Command),
            3 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientCommandError : SqlClientDiagnostic
    {
        public SqlClientCommandError(Guid operationId, string operation, long timestamp, Guid? connectionId, long? transactionId, SqlCommand command, Exception exception)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            TransactionId = transactionId;
            Command = command;
            Exception = exception;
        }

        public Guid? ConnectionId { get; }
        public long? TransactionId { get; }
        public SqlCommand Command { get; }
        public Exception Exception { get; }

        protected sealed override int GetDerivedCount() => 4;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            2 => new KeyValuePair<string, object>(nameof(Command), Command),
            3 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientConnectionOpenBefore : SqlClientDiagnostic
    {
        public SqlClientConnectionOpenBefore(Guid operationId, string operation, long timestamp, SqlConnection connection, string clientVersion)
            : base(operationId, operation, timestamp)
        {
            Connection = connection;
            ClientVersion = clientVersion;
        }

        public SqlConnection Connection { get; }
        public string ClientVersion { get; }

        protected override int GetDerivedCount() => 2;

        protected override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            1 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientConnectionOpenAfter : SqlClientDiagnostic
    {
        public SqlClientConnectionOpenAfter(Guid operationId, string operation, long timestamp, Guid connectionId, SqlConnection connection, string clientVersion, IDictionary statistics)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            ClientVersion = clientVersion;
            Statistics = statistics;
        }

        public Guid ConnectionId { get; }
        public SqlConnection Connection { get; }
        public string ClientVersion { get; }
        public IDictionary Statistics { get; }

        protected override int GetDerivedCount() => 4;

        protected override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
            3 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientConnectionOpenError : SqlClientDiagnostic
    {
        public SqlClientConnectionOpenError(Guid operationId, string operation, long timestamp, Guid connectionId, SqlConnection connection, string clientVersion, Exception exception)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            ClientVersion = clientVersion;
            Exception = exception;
        }

        public Guid ConnectionId { get; }
        public SqlConnection Connection { get; }
        public string ClientVersion { get; }
        public Exception Exception { get; }

        protected override int GetDerivedCount() => 4;

        protected override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(ClientVersion), ClientVersion),
            3 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientConnectionCloseBefore : SqlClientDiagnostic
    {
        public SqlClientConnectionCloseBefore(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
        }

        public Guid? ConnectionId { get; }
        public SqlConnection Connection { get; }
        public IDictionary Statistics { get; }

        protected sealed override int GetDerivedCount() => 3;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientConnectionCloseAfter : SqlClientDiagnostic
    {
        public SqlClientConnectionCloseAfter(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
        }

        public Guid? ConnectionId { get; }
        public SqlConnection Connection { get; }
        public IDictionary Statistics { get; }

        protected sealed override int GetDerivedCount() => 3;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientConnectionCloseError : SqlClientDiagnostic
    {
        public SqlClientConnectionCloseError(Guid operationId, string operation, long timestamp, Guid? connectionId, SqlConnection connection, IDictionary statistics, Exception ex)
            : base(operationId, operation, timestamp)
        {
            ConnectionId = connectionId;
            Connection = connection;
            Statistics = statistics;
            Exception = ex;
        }

        public Guid? ConnectionId { get; }
        public SqlConnection Connection { get; }
        public IDictionary Statistics { get; }
        public Exception Exception { get; }

        protected sealed override int GetDerivedCount() => 4;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(ConnectionId), ConnectionId),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(Statistics), Statistics),
            3 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientTransactionCommitBefore : SqlClientDiagnostic
    {
        public SqlClientTransactionCommitBefore(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
        }

        public IsolationLevel IsolationLevel { get; }
        public SqlConnection Connection { get; }
        public long? TransactionId { get; }

        protected sealed override int GetDerivedCount() => 3;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientTransactionCommitAfter : SqlClientDiagnostic
    {
        public SqlClientTransactionCommitAfter(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
        }

        public IsolationLevel IsolationLevel { get; }
        public SqlConnection Connection { get; }
        public long? TransactionId { get; }

        protected sealed override int GetDerivedCount() => 3;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientTransactionCommitError : SqlClientDiagnostic
    {
        public SqlClientTransactionCommitError(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, Exception ex)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            Exception = ex;
        }

        public IsolationLevel IsolationLevel { get; }
        public SqlConnection Connection { get; }
        public long? TransactionId { get; }
        public Exception Exception { get; }

        protected sealed override int GetDerivedCount() => 4;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            3 => new KeyValuePair<string, object>(nameof(Exception), Exception),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientTransactionRollbackBefore : SqlClientDiagnostic
    {
        public SqlClientTransactionRollbackBefore(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            TransactionName = transactionName;
        }

        public IsolationLevel IsolationLevel { get; }
        public SqlConnection Connection { get; }
        public long? TransactionId { get; }
        public string TransactionName { get; }

        protected sealed override int GetDerivedCount() => 4;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            3 => new KeyValuePair<string, object>(nameof(TransactionName), TransactionName),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientTransactionRollbackAfter : SqlClientDiagnostic
    {
        public SqlClientTransactionRollbackAfter(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            TransactionName = transactionName;
        }

        public IsolationLevel IsolationLevel { get; }
        public SqlConnection Connection { get; }
        public long? TransactionId { get; }
        public string TransactionName { get; }

        protected sealed override int GetDerivedCount() => 4;

        protected sealed override KeyValuePair<string, object> GetDerivedProperty(int index) => index switch
        {
            0 => new KeyValuePair<string, object>(nameof(IsolationLevel), IsolationLevel),
            1 => new KeyValuePair<string, object>(nameof(Connection), Connection),
            2 => new KeyValuePair<string, object>(nameof(TransactionId), TransactionId),
            3 => new KeyValuePair<string, object>(nameof(TransactionName), TransactionName),
            _ => throw new IndexOutOfRangeException(nameof(index)),
        };
    }

    public sealed class SqlClientTransactionRollbackError : SqlClientDiagnostic
    {
        public SqlClientTransactionRollbackError(Guid operationId, string operation, long timestamp, IsolationLevel isolationLevel, SqlConnection connection, long? transactionId, string transactionName, Exception ex)
            : base(operationId, operation, timestamp)
        {
            IsolationLevel = isolationLevel;
            Connection = connection;
            TransactionId = transactionId;
            TransactionName = transactionName;
            Exception = ex;
        }

        public IsolationLevel IsolationLevel { get; }
        public SqlConnection Connection { get; }
        public long? TransactionId { get; }
        public string TransactionName { get; }
        public Exception Exception { get; }

        protected sealed override int GetDerivedCount() => 5;

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

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

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
