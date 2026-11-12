// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;
using static Microsoft.Data.SqlClient.UnitTests.Utilities.EncryptionInfoTestData;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Unit tests for <see cref="SqlConnection"/> members that do not need a server.
/// </summary>
public class SqlConnectionTests
{
    /// <summary>
    /// Verifies the getter returns a non-null empty snapshot on a new connection.
    /// </summary>
    [Fact]
    public void EncryptionInfo_NewConnection_ReturnsEmptySnapshot()
    {
        // Arrange
        using SqlConnection connection = new();

        // Act
        SqlConnectionEncryptionInfo info = connection.EncryptionInfo;

        // Assert
        AssertEmpty(info);
    }

    /// <summary>
    /// Verifies the getter returns a non-null empty snapshot on a connection with a connection
    /// string that has not been opened.
    /// </summary>
    [Fact]
    public void EncryptionInfo_UnopenedWithConnectionString_ReturnsEmptySnapshot()
    {
        // Arrange
        using SqlConnection connection = new("Data Source=localhost;Encrypt=Strict");

        // Act
        SqlConnectionEncryptionInfo info = connection.EncryptionInfo;

        // Assert
        AssertEmpty(info);
    }

    /// <summary>
    /// Verifies the getter returns a non-null empty snapshot after the connection is closed.
    /// </summary>
    [Fact]
    public void EncryptionInfo_ClosedConnection_ReturnsEmptySnapshot()
    {
        // Arrange
        using SqlConnection connection = new("Data Source=localhost");
        connection.Close();

        // Act
        SqlConnectionEncryptionInfo info = connection.EncryptionInfo;

        // Assert
        AssertEmpty(info);
    }

    /// <summary>
    /// Verifies the getter does not throw and returns a non-null empty snapshot after the
    /// connection is disposed.
    /// </summary>
    [Fact]
    public void EncryptionInfo_DisposedConnection_ReturnsEmptySnapshot()
    {
        // Arrange
        SqlConnection connection = new("Data Source=localhost");
        connection.Dispose();

        // Act
        SqlConnectionEncryptionInfo info = connection.EncryptionInfo;

        // Assert
        AssertEmpty(info);
    }
}
