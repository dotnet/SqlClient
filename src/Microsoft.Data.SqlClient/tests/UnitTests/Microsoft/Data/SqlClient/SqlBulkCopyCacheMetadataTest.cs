// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Tests that verify _SqlMetaDataSet.Clone() produces independent copies,
    /// ensuring that null-pruning of unmatched columns in AnalyzeTargetAndCreateUpdateBulkCommand
    /// does not corrupt the cached metadata when CacheMetadata is enabled.
    /// </summary>
    public class SqlBulkCopyCacheMetadataTest
    {
        [Fact]
        public void SqlMetaDataSet_Clone_ProducesIndependentCopy()
        {
            // Arrange: create a metadata set with 3 columns simulating a destination table
            _SqlMetaDataSet original = new _SqlMetaDataSet(3);
            original[0].column = "col1";
            original[1].column = "col2";
            original[2].column = "col3";

            // Act: clone and then null out an entry in the clone (simulating column pruning)
            _SqlMetaDataSet clone = original.Clone();
            clone[2] = null;

            // Assert: the original is not affected by the mutation of the clone
            Assert.NotNull(original[0]);
            Assert.NotNull(original[1]);
            Assert.NotNull(original[2]);
            Assert.Equal("col1", original[0].column);
            Assert.Equal("col2", original[1].column);
            Assert.Equal("col3", original[2].column);
        }

        [Fact]
        public void SqlMetaDataSet_Clone_NullingMultipleEntries_OriginalRetainsAll()
        {
            // Arrange: simulate a table with 4 columns
            _SqlMetaDataSet original = new _SqlMetaDataSet(4);
            original[0].column = "id";
            original[1].column = "name";
            original[2].column = "email";
            original[3].column = "phone";

            // Act: clone and null out entries 1 and 3 (simulating mapping only id and email)
            _SqlMetaDataSet clone = original.Clone();
            clone[1] = null;
            clone[3] = null;

            // Assert: clone has nulls where expected
            Assert.NotNull(clone[0]);
            Assert.Null(clone[1]);
            Assert.NotNull(clone[2]);
            Assert.Null(clone[3]);

            // Assert: original retains all entries
            for (int i = 0; i < 4; i++)
            {
                Assert.NotNull(original[i]);
            }
            Assert.Equal("name", original[1].column);
            Assert.Equal("phone", original[3].column);
        }

        [Fact]
        public void SqlMetaDataSet_Clone_RepeatedCloneAndPrune_OriginalSurvives()
        {
            // Arrange: simulate the scenario where multiple WriteToServer calls each
            // clone and prune different subsets of columns
            _SqlMetaDataSet original = new _SqlMetaDataSet(3);
            original[0].column = "col1";
            original[1].column = "col2";
            original[2].column = "col3";

            // First operation: map only col1 and col2 (prune col3)
            _SqlMetaDataSet clone1 = original.Clone();
            clone1[2] = null;

            // Second operation: map only col1 and col3 (prune col2)
            _SqlMetaDataSet clone2 = original.Clone();
            clone2[1] = null;

            // Third operation: map all columns (no pruning needed)
            _SqlMetaDataSet clone3 = original.Clone();

            // Assert: original is fully intact after all operations
            Assert.NotNull(original[0]);
            Assert.NotNull(original[1]);
            Assert.NotNull(original[2]);
            Assert.Equal("col1", original[0].column);
            Assert.Equal("col2", original[1].column);
            Assert.Equal("col3", original[2].column);

            // Assert: each clone reflects its own pruning
            Assert.Null(clone1[2]);
            Assert.NotNull(clone1[1]);

            Assert.Null(clone2[1]);
            Assert.NotNull(clone2[2]);

            Assert.NotNull(clone3[0]);
            Assert.NotNull(clone3[1]);
            Assert.NotNull(clone3[2]);
        }

        [Fact]
        public void SqlMetaDataSet_Clone_PreservesOrdinals()
        {
            // Verify that cloned entries maintain correct ordinal values,
            // which are used for column matching in AnalyzeTargetAndCreateUpdateBulkCommand
            _SqlMetaDataSet original = new _SqlMetaDataSet(3);
            original[0].column = "col1";
            original[1].column = "col2";
            original[2].column = "col3";

            _SqlMetaDataSet clone = original.Clone();

            Assert.Equal(original[0].ordinal, clone[0].ordinal);
            Assert.Equal(original[1].ordinal, clone[1].ordinal);
            Assert.Equal(original[2].ordinal, clone[2].ordinal);
        }
    }
}
