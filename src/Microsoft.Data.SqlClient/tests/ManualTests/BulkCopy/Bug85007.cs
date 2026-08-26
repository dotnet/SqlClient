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
    public class Bug85007
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            {
                dstConn.Open();

                // Declared before the order table so it is dropped last: the order table holds a
                //   foreign key referencing it.
                using Table targetCustomerTable = new(dstConn, "SqlBulkCopyTest_Bug85007_customer",
                    "([CustomerID] [nchar] (5) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL, PRIMARY KEY CLUSTERED (CustomerID) ON [PRIMARY]) ON [PRIMARY]");

                using Table dstTable = new(dstConn, "SqlBulkCopyTest_Bug85007",
                "(" +
                "    [OrderID] [int] IDENTITY (1, 1) NOT NULL ," +
                "    [CustomerID] [nchar] (5) COLLATE SQL_Latin1_General_CP1_CI_AS NULL ," +
                "    [EmployeeID] [int] NULL ," +
                "    [OrderDate] [datetime] NULL ," +
                "    [RequiredDate] [datetime] NULL ," +
                "    [ShippedDate] [datetime] NULL ," +
                "    [ShipVia] [int] NULL ," +
                "    [Freight] [money] NULL DEFAULT (0)," +
                "    [ShipName] [nvarchar] (40) COLLATE SQL_Latin1_General_CP1_CI_AS NULL ," +
                "    [ShipAddress] [nvarchar] (60) COLLATE SQL_Latin1_General_CP1_CI_AS NULL ," +
                "    [ShipCity] [nvarchar] (15) COLLATE SQL_Latin1_General_CP1_CI_AS NULL ," +
                "    [ShipRegion] [nvarchar] (15) COLLATE SQL_Latin1_General_CP1_CI_AS NULL ," +
                "    [ShipPostalCode] [nvarchar] (10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL ," +
                "    [ShipCountry] [nvarchar] (15) COLLATE SQL_Latin1_General_CP1_CI_AS NULL ," +
                "    PRIMARY KEY  CLUSTERED " +
                "    (" +
                "        [OrderID]" +
                "    )  ON [PRIMARY] ," +
                "    FOREIGN KEY " +
                "    (" +
                "        [CustomerID]" +
                "    ) REFERENCES " + targetCustomerTable.Name + " (" +
                "        [CustomerID]" +
                "    )" +
                ") ON [PRIMARY]");

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand customerCmd = new SqlCommand("SELECT CustomerID from Customers", srcConn))
                {
                    srcConn.Open();

                    // First copy the customer ID list across
                    using (DbDataReader reader = customerCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = targetCustomerTable.Name;
                            bulkcopy.WriteToServer(reader);
                        }
                    }

                    SqlCommand srcCmd = new SqlCommand("select * from orders", srcConn);
                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {

                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable.Name;
                            bulkcopy.BatchSize = 6;

                            SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                            ColumnMappings.Add("OrderID", "OrderID");
                            ColumnMappings.Add("CustomerID", "CustomerID");
                            ColumnMappings.Add("EmployeeID", "EmployeeID");
                            ColumnMappings.Add("RequiredDate", "RequiredDate");
                            ColumnMappings.Add("ShippedDate", "ShippedDate");
                            ColumnMappings.Add("ShipVia", "ShipVia");
                            ColumnMappings.Add("Freight", "Freight");
                            ColumnMappings.Add("ShipName", "ShipName");
                            ColumnMappings.Add("ShipAddress", "ShipAddress");
                            ColumnMappings.Add("ShipCity", "ShipCity");
                            ColumnMappings.Add("ShipRegion", "ShipRegion");
                            ColumnMappings.Add("ShipPostalCode", "ShipPostalCode");
                            ColumnMappings.Add("ShipCountry", "ShipCountry");

                            bulkcopy.WriteToServer(reader);

                            DataTestUtility.AssertEqualsWithDescription(bulkcopy.RowsCopied, 830, "Unexpected number of rows.");
                        }
                        Helpers.VerifyResults(dstConn, dstTable.Name, 14, 830);
                    }
                }
            }
        }
    }
}
