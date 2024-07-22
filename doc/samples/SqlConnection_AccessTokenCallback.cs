using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;
using Azure.Identity;

class Program
{
    static void Main()
    {
        OpenSqlConnection();
        Console.ReadLine();
    }

    private static void OpenSqlConnection()
    {
        const string defaultScopeSuffix = "/.default";
        string connectionString = GetConnectionString();
        DefaultAzureCredential credential = new();

        using (SqlConnection connection = new(connectionString)
        {
            AccessTokenCallback = async (authParams, cancellationToken) =>
            {
                string scope = authParams.Resource.EndsWith(defaultScopeSuffix)
                    ? authParams.Resource
                    : $"{authParams.Resource}{defaultScopeSuffix}";
                AccessToken token = await credential.GetTokenAsync(
                    new TokenRequestContext([scope]),
                    cancellationToken);

                return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
            }
        })
        {
            connection.Open();
            Console.WriteLine("ServerVersion: {0}", connection.ServerVersion);
            Console.WriteLine("State: {0}", connection.State);
        }
    }
}
// </Snippet1>
