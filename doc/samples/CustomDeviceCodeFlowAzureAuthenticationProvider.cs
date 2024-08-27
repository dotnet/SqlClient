//<Snippet1>
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CustomAuthenticationProviderExamples
{
    /// <summary>
    /// Example demonstrating creating a custom device code flow authentication provider and attaching it to the driver.
    /// This is helpful for applications that wish to override the Callback for the Device Code Result implemented by the SqlClient driver.
    /// </summary>
    public class CustomDeviceCodeFlowAzureAuthenticationProvider : SqlAuthenticationProvider
    {
        private const string clientId = "my-client-id";
        private const string clientName = "My Application Name";
        private const string s_defaultScopeSuffix = "/.default";

        // Maintain a copy of the PublicClientApplication object to cache the underlying access tokens it provides
        private static IPublicClientApplication pcApplication;

        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            string[] scopes = new string[] { parameters.Resource.EndsWith(s_defaultScopeSuffix) ? parameters.Resource : parameters.Resource + s_defaultScopeSuffix };

            IPublicClientApplication app = pcApplication;
            if (app == null)
            {
                pcApplication = app = PublicClientApplicationBuilder.Create(clientId)
                    .WithAuthority(parameters.Authority)
                    .WithClientName(clientName)
                    .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                .Build();
            }

            AuthenticationResult result;

            try
            {
                IEnumerable<IAccount> accounts = await app.GetAccountsAsync();
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                result = await app.AcquireTokenWithDeviceCode(scopes,
                        deviceCodeResult => CustomDeviceFlowCallback(deviceCodeResult)).ExecuteAsync();
            }

            return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => authenticationMethod.Equals(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow);

        private static Task<int> CustomDeviceFlowCallback(DeviceCodeResult result)
        {
            Console.WriteLine(result.Message);
            return Task.FromResult(0);
        }
    }

    public class Program
    {
        public static void Main()
        {
            // Register our custom authentication provider class to override Active Directory Device Code Flow
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, new CustomDeviceCodeFlowAzureAuthenticationProvider());
            using (SqlConnection sqlConnection = new SqlConnection("Server=<myserver>.database.windows.net;Authentication=Active Directory Device Code Flow;Database=<db>;"))
            {
                sqlConnection.Open();
                Console.WriteLine("Connected successfully!");
            }
        }
    }
}
//</Snippet1>
