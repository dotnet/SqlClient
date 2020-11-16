using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Sdk;
using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Extensions
{
    public class CryptographyExtensionsShould
    {
        [Theory]
        [EncryptionDecryptionTestData]
        public void EncryptAndDecryptGenereicValuesWithKey<T>(T plaintext)
        {
            DataEncryptionKey[] keys = {
                new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey),
                new PlaintextDataEncryptionKey("EK", plaintextEncryptionKeyBytes)
            };

            foreach (DataEncryptionKey encryptionKey in keys)
            {
                byte[] ciphertext = plaintext.Encrypt(encryptionKey);
                T decrypted = ciphertext.Decrypt<T>(encryptionKey);

                Assert.Equal(plaintext, decrypted);
            }
        }

        [Fact]
        public void EncryptAndDecryptNullableValuesWithKey()
        {
            DataEncryptionKey[] keys = {
                new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey),
                new PlaintextDataEncryptionKey("EK", plaintextEncryptionKeyBytes)
            };

            foreach (DataEncryptionKey encryptionKey in keys)
            {
                int? plaintextInt = null;
                byte[] ciphertextInt = plaintextInt.Encrypt(encryptionKey);
                int? decryptedInt = ciphertextInt.Decrypt<int?>(encryptionKey);

                Assert.Equal(plaintextInt, decryptedInt);

                string plaintextString = null;
                byte[] ciphertextString = plaintextString.Encrypt(encryptionKey);
                string decryptedString = ciphertextString.Decrypt<string>(encryptionKey);

                Assert.Equal(plaintextString, decryptedString);
            }
        }

        [Fact]
        public void ThrowWhenEncryptingWithNullDataEncryptionKey()
        {
            Assert.Throws<ArgumentNullException>(() => "string".Encrypt((DataEncryptionKey)null));
        }

        [Fact]
        public void ThrowWhenDecryptingWithNullDataEncryptionKey()
        {
            Assert.Throws<ArgumentNullException>(() => new byte[] { 0, 19, 28, 3, 74 }.Decrypt<bool>((DataEncryptionKey)null));
        }

        [Theory]
        [EncryptionDecryptionTestData]
        public void EncryptAndDecryptGenereicValuesWithSettings<T>(T plaintext)
        {
            DataEncryptionKey[] keys = {
                new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey),
                new PlaintextDataEncryptionKey("EK", plaintextEncryptionKeyBytes)
            };

            foreach (DataEncryptionKey encryptionKey in keys)
            {
                EncryptionSettings<T> encryptionSettings = new EncryptionSettings<T>(encryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<T>());

                byte[] ciphertext = plaintext.Encrypt(encryptionSettings);
                T decrypted = ciphertext.Decrypt<T>(encryptionSettings);

                Assert.Equal(plaintext, decrypted);
            }
        }

        [Fact]
        public void EncryptAndDecryptNullableValuesWithSettings()
        {
            DataEncryptionKey[] keys = {
                new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey),
                new PlaintextDataEncryptionKey("EK", plaintextEncryptionKeyBytes)
            };

            foreach (DataEncryptionKey encryptionKey in keys)
            {
                EncryptionSettings<DateTime?> encryptionSettingsDateTime = new EncryptionSettings<DateTime?>(encryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<DateTime?>());
                DateTime? plaintextDateTime = null;
                byte[] ciphertextDateTime = plaintextDateTime.Encrypt(encryptionSettingsDateTime);
                DateTime? decryptedDateTime = ciphertextDateTime.Decrypt(encryptionSettingsDateTime);

                Assert.Equal(plaintextDateTime, decryptedDateTime);

                EncryptionSettings<byte[]> encryptionSettingsByteArray = new EncryptionSettings<byte[]>(encryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<byte[]>());
                byte[] plaintextByteArray = null;
                byte[] ciphertextByteArray = plaintextByteArray.Encrypt(encryptionSettingsByteArray);
                byte[] decryptedByteArray = ciphertextByteArray.Decrypt(encryptionSettingsByteArray);

                Assert.Equal(plaintextByteArray, decryptedByteArray);
            }
        }

        [Fact]
        public void ThrowWhenEncryptingWithNullDataEncryptionSettings()
        {
            Assert.Throws<ArgumentNullException>(() => "string".Encrypt((EncryptionSettings<string>)null));
        }

        [Fact]
        public void ThrowWhenDecryptingWithNullDataEncryptionSettings()
        {
            Assert.Throws<ArgumentNullException>(() => new byte[] { 0, 19, 28, 3, 74 }.Decrypt((EncryptionSettings<bool>)null));
        }

        [Fact]
        public void ThrowWhenEncryptingWithPlaintextDataEncryptionSettings()
        {
            EncryptionSettings<string> encryptionSettings = new EncryptionSettings<string>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<string>());
            Assert.Throws<ArgumentException>(() => "string".Encrypt(encryptionSettings));
        }

        [Fact]
        public void ThrowWhenDecryptingWithPlaintextDataEncryptionSettings()
        {
            EncryptionSettings<bool> encryptionSettings = new EncryptionSettings<bool>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<bool>());
            Assert.Throws<ArgumentException>(() => new byte[] { 0, 19, 28, 3, 74 }.Decrypt(encryptionSettings));
        }

        [Theory]
        [EncryptionDecryptionTestData]
        public void EncryptAndDecryptIEnumerablesWithKey<T>(T plaintext)
        {
            DataEncryptionKey[] keys = {
                new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey),
                new PlaintextDataEncryptionKey("EK", plaintextEncryptionKeyBytes)
            };

            foreach (DataEncryptionKey encryptionKey in keys)
            {
                IEnumerable<T> enumerable = Enumerable.Repeat(plaintext, 10);

                byte[][] ciphertext = enumerable.Encrypt(encryptionKey).ToArray();
                T[] decrypted = ciphertext.Decrypt<T>(encryptionKey).ToArray();

                Assert.Equal(enumerable, decrypted);
            }
        }

        [Fact]
        public void ThrowWhenEncryptingIEnumerableWithNullKey()
        {
            IEnumerable<uint> enumerable = new uint[] { 54, 65, 2431, 86, 98 };
            Assert.Throws<ArgumentNullException>(() => enumerable.Encrypt((DataEncryptionKey)null).ToList());
        }

        [Fact]
        public void ThrowWhenDecryptingIEnumerableWithNullKey()
        {
            IEnumerable<byte[]> enumerable = new byte[][] { new byte[] { 0, 19, 28, 3, 74 }, new byte[] { 0, 19, 28, 3, 74 }, new byte[] { 0, 19, 28, 3, 74 } };
            Assert.Throws<ArgumentNullException>(() => enumerable.Decrypt<byte[]>((DataEncryptionKey)null).ToList());
        }

        [Theory]
        [EncryptionDecryptionTestData]
        public void EncryptAndDecryptIEnumerableWithSettings<T>(T plaintext)
        {
            DataEncryptionKey[] keys = {
                new ProtectedDataEncryptionKey("EK", keyEncryptionKey, encryptedDataEncryptionKey),
                new PlaintextDataEncryptionKey("EK", plaintextEncryptionKeyBytes)
            };

            foreach (DataEncryptionKey encryptionKey in keys)
            {
                IEnumerable<T> enumerable = Enumerable.Repeat(plaintext, 10);
                EncryptionSettings<T> encryptionSettings = new EncryptionSettings<T>(encryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<T>());

                byte[][] ciphertext = enumerable.Encrypt(encryptionSettings).ToArray();
                T[] decrypted = ciphertext.Decrypt(encryptionSettings).ToArray();

                Assert.Equal(enumerable, decrypted);
            }
        }

        [Fact]
        public void ThrowWhenEncryptingIEnumerableWithNullDataEncryptionSettings()
        {
            IEnumerable<uint> enumerable = new uint[] { 54, 65, 2431, 86, 98 };
            Assert.Throws<ArgumentNullException>(() => enumerable.Encrypt((EncryptionSettings<uint>)null).ToList());
        }

        [Fact]
        public void ThrowWhenDecryptingIEnumerableWithNullDataEncryptionSettings()
        {
            IEnumerable<byte[]> enumerable = new byte[][] { new byte[] { 0, 19, 28, 3, 74 }, new byte[] { 0, 19, 28, 3, 74 }, new byte[] { 0, 19, 28, 3, 74 } };
            Assert.Throws<ArgumentNullException>(() => enumerable.Decrypt((EncryptionSettings<bool>)null).ToList());
        }

        [Fact]
        public void ThrowWhenEncryptingIEnumerableWithPlaintextDataEncryptionSettings()
        {
            IEnumerable<string> enumerable = new string[] { "54", "65", "2431", "86", "98" };
            EncryptionSettings<string> encryptionSettings = new EncryptionSettings<string>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<string>());
            Assert.Throws<ArgumentException>(() => enumerable.Encrypt(encryptionSettings).ToList());
        }

        [Fact]
        public void ThrowWhenDecryptingIEnumerableWithPlaintextDataEncryptionSettings()
        {
            IEnumerable<byte[]> enumerable = new byte[][] { new byte[] { 0, 19, 28, 3, 74 }, new byte[] { 0, 19, 28, 3, 74 }, new byte[] { 0, 19, 28, 3, 74 } };
            EncryptionSettings<byte[]> encryptionSettings = new EncryptionSettings<byte[]>(dataEncryptionKey, EncryptionType.Plaintext, StandardSerializerFactory.Default.GetDefaultSerializer<byte[]>());
            Assert.Throws<ArgumentException>(() => enumerable.Decrypt(encryptionSettings).ToList());
        }

        [Theory]
        [ByteToStringTestData]
        public void ConvertToAndFromBase64Correctly(byte[] originalBytes)
        {
            string base64String = originalBytes.ToBase64String();
            byte[] convertedBytes = base64String.FromBase64String();

            Assert.Equal(originalBytes, convertedBytes);
        }

        [Fact]
        public void ReturnNullWhenBase64ConvertingNull()
        {
            string nullString = null;
            byte[] nullBytes = null;

            string str = nullBytes.ToBase64String();
            byte[] bytes = nullString.FromBase64String();

            Assert.Null(str);
            Assert.Null(bytes);
        }

        [Theory]
        [InvalidBase64LengthTestData]
        public void ThrowsWhenConvertingBase64StringWhoseLengthIsNotMultipleOfFour(string value)
        {
            Assert.Throws<FormatException>(() => value.FromBase64String());
        }

        [Theory]
        [InvalidBase64FormatTestData]
        public void ThrowsWhenConvertingBase64StringWhoseFormatIsInvalid(string value)
        {
            Assert.Throws<FormatException>(() => value.FromBase64String());
        }

        [Theory]
        [ByteToStringTestData]
        public void ConvertIEnumerableToAndFromBase64Correctly(byte[] originalBytes)
        {
            IEnumerable<byte[]> enumerable = Enumerable.Repeat(originalBytes, 10);
            List<string> base64String = enumerable.ToBase64String().ToList();
            List<byte[]> convertedBytes = base64String.FromBase64String().ToList();

            Assert.Equal(enumerable, convertedBytes);
        }

        [Theory]
        [InvalidBase64LengthTestData]
        public void ThrowsWhenConvertingIEnumerableBase64StringWhoseLengthIsNotMultipleOfFour(string value)
        {
            IEnumerable<string> enumerable = Enumerable.Repeat(value, 10);
            Assert.Throws<FormatException>(() => enumerable.FromBase64String().ToList());
        }

        [Theory]
        [InvalidBase64FormatTestData]
        public void ThrowsWhenConvertingIEnumerableBase64StringWhoseFormatIsInvalid(string value)
        {
            IEnumerable<string> enumerable = Enumerable.Repeat(value, 10);
            Assert.Throws<FormatException>(() => enumerable.FromBase64String().ToList());
        }

        [Theory]
        [ByteToStringTestData]
        public void ConvertToAndFromHexCorrectly(byte[] originalBytes)
        {
            string hexString = originalBytes.ToHexString();
            byte[] convertedBytes = hexString.FromHexString();

            Assert.Equal(originalBytes, convertedBytes);
        }

        [Fact]
        public void ReturnNullWhenHexidecimalConvertingNull()
        {
            string nullString = null;
            byte[] nullBytes = null;

            string str = nullBytes.ToHexString();
            byte[] bytes = nullString.FromHexString();

            Assert.Null(str);
            Assert.Null(bytes);
        }

        [Theory]
        [InvalidHexidecimalLengthTestData]
        public void ThrowsWhenConvertingHexidecimalStringWhoseLengthIsNotMultipleOfTwo(string value)
        {
            Assert.Throws<FormatException>(() => value.FromHexString());
        }

        [Theory]
        [InvalidHexidecimalFormatTestData]
        public void ThrowsWhenConvertingHexidecimalStringWhoseFormatIsInvalid(string value)
        {
            Assert.Throws<FormatException>(() => value.FromHexString());
        }

        [Theory]
        [ByteToStringTestData]
        public void ConvertIEnumerableToAndFromHexidecimalCorrectly(byte[] originalBytes)
        {
            IEnumerable<byte[]> enumerable = Enumerable.Repeat(originalBytes, 10);
            List<string> hexString = enumerable.ToHexString().ToList();
            List<byte[]> convertedBytes = hexString.FromHexString().ToList();

            Assert.Equal(enumerable, convertedBytes);
        }

        [Theory]
        [InvalidHexidecimalLengthTestData]
        public void ThrowsWhenConvertingIEnumerableHexidecimalStringWhoseLengthIsNotMultipleOfTwo(string value)
        {
            IEnumerable<string> enumerable = Enumerable.Repeat(value, 10);
            Assert.Throws<FormatException>(() => enumerable.FromHexString().ToList());
        }

        [Theory]
        [InvalidHexidecimalFormatTestData]
        public void ThrowsWhenConvertingIEnumerableHexidecimalStringWhoseFormatIsInvalid(string value)
        {
            IEnumerable<string> enumerable = Enumerable.Repeat(value, 10);
            Assert.Throws<FormatException>(() => enumerable.FromHexString().ToList());
        }

        private class InvalidHexidecimalFormatTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { "FACE0000" }; // does not begin with '0x'
                yield return new object[] { "0xCABBAGE" }; // contains a non hexidecimal character 'G'
            }
        }

        public class InvalidHexidecimalLengthTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { "0xA" };
                yield return new object[] { "0x123" };
                yield return new object[] { "0xCABBA6E" };
                yield return new object[] { "0x1234567890ABCDEF0" };
                yield return new object[] { "0x" + new string(Enumerable.Repeat('F', 1999).ToArray()) };
            }
        }

        public class InvalidBase64FormatTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { "a#cd" }; // contains a non-base-64 character '#'
                yield return new object[] { "abcde===" }; // contains more than two padding characters
                yield return new object[] { "abcd   c" }; // a non-white space-character among the padding characters.
            }
        }

        public class InvalidBase64LengthTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { "a" };
                yield return new object[] { "ab" };
                yield return new object[] { "abc" };
                yield return new object[] { "abcde" };
                yield return new object[] { "abcdefghijklmnopqrstuvwxyz" };
            }
        }

        public class ByteToStringTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { new byte[] { } };
                yield return new object[] { new byte[] { 0 } };
                yield return new object[] { new byte[] { 0, 19, 28, 3, 74 } };
                yield return new object[] { new byte[] { 26, 60, 114, 103, 139, 37, 229, 66, 170, 179, 244, 229, 233, 102, 44, 186, 234, 9, 5, 211, 216, 143, 103, 144, 252, 254, 96, 111, 233, 1, 149, 240 } };
                yield return new object[] { Enumerable.Repeat((byte)255, 1000).ToArray() };
            }
        }

        public class EncryptionDecryptionTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { true };
                yield return new object[] { (byte)178 };
                yield return new object[] { (sbyte)-118 };
                yield return new object[] { '"' };
                // yield return new object[] { 0.369852147M }; xUnit bug Ref: https://github.com/xunit/xunit/issues/1771 Reinstate when decimal works with generic xUnit test methods.
                yield return new object[] { 10.369852147 };
                yield return new object[] { 10.369852147f };
                yield return new object[] { -987 };
                yield return new object[] { 654U };
                yield return new object[] { -999654L };
                yield return new object[] { 654UL };
                yield return new object[] { (short)-42 };
                yield return new object[] { (ushort)11552 };
                yield return new object[] { "In C#, Equals(String, String) is a String method. It is used to determine whether two String objects have the same value or not. Basically, it checks for equality. If both strings have the same value, it returns true otherwise returns false. This method is different from Compare and CompareTo methods. This method compares two string on the basis of contents." };
                yield return new object[] { DateTime.Today };
                yield return new object[] { DateTimeOffset.UtcNow };
                yield return new object[] { DateTimeOffset.UtcNow };
                yield return new object[] { Guid.Parse("BA5EC0D3-D15C-D0C5-B105-D3CAFFC0FF33") };
                yield return new object[] { TimeSpan.FromDays(99) };
            }
        }

        public class EncryptionDecryptionNullableTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { typeof(bool?), false };
                yield return new object[] { typeof(bool?), null };
                yield return new object[] { typeof(byte?), (byte)77 };
                yield return new object[] { typeof(byte?), null };
                yield return new object[] { typeof(byte[]), new byte[] { 21, 54, 78, 89, 65, 32 } };
                yield return new object[] { typeof(byte[]), null };
                yield return new object[] { typeof(sbyte?), (sbyte)-77 };
                yield return new object[] { typeof(sbyte?), null };
                yield return new object[] { typeof(char?), 'f' };
                yield return new object[] { typeof(char?), null };
                yield return new object[] { typeof(decimal?), 3698.52147M };
                yield return new object[] { typeof(decimal?), null };
                yield return new object[] { typeof(double?), 987.456 };
                yield return new object[] { typeof(double?), null };
                yield return new object[] { typeof(float?), -3.141592654f };
                yield return new object[] { typeof(float?), null };
                yield return new object[] { typeof(int?), 5647849 };
                yield return new object[] { typeof(int?), null };
                yield return new object[] { typeof(uint?), 15961052U };
                yield return new object[] { typeof(uint?), null };
                yield return new object[] { typeof(long?), 789456123L };
                yield return new object[] { typeof(long?), null };
                yield return new object[] { typeof(ulong?), 36928534715826392UL };
                yield return new object[] { typeof(ulong?), null };
                yield return new object[] { typeof(short?), (short)897 };
                yield return new object[] { typeof(short?), null };
                yield return new object[] { typeof(ushort?), (ushort)7415 };
                yield return new object[] { typeof(ushort?), null };
                yield return new object[] { typeof(string), "passionfruit" };
                yield return new object[] { typeof(string), null };
                yield return new object[] { typeof(DateTime?), DateTime.Now };
                yield return new object[] { typeof(DateTime?), null };
                yield return new object[] { typeof(DateTimeOffset?), DateTimeOffset.Now };
                yield return new object[] { typeof(DateTimeOffset?), null };
                yield return new object[] { typeof(Guid?), Guid.Parse("4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f") };
                yield return new object[] { typeof(Guid?), null };
                yield return new object[] { typeof(TimeSpan?), TimeSpan.FromSeconds(978) };
                yield return new object[] { typeof(TimeSpan?), null };
            }
        }
    }
}
