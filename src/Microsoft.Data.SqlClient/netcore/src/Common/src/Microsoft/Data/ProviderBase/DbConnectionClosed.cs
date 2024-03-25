// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.ProviderBase
{
    abstract internal partial class DbConnectionClosed : DbConnectionInternal
    {
        // Construct an "empty" connection
        protected DbConnectionClosed(ConnectionState state, bool hidePassword, bool allowSetConnectionString) : base(state, hidePassword, allowSetConnectionString)
        {
        }

        public override string ServerVersion => throw ADP.ClosedConnectionError();

        public override DbTransaction BeginTransaction(IsolationLevel il) => throw ADP.ClosedConnectionError();

        public override void ChangeDatabase(string database) => throw ADP.ClosedConnectionError();

        internal override void CloseConnection(DbConnection owningObject, DbConnectionFactory connectionFactory)
        {
            // not much to do here...
        }

        protected override void Deactivate() => ADP.ClosedConnectionError();

        protected internal override DataTable GetSchema(DbConnectionFactory factory, DbConnectionPoolGroup poolGroup, DbConnection outerConnection, string collectionName, string[] restrictions)
            => throw ADP.ClosedConnectionError();

        protected override DbReferenceCollection CreateReferenceCollection() => throw ADP.ClosedConnectionError();

        internal override Task<bool> TryOpenConnection(DbConnection outerConnection, DbConnectionFactory connectionFactory, CancellationToken cancellationToken, DbConnectionOptions userOptions)
            => base.TryOpenConnectionInternal(outerConnection, connectionFactory, cancellationToken, userOptions);
    }

    abstract internal class DbConnectionBusy : DbConnectionClosed
    {
        protected DbConnectionBusy(ConnectionState state) : base(state, true, false)
        {
        }

        internal override Task<bool> TryOpenConnection(DbConnection outerConnection, DbConnectionFactory connectionFactory, CancellationToken cancellationToken, DbConnectionOptions userOptions)
            => throw ADP.ConnectionAlreadyOpen(State);
    }

    sealed internal class DbConnectionClosedBusy : DbConnectionBusy
    {
        // Closed Connection, Currently Busy - changing connection string
        internal static readonly DbConnectionInternal SingletonInstance = new DbConnectionClosedBusy();   // singleton object

        private DbConnectionClosedBusy() : base(ConnectionState.Closed)
        {
        }
    }

    sealed internal class DbConnectionOpenBusy : DbConnectionBusy
    {
        // Open Connection, Currently Busy - closing connection
        internal static readonly DbConnectionInternal SingletonInstance = new DbConnectionOpenBusy();   // singleton object

        private DbConnectionOpenBusy() : base(ConnectionState.Open)
        {
        }
    }

    sealed internal class DbConnectionClosedConnecting : DbConnectionBusy
    {
        // Closed Connection, Currently Connecting

        internal static readonly DbConnectionInternal SingletonInstance = new DbConnectionClosedConnecting();   // singleton object

        private DbConnectionClosedConnecting() : base(ConnectionState.Connecting)
        {
        }

        internal override void CloseConnection(DbConnection owningObject, DbConnectionFactory connectionFactory)
        {
            connectionFactory.SetInnerConnectionTo(owningObject, DbConnectionClosedPreviouslyOpened.SingletonInstance);
        }

        internal override Task<bool> TryReplaceConnection(DbConnection outerConnection, DbConnectionFactory connectionFactory, CancellationToken cancellationToken, DbConnectionOptions userOptions)
            => TryOpenConnection(outerConnection, connectionFactory, cancellationToken, userOptions);

        internal override Task<bool> TryOpenConnection(DbConnection outerConnection, DbConnectionFactory connectionFactory, CancellationToken cancellationToken, DbConnectionOptions userOptions)
        {
            throw ADP.ConnectionAlreadyOpen(State);
        }
    }

    sealed internal class DbConnectionClosedNeverOpened : DbConnectionClosed
    {
        // Closed Connection, Has Never Been Opened

        internal static readonly DbConnectionInternal SingletonInstance = new DbConnectionClosedNeverOpened();   // singleton object

        private DbConnectionClosedNeverOpened() : base(ConnectionState.Closed, false, true)
        {
        }
    }

    sealed internal class DbConnectionClosedPreviouslyOpened : DbConnectionClosed
    {
        // Closed Connection, Has Previously Been Opened

        internal static readonly DbConnectionInternal SingletonInstance = new DbConnectionClosedPreviouslyOpened();   // singleton object

        private DbConnectionClosedPreviouslyOpened() : base(ConnectionState.Closed, true, true)
        {
        }

        internal override Task<bool> TryReplaceConnection(DbConnection outerConnection, DbConnectionFactory connectionFactory, CancellationToken cancellationToken, DbConnectionOptions userOptions)
            => TryOpenConnection(outerConnection, connectionFactory, cancellationToken, userOptions);
    }
}
