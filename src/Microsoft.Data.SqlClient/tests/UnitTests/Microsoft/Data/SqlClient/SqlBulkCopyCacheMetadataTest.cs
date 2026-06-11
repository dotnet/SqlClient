// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class SqlBulkCopyCacheMetadataTest
    {
        private static void SetCachedMetadata(SqlBulkCopy bulkCopy, BulkCopySimpleResultSet value)
        {
            typeof(SqlBulkCopy)
                .GetProperty("CachedMetadata", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
                .SetValue(bulkCopy, value);
        }

        [Fact]
        public void CacheMetadata_FlagValue_IsCorrect()
        {
            Assert.Equal(1 << 7, (int)SqlBulkCopyOptions.CacheMetadata);
        }

        [Fact]
        public void CacheMetadata_CanBeCombinedWithOtherOptions()
        {
            SqlBulkCopyOptions combined =
                SqlBulkCopyOptions.CacheMetadata |
                SqlBulkCopyOptions.KeepIdentity |
                SqlBulkCopyOptions.TableLock;

            Assert.True((combined & SqlBulkCopyOptions.CacheMetadata) == SqlBulkCopyOptions.CacheMetadata);
            Assert.True((combined & SqlBulkCopyOptions.KeepIdentity) == SqlBulkCopyOptions.KeepIdentity);
            Assert.True((combined & SqlBulkCopyOptions.TableLock) == SqlBulkCopyOptions.TableLock);
        }

        [Fact]
        public void SqlBulkCopyOptions_AllValues_AreUnique()
        {
            int[] values = Enum.GetValues(typeof(SqlBulkCopyOptions))
                .Cast<int>()
                .ToArray();

            Assert.Equal(values.Length, values.Distinct().Count());
        }

        [Fact]
        public void ClearCachedMetadata_ClearsCachedMetadata()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.CacheMetadata, null);

            SetCachedMetadata(bulkCopy, new BulkCopySimpleResultSet());

            bulkCopy.ClearCachedMetadata();

            Assert.Null(bulkCopy.CachedMetadata);
        }

        [Fact]
        public void ClearCachedMetadata_CanBeCalledMultipleTimes()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.CacheMetadata, null);

            SetCachedMetadata(bulkCopy, new BulkCopySimpleResultSet());

            bulkCopy.ClearCachedMetadata();
            bulkCopy.ClearCachedMetadata();
            bulkCopy.ClearCachedMetadata();

            Assert.Null(bulkCopy.CachedMetadata);
        }

        [Fact]
        public void ClearCachedMetadata_WhenNoCachedData_DoesNotThrow()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.CacheMetadata, null);

            Assert.Null(bulkCopy.CachedMetadata);

            bulkCopy.ClearCachedMetadata();

            Assert.Null(bulkCopy.CachedMetadata);
        }

        [Fact]
        public void ClearCachedMetadata_WithoutCacheMetadataOption_ClearsCachedMetadata()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.Default, null);

            SetCachedMetadata(bulkCopy, new BulkCopySimpleResultSet());

            bulkCopy.ClearCachedMetadata();

            Assert.Null(bulkCopy.CachedMetadata);
        }

        [Fact]
        public void DestinationTableName_Change_ClearsCachedMetadata()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.CacheMetadata, null);

            // Set backing field first so the setter sees a matching name
            bulkCopy.DestinationTableName = "Table1";

            // Simulate cached state after a WriteToServer call
            SetCachedMetadata(bulkCopy, new BulkCopySimpleResultSet());

            // Setting the same name should NOT clear the cache
            bulkCopy.DestinationTableName = "Table1";
            Assert.NotNull(bulkCopy.CachedMetadata);

            // Changing to a different table should clear the cache
            bulkCopy.DestinationTableName = "Table2";
            Assert.Null(bulkCopy.CachedMetadata);
        }

        [Fact]
        public void Constructor_WithCacheMetadataOption_Succeeds()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.CacheMetadata, null);
            Assert.NotNull(bulkCopy);
        }

        [Fact]
        public void Constructor_WithCacheMetadataAndConnectionString_Succeeds()
        {
            using SqlBulkCopy bulkCopy = new("Server=localhost", SqlBulkCopyOptions.CacheMetadata);
            Assert.NotNull(bulkCopy);
        }
    }
}
