// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Tds.State
{
    /// <summary>
    /// Captures TDS Transaction state information
    /// </summary>
    internal class TdsTransactionState
    {
        private SqlInternalTransaction _currentTransaction;

        private SqlInternalTransaction _pendingTransaction;    // pending transaction for 2005 and beyond.

        // This counter is used for the entire connection to track the open result count for all
        // operations not under a transaction.
        private int _nonTransactedOpenResultCount = 0;

        //  need to hold on to the transaction id if distributed transaction merely rolls back without defecting.
        internal long _retainedTransactionId = SqlInternalTransaction.NullTransactionId;

        internal SqlInternalTransaction CurrentTransaction => _currentTransaction;

        internal SqlInternalTransaction PendingTransaction => _pendingTransaction;

        internal int IncrementNonTransactedOpenResultCount()
        {
            // IMPORTANT - this increments the connection wide open result count for all
            // operations not under a transaction!  Do not call if you intend to modify the
            // count for a transaction!
            Debug.Assert(_nonTransactedOpenResultCount >= 0, "Unexpected result count state");
            int result = Interlocked.Increment(ref _nonTransactedOpenResultCount);
            return result;
        }

        internal void DecrementNonTransactedOpenResultCount()
        {
            // IMPORTANT - this decrements the connection wide open result count for all
            // operations not under a transaction!  Do not call if you intend to modify the
            // count for a transaction!
            Interlocked.Decrement(ref _nonTransactedOpenResultCount);
            Debug.Assert(_nonTransactedOpenResultCount >= 0, "Unexpected result count state");
        }

        internal void DisconnectTransaction(SqlInternalTransaction internalTransaction)
        {
            Debug.Assert(_currentTransaction != null && _currentTransaction == internalTransaction, "disconnecting different transaction");

            if (_currentTransaction != null && _currentTransaction == internalTransaction)
            {
                _currentTransaction = null;
            }
        }

        internal void RollbackOrphanedAPITransactions()
        {
            // Any active, non-distributed transaction must be rolled back.
            if (null != _currentTransaction && _currentTransaction.HasParentTransaction && _currentTransaction.IsOrphaned)
            {
                _currentTransaction.CloseFromConnection();
                Debug.Assert(null == _currentTransaction, "rollback didn't clear current transaction?");
            }
        }
    }
}
