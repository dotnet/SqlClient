// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlBulkCopyCacheMetadataTest
    {
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
        public void CacheMetadata_DoesNotOverlapExistingFlags()
        {
            int cacheMetadataValue = (int)SqlBulkCopyOptions.CacheMetadata;
            Assert.NotEqual((int)SqlBulkCopyOptions.Default, cacheMetadataValue);
            Assert.NotEqual((int)SqlBulkCopyOptions.KeepIdentity, cacheMetadataValue);
            Assert.NotEqual((int)SqlBulkCopyOptions.CheckConstraints, cacheMetadataValue);
            Assert.NotEqual((int)SqlBulkCopyOptions.TableLock, cacheMetadataValue);
            Assert.NotEqual((int)SqlBulkCopyOptions.KeepNulls, cacheMetadataValue);
            Assert.NotEqual((int)SqlBulkCopyOptions.FireTriggers, cacheMetadataValue);
            Assert.NotEqual((int)SqlBulkCopyOptions.UseInternalTransaction, cacheMetadataValue);
            Assert.NotEqual((int)SqlBulkCopyOptions.AllowEncryptedValueModifications, cacheMetadataValue);
        }

        [Fact]
        public void InvalidateMetadataCache_CanBeCalledWithoutError()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection());
            bulkCopy.InvalidateMetadataCache();
        }

        [Fact]
        public void InvalidateMetadataCache_CanBeCalledMultipleTimes()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection());
            bulkCopy.InvalidateMetadataCache();
            bulkCopy.InvalidateMetadataCache();
            bulkCopy.InvalidateMetadataCache();
        }

        [Fact]
        public void InvalidateMetadataCache_WithCacheMetadataOption()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.CacheMetadata, null);
            bulkCopy.InvalidateMetadataCache();
        }

        [Fact]
        public void InvalidateMetadataCache_WithoutCacheMetadataOption()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.Default, null);
            bulkCopy.InvalidateMetadataCache();
        }

        [Fact]
        public void DestinationTableName_Change_DoesNotThrowWithCacheMetadata()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.CacheMetadata, null);
            bulkCopy.DestinationTableName = "Table1";
            bulkCopy.DestinationTableName = "Table2";
            bulkCopy.DestinationTableName = "Table1";
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
