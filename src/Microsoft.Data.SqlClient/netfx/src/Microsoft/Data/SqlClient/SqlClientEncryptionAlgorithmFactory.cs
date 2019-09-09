// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Abstract base class for all TCE encryption algorithm factory classes. Factory classes create instances of an encryption algorithm
    /// with a given key. At runtime when we determine a particular column is marked for TCE, based on the encryption algorithm we invoke 
    /// the corresponding factory class and retrieve an object to an encryption algorithm.
    /// </summary>
    internal abstract class SqlClientEncryptionAlgorithmFactory
    {
        /// <summary>
        /// Creates an encrytion algorithm with a given key.
        /// </summary>
        /// <param name="encryptionKey"></param>
        /// <param name="encryptionType"></param>
        /// <param name="encryptionAlgorithm"></param>
        /// <returns></returns>
        internal abstract SqlClientEncryptionAlgorithm Create(SqlClientSymmetricKey encryptionKey, SqlClientEncryptionType encryptionType, string encryptionAlgorithm);
    }
}
