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
        string connectionString = GetConnectionString();
        using (SqlConnection connection = new SqlConnection("Data Source=contoso.database.windows.net;Initial Catalog=AdventureWorks;")
        {
            AccessTokenCallback = async (authParams, cancellationToken) =>
            {
                var cred = new DefaultAzureCredential();
                string scope = authParams.Resource.EndsWith(s_defaultScopeSuffix) ? authParams.Resource : authParams.Resource + s_defaultScopeSuffix;
                var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken);
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
