// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted
{
    /// <summary>
    /// Base class containing raw key bytes for symmetric key algorithms. Some encryption algorithms can use the key directly while others derive sub keys from this.
    /// If an algorithm needs to derive more keys, have a derived class from this and use it in the corresponding encryption algorithm.
    /// </summary>
    internal class SymmetricKey
    {
        /// <summary>
        /// Constructor that initializes the root key.
        /// </summary>
        /// <param name="rootKey">Root key</param>
        public SymmetricKey(byte[]? rootKey)
        {
            // Key validation
            if (rootKey is null || rootKey.Length == 0)
            {
                throw SQL.NullColumnEncryptionKeySysErr();
            }

            RootKey = rootKey;
        }

        /// <summary>
        /// Returns the plain text key.
        /// This is needed for actual encryption/decryption.
        /// </summary>
        public byte[] RootKey { get; }
    }
}
