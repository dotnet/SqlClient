// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.SqlServer.Server;
using System.Transactions;

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// 
    /// </summary>
    [DefaultEvent("InfoMessage")]
    [DesignerCategory("")]
    public sealed partial class SqlConnectionX : DbConnection, ICloneable
    {
        //TODO: reference to internal connection

        /// <summary>
        /// Initializes a new instance of the System.Data.Common.SqlConnectionX class.
        /// </summary>
        SqlConnectionX() : base()
        {
        }

        /// <inheritdoc/>
        [Browsable(false)]
        public override ConnectionState State { get => throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, gets a string that represents the version
        //     of the server to which the object is connected.
        //
        // Returns:
        //     The version of the database. The format of the string returned depends on the
        //     specific type of connection you are using.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     System.Data.Common.DbConnection.ServerVersion was called while the returned Task
        //     was not completed and the connection was not opened after a call to Overload:System.Data.Common.DbConnection.OpenAsync.
        [Browsable(false)]
        public override string ServerVersion { get => throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, gets the name of the database server to which
        //     to connect.
        //
        // Returns:
        //     The name of the database server to which to connect. The default value is an
        //     empty string.
        public override string DataSource { get => throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, gets the name of the current database after
        //     a connection is opened, or the database name specified in the connection string
        //     before the connection is opened.
        //
        // Returns:
        //     The name of the current database or the name of the database to be used after
        //     a connection is opened. The default value is an empty string.
        public override string Database => throw new NotImplementedException();
/// <inheritdoc/>

        //
        // Summary:
        //     Gets the time to wait (in seconds) while establishing a connection before terminating
        //     the attempt and generating an error.
        //
        // Returns:
        //     The time (in seconds) to wait for a connection to open. The default value is
        //     determined by the specific type of connection that you are using.
        public override int ConnectionTimeout
        {
            get => throw new NotImplementedException();
        }
        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, gets or sets the string used to open the
        //     connection.
        //
        // Returns:
        //     The connection string used to establish the initial connection. The exact contents
        //     of the connection string depend on the specific data source for this connection.
        //     The default value is an empty string.
        [DefaultValue("")]
        [RefreshProperties(RefreshProperties.All)]
        [SettingsBindable(true)]
        public override string ConnectionString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
/// <inheritdoc/>

        //
        // Summary:
        //     Gets a value that indicates whether this System.Data.Common.DbConnection instance
        //     supports the System.Data.Common.DbBatch class.
        //
        // Returns:
        //     true if this instance supports the System.Data.Common.DbBatch class; otherwise,
        //     false. The default is false.
        public override bool CanCreateBatch { get => throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     Gets the System.Data.Common.DbProviderFactory for this System.Data.Common.DbConnection.
        //
        //
        // Returns:
        //     A set of methods for creating instances of a provider's implementation of the
        //     data source classes.
        protected override DbProviderFactory? DbProviderFactory { get => throw new NotImplementedException(); }


        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, changes the current database for an open
        //     connection.
        //
        // Parameters:
        //   databaseName:
        //     The name of the database for the connection to use.
        public override void ChangeDatabase(string databaseName)
        {
            throw new NotImplementedException();
        }
/// <inheritdoc/>

        //
        // Summary:
        //     Asynchronously changes the current database for an open connection.
        //
        // Parameters:
        //   databaseName:
        //     The name of the database for the connection to use.
        //
        //   cancellationToken:
        //     An optional token to cancel the asynchronous operation. The default value is
        //     System.Threading.CancellationToken.None.
        //
        // Returns:
        //     A task representing the asynchronous operation.
        public override Task ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default) { throw new NotImplementedException(); }
/// <inheritdoc/>

        public object Clone()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, closes the connection to the database.
        public override void Close()
        {
            throw new NotImplementedException();
        }
/// <inheritdoc/>

        //
        // Summary:
        //     Asynchronously closes the connection to the database.
        //
        // Returns:
        //     A System.Threading.Tasks.Task representing the asynchronous operation.
        public override Task CloseAsync() { throw new NotImplementedException(); }


        /// <inheritdoc/>
        //
        // Summary:
        //     Asynchronously diposes the connection object.
        //
        // Returns:
        //     A System.Threading.Tasks.ValueTask representing the asynchronous operation.
        public override ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        //
        // Summary:
        //     Enlists in the specified transaction.
        //
        // Parameters:
        //   transaction:
        //     A reference to an existing System.Transactions.Transaction in which to enlist.
        public override void EnlistTransaction(Transaction? transaction) {  throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     Returns schema information for the data source of this System.Data.Common.DbConnection.
        //
        //
        // Returns:
        //     A System.Data.DataTable that contains schema information.
        public override DataTable GetSchema() {  throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     Returns schema information for the data source of this System.Data.Common.DbConnection
        //     using the specified string for the schema name and the specified string array
        //     for the restriction values.
        //
        // Parameters:
        //   collectionName:
        //     Specifies the name of the schema to return.
        //
        //   restrictionValues:
        //     Specifies a set of restriction values for the requested schema.
        //
        // Returns:
        //     A System.Data.DataTable that contains schema information.
        //
        // Exceptions:
        //   T:System.ArgumentException:
        //     collectionName is specified as null.
        public override DataTable GetSchema(string collectionName, string?[] restrictionValues) { throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     Returns schema information for the data source of this System.Data.Common.DbConnection
        //     using the specified string for the schema name.
        //
        // Parameters:
        //   collectionName:
        //     Specifies the name of the schema to return.
        //
        // Returns:
        //     A System.Data.DataTable that contains schema information.
        //
        // Exceptions:
        //   T:System.ArgumentException:
        //     collectionName is specified as null.
        public override DataTable GetSchema(string collectionName) { throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     This is an asynchronous version of System.Data.Common.DbConnection.GetSchema.
        //     Providers should override with an appropriate implementation. The cancellationToken
        //     can optionally be honored. The default implementation invokes the synchronous
        //     System.Data.Common.DbConnection.GetSchema call and returns a completed task.
        //     The default implementation will return a cancelled task if passed an already
        //     cancelled cancellationToken. Exceptions thrown by System.Data.Common.DbConnection.GetSchema
        //     will be communicated via the returned Task Exception property.
        //
        // Parameters:
        //   cancellationToken:
        //     The cancellation instruction.
        //
        // Returns:
        //     A task representing the asynchronous operation.
        public override Task<DataTable> GetSchemaAsync(CancellationToken cancellationToken = default) { throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     This is the asynchronous version of System.Data.Common.DbConnection.GetSchema(System.String).
        //     Providers should override with an appropriate implementation. The cancellationToken
        //     can optionally be honored. The default implementation invokes the synchronous
        //     System.Data.Common.DbConnection.GetSchema(System.String) call and returns a completed
        //     task. The default implementation will return a cancelled task if passed an already
        //     cancelled cancellationToken. Exceptions thrown by System.Data.Common.DbConnection.GetSchema(System.String)
        //     will be communicated via the returned Task Exception property.
        //
        // Parameters:
        //   collectionName:
        //     Specifies the name of the schema to return.
        //
        //   cancellationToken:
        //     The cancellation instruction.
        //
        // Returns:
        //     A task representing the asynchronous operation.
        public override Task<DataTable> GetSchemaAsync(string collectionName, CancellationToken cancellationToken = default) { throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     This is the asynchronous version of System.Data.Common.DbConnection.GetSchema(System.String,System.String[]).
        //     Providers should override with an appropriate implementation. The cancellationToken
        //     can optionally be honored. The default implementation invokes the synchronous
        //     System.Data.Common.DbConnection.GetSchema(System.String,System.String[]) call
        //     and returns a completed task. The default implementation will return a cancelled
        //     task if passed an already cancelled cancellationToken. Exceptions thrown by System.Data.Common.DbConnection.GetSchema(System.String,System.String[])
        //     will be communicated via the returned Task Exception property.
        //
        // Parameters:
        //   collectionName:
        //     Specifies the name of the schema to return.
        //
        //   restrictionValues:
        //     Specifies a set of restriction values for the requested schema.
        //
        //   cancellationToken:
        //     The cancellation instruction.
        //
        // Returns:
        //     A task representing the asynchronous operation.
        public override Task<DataTable> GetSchemaAsync(string collectionName, string?[] restrictionValues, CancellationToken cancellationToken = default) { throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, opens a database connection with the settings
        //     specified by the System.Data.Common.DbConnection.ConnectionString.
        public override void Open()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>

        //
        // Summary:
        //     This is the asynchronous version of System.Data.Common.DbConnection.Open. Providers
        //     should override with an appropriate implementation. The cancellation token can
        //     optionally be honored. The default implementation invokes the synchronous System.Data.Common.DbConnection.Open
        //     call and returns a completed task. The default implementation will return a cancelled
        //     task if passed an already cancelled cancellationToken. Exceptions thrown by Open
        //     will be communicated via the returned Task Exception property. Do not invoke
        //     other methods and properties of the DbConnection object until the returned Task
        //     is complete.
        //
        // Parameters:
        //   cancellationToken:
        //     The cancellation instruction.
        //
        // Returns:
        //     A task representing the asynchronous operation.
        public override Task OpenAsync(CancellationToken cancellationToken) { throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, starts a database transaction.
        //
        // Parameters:
        //   isolationLevel:
        //     One of the enumeration values that specifies the isolation level for the transaction
        //     to use.
        //
        // Returns:
        //     An object representing the new transaction.

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>

        //
        // Summary:
        //     Asynchronously starts a database transaction.
        //
        // Parameters:
        //   isolationLevel:
        //     One of the enumeration values that specifies the isolation level for the transaction
        //     to use.
        //
        //   cancellationToken:
        //     A token to cancel the asynchronous operation.
        //
        // Returns:
        //     A task whose System.Threading.Tasks.Task`1.Result property is an object representing
        //     the new transaction.
        protected override ValueTask<DbTransaction> BeginDbTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, returns a new instance of the provider's
        //     class that implements the System.Data.Common.DbBatch class.
        //
        // Returns:
        //     A new instance of System.Data.Common.DbBatch.
        protected override DbBatch CreateDbBatch() { throw new NotImplementedException(); }
        /// <inheritdoc/>
        //
        // Summary:
        //     When overridden in a derived class, creates and returns a System.Data.Common.DbCommand
        //     object associated with the current connection.
        //
        // Returns:
        //     A System.Data.Common.DbCommand object.

        protected override DbCommand CreateDbCommand()
        {
            throw new NotImplementedException();
        }
    }
}
