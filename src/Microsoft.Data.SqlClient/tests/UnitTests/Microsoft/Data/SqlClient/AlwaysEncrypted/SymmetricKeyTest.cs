// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.AlwaysEncrypted;
using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted;

/// <summary>
/// Unit tests to verify that the SymmetricKey wrapper class
/// and its validation logic behave correctly.
/// </summary>
public class SymmetricKeyTest
{
    /// <summary>
    /// Verifies that the wrapper class wraps the key material passed
    /// to it, rather than copies it.
    /// </summary>
    [Fact]
    public void Constructor_WrapsByteArray()
    {
        byte[] dummySymmetricKeyMaterial = [0x00];
        SymmetricKey symmetricKey = new(dummySymmetricKeyMaterial);

        Assert.Same(dummySymmetricKeyMaterial, symmetricKey.RootKey);
    }

    /// <summary>
    /// Verifies that the wrapper class throws an exception when passed
    /// a null or a zero-length array.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullOrEmptyArray()
    {
        Action createNullArray = () => new SymmetricKey(rootKey: null);
        Action createEmptyArray = () => new SymmetricKey(rootKey: []);

        Assert.Throws<ArgumentNullException>(createNullArray);
        Assert.Throws<ArgumentNullException>(createEmptyArray);
    }
}
