// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.AlwaysEncrypted;
using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted;

/// <summary>
/// Unit tests to verify that AeadAes256CbcHmac256Factory's
/// caching and validation logic behave correctly.
/// </summary>
public class AeadAes256CbcHmac256FactoryTest
{
    /// <summary>
    /// Verifies that if called with an invalid encryption type,
    /// Create will throw.
    /// </summary>
    [Fact]
    public void InvalidEncryptionType_Throws()
    {
        byte[] dummySymmetricKeyMaterial = [0x00];
        SymmetricKey symmetricKey = new(dummySymmetricKeyMaterial);
        Action createEncryptionAlgorithm = () =>
            AeadAes256CbcHmac256Factory.Instance.Create(symmetricKey, (EncryptionType)0xFF, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName);

        Assert.Throws<ArgumentException>(createEncryptionAlgorithm);
    }

    /// <summary>
    /// Verifies that if called twice with the same root key,
    /// Create will return the same algorithm instance.
    /// </summary>
    [Fact]
    public void MultipleCreateCalls_ReturnCachedAlgorithm()
    {
        byte[] validFirstRootKeyMaterial = [
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08
        ];
        byte[] validSecondRootKeyMaterial = [
            0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
            0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
            0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
            0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01
        ];
        SymmetricKey firstRootKey = new(validFirstRootKeyMaterial);
        SymmetricKey secondRootKey = new(validSecondRootKeyMaterial);

        SqlClientEncryptionAlgorithm initialFirstAlgorithm = AeadAes256CbcHmac256Factory.Instance.Create(firstRootKey, EncryptionType.Deterministic, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName);
        SqlClientEncryptionAlgorithm cachedFirstAlgorithm = AeadAes256CbcHmac256Factory.Instance.Create(firstRootKey, EncryptionType.Deterministic, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName);
        SqlClientEncryptionAlgorithm initialSecondAlgorithm = AeadAes256CbcHmac256Factory.Instance.Create(secondRootKey, EncryptionType.Deterministic, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName);

        Assert.Equal(initialFirstAlgorithm, cachedFirstAlgorithm);
        Assert.NotEqual(initialFirstAlgorithm, initialSecondAlgorithm);

        Assert.IsType<SqlAeadAes256CbcHmac256Algorithm>(initialFirstAlgorithm);
        Assert.IsType<SqlAeadAes256CbcHmac256Algorithm>(initialSecondAlgorithm);
    }
}
