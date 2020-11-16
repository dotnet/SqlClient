using Microsoft.Data.Encryption.Cryptography;
using static Microsoft.Data.Encryption.Cryptography.CryptographyExtensions;
using System;
using System.Collections.Generic;
using System.Linq;

using static Microsoft.Data.Encryption.Demo.Program;

namespace Microsoft.Data.Encryption.Demo
{
    public static class IEnumerablePipelineEncryption
    {
        public static void Demo()
        {
            // Encrypt 25 numbers from an infinite stream and print them.
            GetIntStream()
                .Take(25)
                .Encrypt(encryptionKey)
                .ToBase64String()
                .ForEach(Console.WriteLine);

            Console.Clear();
        }

        #region Helper Methods

        private static IEnumerable<int> GetIntStream()
        {
            Random random = new Random();

            while (true)
            {
                yield return random.Next(0, 20);
            }
        }

        #region ForEach
        private static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T item in source)
            {
                action.Invoke(item);
            }
        }
        #endregion

        #endregion
    }
}
