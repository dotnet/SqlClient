// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Base class containing raw key bytes for symmetric key algorithms. Some encryption algorithms can use the key directly while others derive sub keys from this.
    /// If an algorithm needs to derive more keys, have a derived class from this and use it in the corresponding encryption algorithm.
    /// </summary>
    internal class SqlClientSymmetricKey
    {
        /// <summary>
        /// The underlying key material
        /// </summary>
        protected readonly byte[] _rootKey;

        /// <summary>
        /// Constructor that initializes the root key.
        /// </summary>
        /// <param name="rootKey">root key</param>
        internal SqlClientSymmetricKey(byte[] rootKey)
        {
            // Key validation
            if (rootKey == null || rootKey.Length == 0)
            {
                throw SQL.NullColumnEncryptionKeySysErr();
            }

            _rootKey = rootKey;
        }

        /// <summary>
        /// Returns a copy of the plain text key
        /// This is needed for actual encryption/decryption.
        /// </summary>
        internal virtual byte[] RootKey
        {
            get
            {
                return _rootKey;
            }
        }

        /// <summary>
        /// Computes SHA256 value of the plain text key bytes
        /// </summary>
        /// <returns>A string containing SHA256 hash of the root key</returns>
        internal virtual string GetKeyHash()
        {
            return SqlSecurityUtility.GetSHA256Hash(RootKey);
        }

        /// <summary>
        /// Gets the length of the root key
        /// </summary>
        /// <returns>
        /// Returns the length of the root key
        /// </returns>
        internal virtual int Length()
        {
            return _rootKey.Length;
        }
    }
}
