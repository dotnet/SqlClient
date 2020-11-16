using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Data.Encryption.FileEncryption
{
    internal static class FileEncryptionExtensions
    {
        internal static IEnumerable<byte[]> Encrypt(this IEnumerable source, EncryptionSettings settings)
        {
            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(settings.DataEncryptionKey, settings.EncryptionType);
            ISerializer serializer = settings.GetSerializer();

            foreach (var item in source)
            {
                byte[] serializedData = serializer.Serialize(item);
                yield return encryptionAlgorithm.Encrypt(serializedData);
            }
        }

        internal static IList Decrypt(this IEnumerable source, EncryptionSettings settings)
        {
            DataProtector encryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(settings.DataEncryptionKey, settings.EncryptionType);
            ISerializer serializer = settings.GetSerializer();

            Type type = serializer.GetType().BaseType.GetGenericArguments()[0];
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));

            foreach (var item in source)
            {
                byte[] plaintextData = encryptionAlgorithm.Decrypt((byte[])item);
                list.Add(serializer.Deserialize(plaintextData));
            }

            return list;
        }

        internal static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T item in source)
            {
                action.Invoke(item);
            }
        }

        internal static Type GetGenericType(this ISerializer serializer)
        {
            Type type = serializer.GetType();

            while (type.BaseType != null)
            {
                type = type.BaseType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Serializer<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }

            throw new InvalidOperationException("Base type was not found");
        }
    }
}
