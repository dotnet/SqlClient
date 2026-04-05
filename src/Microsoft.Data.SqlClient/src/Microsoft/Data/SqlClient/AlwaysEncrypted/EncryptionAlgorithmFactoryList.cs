// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted;

/// <summary>
/// Implements a global mapping from a SQL Server cryptographic algorithm's name to its implementation.
/// </summary>
internal static class EncryptionAlgorithmFactoryList
{
    /// <summary>
    /// Get the available list of algorithms as a comma separated list with algorithm names
    /// wrapped in single quotes.
    /// </summary>
    public const string RegisteredCipherAlgorithmNames = $"'{SqlAeadAes256CbcHmac256Algorithm.AlgorithmName}'";

    /// <summary>
    /// Gets the implementation for a given algorithm and instantiates it using the provided root key and the encryption type.
    /// </summary>
    /// <param name="key">The root key to use.</param>
    /// <param name="type">Encryption type (read from SQL Server.)</param>
    /// <param name="algorithmName">Name of the cryptographic algorithm.</param>
    /// <param name="encryptionAlgorithm">Specified cryptographic algorithm's implementation.</param>
    public static void GetAlgorithm(SqlClientSymmetricKey key, byte type, string algorithmName, out SqlClientEncryptionAlgorithm encryptionAlgorithm)
    {
        EncryptionAlgorithmFactory factory = algorithmName switch
        {
            SqlAeadAes256CbcHmac256Algorithm.AlgorithmName => SqlAeadAes256CbcHmac256Factory.Instance,
            _ => throw SQL.UnknownColumnEncryptionAlgorithm(algorithmName, RegisteredCipherAlgorithmNames)
        };

        encryptionAlgorithm = factory.Create(key, (SqlClientEncryptionType)type, algorithmName);
    }
}
