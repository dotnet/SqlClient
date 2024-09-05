//<Snippet1>
using System;
using Microsoft.Data.SqlClient;

namespace CustomAuthenticationProviderExamples
{
    public class Program
    {
        public static void Main()
        {
            // Supported for all authentication modes supported by ActiveDirectoryAuthenticationProvider
            ActiveDirectoryAuthenticationProvider provider = new ActiveDirectoryAuthenticationProvider("<application_client_id>");
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
