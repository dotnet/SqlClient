using System.Data.Common;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    class OrderHintTransaction
    {
        private static readonly string destinationTable = null;
        private static readonly string sourceTable = "Customers";
        private static readonly string initialQueryTemplate = "create table {0} (CustomerID nvarchar(50), CompanyName nvarchar(50), ContactName nvarchar(50))";
        private static readonly string sourceQueryTemplate = "SELECT CustomerID, CompanyName, ContactName FROM {0}";

        public static void Test(string srcConstr, string dstConstr, string dstTable)
        {
            dstTable = destinationTable != null ? destinationTable : dstTable;
            string initialQuery = string.Format(initialQueryTemplate, dstTable);
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                Helpers.TryExecute(dstCmd, initialQuery);
                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand(sourceQuery, srcConn))
                {
                    srcConn.Open();
                    SqlTransaction txn = dstConn.BeginTransaction();
                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(
                            dstConn, SqlBulkCopyOptions.CheckConstraints, txn))
                        {
                            try
                            {
                                bulkcopy.DestinationTableName = dstTable;
                                bulkcopy.ColumnMappings.Add(0, 2);
                                bulkcopy.ColumnMappings.Add(2, 0);
                                bulkcopy.ColumnOrderHints.Add("CustomerID", SortOrder.Ascending);
                                bulkcopy.ColumnOrderHints.Add("ContactName", SortOrder.Descending);
                                bulkcopy.WriteToServer(reader);
                            }
                            finally
                            {
                                txn.Rollback();
                            }
                        }
                    }
                }
            }
        }
    }
}
