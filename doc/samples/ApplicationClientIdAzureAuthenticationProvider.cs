//<Snippet1>
using System;
using Microsoft.Data.SqlClient;

namespace ApplicationClientIdAzureAuthenticationProvider
{
    public class Program
    {
        public static void Main()
        {
            // Disabling WAM requires your own app registration, not SqlClient's first-party app ID.
            ActiveDirectoryAuthenticationProvider provider = new ActiveDirectoryAuthenticationProvider(
                new ActiveDirectoryAuthenticationProviderOptions
                {
                    ApplicationClientId = "<application_client_id>",
                    UseWamBroker = false
                });
            if (provider.IsSupported(SqlAuthenticationMethod.ActiveDirectoryInteractive))
            {
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, provider);
            }
            
            using (SqlConnection sqlConnection = new SqlConnection("Server=<myserver>.database.windows.net;Authentication=Active Directory Interactive;Database=<db>;"))
            {
                sqlConnection.Open();
                Console.WriteLine("Connected successfully!");
            }
        }
    }
}
//</Snippet1>
