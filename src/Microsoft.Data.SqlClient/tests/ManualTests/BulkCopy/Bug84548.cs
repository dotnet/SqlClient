// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class Bug84548
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;

            using SqlConnection dstConn = new SqlConnection(dstConstr);
            dstConn.Open();

            // The order table takes a foreign key on the customer table, so it must be dropped first.
            // Disposal runs in reverse declaration order, which gives that for free.
            using Table customerTable = new Table(dstConn, "SqlBulkCopyTest_Bug84548_customer",
                "([CustomerID] [nchar] (5) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,"
                    + " PRIMARY KEY CLUSTERED (CustomerID) ON [PRIMARY]) ON [PRIMARY]");

            using Table orderTable = new Table(dstConn, "SqlBulkCopyTest_Bug84548",
                "([OrderID] [int] NOT NULL,"
                    + " [CustomerID] [nchar] (5) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,"
                    + " PRIMARY KEY CLUSTERED ([OrderID]) ON [PRIMARY],"
                    + $" FOREIGN KEY ([CustomerID]) REFERENCES {customerTable.Name} ([CustomerID])"
                    + ") ON [PRIMARY]");

            using (SqlConnection srcConn = new SqlConnection(srcConstr))
            {
                srcConn.Open();

                // First copy the customer ID list across
                using SqlCommand customerCommand = new SqlCommand("SELECT CustomerID from Customers", srcConn);
                using (DbDataReader reader = customerCommand.ExecuteReader())
                {
                    using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                    {
                        bulkcopy.DestinationTableName = customerTable.Name;
                        bulkcopy.WriteToServer(reader);
                    }
                }

                using SqlCommand srcCmd = new SqlCommand("select OrderID, CustomerID from Orders where OrderId = 10643", srcConn);
                using (DbDataReader reader = srcCmd.ExecuteReader())
                {
                    using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                    {
                        bulkcopy.DestinationTableName = orderTable.Name;
                        SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                        ColumnMappings.Add("OrderID", "OrderID");
                        ColumnMappings.Add("CustomerID", "CustomerID");

                        bulkcopy.WriteToServer(reader);

                        DataTestUtility.AssertEqualsWithDescription(bulkcopy.RowsCopied, 1, "Unexpected number of rows.");
                    }
                }
            }

            Helpers.VerifyResults(dstConn, orderTable.Name, 2, 1);
        }
    }
}
