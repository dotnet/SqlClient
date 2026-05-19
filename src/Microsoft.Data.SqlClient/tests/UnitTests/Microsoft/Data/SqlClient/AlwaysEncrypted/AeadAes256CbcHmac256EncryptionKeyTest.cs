// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.AlwaysEncrypted;
using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted;

/// <summary>
/// Unit tests to verify that AeadAes256CbcHmac256EncryptionKey's
/// cryptographic and validation logic behave correctly.
/// </summary>
public class AeadAes256CbcHmac256EncryptionKeyTest
{
    /// <summary>
    /// Verifies that if the AeadAes256CbcHmac256EncryptionKey is
    /// constructed with a root key of incorrect length, it throws
    /// an exception.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnInvalidSize()
    {
        byte[] invalidSizeRootKey = [0x01, 0x02, 0x03, 0x04];
        Action createEncryptionKey = () => new AeadAes256CbcHmac256EncryptionKey(invalidSizeRootKey);

        Assert.NotEqual(AeadAes256CbcHmac256EncryptionKey.KeySizeInBytes, invalidSizeRootKey.Length);
        Assert.Throws<ArgumentException>(createEncryptionKey);
    }

    /// <summary>
    /// Verifies that if the AeadAes256CbcHmac256EncryptionKey is
    /// constructed with a valid root key, it successfully generates
    /// an encryption key, MAC key and IV key.
    /// </summary>
    [Fact]
    public void Constructor_SucceedsOnValidSize()
    {
        byte[] validRootKey = [
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08
        ];
        AeadAes256CbcHmac256EncryptionKey encryptionKey = new(validRootKey);

        Assert.Equal(AeadAes256CbcHmac256EncryptionKey.KeySizeInBytes, encryptionKey.EncryptionKey.Length);
        Assert.Equal(AeadAes256CbcHmac256EncryptionKey.KeySizeInBytes, encryptionKey.MacKey.Length);
        Assert.Equal(AeadAes256CbcHmac256EncryptionKey.KeySizeInBytes, encryptionKey.IvKey.Length);
    }
}
