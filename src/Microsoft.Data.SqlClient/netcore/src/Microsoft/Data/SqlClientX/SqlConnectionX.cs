// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// A connection object that utilizes the SqlClientX networking and pooling implementations.
    /// </summary>
    [DefaultEvent("InfoMessage")]
    [DesignerCategory("")]
    internal sealed class SqlConnectionX : DbConnection, ICloneable
    {
        #region private
        private static readonly SqlConnectionStringBuilder DefaultSettings = new();

        private SqlCredential? _credential;
        private SqlDataSource? _dataSource;
        private SqlConnector? _internalConnection;

        private bool _disposed;

        //TODO: Investigate if we can just use dataSource.ConnectionString. Do this when this class can resolve its own data source.
        private string _connectionString = string.Empty;
        
        private ConnectionState _connectionState = ConnectionState.Closed;
        #endregion

        #region constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlConnectionX"/> class.
        /// </summary>
        public SqlConnectionX()
            => GC.SuppressFinalize(this);

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlConnectionX"/> class.
        /// </summary>
        internal SqlConnectionX(string connectionString) : this()
        {
            _connectionState = ConnectionState.Connecting;
            _connectionString = connectionString;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlConnectionX"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string used to connect to the database.</param>
        /// <param name="credential">The credentials used to connect to the database.</param>
        internal SqlConnectionX(string connectionString, SqlCredential credential) : this(connectionString)
        {
            _credential = credential;
        }

        /// <summary>
        /// Initializes a connection using the provided data source.
        /// </summary>
        /// <param name="dataSource">A data source that provides internal connection objects.</param>
        /// <returns></returns>
        internal static SqlConnectionX FromDataSource(SqlDataSource dataSource)
            => new()
            {
                _connectionString = dataSource.ConnectionString,
                _dataSource = dataSource,
                _credential = dataSource.Credential
            };

        #endregion

        #region public properties

        /// <inheritdoc/>
        [Browsable(false)]
        public override ConnectionState State => _connectionState;

        /// <inheritdoc/>
        [Browsable(false)]
        public override string ServerVersion
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override string DataSource
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override string Database
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override int ConnectionTimeout => Settings.ConnectTimeout;

        /// <summary>
        /// Gets or sets the connection string used to connect to the database.
        /// </summary>
        [AllowNull]
        [DefaultValue("")]
        [RefreshProperties(RefreshProperties.All)]
        [SettingsBindable(true)]
        public override string ConnectionString
        {
            get => _connectionString;
            set
            {
                switch (State)
                {
                    case ConnectionState.Open:
                    case ConnectionState.Connecting:
                    case ConnectionState.Fetching:
                    case ConnectionState.Executing:
                        throw ADP.OpenConnectionPropertySet(nameof(ConnectionString), State);
                }

                _connectionString = value ?? string.Empty;

                //TODO: build new data source or find existing data source based on connection string
            }
        }

        /// <inheritdoc/>
        public override bool CanCreateBatch
            => throw new NotImplementedException();

        #endregion

        #region protected properties

        /// <inheritdoc/>
        protected override DbProviderFactory? DbProviderFactory
            => throw new NotImplementedException();

        internal SqlConnector? InternalConnection => _internalConnection;

        internal SqlConnectionStringBuilder Settings { get; private set; } = DefaultSettings;

        #endregion

        /// <inheritdoc/>
        public override void ChangeDatabase(string databaseName)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override Task ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public object Clone()
            => throw new NotImplementedException();

        #region close

        /// <inheritdoc/>
        public override void Close()
            => Close(async: false).GetAwaiter().GetResult();

        /// <inheritdoc/>
        public override Task CloseAsync()
            => Close(async: true);

        internal Task Close(bool async)
        {
            //TODO: make thread safe?

            switch (State)
            {
                case ConnectionState.Open:
                case ConnectionState.Executing:
                case ConnectionState.Fetching:
                    break;
                case ConnectionState.Connecting:
                    //TODO: change this to match current behavior. cancels any pending async open tasks
                    break;
                default:
                    return Task.CompletedTask;
            }

            return CloseAsync(async);

            Task CloseAsync(bool async)
            {
                Debug.Assert(_internalConnection != null);
                Debug.Assert(_dataSource != null);

                var internalConnection = _internalConnection;

                if (internalConnection != null)
                {
                    //TODO: cancel outstanding operations on connection before close
                    //TODO: if pooling, reset the connector

                    internalConnection.OwningConnection = null;
                    internalConnection.Return();
                    _internalConnection = null;
                }

                _connectionState = ConnectionState.Closed;
                return Task.CompletedTask;
            }
        }

        #endregion

        /// <summary>
        /// Releases all resources used by the <see cref="SqlConnectionX"/>.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose"/>;
        /// <see langword="false"/> when being called from the finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                Close();
            }
            
            _disposed = true;
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await CloseAsync().ConfigureAwait(false);
            _disposed = true;
        }
        /// <inheritdoc/>
        public override void EnlistTransaction(Transaction? transaction)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override DataTable GetSchema()
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override DataTable GetSchema(string collectionName, string?[] restrictionValues)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override DataTable GetSchema(string collectionName)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override Task<DataTable> GetSchemaAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override Task<DataTable> GetSchemaAsync(string collectionName, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override Task<DataTable> GetSchemaAsync(string collectionName, string?[] restrictionValues, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        #region open

        /// <inheritdoc/>
        public override void Open() => Open(false, CancellationToken.None).GetAwaiter().GetResult();

        /// <inheritdoc/>
        public override Task OpenAsync(CancellationToken cancellationToken) => Open(true, cancellationToken);

        internal async Task Open(bool async, CancellationToken cancellationToken)
        {
            if (_dataSource == null)
            {
                throw ADP.NoConnectionString();
            }

            _internalConnection = await _dataSource.GetInternalConnection(this, TimeSpan.FromSeconds(ConnectionTimeout), async, cancellationToken).ConfigureAwait(false);
            _connectionState = ConnectionState.Open;
        }

        #endregion

        /// <inheritdoc/>
        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        protected override ValueTask<DbTransaction> BeginDbTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        protected override DbBatch CreateDbBatch()
            => throw new NotImplementedException();

        /// <inheritdoc/>
        protected override DbCommand CreateDbCommand()
            => throw new NotImplementedException();
    }
}

#endif
