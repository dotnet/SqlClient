// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.ProviderBase
{
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Common;
    using SysTx = System.Transactions;

    abstract internal class DbConnectionClosed : DbConnectionInternal
    {
        // Construct an "empty" connection
        protected DbConnectionClosed(ConnectionState state, bool hidePassword, bool allowSetConnectionString) : base(state, hidePassword, allowSetConnectionString)
        {
        }

        override public string ServerVersion
        {
            get
            {
                throw ADP.ClosedConnectionError();
            }
        }

        override protected void Activate(SysTx.Transaction transaction)
        {
            throw ADP.ClosedConnectionError();
        }

        override public DbTransaction BeginTransaction(IsolationLevel il)
        {
            throw ADP.ClosedConnectionError();
        }

        override public void ChangeDatabase(string database)
        {
            throw ADP.ClosedConnectionError();
        }

        internal override void CloseConnection(DbConnection owningObject, DbConnectionFactory connectionFactory)
        {
            // not much to do here...
        }

        override protected void Deactivate()
        {
            throw ADP.ClosedConnectionError();
        }

        override public void EnlistTransaction(SysTx.Transaction transaction)
        {
            throw ADP.ClosedConnectionError();
        }

        override protected internal DataTable GetSchema(DbConnectionFactory factory, DbConnectionPoolGroup poolGroup, DbConnection outerConnection, string collectionName, string[] restrictions)
        {
            throw ADP.ClosedConnectionError();
        }

        protected override DbReferenceCollection CreateReferenceCollection()
        {
            throw ADP.ClosedConnectionError();
        }

        internal override Task<bool> TryOpenConnection(DbConnection outerConnection, DbConnectionFactory connectionFactory, CancellationToken cancellationToken, DbConnectionOptions userOptions)
        {
            return base.TryOpenConnectionInternal(outerConnection, connectionFactory, cancellationToken, userOptions);
        }
    }

    abstract internal class DbConnectionBusy : DbConnectionClosed
    {

        protected DbConnectionBusy(ConnectionState state) : base(state, true, false)
        {
        }
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
        {
            return TryOpenConnection(outerConnection, connectionFactory, cancellationToken, userOptions);
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
        {
            return TryOpenConnection(outerConnection, connectionFactory, cancellationToken, userOptions);
        }
    }
}
