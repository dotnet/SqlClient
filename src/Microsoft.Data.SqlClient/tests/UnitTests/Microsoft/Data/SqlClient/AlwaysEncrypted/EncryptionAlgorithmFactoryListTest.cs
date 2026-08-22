// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.AlwaysEncrypted;
using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted;

/// <summary>
/// Unit tests to verify the validation logic within EncryptionAlgorithmFactoryList.
/// </summary>
public class EncryptionAlgorithmFactoryListTest
{
    /// <summary>
    /// Validates that if an unknown Always Encrypted algorithm name is specified,
    /// GetAlgorithm throws.
    /// </summary>
    [Fact]
    public void GetAlgorithmWithInvalidAlgorithm_Throws()
    {
        const string InvalidAlgorithmName = nameof(EncryptionAlgorithmFactoryListTest);

        byte[] dummySymmetricKeyMaterial = [0x00];
        SymmetricKey dummySymmetricKey = new(dummySymmetricKeyMaterial);
        Action getAlgorithm = () => EncryptionAlgorithmFactoryList.GetAlgorithm(dummySymmetricKey, 0x01, InvalidAlgorithmName, out _);

        ArgumentException thrownException = Assert.Throws<ArgumentException>(getAlgorithm);

        Assert.Contains(InvalidAlgorithmName, thrownException.Message);
        Assert.Contains(EncryptionAlgorithmFactoryList.RegisteredCipherAlgorithmNames, thrownException.Message);
    }
}
