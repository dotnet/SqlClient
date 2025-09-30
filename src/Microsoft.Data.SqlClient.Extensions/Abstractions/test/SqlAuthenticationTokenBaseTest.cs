namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationTokenBaseTest
{
    // Derive from SqlAuthenticationTokenBase to test the abstract class'
    // functionality.
    private class Token : SqlAuthenticationTokenBase
    {
        public Token(string accessToken, DateTimeOffset expiresOn)
        : base(accessToken, expiresOn)
        {
        }
    }

    // Verify that the properties are set correctly.
    [Fact]
    public void Constructor_Success()
    {
        var token = "test";
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        Token authToken = new(token, expiry);

        Assert.Equal(token, authToken.AccessToken);
        Assert.Equal(expiry, authToken.ExpiresOn);
    }
}
