//<Snippet1>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;

namespace CustomDeviceCodeFlowAuthProviderExample
{
    /// <summary>
    /// Example demonstrating creating a custom device code flow authentication provider and attaching it to the driver.
    /// This is helpful for applications that wish to override the Callback for the Device Code Result implemented by the SqlClient driver.
    /// </summary>
    public class CustomDeviceCodeFlowAzureAuthenticationProvider : SqlAuthenticationProvider
    {
        private const string ClientId = "my-client-id";
        private const string ClientName = "My Application Name";
        private const string DefaultScopeSuffix = "/.default";

        // Maintain a copy of the PublicClientApplication object to cache the underlying access tokens it provides
        private static IPublicClientApplication pcApplication;

        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            string[] scopes = [ parameters.Resource.EndsWith(DefaultScopeSuffix) ? parameters.Resource : parameters.Resource + DefaultScopeSuffix ];

            IPublicClientApplication app = pcApplication;
            if (app == null)
            {
                pcApplication = app = PublicClientApplicationBuilder.Create(ClientId)
                    .WithAuthority(parameters.Authority)
                    .WithClientName(ClientName)
                    .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                    .Build();
            }

            AuthenticationResult result;
            using CancellationTokenSource connectionTimeoutCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(parameters.ConnectionTimeout));

            try
            {
                IEnumerable<IAccount> accounts = await app.GetAccountsAsync();
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync(connectionTimeoutCancellation.Token);
            }
            catch (MsalUiRequiredException)
            {
                result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeResult => CustomDeviceFlowCallback(deviceCodeResult))
                    .ExecuteAsync(connectionTimeoutCancellation.Token);
            }

            return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
            => authenticationMethod.Equals(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow);

        private static Task CustomDeviceFlowCallback(DeviceCodeResult result)
        {
            Console.WriteLine(result.Message);
            return Task.CompletedTask;
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
