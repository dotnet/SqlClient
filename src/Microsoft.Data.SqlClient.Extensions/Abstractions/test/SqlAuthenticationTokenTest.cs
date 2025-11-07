// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationTokenTest
{
    #region Tests

    /// <summary>
    /// Verify that the properties are set correctly.
    /// </summary>
    [Fact]
    public void Constructor_ValidArguments()
    {
        var token = "test";
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        SqlAuthenticationToken authToken = new(token, expiry);

        Assert.Equal(token, authToken.AccessToken);
        Assert.Equal(expiry, authToken.ExpiresOn);
    }

    /// <summary>
    /// Verify that a null token is rejected.
    /// </summary>
    [Fact]
    public void Constructor_InvalidArguments_NullToken()
    {
        string? token = null;
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        var ex = Assert.ThrowsAny<SqlAuthenticationProviderException>(() =>
        {
            new SqlAuthenticationToken(token!, expiry);
        });
    
        Assert.Equal("AccessToken must not be null or empty.", ex.Message);
    }

    /// <summary>
    /// Verify that an empty token is rejected.
    /// </summary>
    [Fact]
    public void Constructor_InvalidArguments_EmptyToken()
    {
        string token = string.Empty;
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        var ex = Assert.ThrowsAny<SqlAuthenticationProviderException>(() =>
        {
            new SqlAuthenticationToken(token, expiry);
        });
    
        Assert.Equal("AccessToken must not be null or empty.", ex.Message);
    }

    #endregion
}
