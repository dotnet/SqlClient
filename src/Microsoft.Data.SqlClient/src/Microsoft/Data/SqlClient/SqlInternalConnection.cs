// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Diagnostics;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

#if NETFRAMEWORK
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
#endif

namespace Microsoft.Data.SqlClient
{
    internal abstract class SqlInternalConnection : DbConnectionInternal
    {
        #region Properties

        /// <summary>
        /// A reference to the SqlConnection that owns this internal connection.
        /// </summary>
        internal SqlConnection Connection => (SqlConnection)Owner;

        /// <summary>
        /// The connection options to be used for this connection.
        /// </summary>
        internal SqlConnectionString ConnectionOptions { get; init; }

        /// <summary>
        /// The current database for this connection.
        /// Null if the connection is not open yet.
        /// </summary>
        internal string CurrentDatabase { get; set; }

        /// <summary>
        /// The current data source for this connection.
        /// 
        /// if connection is not open yet, CurrentDataSource is null
        /// if connection is open:
        /// * for regular connections, it is set to the Data Source value from connection string
        /// * for failover connections, it is set to the FailoverPartner value from the connection string
        /// </summary>
        internal string CurrentDataSource { get; set; }

        /// <summary>
        /// The delegated (or promoted) transaction this connection is responsible for.
        /// </summary>
        internal SqlDelegatedTransaction DelegatedTransaction { get; set; }

        /// <summary>
        /// Whether the server version is SQL Server 2008 or newer.
        /// </summary>
        abstract internal bool Is2008OrNewer { get; }

        /// <summary>
        /// Whether this connection is to an Azure SQL Database.
        /// </summary>
        internal bool IsAzureSqlConnection { get; set; }

        /// <summary>
        /// Indicates whether the connection is currently enlisted in a transaction.
        /// </summary>
        internal bool IsEnlistedInTransaction { get; set; }

        /// <summary>
        /// Whether this is a Global Transaction (Non-MSDTC, Azure SQL DB Transaction)
        /// TODO: overlaps with IsGlobalTransactionsEnabledForServer, need to consolidate to avoid bugs
        /// </summary>
        internal bool IsGlobalTransaction { get; set; }

        /// <summary>
        /// Whether Global Transactions are enabled. Only supported by Azure SQL.
        /// False if disabled or connected to on-prem SQL Server.
        /// </summary>
        internal bool IsGlobalTransactionsEnabledForServer { get; set; }

        /// <summary>
        /// Whether this connection is locked for bulk copy operations.
        /// </summary>
        abstract internal bool IsLockedForBulkCopy { get; }

        /// <summary>
        /// Whether this connection is the root of a delegated or promoted transaction.
        /// </summary>
        override internal bool IsTransactionRoot
        {
            get
            {
                SqlDelegatedTransaction delegatedTransaction = DelegatedTransaction;
                return delegatedTransaction != null && (delegatedTransaction.IsActive);
            }
        }

        /// <summary>
        /// A token returned by the server when we promote transaction.
        /// </summary>
        internal byte[] PromotedDtcToken { get; set; }

        #endregion
    }
}
