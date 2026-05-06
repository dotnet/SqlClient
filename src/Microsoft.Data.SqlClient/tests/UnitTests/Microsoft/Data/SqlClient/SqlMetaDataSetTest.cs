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
    public class SqlMetaDataSetTest
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

        [Fact]
        public void SqlMetaDataSet_Clone_PreservesCekTable()
        {
            // Verify that cloning preserves the CEK table reference, which is needed by
            // WriteCekTable in TdsParser to send encryption key entries to SQL Server.
            // Without this, WriteCekTable sees cekTable == null and writes 0 CEK entries.
            SqlTceCipherInfoTable cekTable = new SqlTceCipherInfoTable(2);
            cekTable[0] = new SqlTceCipherInfoEntry(ordinal: 0);
            cekTable[1] = new SqlTceCipherInfoEntry(ordinal: 1);

            _SqlMetaDataSet original = new _SqlMetaDataSet(2, cekTable);
            original[0].column = "col1";
            original[1].column = "col2";

            _SqlMetaDataSet clone = original.Clone();

            Assert.NotNull(clone.cekTable);
            Assert.Same(original.cekTable, clone.cekTable);
            Assert.Equal(2, clone.cekTable.Size);
        }

        [Fact]
        public void SqlMetaData_Clone_PreservesIsEncrypted()
        {
            // Verify that cloning a _SqlMetaData entry preserves the isEncrypted flag.
            // WriteBulkCopyMetaData checks md.isEncrypted to set the TDS IsEncrypted flag
            // and WriteCryptoMetadata checks it to decide whether to write cipher metadata.
            // If lost, encrypted columns are sent as plaintext.
            _SqlMetaDataSet original = new _SqlMetaDataSet(1);
            original[0].column = "encrypted_col";
            original[0].isEncrypted = true;

            _SqlMetaDataSet clone = original.Clone();

            Assert.True(clone[0].isEncrypted);
        }

        [Fact]
        public void SqlMetaData_Clone_PreservesCipherMetadata()
        {
            // Verify that cloning preserves cipherMD, which is needed by
            // WriteCryptoMetadata (for CekTableOrdinal, CipherAlgorithmId, etc.)
            // and LoadColumnEncryptionKeys (to decrypt symmetric keys).
            SqlTceCipherInfoEntry cekEntry = new SqlTceCipherInfoEntry(ordinal: 0);
            SqlCipherMetadata cipherMD = new SqlCipherMetadata(
                sqlTceCipherInfoEntry: cekEntry,
                ordinal: 0,
                cipherAlgorithmId: 2,
                cipherAlgorithmName: "AEAD_AES_256_CBC_HMAC_SHA256",
                encryptionType: 1,
                normalizationRuleVersion: 1
            );

            _SqlMetaDataSet original = new _SqlMetaDataSet(1);
            original[0].column = "encrypted_col";
            original[0].isEncrypted = true;
            original[0].cipherMD = cipherMD;

            _SqlMetaDataSet clone = original.Clone();

            Assert.NotNull(clone[0].cipherMD);
            Assert.Equal(2, clone[0].cipherMD.CipherAlgorithmId);
            Assert.Equal("AEAD_AES_256_CBC_HMAC_SHA256", clone[0].cipherMD.CipherAlgorithmName);
            Assert.Equal(1, clone[0].cipherMD.EncryptionType);
            Assert.Equal(1, clone[0].cipherMD.NormalizationRuleVersion);
        }

        [Fact]
        public void SqlMetaData_Clone_PreservesBaseTI()
        {
            // Verify that cloning preserves baseTI, which represents the plaintext
            // TYPE_INFO for encrypted columns. WriteCryptoMetadata calls
            // WriteTceUserTypeAndTypeInfo(md.baseTI) to send the unencrypted type info.
            SqlMetaDataPriv baseTI = new SqlMetaDataPriv();
            baseTI.type = System.Data.SqlDbType.NVarChar;
            baseTI.length = 100;
            baseTI.precision = 0;
            baseTI.scale = 0;

            _SqlMetaDataSet original = new _SqlMetaDataSet(1);
            original[0].column = "encrypted_col";
            original[0].isEncrypted = true;
            original[0].baseTI = baseTI;

            _SqlMetaDataSet clone = original.Clone();

            Assert.NotNull(clone[0].baseTI);
            Assert.Equal(System.Data.SqlDbType.NVarChar, clone[0].baseTI.type);
            Assert.Equal(100, clone[0].baseTI.length);
        }

        [Fact]
        public void SqlMetaDataSet_Clone_PreservesFullAlwaysEncryptedMetadata()
        {
            // End-to-end test: verify that a cloned _SqlMetaDataSet with Always Encrypted
            // metadata retains all AE fields needed by the bulk copy TDS write path:
            // cekTable (for WriteCekTable), isEncrypted (for flag writing),
            // cipherMD (for WriteCryptoMetadata), and baseTI (for WriteTceUserTypeAndTypeInfo).
            SqlTceCipherInfoEntry cekEntry = new SqlTceCipherInfoEntry(ordinal: 0);
            SqlTceCipherInfoTable cekTable = new SqlTceCipherInfoTable(1);
            cekTable[0] = cekEntry;

            SqlCipherMetadata cipherMD = new SqlCipherMetadata(
                sqlTceCipherInfoEntry: cekEntry,
                ordinal: 0,
                cipherAlgorithmId: 2,
                cipherAlgorithmName: "AEAD_AES_256_CBC_HMAC_SHA256",
                encryptionType: 1,
                normalizationRuleVersion: 1
            );

            SqlMetaDataPriv baseTI = new SqlMetaDataPriv();
            baseTI.type = System.Data.SqlDbType.Int;

            _SqlMetaDataSet original = new _SqlMetaDataSet(2, cekTable);
            original[0].column = "id";
            original[1].column = "secret";
            original[1].isEncrypted = true;
            original[1].cipherMD = cipherMD;
            original[1].baseTI = baseTI;

            // Clone and prune column 0 (simulating mapping only the encrypted column)
            _SqlMetaDataSet clone = original.Clone();
            clone[0] = null;

            // The pruning must not affect the encrypted column's metadata
            Assert.NotNull(clone[1]);
            Assert.True(clone[1].isEncrypted);
            Assert.NotNull(clone[1].cipherMD);
            Assert.NotNull(clone[1].baseTI);
            Assert.Equal(System.Data.SqlDbType.Int, clone[1].baseTI.type);

            // The cekTable must be preserved on the clone
            Assert.NotNull(clone.cekTable);
            Assert.Equal(1, clone.cekTable.Size);

            // The original must remain completely intact
            Assert.NotNull(original[0]);
            Assert.NotNull(original[1]);
            Assert.NotNull(original.cekTable);
            Assert.True(original[1].isEncrypted);
        }
    }
}