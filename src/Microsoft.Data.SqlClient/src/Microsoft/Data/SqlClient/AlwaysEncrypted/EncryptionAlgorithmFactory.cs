// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted;

/// <summary>
/// Abstract base class for all TCE cryptographic algorithm factory classes. Factory classes create instances of a cryptographic algorithm
/// with a given key. At runtime when we determine a particular column is marked for TCE, based on the cryptographic algorithm we invoke 
/// the corresponding factory class and retrieve an implementation of an cryptographic algorithm.
/// </summary>
internal abstract class EncryptionAlgorithmFactory
{
    /// <summary>
    /// Creates a cryptographic algorithm with a given key.
    /// </summary>
    /// <param name="encryptionKey">Root key that should be passed to the cryptographic algorithm to be created</param>
    /// <param name="encryptionType">Encryption Type, some algorithms will need this</param>
    /// <param name="encryptionAlgorithm">Cryptographic algorithm name. Needed for extracting version bits</param>
    /// <returns>Return a newly created SqlClientEncryptionAlgorithm instance</returns>
    internal abstract SqlClientEncryptionAlgorithm Create(SqlClientSymmetricKey encryptionKey, SqlClientEncryptionType encryptionType, string encryptionAlgorithm);
}
