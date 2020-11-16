using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// Extension methods for data protection.
    /// </summary>
    public static class CryptographyExtensions
    {
        private const string hexPrefix = "0x";

        /// <summary>
        /// Encrypts the provided <paramref name="plaintext"/> value using the provided <see cref="DataEncryptionKey"/>.
        /// </summary>
        /// <typeparam name="T">The <paramref name="plaintext"/> value <see cref="Type"/>.</typeparam>
        /// <param name="plaintext">The plaintext value to encrypt.</param>
        /// <param name="encryptionKey">The key used to encrypt the <paramref name="plaintext"/> value.</param>
        /// <returns>The encrypted <paramref name="plaintext"/> value.</returns>
        /// <remarks>
        /// This method encrypts using <see cref="EncryptionType.Randomized"/> encryption and the 
        /// default serializer registerd under type <typeparamref name="T"/> with the <see cref="StandardSerializerFactory"/>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="encryptionKey"/> is null.</exception>
        public static byte[] Encrypt<T>(this T plaintext, DataEncryptionKey encryptionKey)
        {
            encryptionKey.ValidateNotNull(nameof(encryptionKey));

            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, EncryptionType.Randomized);
            Serializer<T> serializer = StandardSerializerFactory.Default.GetDefaultSerializer<T>();
            byte[] serializedData = serializer.Serialize(plaintext);
            return encryptionAlgorithm.Encrypt(serializedData);
        }

        /// <summary>
        /// Decrypts the provided <paramref name="ciphertext"/> value using the provided <see cref="DataEncryptionKey"/>.
        /// </summary>
        /// <typeparam name="T">The plaintext value <see cref="Type"/>.</typeparam>
        /// <param name="ciphertext">The encrypted value.</param>
        /// <param name="encryptionKey">The key used to decrypt the <paramref name="ciphertext"/> value.</param>
        /// <returns>The decrypted <paramref name="ciphertext"/> value.</returns>
        /// <remarks>
        /// This method decrypts data that was encrypted using <see cref="EncryptionType.Randomized"/> encryption and the 
        /// default serializer registerd under type <typeparamref name="T"/> with the <see cref="StandardSerializerFactory"/>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="encryptionKey"/> is null.</exception>
        public static T Decrypt<T>(this byte[] ciphertext, DataEncryptionKey encryptionKey)
        {
            encryptionKey.ValidateNotNull(nameof(encryptionKey));

            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, EncryptionType.Randomized);
            Serializer<T> serializer = StandardSerializerFactory.Default.GetDefaultSerializer<T>();
            byte[] plaintextData = encryptionAlgorithm.Decrypt(ciphertext);
            return serializer.Deserialize(plaintextData);
        }

        /// <summary>
        /// Encrypts the provided <paramref name="plaintext"/> value using the provided <see cref="EncryptionSettings{T}"/>.
        /// </summary>
        /// <typeparam name="T">The <paramref name="plaintext"/> value <see cref="Type"/>.</typeparam>
        /// <param name="plaintext">The plaintext value to encrypt.</param>
        /// <param name="encryptionSettings">The settings used to configure how the <paramref name="plaintext"/> should be encrypted.</param>
        /// <returns>The encrypted <paramref name="plaintext"/> value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="encryptionSettings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="encryptionSettings"/> <see cref="EncryptionType"/> is set to <see cref="EncryptionType.Plaintext"/>.</exception>
        public static byte[] Encrypt<T>(this T plaintext, EncryptionSettings<T> encryptionSettings)
        {
            encryptionSettings.ValidateNotNull(nameof(encryptionSettings));
            encryptionSettings.ValidateNotPlaintext(nameof(encryptionSettings));

            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionSettings.DataEncryptionKey, encryptionSettings.EncryptionType);
            ISerializer serializer = encryptionSettings.GetSerializer();
            byte[] serializedData = serializer.Serialize(plaintext);
            return encryptionAlgorithm.Encrypt(serializedData);
        }

        /// <summary>
        /// Decrypts the provided <paramref name="ciphertext"/> value using the provided <see cref="EncryptionSettings{T}"/>.
        /// </summary>
        /// <typeparam name="T">The plaintext value <see cref="Type"/>.</typeparam>
        /// <param name="ciphertext">The encrypted value.</param>
        /// <param name="encryptionSettings">The settings used to configure how the <paramref name="ciphertext"/> should be decrypted.</param>
        /// <returns>The decrypted <paramref name="ciphertext"/> value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="encryptionSettings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="encryptionSettings"/> <see cref="EncryptionType"/> is set to <see cref="EncryptionType.Plaintext"/>.</exception>
        public static T Decrypt<T>(this byte[] ciphertext, EncryptionSettings<T> encryptionSettings)
        {
            encryptionSettings.ValidateNotNull(nameof(encryptionSettings));
            encryptionSettings.ValidateNotPlaintext(nameof(encryptionSettings));

            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionSettings.DataEncryptionKey, EncryptionType.Plaintext);
            Serializer<T> serializer = encryptionSettings.Serializer;
            byte[] plaintextData = encryptionAlgorithm.Decrypt(ciphertext);
            return serializer.Deserialize(plaintextData);
        }

        /// <summary>
        /// Encrypts each plaintext value of a sequence using the provided <see cref="DataEncryptionKey"/>.
        /// </summary>
        /// <typeparam name="T">The type of the plaintext elements of <paramref name="source"/>.</typeparam>
        /// <param name="source">A sequence of values to encrypt.</param>
        /// <param name="encryptionKey">The key used to encrypt the plaintext values of <paramref name="source"/>.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="T:Byte[]"/> whose elements are the result of encrypting each element of <paramref name="source"/>.</returns>
        /// <remarks>
        /// This method encrypts using <see cref="EncryptionType.Randomized"/> encryption and the 
        /// default serializer registerd under type <typeparamref name="T"/> with the <see cref="StandardSerializerFactory"/>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="encryptionKey"/> is null.</exception>
        public static IEnumerable<byte[]> Encrypt<T>(this IEnumerable<T> source, DataEncryptionKey encryptionKey)
        {
            encryptionKey.ValidateNotNull(nameof(encryptionKey));

            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, EncryptionType.Randomized);
            Serializer<T> serializer = StandardSerializerFactory.Default.GetDefaultSerializer<T>();

            foreach (T item in source)
            {
                byte[] serializedData = serializer.Serialize(item);
                yield return encryptionAlgorithm.Encrypt(serializedData);
            }
        }

        /// <summary>
        /// Decrypts each ciphertext value of a sequence using the provided <see cref="DataEncryptionKey"/>.
        /// </summary>
        /// <typeparam name="T">The type of the plaintext elements of <paramref name="source"/></typeparam>
        /// <param name="source">A sequence of encrypted values to decrypt.</param>
        /// <param name="encryptionKey">The key used to decrypt the ciphertext values of <paramref name="source"/>.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> whose elements are the result of decrypting each element of <paramref name="source"/>.</returns>
        /// <remarks>
        /// This method decrypts data that was encrypted using <see cref="EncryptionType.Randomized"/> encryption and the 
        /// default serializer registerd under type <typeparamref name="T"/> with the <see cref="StandardSerializerFactory"/>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="encryptionKey"/> is null.</exception>
        public static IEnumerable<T> Decrypt<T>(this IEnumerable<byte[]> source, DataEncryptionKey encryptionKey)
        {
            encryptionKey.ValidateNotNull(nameof(encryptionKey));

            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionKey, EncryptionType.Randomized);
            Serializer<T> serializer = StandardSerializerFactory.Default.GetDefaultSerializer<T>();
            StandardSerializerFactory myFactory = new StandardSerializerFactory();
            myFactory.RegisterSerializer(typeof(string), new SqlNcharSerializer());

            foreach (byte[] item in source)
            {
                byte[] plaintextData = encryptionAlgorithm.Decrypt(item);
                yield return serializer.Deserialize(plaintextData);
            }
        }

        /// <summary>
        /// Encrypts each plaintext value of a sequence using the provided <see cref="EncryptionSettings{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the plaintext elements of <paramref name="source"/>.</typeparam>
        /// <param name="source">A sequence of values to encrypt.</param>
        /// <param name="encryptionSettings">The settings used to configure how the values of <paramref name="source"/> should be encrypted.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="T:Byte[]"/> whose elements are the result of encrypting each element of <paramref name="source"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="encryptionSettings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="encryptionSettings"/> <see cref="EncryptionType"/> is set to <see cref="EncryptionType.Plaintext"/>.</exception>
        public static IEnumerable<byte[]> Encrypt<T>(this IEnumerable<T> source, EncryptionSettings<T> encryptionSettings)
        {
            encryptionSettings.ValidateNotNull(nameof(encryptionSettings));
            encryptionSettings.ValidateNotPlaintext(nameof(encryptionSettings));

            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionSettings.DataEncryptionKey, encryptionSettings.EncryptionType);
            Serializer<T> serializer = encryptionSettings.Serializer;

            foreach (T item in source)
            {
                byte[] serializedData = serializer.Serialize(item);
                yield return encryptionAlgorithm.Encrypt(serializedData);
            }
        }

        /// <summary>
        /// Decrypts each ciphertext value of a sequence using the provided <see cref="EncryptionSettings{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the plaintext elements of <paramref name="source"/>.</typeparam>
        /// <param name="source">A sequence of values to decrypt.</param>
        /// <param name="encryptionSettings">The settings used to configure how the values of <paramref name="source"/> should be decrypted.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> whose elements are the result of decrypting each element of <paramref name="source"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="encryptionSettings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="encryptionSettings"/> <see cref="EncryptionType"/> is set to <see cref="EncryptionType.Plaintext"/>.</exception>
        public static IEnumerable<T> Decrypt<T>(this IEnumerable<byte[]> source, EncryptionSettings<T> encryptionSettings)
        {
            encryptionSettings.ValidateNotNull(nameof(encryptionSettings));
            encryptionSettings.ValidateNotPlaintext(nameof(encryptionSettings));

            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(encryptionSettings.DataEncryptionKey, EncryptionType.Plaintext);
            Serializer<T> serializer = encryptionSettings.Serializer;

            foreach (byte[] item in source)
            {
                byte[] plaintextData = encryptionAlgorithm.Decrypt(item);
                yield return serializer.Deserialize(plaintextData);
            }
        }

        /// <summary>
        /// Converts an array bytes to its equivalent string representation that is encoded with base-64 digits.
        /// </summary>
        /// <param name="source">An array of bytes.</param>
        /// <returns>The string representation, in base 64, of the contents of <paramref name="source"/>.</returns>
        public static string ToBase64String(this byte[] source) => source.IsNull() ? null : Convert.ToBase64String(source);

        /// <summary>
        /// Converts the specified string, which encodes binary data as base-64 digits, to an equivalent byte array.
        /// </summary>
        /// <param name="source">The string to convert.</param>
        /// <returns>An array of bytes that is equivalent to <paramref name="source"/>.</returns>
        /// <exception cref="FormatException">
        /// The length of <paramref name="source"/>, ignoring white-space characters, is not zero or a multiple of
        /// 4. -or- The format of <paramref name="source"/> is invalid. <paramref name="source"/> contains a non-base-64 character, more
        /// than two padding characters, or a non-white space-character among the padding characters.
        /// </exception>
        public static byte[] FromBase64String(this string source) => source.IsNull() ? null : Convert.FromBase64String(source);

        /// <summary>
        /// Converts each byte array in the <paramref name="source"/> sequance to its equivalent string representation that is encoded with base-64 digits.
        /// </summary>
        /// <param name="source">A sequence of byte arrays to convert.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="string"/> whose elements are the result of being encoded with base-64 digits.</returns>
        public static IEnumerable<string> ToBase64String(this IEnumerable<byte[]> source)
        {
            foreach (byte[] item in source)
            {
                yield return ToBase64String(item);
            }
        }

        /// <summary>
        /// Converts each <see cref="string"/> element of <paramref name="source"/>, which encodes binary data as base-64 digits, to an equivalent byte array.
        /// </summary>
        /// <param name="source">A sequence of strings to convert.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of byte arrays that is equivalent to <paramref name="source"/>.</returns>
        /// <exception cref="FormatException">
        /// The length of a <paramref name="source"/> element, ignoring white-space characters, is not zero or a multiple of
        /// 4. -or- The format of a <paramref name="source"/> element is invalid. <paramref name="source"/> element contains a non-base-64 character, more
        /// than two padding characters, or a non-white space-character among the padding characters.
        /// </exception>
        public static IEnumerable<byte[]> FromBase64String(this IEnumerable<string> source)
        {
            foreach (string item in source)
            {
                yield return FromBase64String(item);
            }
        }

        /// <summary>
        /// Converts the numeric value of each element of a specified array of bytes to its equivalent hexadecimal string representation.
        /// </summary>
        /// <param name="source">An array of bytes to convert.</param>
        /// <returns>A string of hexadecimal characters</returns>
        /// <remarks>
        /// Produces a string of hexadecimal characterspairs preceded with "0x", where each pair represents the corresponding element in value; for example, "0x7F2C4A00".
        /// </remarks>
        public static string ToHexString(this byte[] source)
        {
            if (source.IsNull())
            {
                return null;
            }

            return hexPrefix + BitConverter.ToString(source).Replace("-", "");
        }

        /// <summary>
        /// Converts the string representation of a number in hexidecimal to an equivalent array of bytes.
        /// </summary>
        /// <param name="source">The string to convert.</param>
        /// <returns>An array of bytes that is equivalent to <paramref name="source"/>.</returns>
        /// <exception cref="FormatException">
        /// The length of <paramref name="source"/> is not a multiple of 2, the <paramref name="source"/> 
        /// contains a hexidecimal character, or does not begin with the literal prefix '0x'.
        /// </exception>
        public static byte[] FromHexString(this string source)
        {
            return source.IsNull() ? null : ConvertToBytes(source);

            byte[] ConvertToBytes(string hex)
            {
                if (hex.Length < 2 || hex.Substring(0, 2) != hexPrefix || hex.Length % 2 != 0)
                {
                    throw new FormatException("The input is not a valid Hexidecimal string as it contains a non hexidecimal character, is not a multiple of 2 characters, or does not begin with '0x'.");
                }

                hex = hex.Substring(2);

                byte[] bytes = new byte[hex.Length / 2];

                for (int i = 0; i < hex.Length; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                }

                return bytes;
            }
        }

        /// <summary>
        /// Converts each byte array in the <paramref name="source"/> sequance to its equivalent string representation that is encoded with hexidecimal digits.
        /// </summary>
        /// <param name="source">A sequence of byte arrays to convert.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="string"/> whose elements are the result of being encoded with hexidecimal digits.</returns>
        public static IEnumerable<string> ToHexString(this IEnumerable<byte[]> source)
        {
            foreach (byte[] item in source)
            {
                yield return ToHexString(item);
            }
        }

        /// <summary>
        /// Converts each <see cref="string"/> element of <paramref name="source"/>, which encodes binary data as hexidecimal digits, to an equivalent byte array.
        /// </summary>
        /// <param name="source">A sequence of strings to convert.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of byte arrays that is equivalent to <paramref name="source"/>.</returns>
        /// <exception cref="FormatException">
        /// The length of <paramref name="source"/> element is not a multiple of 2, contains a hexidecimal character, or does not begin with the literal prefix '0x'.
        /// </exception>
        public static IEnumerable<byte[]> FromHexString(this IEnumerable<string> source)
        {
            foreach (string item in source)
            {
                yield return FromHexString(item);
            }
        }
    }
}
