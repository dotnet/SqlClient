using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using Microsoft.Data.Encryption.FileEncryption;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

using static Microsoft.Data.Encryption.TestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.FileEncryption.Tests
{
    public class ColumnCryptographerShould
    {
        // Encryptd values for integers 1 and 2
        private static readonly byte[] randomizedOne = new byte[] { 1, 52, 97, 134, 136, 23, 141, 160, 123, 10, 212, 65, 134, 72, 52, 161, 127, 117, 3, 191, 203, 58, 33, 167, 175, 215, 152, 225, 195, 165, 236, 128, 119, 151, 216, 192, 68, 58, 192, 7, 250, 79, 146, 143, 219, 195, 58, 133, 121, 223, 224, 209, 212, 135, 171, 105, 92, 111, 243, 25, 170, 209, 94, 218, 225 };
        private static readonly byte[] randomizedTwo = new byte[] { 1, 125, 121, 16, 27, 110, 98, 193, 160, 72, 93, 143, 222, 204, 144, 82, 15, 36, 101, 36, 171, 109, 216, 141, 244, 176, 190, 71, 160, 169, 42, 196, 193, 189, 184, 76, 195, 35, 237, 61, 237, 105, 248, 208, 117, 125, 86, 194, 107, 55, 22, 129, 14, 142, 2, 110, 36, 174, 170, 174, 142, 143, 82, 156, 187 };
        private static readonly byte[] deterministicOne = new byte[] { 1, 90, 143, 38, 67, 73, 171, 43, 123, 52, 65, 187, 51, 81, 168, 68, 125, 140, 48, 115, 127, 141, 114, 188, 123, 113, 147, 23, 200, 163, 226, 69, 28, 190, 189, 205, 158, 229, 57, 149, 181, 236, 68, 141, 237, 16, 138, 242, 230, 60, 149, 11, 239, 24, 76, 182, 93, 139, 71, 136, 127, 219, 169, 123, 168 };
        private static readonly byte[] deterministicTwo = new byte[] { 1, 63, 109, 251, 26, 214, 77, 181, 124, 86, 209, 208, 232, 143, 38, 194, 208, 164, 200, 218, 156, 250, 65, 193, 81, 168, 122, 134, 194, 27, 25, 83, 240, 188, 135, 31, 6, 197, 127, 84, 189, 71, 102, 50, 35, 2, 47, 198, 103, 219, 248, 60, 126, 112, 93, 46, 252, 41, 118, 118, 75, 75, 202, 47, 249 };

        [Fact]
        public void ThrowWhenConstructorReaderIsNull()
        {
            Mock<IColumnarDataWriter> writer = new Mock<IColumnarDataWriter>();
            Assert.Throws<ArgumentNullException>(() => new ColumnarCryptographer(null, writer.Object));
        }

        [Fact]
        public void ThrowWhenConstructorWriterIsNull()
        {
            Mock<IColumnarDataReader> reader = new Mock<IColumnarDataReader>();
            Assert.Throws<ArgumentNullException>(() => new ColumnarCryptographer(reader.Object, null));
        }

        [Fact]
        public void EncryptColumnsThatNeedEncryption()
        {
            // Initialize a reader four plaintext integer columns
            TestReader reader = new TestReader(
                settings: new List<FileEncryptionSettings>() {
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    },
                data: new List<List<IColumn>>() { 
                        new List<IColumn>(){ 
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-One" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Two" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Three" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Four" },
                        },
                        new List<IColumn>(){
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-One" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Two" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Three" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Four" },
                        }
                    }
            );

            // Initialize a writer that encrypts columns at index 1 and 3 with Randomized and Deterministic encryption respectively.
            TestWriter writer = new TestWriter(new List<FileEncryptionSettings>() {
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                }
            );

            // Perform the transformation
            ColumnarCryptographer transformer = new ColumnarCryptographer(reader, writer);
            transformer.Transform();

            // Assert columns at index 1 and 3 are of type byte[] and not int. (Encrypted)
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(1).DataType);
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(3).DataType);

            // Assert columns at index 0 and 2 are of type int and not byte. (NOT Encrypted)
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(0).DataType);
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(2).DataType);

            // Assert that decrypting the same columns with original key results in the same plaintext values
            List<int> expectedDataGroup1 = new List<int>() { 1, 1, 1, 1, 1 };
            List<int> expectedDataGroup2 = new List<int>() { 2, 2, 2, 2, 2 };
            List<int> decryptedRandomizedIntsGroup1 = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(1).Data).Decrypt<int>(dataEncryptionKey).ToList();
            List<int> decryptedRandomizedIntsGroup2 = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(1).Data).Decrypt<int>(dataEncryptionKey).ToList();
            List<int> decryptedDeterministicIntsGroup1 = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(3).Data).Decrypt<int>(dataEncryptionKey).ToList();
            List<int> decryptedDeterministicIntsGroup2 = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(3).Data).Decrypt<int>(dataEncryptionKey).ToList();
            Assert.Equal(expectedDataGroup1, decryptedRandomizedIntsGroup1);
            Assert.Equal(expectedDataGroup1, decryptedDeterministicIntsGroup1);
            Assert.Equal(expectedDataGroup2, decryptedRandomizedIntsGroup2);
            Assert.Equal(expectedDataGroup2, decryptedDeterministicIntsGroup2);
        }

        [Fact]
        public void DecryptColumnsThatNeedDecryption()
        {
            // Initialize a reader with two encrypted columns
            TestReader reader = new TestReader(
                settings: new List<FileEncryptionSettings>() {
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    },
                data: new List<List<IColumn>>() {
                        new List<IColumn>(){
                            new Column<byte[]>(Enumerable.Repeat(deterministicOne, 5).ToList()) { Name = "One-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedOne, 5).ToList()) { Name = "One-Two" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Three" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Four" },
                        },
                        new List<IColumn>(){
                            new Column<byte[]>(Enumerable.Repeat(deterministicTwo, 5).ToList()) { Name = "Two-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedTwo, 5).ToList()) { Name = "Two-Two" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Three" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Four" },
                        }
                    }
            );

            // Initialize a writer that decrypts columns at index 0 and 1.
            TestWriter writer = new TestWriter(new List<FileEncryptionSettings>() {
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                }
            );

            // Perform the transformation
            ColumnarCryptographer transformer = new ColumnarCryptographer(reader, writer);
            transformer.Transform();

            // Assert all columns are of type int and not byte. (NOT Encrypted)
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(0).DataType);
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(1).DataType);
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(2).DataType);
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(3).DataType);

            // Assert that decrypted columns are the expected plaintext values
            int[] expectedDataGroup1 = { 1, 1, 1, 1, 1 };
            int[] expectedDataGroup2 = { 2, 2, 2, 2, 2 };
            Assert.Equal(expectedDataGroup1, writer.OutputData.ElementAt(0).ElementAt(0).Data);
            Assert.Equal(expectedDataGroup1, writer.OutputData.ElementAt(0).ElementAt(1).Data);
            Assert.Equal(expectedDataGroup2, writer.OutputData.ElementAt(1).ElementAt(0).Data);
            Assert.Equal(expectedDataGroup2, writer.OutputData.ElementAt(1).ElementAt(1).Data);
        }

        [Fact]
        public void ReincryptDataWithDataEncryptionKeyRotation()
        {
            ProtectedDataEncryptionKey oldKey = dataEncryptionKey;
            ProtectedDataEncryptionKey newKey = new ProtectedDataEncryptionKey("newDEK", keyEncryptionKey);
            
            // Initialize a reader with two encrypted columns at index 0 and 1
            TestReader reader = new TestReader(
                settings: new List<FileEncryptionSettings>() {
                        new FileEncryptionSettings<int>(oldKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(oldKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(oldKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(oldKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    },
                data: new List<List<IColumn>>() {
                        new List<IColumn>(){
                            new Column<byte[]>(Enumerable.Repeat(deterministicOne, 5).ToList()) { Name = "One-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedOne, 5).ToList()) { Name = "One-Two" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Three" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Four" },
                        },
                        new List<IColumn>(){
                            new Column<byte[]>(Enumerable.Repeat(deterministicTwo, 5).ToList()) { Name = "Two-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedTwo, 5).ToList()) { Name = "Two-Two" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Three" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Four" },
                        }
                    }
            );

            // Initialize a writer that rotates data encryption keys for columns at index 0 and 1.
            TestWriter writer = new TestWriter(new List<FileEncryptionSettings>() {
                    new FileEncryptionSettings<int>(newKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(newKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(newKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(newKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                }
            );

            // Perform the transformation
            ColumnarCryptographer transformer = new ColumnarCryptographer(reader, writer);
            transformer.Transform();

            // Assert columns at index 0 and 1 are of type byte[] and not int. (Encrypted)
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(0).DataType);
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(1).DataType);

            // Assert columns at index 2 and 3 are of type int and not byte. (NOT Encrypted)
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(2).DataType);
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(3).DataType);

            // Assert that decrypting the same column with both keys results in the same plaintext values
            List<int> olddecryptedInts = ((byte[][])reader.Read().ElementAt(0).ElementAt(0).Data).Decrypt<int>(oldKey).ToList();
            List<int> newdecryptedInts = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(0).Data).Decrypt<int>(newKey).ToList();
            Assert.Equal(olddecryptedInts, newdecryptedInts);

            // Compare randomized column before and after transformation. If it were reincrypted then the ciphertext would be different.
            List<string> randomizedGroup0DataBefore = ((byte[][])reader.Read().ElementAt(0).ElementAt(1).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedGroup0DataAfter = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(1).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedGroup1DataBefore = ((byte[][])reader.Read().ElementAt(0).ElementAt(1).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedGroup1DataAfter = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(1).Data).Select(b => b.ToHexString()).ToList();
            Assert.NotEqual(randomizedGroup0DataBefore, randomizedGroup0DataAfter);
            Assert.NotEqual(randomizedGroup1DataBefore, randomizedGroup1DataAfter);

            // Assert using old key to decrypt newly rotated ciphertext throws exception
            Assert.Throws<CryptographicException>(() => ((byte[][])writer.OutputData.ElementAt(0).ElementAt(0).Data).Decrypt<int>(oldKey).ToList());
        }

        [Fact]
        public void NotReincryptDataWithKeyEncryptionKeyRotation()
        {
            ProtectedDataEncryptionKey oldKey = dataEncryptionKey;

            KeyEncryptionKey newKeyEncryptionKey = new KeyEncryptionKey("CMK", keyEncryptionKeyPath2, azureKeyProvider);
            ProtectedDataEncryptionKey newKey = new ProtectedDataEncryptionKey("CEK", newKeyEncryptionKey, newKeyEncryptionKey.EncryptEncryptionKey(plaintextEncryptionKeyBytes));

            // Initialize a reader with two encrypted columns at index 0 and 1
            TestReader reader = new TestReader(
                settings: new List<FileEncryptionSettings>() {
                        new FileEncryptionSettings<int>(oldKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(oldKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(oldKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(oldKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    },
                data: new List<List<IColumn>>() {
                        new List<IColumn>(){
                            new Column<byte[]>(Enumerable.Repeat(deterministicOne, 5).ToList()) { Name = "One-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedOne, 5).ToList()) { Name = "One-Two" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Three" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Four" },
                        },
                        new List<IColumn>(){
                            new Column<byte[]>(Enumerable.Repeat(deterministicTwo, 5).ToList()) { Name = "Two-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedTwo, 5).ToList()) { Name = "Two-Two" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Three" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Four" },
                        }
                    }
            );

            // Initialize a writer that rotates key encryption key for columns at index 0 and 1.
            TestWriter writer = new TestWriter(new List<FileEncryptionSettings>() {
                    new FileEncryptionSettings<int>(newKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(newKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(newKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(newKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                }
            );

            // Perform the transformation
            ColumnarCryptographer transformer = new ColumnarCryptographer(reader, writer);
            transformer.Transform();

            // Compare randomized column before and after transformation. If it were not reincrypted then the ciphertext would be the exact same.
            List<string> randomizedDataGroup0Before = ((byte[][])reader.Read().ElementAt(0).ElementAt(1).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedDataGroup1Before = ((byte[][])reader.Read().ElementAt(1).ElementAt(1).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedDataGroup0After = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(1).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedDataGroup1After = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(1).Data).Select(b => b.ToHexString()).ToList();
            Assert.Equal(randomizedDataGroup0Before, randomizedDataGroup0After);
            Assert.Equal(randomizedDataGroup1Before, randomizedDataGroup1After);

            // With a key encryption key rotation, the data encryption root key should be the same.
            string oldKeyRoot = reader.FileEncryptionSettings[1].DataEncryptionKey.RootKeyBytes.ToHexString();
            string newKeyRoot = writer.FileEncryptionSettings[1].DataEncryptionKey.RootKeyBytes.ToHexString();
            Assert.Equal(oldKeyRoot, newKeyRoot);

            // With a key encryption key rotation, the data encryption root key is the same but encrypted values should be different.
            string oldKeyEncrypted = reader.FileEncryptionSettings[1].DataEncryptionKey.EncryptedValue.ToHexString();
            string newKeyEncrypted = writer.FileEncryptionSettings[1].DataEncryptionKey.EncryptedValue.ToHexString();
            Assert.NotEqual(oldKeyEncrypted, newKeyEncrypted);

            // With a key encryption key rotation, the key encryption keypath should be different.
            string oldKeyPath = reader.FileEncryptionSettings[1].DataEncryptionKey.KeyEncryptionKey.Path;
            string newKeyPath = writer.FileEncryptionSettings[1].DataEncryptionKey.KeyEncryptionKey.Path;
            Assert.NotEqual(oldKeyPath, newKeyPath);
        }

        [Fact]
        public void ReincryptDataWhenEncryptionTypeChanges()
        {
            // Initialize a reader with four columns of various encryption types
            TestReader reader = new TestReader(
                settings: new List<FileEncryptionSettings>() {
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    },
                data: new List<List<IColumn>>() {
                        new List<IColumn>(){
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedOne, 5).ToList()) { Name = "One-Two" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicOne, 5).ToList()) { Name = "One-Three" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicOne, 5).ToList()) { Name = "One-Four" },
                        },
                        new List<IColumn>(){
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedTwo, 5).ToList()) { Name = "Two-Two" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicTwo, 5).ToList()) { Name = "Two-Three" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicTwo, 5).ToList()) { Name = "Two-Four" },
                        }
                    }
            );

            // Initialize a writer that changes encryptionType at index 1 and 2 from Randomized to Deterministic and from deterministic to randomized respectively.
            TestWriter writer = new TestWriter(new List<FileEncryptionSettings>() {
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(dataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                }
            );

            // Perform the transformation
            ColumnarCryptographer transformer = new ColumnarCryptographer(reader, writer);
            transformer.Transform();

            // Assert columns at index 1, 2 and 3 are of type byte[] and not int. (Encrypted)
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(1).DataType);
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(2).DataType);
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(3).DataType);
            
            // Assert the column at index 0 is of type int and not byte. (NOT Encrypted)
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(0).DataType);
            
            // Assert that decrypting columns at index 1 and 2 with original key results in the expected plaintext values
            List<int> expectedDataGroup1 = new List<int>() { 1, 1, 1, 1, 1 };
            List<int> expectedDataGroup2 = new List<int>() { 2, 2, 2, 2, 2 };
            List<int> decryptedRandomizedIntsGroup1 = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(1).Data).Decrypt<int>(dataEncryptionKey).ToList();
            List<int> decryptedRandomizedIntsGroup2 = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(1).Data).Decrypt<int>(dataEncryptionKey).ToList();
            List<int> decryptedDeterministicIntsGroup1 = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(2).Data).Decrypt<int>(dataEncryptionKey).ToList();
            List<int> decryptedDeterministicIntsGroup2 = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(2).Data).Decrypt<int>(dataEncryptionKey).ToList();
            Assert.Equal(expectedDataGroup1, decryptedRandomizedIntsGroup1);
            Assert.Equal(expectedDataGroup1, decryptedDeterministicIntsGroup1);
            Assert.Equal(expectedDataGroup2, decryptedRandomizedIntsGroup2);
            Assert.Equal(expectedDataGroup2, decryptedDeterministicIntsGroup2);

            // Assert that all ciphertext elements in both groups are the same for columns 2 and one
            // Both should be Deterministic and so the ciphertext should be the same for the same plaintext data.
            IEnumerable<byte[]> preTransformationDataGroup0Column2 = (byte[][])reader.Read().ElementAt(0).ElementAt(2).Data;
            IEnumerable<byte[]> postTransformationDataGroup0Column1 = (byte[][])writer.OutputData.ElementAt(0).ElementAt(1).Data;
            IEnumerable<byte[]> group0combinedDeterministicData = preTransformationDataGroup0Column2.Concat(postTransformationDataGroup0Column1);
            Assert.True(group0combinedDeterministicData.Select(a => a.ToHexString()).Distinct().Count() == 1);
            IEnumerable<byte[]> preTransformationDataGroup1Column2 = (byte[][])reader.Read().ElementAt(1).ElementAt(2).Data;
            IEnumerable<byte[]> postTransformationDataGroup1Column1 = (byte[][])writer.OutputData.ElementAt(1).ElementAt(1).Data;
            IEnumerable<byte[]> group1combinedDeterministicData = preTransformationDataGroup1Column2.Concat(postTransformationDataGroup1Column1);
            Assert.True(group1combinedDeterministicData.Select(a => a.ToHexString()).Distinct().Count() == 1);

            // Assert that the column at index 2 after transformation is now randomized. All ciphertext elements should be different for both groups of data.
            IEnumerable<byte[]> postTransformationDataGroup0Column2 = (byte[][])writer.OutputData.ElementAt(0).ElementAt(2).Data;
            IEnumerable<byte[]> postTransformationDataGroup1Column2 = (byte[][])writer.OutputData.ElementAt(1).ElementAt(2).Data;
            IEnumerable<byte[]> PostTransformationDataCombinedGroupColumn2 = postTransformationDataGroup0Column2.Concat(postTransformationDataGroup1Column2);
            Assert.True(PostTransformationDataCombinedGroupColumn2.Select(a => a.ToHexString()).Distinct().Count() == PostTransformationDataCombinedGroupColumn2.Count());
        }

        [Fact]
        public void HandleMultipleEncryptionTypeOperations()
        {
            ProtectedDataEncryptionKey originalDataEncryptionKey = dataEncryptionKey;
            ProtectedDataEncryptionKey newDataEncryptionKeyRotationKey = new ProtectedDataEncryptionKey("newDEK", keyEncryptionKey);
            KeyEncryptionKey newKeyEncryptionKey = new KeyEncryptionKey("CMK", keyEncryptionKeyPath2, azureKeyProvider);
            ProtectedDataEncryptionKey newKeyEncryptionKeyRotationKey = new ProtectedDataEncryptionKey("CEK", newKeyEncryptionKey, newKeyEncryptionKey.EncryptEncryptionKey(plaintextEncryptionKeyBytes));


            // Initialize a reader five integer columns of various configurations
            TestReader reader = new TestReader(
                settings: new List<FileEncryptionSettings>() {
                        new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                        new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    },
                data: new List<List<IColumn>>() {
                        new List<IColumn>(){
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedOne, 5).ToList()) { Name = "One-Two" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicOne, 5).ToList()) { Name = "One-Three" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicOne, 5).ToList()) { Name = "One-Four" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicOne, 5).ToList()) { Name = "One-Five" },
                            new Column<int>(new List<int>(){ 1, 1, 1, 1, 1}) { Name = "One-Six" },
                        },
                        new List<IColumn>(){
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-One" },
                            new Column<byte[]>(Enumerable.Repeat(randomizedTwo, 5).ToList()) { Name = "Two-Two" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicTwo, 5).ToList()) { Name = "Two-Three" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicTwo, 5).ToList()) { Name = "Two-Four" },
                            new Column<byte[]>(Enumerable.Repeat(deterministicTwo, 5).ToList()) { Name = "Two-Five" },
                            new Column<int>(new List<int>(){ 2, 2, 2, 2, 2}) { Name = "Two-Six" },
                        }
                    }
            );

            // Initialize a writer that transforms the data in 5 different ways.
            // Column 0 = Column Encryption
            // Column 1 = Column Decryption
            // Column 2 = Data encryption key rotation
            // Column 3 = Key encryption key rotation
            // Column 4 = EncryptionType change (Deterministic to Randomized)
            // Column 5 = No change
            TestWriter writer = new TestWriter(new List<FileEncryptionSettings>() {
                    new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(newDataEncryptionKeyRotationKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(newKeyEncryptionKeyRotationKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                    new FileEncryptionSettings<int>(originalDataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<int>()),
                }
            );

            // Perform the transformations
            ColumnarCryptographer transformer = new ColumnarCryptographer(reader, writer);
            transformer.Transform();

            #region Column 0 Assertions

            // Assert that column 0 was encrypted and thus is of type byte[]
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(0).DataType);
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(1).ElementAt(0).DataType);

            // Assert that column 0 was encrypted randomly
            IEnumerable<byte[]> postTransformationDataGroup0Column0 = (byte[][])writer.OutputData.ElementAt(0).ElementAt(0).Data;
            IEnumerable<byte[]> postTransformationDataGroup1Column0 = (byte[][])writer.OutputData.ElementAt(1).ElementAt(0).Data;
            IEnumerable<byte[]> PostTransformationDataCombinedGroupColumn2 = postTransformationDataGroup0Column0.Concat(postTransformationDataGroup1Column0);
            Assert.True(PostTransformationDataCombinedGroupColumn2.Select(a => a.ToHexString()).Distinct().Count() == PostTransformationDataCombinedGroupColumn2.Count());
            
            // Assert that column 0 decrypts to expected values after encryption.
            List<int> expectedDataGroup0Column0 = new List<int>() { 1, 1, 1, 1, 1 };
            List<int> expectedDataGroup1Column0 = new List<int>() { 2, 2, 2, 2, 2 };
            List<int> decryptedGroup0Column0 = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(0).Data).Decrypt<int>(originalDataEncryptionKey).ToList();
            List<int> decryptedGroup1Column0 = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(0).Data).Decrypt<int>(originalDataEncryptionKey).ToList();
            Assert.Equal(expectedDataGroup0Column0, decryptedGroup0Column0);
            Assert.Equal(expectedDataGroup1Column0, decryptedGroup1Column0);

            #endregion

            #region Column 1 Assertions

            // Assert that column 1 was decrypted and thus is of type int.
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(1).DataType);
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(1).ElementAt(1).DataType);

            // Assert that the decrypted data in column 1 is the expected data.
            int[] expectedDataGroup0Column1 = { 1, 1, 1, 1, 1 };
            int[] expectedDataGroup1Column1 = { 2, 2, 2, 2, 2 };
            Assert.Equal(expectedDataGroup0Column1, writer.OutputData.ElementAt(0).ElementAt(1).Data);
            Assert.Equal(expectedDataGroup1Column1, writer.OutputData.ElementAt(1).ElementAt(1).Data);

            #endregion

            #region Column 2 Assertions

            // Assert that column 2 is of type byte[] and not int. (Encrypted)
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(2).DataType);
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(1).ElementAt(2).DataType);

            // Assert that decrypting the same column with both keys results in the same plaintext values
            List<int> olddecryptedInts = ((byte[][])reader.Read().ElementAt(0).ElementAt(2).Data).Decrypt<int>(originalDataEncryptionKey).ToList();
            List<int> newdecryptedInts = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(2).Data).Decrypt<int>(newDataEncryptionKeyRotationKey).ToList();
            Assert.Equal(olddecryptedInts, newdecryptedInts);

            // Compare column 2 data before and after transformation. Since the column is of type randomized then if it were reincrypted then the ciphertext should be different.
            List<string> randomizedGroup0DataBefore = ((byte[][])reader.Read().ElementAt(0).ElementAt(2).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedGroup0DataAfter = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(2).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedGroup1DataBefore = ((byte[][])reader.Read().ElementAt(1).ElementAt(2).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedGroup1DataAfter = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(2).Data).Select(b => b.ToHexString()).ToList();
            Assert.NotEqual(randomizedGroup0DataBefore, randomizedGroup0DataAfter);
            Assert.NotEqual(randomizedGroup1DataBefore, randomizedGroup1DataAfter);

            // Assert using original key to decrypt newly rotated ciphertext in column 2 throws CryptographicException
            Assert.Throws<CryptographicException>(() => ((byte[][])writer.OutputData.ElementAt(0).ElementAt(2).Data).Decrypt<int>(originalDataEncryptionKey).ToList());

            #endregion

            #region Column 3 Assertions

            // Compare column 3 before and after transformation. If it were not reincrypted then 
            // randomized ciphertext would be the exact same.
            List<string> randomizedDataGroup0Before = ((byte[][])reader.Read().ElementAt(0).ElementAt(3).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedDataGroup1Before = ((byte[][])reader.Read().ElementAt(1).ElementAt(3).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedDataGroup0After = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(3).Data).Select(b => b.ToHexString()).ToList();
            List<string> randomizedDataGroup1After = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(3).Data).Select(b => b.ToHexString()).ToList();
            Assert.Equal(randomizedDataGroup0Before, randomizedDataGroup0After);
            Assert.Equal(randomizedDataGroup1Before, randomizedDataGroup1After);

            // With a key encryption key rotation, the data encryption root key for column 3 should be the same before and afer transformation.
            string oldKeyRoot = reader.FileEncryptionSettings[3].DataEncryptionKey.RootKeyBytes.ToHexString();
            string newKeyRoot = writer.FileEncryptionSettings[3].DataEncryptionKey.RootKeyBytes.ToHexString();
            Assert.Equal(oldKeyRoot, newKeyRoot);

            // With a key encryption key rotation, the encrypted values for column 3 should be different afer transformation.
            string oldKeyEncrypted = reader.FileEncryptionSettings[3].DataEncryptionKey.EncryptedValue.ToHexString();
            string newKeyEncrypted = writer.FileEncryptionSettings[3].DataEncryptionKey.EncryptedValue.ToHexString();
            Assert.NotEqual(oldKeyEncrypted, newKeyEncrypted);

            // With a key encryption key rotation, the key encryption keypath for column 3 should be different.
            string oldKeyPath = reader.FileEncryptionSettings[3].DataEncryptionKey.KeyEncryptionKey.Path;
            string newKeyPath = writer.FileEncryptionSettings[3].DataEncryptionKey.KeyEncryptionKey.Path;
            Assert.NotEqual(oldKeyPath, newKeyPath);

            #endregion

            #region Column 4 Assertions

            // Assert columns at index 4 is of type byte[] and not int. (Encrypted)
            Assert.Equal(typeof(byte[]), writer.OutputData.ElementAt(0).ElementAt(4).DataType);

            // Assert that decrypting column at index 4 with original key results in the expected plaintext values
            List<int> expectedDataGroup0 = new List<int>() { 1, 1, 1, 1, 1 };
            List<int> expectedDataGroup1 = new List<int>() { 2, 2, 2, 2, 2 };
            List<int> decryptedRandomizedIntsGroup1 = ((byte[][])writer.OutputData.ElementAt(0).ElementAt(4).Data).Decrypt<int>(dataEncryptionKey).ToList();
            List<int> decryptedRandomizedIntsGroup2 = ((byte[][])writer.OutputData.ElementAt(1).ElementAt(4).Data).Decrypt<int>(dataEncryptionKey).ToList();
            Assert.Equal(expectedDataGroup0, decryptedRandomizedIntsGroup1);
            Assert.Equal(expectedDataGroup1, decryptedRandomizedIntsGroup2);
            
            // Assert that the column at index 4 before transformation is deterministic. All ciphertext elements should be the same for both groups of data.
            IEnumerable<byte[]> preTransformationDataGroup0Column4 = (byte[][])reader.Read().ElementAt(0).ElementAt(4).Data;
            IEnumerable<byte[]> preTransformationDataGroup1Column4 = (byte[][])reader.Read().ElementAt(1).ElementAt(4).Data;
            Assert.True(preTransformationDataGroup0Column4.Select(a => a.ToHexString()).Distinct().Count() == 1);
            Assert.True(preTransformationDataGroup1Column4.Select(a => a.ToHexString()).Distinct().Count() == 1);

            // Assert that the column at index 2 after transformation is now randomized. All ciphertext elements should be different for both groups of data.
            IEnumerable<byte[]> postTransformationDataGroup0Column4 = (byte[][])writer.OutputData.ElementAt(0).ElementAt(2).Data;
            IEnumerable<byte[]> postTransformationDataGroup1Column4 = (byte[][])writer.OutputData.ElementAt(1).ElementAt(2).Data;
            IEnumerable<byte[]> PostTransformationDataCombinedGroupColumn4 = postTransformationDataGroup0Column4.Concat(postTransformationDataGroup1Column4);
            Assert.True(PostTransformationDataCombinedGroupColumn4.Select(a => a.ToHexString()).Distinct().Count() == PostTransformationDataCombinedGroupColumn4.Count());

            #endregion

            #region Column 5 Assertions

            // Assert the column at index 5 is of type int and not byte before and after transformation. (NOT Encrypted)
            Assert.Equal(typeof(int), reader.Read().ElementAt(0).ElementAt(5).DataType);
            Assert.Equal(typeof(int), reader.Read().ElementAt(1).ElementAt(5).DataType);
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(0).ElementAt(5).DataType);
            Assert.Equal(typeof(int), writer.OutputData.ElementAt(1).ElementAt(5).DataType);

            // Assert that the data is what is expected to be if no change has occurred
            int[] expectedDataGroup0Column5 = { 1, 1, 1, 1, 1 };
            int[] expectedDataGroup1Column5 = { 2, 2, 2, 2, 2 };
            Assert.Equal(expectedDataGroup0Column5, reader.Read().ElementAt(0).ElementAt(5).Data);
            Assert.Equal(expectedDataGroup0Column5, writer.OutputData.ElementAt(0).ElementAt(5).Data);
            Assert.Equal(expectedDataGroup1Column5, reader.Read().ElementAt(1).ElementAt(5).Data);
            Assert.Equal(expectedDataGroup1Column5, writer.OutputData.ElementAt(1).ElementAt(5).Data);

            #endregion
        }

        private class TestReader : IColumnarDataReader
        {
            private List<FileEncryptionSettings> Settings { get; set; }

            private IEnumerable<IList<IColumn>> Data { get; set; }

            public TestReader(List<FileEncryptionSettings> settings, IEnumerable<IList<IColumn>> data)
            {
                Settings = settings;
                Data = data;
            }

            public IList<FileEncryptionSettings> FileEncryptionSettings => Settings;

            public IEnumerable<IEnumerable<IColumn>> Read() => Data;

            public void RegisterKeyStoreProviders(IDictionary<string, EncryptionKeyStoreProvider> encryptionKeyStoreProviders)
            {
                throw new NotImplementedException();
            }
        }

        private class TestWriter : IColumnarDataWriter
        {
            public IEnumerable<IEnumerable<IColumn>> OutputData { get; private set; } = new List<IList<IColumn>>();

            public string Metadata { get; private set; }

            public TestWriter(IList<FileEncryptionSettings> settings)
            {
                FileEncryptionSettings = settings;
            }

            public IList<FileEncryptionSettings> FileEncryptionSettings { get; private set; }

            public void Write(IEnumerable<IColumn> columns)
            {
                CryptoMetadata metadata = CryptoMetadata.CompileMetadata(columns, FileEncryptionSettings);
                Metadata = JsonConvert.SerializeObject(
                    value: metadata,
                    settings: new JsonSerializerSettings() {
                        NullValueHandling = NullValueHandling.Ignore,
                        Converters = { new StringEnumConverter() },
                        Formatting = Formatting.Indented
                    }
                );

                OutputData = OutputData.Append(columns);
            }
        }
    }
}
