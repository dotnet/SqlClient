namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

public class SqlAuthenticationParametersBaseTest
{
    // Derive from SqlAuthenticationParametersBase to test the abstract class'
    // functionality.
    private class Parameters : SqlAuthenticationParametersBase
    {
        public Parameters(
            SqlAuthenticationMethod authenticationMethod,
            string serverName,
            string databaseName,
            string resource,
            string authority,
            string? userId,
            string? password,
            Guid connectionId,
            int connectionTimeout)
        : base(
            authenticationMethod,
            serverName,
            databaseName,
            resource,
            authority,
            userId,
            password,
            connectionId,
            connectionTimeout)
        {
        }
    }

    // Verify that the properties are set correctly.
    [Fact]
    public void Constructor_Success()
    {

    }
}
