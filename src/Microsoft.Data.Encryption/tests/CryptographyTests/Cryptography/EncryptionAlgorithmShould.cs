using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Xunit;
using Xunit.Sdk;
using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Cryptography
{
    public class EncryptionAlgorithmShould
    {
        private static Random random = new Random();

        [Fact]
        public void CacheEncryptionAlgorithmsCorrectlyWhenCallingGetOrCreate()
        {
            DataEncryptionKey key1 = new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey);
            DataEncryptionKey key2 = new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey);
            DataEncryptionKey key3 = new ProtectedDataEncryptionKey("Not_EK", keyEncryptionKey, encryptedDataEncryptionKey);

            AeadAes256CbcHmac256EncryptionAlgorithm algorithm1 = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(key1, EncryptionType.Deterministic);
            AeadAes256CbcHmac256EncryptionAlgorithm algorithm2 = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(key2, EncryptionType.Deterministic);

            Assert.Same(algorithm1, algorithm2);

            AeadAes256CbcHmac256EncryptionAlgorithm algorithm3 = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(key1, EncryptionType.Deterministic);
            AeadAes256CbcHmac256EncryptionAlgorithm algorithm4 = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(key2, EncryptionType.Randomized);

            Assert.NotSame(algorithm3, algorithm4);

            AeadAes256CbcHmac256EncryptionAlgorithm algorithm5 = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(key1, EncryptionType.Randomized);
            AeadAes256CbcHmac256EncryptionAlgorithm algorithm6 = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(key3, EncryptionType.Randomized);

            Assert.NotSame(algorithm5, algorithm6);
        }

        [Fact]
        public void ReturnNullWhenEncryptingNull()
        {
            DataEncryptionKey encryptionKey = new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey);
            AeadAes256CbcHmac256EncryptionAlgorithm encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, EncryptionType.Deterministic);
            byte[] ciphertext = encryptionAlgorithm.Encrypt(null);

            Assert.Null(ciphertext);
        }

        [Fact]
        public void ReturnNullWhenDecryptingNull()
        {
            DataEncryptionKey encryptionKey = new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey);
            AeadAes256CbcHmac256EncryptionAlgorithm encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, EncryptionType.Deterministic);
            byte[] plaintext = encryptionAlgorithm.Decrypt(null);

            Assert.Null(plaintext);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64 })]
        public void ThrowWhenDecryptingLessThanSixtyFiveBytes(byte[] ciphertext)
        {
            DataEncryptionKey encryptionKey = new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey);
            AeadAes256CbcHmac256EncryptionAlgorithm encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, EncryptionType.Deterministic);

            Assert.Throws<ArgumentException>(() => encryptionAlgorithm.Decrypt(ciphertext));
        }

        [Fact]
        public void ThrowWhenDecryptionAnInvalidAuthenticationTag()
        {
            byte[] invalidAuthTag = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65 };
            DataEncryptionKey encryptionKey = new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey);
            AeadAes256CbcHmac256EncryptionAlgorithm encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, EncryptionType.Deterministic);

            Assert.Throws<CryptographicException>(() => encryptionAlgorithm.Decrypt(invalidAuthTag));
        }

        [Theory]
        [DataEncryptionKeyTestData]
        public void EncryptToSameCiphertextWhenDeterministicEncryptionTypeSelected(DataEncryptionKey encryptionKey)
        {
            EncryptionType encryptionType = EncryptionType.Deterministic;

            byte[] serializedPlaintext = new byte[] { 1, 2, 3, 4, 5 };
            AeadAes256CbcHmac256EncryptionAlgorithm encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, encryptionType);

            byte[] ciphertext1 = encryptionAlgorithm.Encrypt(serializedPlaintext);
            byte[] ciphertext2 = encryptionAlgorithm.Encrypt(serializedPlaintext);
            byte[] ciphertext3 = encryptionAlgorithm.Encrypt(serializedPlaintext);

            Assert.Equal(ciphertext1, ciphertext2);
            Assert.Equal(ciphertext2, ciphertext3);
            Assert.Equal(ciphertext1, ciphertext3);
        }

        [Theory]
        [DataEncryptionKeyTestData]
        public void EncryptToDifferentCiphertextWhenRandomizedEncryptionTypeSelected(DataEncryptionKey encryptionKey)
        {
            EncryptionType encryptionType = EncryptionType.Randomized;

            byte[] serializedPlaintext = new byte[] { 1, 2, 3, 4, 5 };
            AeadAes256CbcHmac256EncryptionAlgorithm encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, encryptionType);

            byte[] ciphertext1 = encryptionAlgorithm.Encrypt(serializedPlaintext);
            byte[] ciphertext2 = encryptionAlgorithm.Encrypt(serializedPlaintext);
            byte[] ciphertext3 = encryptionAlgorithm.Encrypt(serializedPlaintext);

            Assert.NotEqual(ciphertext1, ciphertext2);
            Assert.NotEqual(ciphertext2, ciphertext3);
            Assert.NotEqual(ciphertext1, ciphertext3);
        }

        [Theory]
        [EncryptionAlgorithmTestData]
        public void EncryptAndDecryptToTheSameValue<T>(T originalPlaintext, Serializer<T> serializer)
        {
            DataEncryptionKey[] keys = {
                new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey),
                new PlaintextDataEncryptionKey("EK", plaintextEncryptionKeyBytes)
            };

            foreach (DataEncryptionKey encryptionKey in keys)
            {
                EncryptionType encryptionType = (EncryptionType)random.Next(1, 2);
                AeadAes256CbcHmac256EncryptionAlgorithm encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, encryptionType);

                byte[] serializedPlaintext = serializer.Serialize(originalPlaintext);
                byte[] ciphhertext = encryptionAlgorithm.Encrypt(serializedPlaintext);
                byte[] decryptedPlaintext = encryptionAlgorithm.Decrypt(ciphhertext);
                T actualPlaintext = serializer.Deserialize(decryptedPlaintext);

                Assert.Equal(originalPlaintext, actualPlaintext);
            }
        }

        public class DataEncryptionKeyTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey) };
                yield return new object[] { new PlaintextDataEncryptionKey("EK", plaintextEncryptionKeyBytes) };
            }
        }

        public class EncryptionAlgorithmTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { true, StandardSerializerFactory.Default.GetDefaultSerializer<bool>() };
                yield return new object[] { (byte)128, StandardSerializerFactory.Default.GetDefaultSerializer<byte>() };
                yield return new object[] { (sbyte)-64, StandardSerializerFactory.Default.GetDefaultSerializer<sbyte>() };
                yield return new object[] { 'T', StandardSerializerFactory.Default.GetDefaultSerializer<char>() };
                yield return new object[] { 1.23456, StandardSerializerFactory.Default.GetDefaultSerializer<double>() };
                yield return new object[] { 3.14159F, StandardSerializerFactory.Default.GetDefaultSerializer<float>() };
                yield return new object[] { int.MaxValue, StandardSerializerFactory.Default.GetDefaultSerializer<int>() };
                yield return new object[] { uint.MinValue, StandardSerializerFactory.Default.GetDefaultSerializer<uint>() };
                yield return new object[] { -987654321L, StandardSerializerFactory.Default.GetDefaultSerializer<long>() };
                yield return new object[] { 9876543210UL, StandardSerializerFactory.Default.GetDefaultSerializer<ulong>() };
                yield return new object[] { (short)-12345, StandardSerializerFactory.Default.GetDefaultSerializer<short>() };
                yield return new object[] { (ushort)12345, StandardSerializerFactory.Default.GetDefaultSerializer<ushort>() };
                yield return new object[] { TimeSpan.FromDays(28), StandardSerializerFactory.Default.GetDefaultSerializer<TimeSpan>() };
                yield return new object[] { DateTime.Now, StandardSerializerFactory.Default.GetDefaultSerializer<DateTime>() };
                yield return new object[] { DateTimeOffset.Parse("Fri September 25, 2020 11:36 PM"), StandardSerializerFactory.Default.GetDefaultSerializer<DateTimeOffset>() };
                yield return new object[] { Guid.NewGuid(), StandardSerializerFactory.Default.GetDefaultSerializer<Guid>() };
            }
        }
    }
}
