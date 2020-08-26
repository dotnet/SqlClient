//<Snippet1>
using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Data.SqlClient;

namespace CustomAuthenticationProviderExamples
{
    public class Program
    {
        public static void Main()
        {
            SqlAuthenticationProvider authProvider = new ActiveDirectoryAuthenticationProvider(CustomDeviceFlowCallback);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, authProvider);
            using (SqlConnection sqlConnection = new SqlConnection("Server=<myserver>.database.windows.net;Authentication=Active Directory Device Code Flow;Database=<db>;"))
            {
                sqlConnection.Open();
                Console.WriteLine("Connected successfully!");
            }
        }

        private static Task CustomDeviceFlowCallback(DeviceCodeResult result)
        {
            // Provide custom logic to process result information and read device code.
            Console.WriteLine(result.Message);
            return Task.FromResult(0);
        }
    }
}
//</Snippet1>
