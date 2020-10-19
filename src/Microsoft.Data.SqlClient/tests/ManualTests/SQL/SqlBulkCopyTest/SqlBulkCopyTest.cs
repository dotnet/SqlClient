// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlBulkCopyTest
    {
        private string _connStr = null;
        private static bool IsAzureServer() => !DataTestUtility.IsNotAzureServer();
        private static bool IsNotAzureSynapse => DataTestUtility.IsNotAzureSynapse();
        private static bool AreConnectionStringsSetup() => DataTestUtility.AreConnStringsSetup();

        public SqlBulkCopyTest()
        {
            _connStr = DataTestUtility.TCPConnectionString;
        }

        public string AddGuid(string stringin)
        {
            stringin += "_" + Guid.NewGuid().ToString().Replace('-', '_');
            return stringin;
        }

        // Synapse: Promote Transaction not supported by Azure Synapse
        [ConditionalFact(nameof(AreConnectionStringsSetup), nameof(IsNotAzureSynapse), nameof(IsAzureServer))]
        public void AzureDistributedTransactionTest()
        {
            AzureDistributedTransaction.Test();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyAllFromReaderTest()
        {
            CopyAllFromReader.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_CopyAllFromReader"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyAllFromReader1Test()
        {
            CopyAllFromReader1.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_CopyAllFromReader1"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyMultipleReadersTest()
        {
            CopyMultipleReaders.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_CopyMultipleReaders"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopySomeFromReaderTest()
        {
            CopySomeFromReader.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_CopySomeFromReader"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopySomeFromDataTableTest()
        {
            CopySomeFromDataTable.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_CopySomeFromDataTable"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopySomeFromRowArrayTest()
        {
            CopySomeFromRowArray.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_CopySomeFromRowArray"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyWithEventTest()
        {
            CopyWithEvent.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_CopyWithEvent"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyWithEvent1Test()
        {
            CopyWithEvent1.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_CopyWithEvent1"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void InvalidAccessFromEventTest()
        {
            InvalidAccessFromEvent.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_InvalidAccessFromEvent"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Bug84548Test()
        {
            Bug84548.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_Bug84548"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void MissingTargetTableTest()
        {
            MissingTargetTable.Test(_connStr, _connStr, AddGuid("@SqlBulkCopyTest_MissingTargetTable"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void MissingTargetColumnTest()
        {
            MissingTargetColumn.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_MissingTargetColumn"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Bug85007Test()
        {
            Bug85007.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_Bug85007"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CheckConstraintsTest()
        {
            CheckConstraints.Test(_connStr, AddGuid("SqlBulkCopyTest_Extensionsrc"), AddGuid("SqlBulkCopyTest_Extensiondst"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void TransactionTest()
        {
            Transaction.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_Transaction0"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Transaction1Test()
        {
            Transaction1.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_Transaction1"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Transaction2Test()
        {
            Transaction2.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_Transaction2"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Transaction3Test()
        {
            Transaction3.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_Transaction3"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Transaction4Test()
        {
            Transaction4.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_Transaction4"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyVariantsTest()
        {
            CopyVariants.Test(_connStr, AddGuid("SqlBulkCopyTest_Variants"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Bug98182Test()
        {
            Bug98182.Test(_connStr, AddGuid("@SqlBulkCopyTest_Bug98182 "));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void FireTriggerTest()
        {
            FireTrigger.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_FireTrigger"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void ErrorOnRowsMarkedAsDeletedTest()
        {
            ErrorOnRowsMarkedAsDeleted.Test(_connStr, AddGuid("SqlBulkCopyTest_ErrorOnRowsMarkedAsDeleted"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void SpecialCharacterNamesTest()
        {
            SpecialCharacterNames.Test(_connStr, _connStr, AddGuid("@SqlBulkCopyTest_SpecialCharacterNames"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Bug903514Test()
        {
            Bug903514.Test(_connStr, AddGuid("SqlBulkCopyTest_Bug903514"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void ColumnCollationTest()
        {
            ColumnCollation.Test(_connStr, AddGuid("SqlBulkCopyTest_ColumnCollation"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyAllFromReaderAsyncTest()
        {
            CopyAllFromReaderAsync.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_AsyncTest1")); //Async + Reader
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopySomeFromRowArrayAsyncTest()
        {
            CopySomeFromRowArrayAsync.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_AsyncTest2")); //Async + Some Rows
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopySomeFromDataTableAsyncTest()
        {
            CopySomeFromDataTableAsync.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_AsyncTest3")); //Async + Some Table
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyWithEventAsyncTest()
        {
            CopyWithEventAsync.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_AsyncTest4")); //Async + Rows + Notification
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void CopyAllFromReaderCancelAsyncTest()
        {
            CopyAllFromReaderCancelAsync.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_AsyncTest5")); //Async + Reader + cancellation token
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void CopyAllFromReaderConnectionClosedAsyncTest()
        {
            CopyAllFromReaderConnectionClosedAsync.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_AsyncTest6")); //Async + Reader + Connection closed
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void CopyAllFromReaderConnectionClosedOnEventAsyncTest()
        {
            CopyAllFromReaderConnectionClosedOnEventAsync.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_AsyncTest7")); //Async + Reader + Connection closed during the event
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TransactionTestAsyncTest()
        {
            TransactionTestAsync.Test(_connStr, _connStr, AddGuid("SqlBulkCopyTest_TransactionTestAsync")); //Async + Transaction rollback
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void CopyWidenNullInexactNumericsTest()
        {
            CopyWidenNullInexactNumerics.Test(_connStr, _connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void DestinationTableNameWithSpecialCharTest()
        {
            DestinationTableNameWithSpecialChar.Test(_connStr, AddGuid("SqlBulkCopyTest_DestinationTableNameWithSpecialChar"));
        }

        // TODO Synapse: Remove dependency on Northwind database
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void OrderHintTest()
        {
            OrderHint.Test(_connStr, AddGuid("SqlBulkCopyTest_OrderHint"), AddGuid("SqlBulkCopyTest_OrderHint2"));
        }

        // Synapse: Cannot create more than one clustered index on table '<table_name>'.
        // Drop the existing clustered index 'ClusteredIndex_fe3d8c967ac142468ec4f81ff1faaa50' before creating another.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void OrderHintAsyncTest()
        {
            OrderHintAsync.Test(_connStr, AddGuid("SqlBulkCopyTest_OrderHintAsync"), AddGuid("SqlBulkCopyTest_OrderHintAsync2"));
        }

        // Synapse: Remove dependency on Northwind database.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void OrderHintMissingTargetColumnTest()
        {
            OrderHintMissingTargetColumn.Test(_connStr, AddGuid("SqlBulkCopyTest_OrderHintMissingTargetColumn"));
        }

        // Synapse: Remove dependency on Northwind database.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void OrderHintDuplicateColumnTest()
        {
            OrderHintDuplicateColumn.Test(_connStr, AddGuid("SqlBulkCopyTest_OrderHintDuplicateColumn"));
        }

        // Synapse: 111212;Operation cannot be performed within a transaction.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void OrderHintTransactionTest()
        {
            OrderHintTransaction.Test(_connStr, AddGuid("SqlBulkCopyTest_OrderHintTransaction"));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [ActiveIssue(12219)]
        public void OrderHintIdentityColumnTest()
        {
            OrderHintIdentityColumn.Test(_connStr, AddGuid("SqlBulkCopyTest_OrderHintIdentityColumn"));
        }
    }
}
