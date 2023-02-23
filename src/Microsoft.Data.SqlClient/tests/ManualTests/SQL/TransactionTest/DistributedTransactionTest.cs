// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

#if NET7_0_OR_GREATER

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [PlatformSpecific(TestPlatforms.Windows)]
    public class DistributedTransactionTest
    {
        private static bool s_DelegatedTransactionCondition => DataTestUtility.AreConnStringsSetup() && DataTestUtility.IsNotAzureServer() && PlatformDetection.IsNotX86Process;

        [ConditionalFact(nameof(s_DelegatedTransactionCondition), Timeout = 10000)]
        public async Task Delegated_transaction_deadlock_in_SinglePhaseCommit()
        {
            TransactionManager.ImplicitDistributedTransactions = true;
            using var transaction = new CommittableTransaction();

            // Uncommenting the following makes the deadlock go away as a workaround. If the transaction is promoted before
            // the first SqlClient enlistment, it never goes into the delegated state.
            // _ = TransactionInterop.GetTransmitterPropagationToken(transaction);
            await using var conn = new SqlConnection(DataTestUtility.TCPConnectionString);
            await conn.OpenAsync();
            conn.EnlistTransaction(transaction);

            // Enlisting the transaction in second connection causes the transaction to be promoted.
            // After this, the transaction state will be "delegated" (delegated to SQL Server), and the commit below will
            // trigger a call to SqlDelegatedTransaction.SinglePhaseCommit.
            await using var conn2 = new SqlConnection(DataTestUtility.TCPConnectionString);
            await conn2.OpenAsync();
            conn2.EnlistTransaction(transaction);

            // Possible deadlock
            transaction.Commit();
        }
    }
}

#endif
