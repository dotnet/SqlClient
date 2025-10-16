namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationTokenTest
{
    // Verify that the properties are set correctly.
    [Fact]
    public void Constructor()
    {
        var token = "test";
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        SqlAuthenticationToken authToken = new(token, expiry);

        Assert.Equal(token, authToken.AccessToken);
        Assert.Equal(expiry, authToken.ExpiresOn);
    }

    // Verify that a null token is rejected.
    [Fact]
    public void Constructor_NullToken()
    {
        string? token = null;
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        var ex = Assert.Throws<SqlAuthenticationToken.TokenException>(() =>
        {
            new SqlAuthenticationToken(token!, expiry);
        });
    
        Assert.Equal("AccessToken must not be null or empty.", ex.Message);
    }

    // Verify that an empty token is rejected.
    [Fact]
    public void Constructor_EmptyToken()
    {
        string token = string.Empty;
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        var ex = Assert.Throws<SqlAuthenticationToken.TokenException>(() =>
        {
            new SqlAuthenticationToken(token, expiry);
        });
    
        Assert.Equal("AccessToken must not be null or empty.", ex.Message);
    }
}
