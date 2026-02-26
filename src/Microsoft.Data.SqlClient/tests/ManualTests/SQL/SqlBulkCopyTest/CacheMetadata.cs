// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class CacheMetadata
    {
        private static readonly string sourceTable = "employees";
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(20), col3 nvarchar(10))";
        private static readonly string sourceQueryTemplate = "select top 5 EmployeeID, LastName, FirstName from {0}";

        // Test that CacheMetadata option works for multiple WriteToServer calls to the same table.
        public static void Test(string srcConstr, string dstConstr, string dstTable)
        {
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string initialQuery = string.Format(initialQueryTemplate, dstTable);

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery);

                using SqlBulkCopy bulkcopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata, null);
                bulkcopy.DestinationTableName = dstTable;

                // First WriteToServer: metadata is queried and cached.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 5);

                // Second WriteToServer: should reuse cached metadata.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 10);

                // Third WriteToServer: should still reuse cached metadata.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 15);
            }
            finally
            {
                Helpers.TryExecute(dstCmd, "drop table " + dstTable);
            }
        }
    }

    public class CacheMetadataInvalidate
    {
        private static readonly string sourceTable = "employees";
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(20), col3 nvarchar(10))";
        private static readonly string sourceQueryTemplate = "select top 5 EmployeeID, LastName, FirstName from {0}";

        // Test that ClearCachedMetadata forces a fresh metadata query.
        public static void Test(string srcConstr, string dstConstr, string dstTable)
        {
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string initialQuery = string.Format(initialQueryTemplate, dstTable);

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery);

                using SqlBulkCopy bulkcopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata, null);
                bulkcopy.DestinationTableName = dstTable;

                // First WriteToServer: metadata is queried and cached.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 5);

                // Invalidate the cache and write again: should still succeed after re-querying metadata.
                bulkcopy.ClearCachedMetadata();

                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 10);
            }
            finally
            {
                Helpers.TryExecute(dstCmd, "drop table " + dstTable);
            }
        }
    }

    public class CacheMetadataDestinationChange
    {
        private static readonly string sourceTable = "employees";
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(20), col3 nvarchar(10))";
        private static readonly string sourceQueryTemplate = "select top 5 EmployeeID, LastName, FirstName from {0}";

        // Test that changing DestinationTableName invalidates the cache and works correctly with a new table.
        public static void Test(string srcConstr, string dstConstr, string dstTable1, string dstTable2)
        {
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string initialQuery1 = string.Format(initialQueryTemplate, dstTable1);
            string initialQuery2 = string.Format(initialQueryTemplate, dstTable2);

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery1);
                Helpers.TryExecute(dstCmd, initialQuery2);

                using SqlBulkCopy bulkcopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata, null);

                // Write to first table.
                bulkcopy.DestinationTableName = dstTable1;
                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable1, 3, 5);

                // Change destination table: cache should be invalidated automatically.
                bulkcopy.DestinationTableName = dstTable2;
                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable2, 3, 5);
            }
            finally
            {
                Helpers.TryDropTable(dstConstr, dstTable1);
                Helpers.TryDropTable(dstConstr, dstTable2);
            }
        }
    }

    public class CacheMetadataWithoutFlag
    {
        private static readonly string sourceTable = "employees";
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(20), col3 nvarchar(10))";
        private static readonly string sourceQueryTemplate = "select top 5 EmployeeID, LastName, FirstName from {0}";

        // Test that without the CacheMetadata flag, multiple writes still work (no regression).
        public static void Test(string srcConstr, string dstConstr, string dstTable)
        {
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string initialQuery = string.Format(initialQueryTemplate, dstTable);

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery);

                using SqlBulkCopy bulkcopy = new(dstConn);
                bulkcopy.DestinationTableName = dstTable;

                // First WriteToServer without CacheMetadata.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 5);

                // Second WriteToServer without CacheMetadata.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    srcConn.Open();
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = srcCmd.ExecuteReader();
                    bulkcopy.WriteToServer(reader);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 10);
            }
            finally
            {
                Helpers.TryExecute(dstCmd, "drop table " + dstTable);
            }
        }
    }

    public class CacheMetadataWithDataTable
    {
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(50), col3 nvarchar(50))";

        // Test that CacheMetadata works with DataTable source as well as IDataReader.
        public static void Test(string dstConstr, string dstTable)
        {
            string initialQuery = string.Format(initialQueryTemplate, dstTable);

            using DataTable sourceData = new();
            sourceData.Columns.Add("col1", typeof(int));
            sourceData.Columns.Add("col2", typeof(string));
            sourceData.Columns.Add("col3", typeof(string));
            sourceData.Rows.Add(1, "Alice", "Smith");
            sourceData.Rows.Add(2, "Bob", "Jones");
            sourceData.Rows.Add(3, "Charlie", "Brown");

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery);

                using SqlBulkCopy bulkcopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata, null);
                bulkcopy.DestinationTableName = dstTable;

                // First WriteToServer with DataTable: metadata is queried and cached.
                bulkcopy.WriteToServer(sourceData);
                Helpers.VerifyResults(dstConn, dstTable, 3, 3);

                // Second WriteToServer with DataTable: should reuse cached metadata.
                bulkcopy.WriteToServer(sourceData);
                Helpers.VerifyResults(dstConn, dstTable, 3, 6);
            }
            finally
            {
                Helpers.TryExecute(dstCmd, "drop table " + dstTable);
            }
        }
    }

    public class CacheMetadataColumnMappingsChange
    {
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(50), col3 nvarchar(50))";

        // Test that changing ColumnMappings between WriteToServer calls works correctly with CacheMetadata.
        // The cached metadata describes the destination table schema, not the column mappings,
        // so modifying mappings between calls should work without cache invalidation.
        public static void Test(string dstConstr, string dstTable)
        {
            string initialQuery = string.Format(initialQueryTemplate, dstTable);

            using DataTable sourceData = new DataTable();
            sourceData.Columns.Add("id", typeof(int));
            sourceData.Columns.Add("firstName", typeof(string));
            sourceData.Columns.Add("lastName", typeof(string));
            sourceData.Rows.Add(1, "Alice", "Smith");
            sourceData.Rows.Add(2, "Bob", "Jones");

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery);

                using SqlBulkCopy bulkcopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata, null);
                bulkcopy.DestinationTableName = dstTable;

                // First write: map firstName -> col2, lastName -> col3.
                bulkcopy.ColumnMappings.Add("id", "col1");
                bulkcopy.ColumnMappings.Add("firstName", "col2");
                bulkcopy.ColumnMappings.Add("lastName", "col3");
                bulkcopy.WriteToServer(sourceData);
                Helpers.VerifyResults(dstConn, dstTable, 3, 2);

                // Verify first mapping: col2 should contain firstName values.
                using (SqlCommand verifyCmd = new("select col2 from " + dstTable + " where col1 = 1", dstConn))
                {
                    object result = verifyCmd.ExecuteScalar();
                    Assert.Equal("Alice", result);
                }

                // Change mappings: swap col2 and col3 targets.
                bulkcopy.ColumnMappings.Clear();
                bulkcopy.ColumnMappings.Add("id", "col1");
                bulkcopy.ColumnMappings.Add("firstName", "col3");
                bulkcopy.ColumnMappings.Add("lastName", "col2");
                bulkcopy.WriteToServer(sourceData);
                Helpers.VerifyResults(dstConn, dstTable, 3, 4);

                // Verify second mapping: col3 should now contain firstName values for the new rows.
                using (SqlCommand verifyCmd = new("select col3 from " + dstTable + " where col1 = 1 order by col2", dstConn))
                {
                    using SqlDataReader reader = verifyCmd.ExecuteReader();

                    // First row (from first write): col3 = "Smith" (lastName).
                    Assert.True(reader.Read());
                    Assert.Equal("Smith", reader.GetString(0));

                    // Second row (from second write): col3 = "Alice" (firstName).
                    Assert.True(reader.Read());
                    Assert.Equal("Alice", reader.GetString(0));
                }
            }
            finally
            {
                Helpers.TryExecute(dstCmd, "drop table " + dstTable);
            }
        }
    }

    public class CacheMetadataColumnSubsetChange
    {
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(50), col3 nvarchar(50))";

        // Test that mapping a subset of columns on the first call, then all columns on the
        // second call, works correctly with CacheMetadata. This verifies that null-pruning of
        // unmatched columns in AnalyzeTargetAndCreateUpdateBulkCommand does not mutate the
        // cached metadata, which would cause a NullReferenceException on the second call.
        public static void Test(string dstConstr, string dstTable)
        {
            string initialQuery = string.Format(initialQueryTemplate, dstTable);

            using DataTable sourceData = new DataTable();
            sourceData.Columns.Add("id", typeof(int));
            sourceData.Columns.Add("firstName", typeof(string));
            sourceData.Columns.Add("lastName", typeof(string));
            sourceData.Rows.Add(1, "Alice", "Smith");
            sourceData.Rows.Add(2, "Bob", "Jones");

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery);

                using SqlBulkCopy bulkcopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata, null);
                bulkcopy.DestinationTableName = dstTable;

                // First write: map only col1 and col2 (col3 is unmatched and will be pruned).
                bulkcopy.ColumnMappings.Add("id", "col1");
                bulkcopy.ColumnMappings.Add("firstName", "col2");
                bulkcopy.WriteToServer(sourceData);
                Helpers.VerifyResults(dstConn, dstTable, 3, 2);

                // Second write: map all three columns including col3.
                // Without the clone fix, this would fail because col3 metadata was
                // permanently nulled in the cache during the first call.
                bulkcopy.ColumnMappings.Clear();
                bulkcopy.ColumnMappings.Add("id", "col1");
                bulkcopy.ColumnMappings.Add("firstName", "col2");
                bulkcopy.ColumnMappings.Add("lastName", "col3");
                bulkcopy.WriteToServer(sourceData);
                Helpers.VerifyResults(dstConn, dstTable, 3, 4);

                // Verify col3 has the expected data from the second write.
                using (SqlCommand verifyCmd = new("select col3 from " + dstTable + " where col1 = 1 and col3 is not null", dstConn))
                {
                    object result = verifyCmd.ExecuteScalar();
                    Assert.Equal("Smith", result);
                }
            }
            finally
            {
                Helpers.TryExecute(dstCmd, "drop table " + dstTable);
            }
        }
    }

    public class CacheMetadataAsync
    {
        private static readonly string sourceTable = "employees";
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(20), col3 nvarchar(10))";
        private static readonly string sourceQueryTemplate = "select top 5 EmployeeID, LastName, FirstName from {0}";

        // Test that CacheMetadata works correctly with WriteToServerAsync.
        public static void Test(string srcConstr, string dstConstr, string dstTable)
        {
            Task t = TestAsync(srcConstr, dstConstr, dstTable);
            t.Wait();
            Assert.True(t.IsCompleted, "Task did not complete! Status: " + t.Status);
        }

        private static async Task TestAsync(string srcConstr, string dstConstr, string dstTable)
        {
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string initialQuery = string.Format(initialQueryTemplate, dstTable);

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery);

                using SqlBulkCopy bulkcopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata, null);
                bulkcopy.DestinationTableName = dstTable;

                // First WriteToServerAsync: metadata is queried and cached.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    await srcConn.OpenAsync().ConfigureAwait(false);
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = await srcCmd.ExecuteReaderAsync().ConfigureAwait(false);
                    await bulkcopy.WriteToServerAsync(reader).ConfigureAwait(false);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 5);

                // Second WriteToServerAsync: should reuse cached metadata.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    await srcConn.OpenAsync().ConfigureAwait(false);
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = await srcCmd.ExecuteReaderAsync().ConfigureAwait(false);
                    await bulkcopy.WriteToServerAsync(reader).ConfigureAwait(false);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 10);

                // Third WriteToServerAsync: should still reuse cached metadata.
                using (SqlConnection srcConn = new(srcConstr))
                {
                    await srcConn.OpenAsync().ConfigureAwait(false);
                    using SqlCommand srcCmd = new(sourceQuery, srcConn);
                    using IDataReader reader = await srcCmd.ExecuteReaderAsync().ConfigureAwait(false);
                    await bulkcopy.WriteToServerAsync(reader).ConfigureAwait(false);
                }
                Helpers.VerifyResults(dstConn, dstTable, 3, 15);
            }
            finally
            {
                Helpers.TryExecute(dstCmd, "drop table " + dstTable);
            }
        }
    }

    public class CacheMetadataCombinedWithKeepNulls
    {
        private static readonly string initialQueryTemplate = "create table {0} (col1 int, col2 nvarchar(50) default 'DefaultVal', col3 nvarchar(50))";

        // Test that CacheMetadata works correctly when combined with other SqlBulkCopyOptions.
        public static void Test(string dstConstr, string dstTable)
        {
            string initialQuery = string.Format(initialQueryTemplate, dstTable);

            using DataTable sourceData = new();
            sourceData.Columns.Add("col1", typeof(int));
            sourceData.Columns.Add("col2", typeof(string));
            sourceData.Columns.Add("col3", typeof(string));
            sourceData.Rows.Add(1, DBNull.Value, "Smith");
            sourceData.Rows.Add(2, "Bob", DBNull.Value);

            using SqlConnection dstConn = new(dstConstr);
            using SqlCommand dstCmd = dstConn.CreateCommand();
            dstConn.Open();

            try
            {
                Helpers.TryExecute(dstCmd, initialQuery);

                using SqlBulkCopy bulkcopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata | SqlBulkCopyOptions.KeepNulls, null);
                bulkcopy.DestinationTableName = dstTable;
                bulkcopy.ColumnMappings.Add("col1", "col1");
                bulkcopy.ColumnMappings.Add("col2", "col2");
                bulkcopy.ColumnMappings.Add("col3", "col3");

                // First write with CacheMetadata | KeepNulls.
                bulkcopy.WriteToServer(sourceData);
                Helpers.VerifyResults(dstConn, dstTable, 3, 2);

                // Verify nulls were kept (not replaced by default values).
                using SqlCommand verifyCmd = new("select col2 from " + dstTable + " where col1 = 1", dstConn);
                object result = verifyCmd.ExecuteScalar();
                Assert.Equal(System.DBNull.Value, result);

                // Second write should reuse cached metadata.
                bulkcopy.WriteToServer(sourceData);
                Helpers.VerifyResults(dstConn, dstTable, 3, 4);
            }
            finally
            {
                Helpers.TryExecute(dstCmd, "drop table " + dstTable);
            }
        }
    }
}
