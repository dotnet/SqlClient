// <Snippet1>
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
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

    // Reuse credential objects to take advantage of underlying token caches.
    private static ConcurrentDictionary<string, DefaultAzureCredential> credentials = new ConcurrentDictionary<string, DefaultAzureCredential>();

    // Use a shared callback function for connections that should be in the same connection pool.
    private static Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> myAccessTokenCallback =
        async (authParams, cancellationToken) =>
        {
            string scope = authParams.Resource.EndsWith(defaultScopeSuffix)
                ? authParams.Resource
                : $"{authParams.Resource}{defaultScopeSuffix}";

            DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions();
            options.ManagedIdentityClientId = authParams.UserId;

            // Reuse the same credential object if we are using the same MI Client Id.
            AccessToken token = await credentials.GetOrAdd(authParams.UserId, new DefaultAzureCredential(options)).GetTokenAsync(
                new TokenRequestContext(new string[] { scope }),
                cancellationToken);

            return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
        };

    private static void OpenSqlConnection()
    {
        // (Optional) Pass a User-Assigned Managed Identity Client ID.
        // This will ensure different MI Client IDs are in different connection pools.
        string connectionString = "Server=myServer.database.windows.net;Encrypt=Mandatory;UserId=<ManagedIdentitityClientId>;";

        using (SqlConnection connection = new SqlConnection(connectionString)
        {
            // The callback function is part of the connection pool key. Using a static callback function
            // ensures connections will not create a new pool per connection just for the callback.
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
