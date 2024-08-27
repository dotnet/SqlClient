// <Snippet1>
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        OpenSqlConnection();
        Console.ReadLine();
    }

    const string defaultScopeSuffix = "/.default";
    // Reuse the credential object to take advantage of the underlying token cache
    private static DefaultAzureCredential credential = new();

    // Use a shared callback function for connections that should be in the same connection pool
    private static Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> myAccessTokenCallback =
        async (authParams, cancellationToken) =>
        {
            string scope = authParams.Resource.EndsWith(defaultScopeSuffix)
                ? authParams.Resource
                : $"{authParams.Resource}{defaultScopeSuffix}";
            AccessToken token = await credential.GetTokenAsync(
                new TokenRequestContext([scope]),
                cancellationToken);

            return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
        };

    private static void OpenSqlConnection()
    {
        string connectionString = GetConnectionString();

        using (SqlConnection connection = new(connectionString)
        {
            AccessTokenCallback = myAccessTokenCallback
        })
        {
            connection.Open();
            Console.WriteLine("ServerVersion: {0}", connection.ServerVersion);
            Console.WriteLine("State: {0}", connection.State);
        }
    }
}
// </Snippet1>
