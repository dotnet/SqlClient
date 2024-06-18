// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

#nullable enable

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// 
    /// </summary>
    [DefaultEvent("InfoMessage")]
    [DesignerCategory("")]
    internal sealed class SqlConnectionX : DbConnection, ICloneable
    {
        //TODO: reference to internal connection

        /// <summary>
        /// Initializes a new instance of the System.Data.Common.SqlConnectionX class.
        /// </summary>
        internal SqlConnectionX() : base()
        {
        }

        /// <inheritdoc/>
        [Browsable(false)]
        public override ConnectionState State
            => throw new NotImplementedException();

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
        public override int ConnectionTimeout
            => throw new NotImplementedException();

        /// <inheritdoc/>
        [AllowNull]
        [DefaultValue("")]
        [RefreshProperties(RefreshProperties.All)]
        [SettingsBindable(true)]
        public override string ConnectionString
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override bool CanCreateBatch
            => throw new NotImplementedException();

        /// <inheritdoc/>
        protected override DbProviderFactory? DbProviderFactory
            => throw new NotImplementedException();


        /// <inheritdoc/>
        public override void ChangeDatabase(string databaseName)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override Task ChangeDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public object Clone()
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override void Close()
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override Task CloseAsync()
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override ValueTask DisposeAsync()
            => throw new NotImplementedException();

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

        /// <inheritdoc/>
        public override void Open()
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public override Task OpenAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException();

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
