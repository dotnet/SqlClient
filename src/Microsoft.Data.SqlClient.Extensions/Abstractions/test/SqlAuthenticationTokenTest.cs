// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies token construction preserves values and reports localized validation errors.</summary>
public class SqlAuthenticationTokenTest
{
    /// <summary>
    /// Verify that the properties are set correctly.
    /// </summary>
    [Fact]
    public void Constructor_ValidArguments()
    {
        // Arrange
        var token = "test";
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        SqlAuthenticationToken authToken = new(token, expiry);

        // Assert
        Assert.Equal(token, authToken.AccessToken);
        Assert.Equal(expiry, authToken.ExpiresOn);
    }

    /// <summary>
    /// Verify that a null token is rejected.
    /// </summary>
    [Fact]
    public void Constructor_NullToken_ThrowsTokenException()
    {
        // Arrange
        string? token = null;
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var ex = Assert.Throws<SqlAuthenticationToken.TokenException>(() =>
        {
            new SqlAuthenticationToken(token!, expiry);
        });

        // Assert
        Assert.Equal(AuthenticationStrings.Format("SQL_EmptyAccessToken"), ex.Message);
        Assert.Null(ex.InnerException);
    }

    /// <summary>
    /// Verify that an empty token is rejected.
    /// </summary>
    [Fact]
    public void Constructor_EmptyToken_ThrowsTokenException()
    {
        // Arrange
        string token = string.Empty;
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var ex = Assert.Throws<SqlAuthenticationToken.TokenException>(() =>
        {
            new SqlAuthenticationToken(token, expiry);
        });

        // Assert
        Assert.Equal(AuthenticationStrings.Format("SQL_EmptyAccessToken"), ex.Message);
        Assert.Null(ex.InnerException);
    }
}
