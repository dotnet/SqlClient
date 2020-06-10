// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// Default auth provider for AD Integrated.
    /// </summary>
    internal class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        private static readonly string s_defaultScopeSuffix = "/.default";
        private readonly string _type = typeof(ActiveDirectoryAuthenticationProvider).Name;
        private readonly SqlClientLogger _logger = new SqlClientLogger();

        /// <summary>
        /// Get Token
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns>Authentication token</returns>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
            AcquireTokenAsync(parameters, DefaultDeviceFlowCallback);

        /// <summary>
        /// Get token.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="deviceCodeResultCallback"></param>
        /// <returns>Authentication token</returns>
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters, Func<DeviceCodeResult, Task> deviceCodeResultCallback) => Task.Run(async () =>
        {
            AuthenticationResult result;
            string scope = parameters.Resource.EndsWith(s_defaultScopeSuffix) ? parameters.Resource : parameters.Resource + s_defaultScopeSuffix;
            string[] scopes = new string[] { scope };

            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)
            {
                IConfidentialClientApplication ccApp = ConfidentialClientApplicationBuilder.Create(parameters.UserId)
                    .WithAuthority(parameters.Authority)
                    .WithClientSecret(parameters.Password)
                    .WithClientName(Common.DbConnectionStringDefaults.ApplicationName)
                    .WithClientVersion(Common.ADP.GetAssemblyVersion().ToString())
                    .Build();

                result = ccApp.AcquireTokenForClient(scopes).ExecuteAsync().Result;
                return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
            }

            /* 
             * Today, MSAL.NET uses another redirect URI by default in desktop applications that run on Windows
             * (urn:ietf:wg:oauth:2.0:oob). In the future, we'll want to change this default, so we recommend
             * that you use https://login.microsoftonline.com/common/oauth2/nativeclient.
             * 
             * https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-desktop-app-registration#redirect-uris
             */
            string redirectURI = "https://login.microsoftonline.com/common/oauth2/nativeclient";

#if netcoreapp
            if(parameters.AuthenticationMethod != SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
            {
                redirectURI = "http://localhost";
            }
#endif
            IPublicClientApplication app = PublicClientApplicationBuilder.Create(ActiveDirectoryAuthentication.AdoClientId)
                .WithAuthority(parameters.Authority)
                .WithClientName(Common.DbConnectionStringDefaults.ApplicationName)
                .WithClientVersion(Common.ADP.GetAssemblyVersion().ToString())
                .WithRedirectUri(redirectURI)
                .Build();

            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
            {
                if (!string.IsNullOrEmpty(parameters.UserId))
                {
                    result = app.AcquireTokenByIntegratedWindowsAuth(scopes)
                        .WithCorrelationId(parameters.ConnectionId)
                        .WithUsername(parameters.UserId)
                        .ExecuteAsync().Result;
                }
                else
                {
                    result = app.AcquireTokenByIntegratedWindowsAuth(scopes)
                        .WithCorrelationId(parameters.ConnectionId)
                        .ExecuteAsync().Result;
                }
            }
            else if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryPassword)
            {
                SecureString password = new SecureString();
                foreach (char c in parameters.Password)
                    password.AppendChar(c);
                password.MakeReadOnly();
                result = app.AcquireTokenByUsernamePassword(scopes, parameters.UserId, password)
                    .WithCorrelationId(parameters.ConnectionId)
                    .ExecuteAsync().Result;
            }
            else if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive ||
                     parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
            {
                var accounts = await app.GetAccountsAsync();
                IAccount account;
                if (!string.IsNullOrEmpty(parameters.UserId))
                {
                    account = accounts.FirstOrDefault(a => parameters.UserId.Equals(a.Username, System.StringComparison.InvariantCultureIgnoreCase));
                }
                else
                {
                    account = accounts.FirstOrDefault();
                }

                if (null != account)
                {
                    try
                    {
                        result = await app.AcquireTokenSilent(scopes, account).ExecuteAsync();
                    }
                    catch (MsalUiRequiredException)
                    {
                        result = await AcquireTokenInteractiveDeviceFlow(app, scopes, parameters.ConnectionId, parameters.UserId, parameters.AuthenticationMethod, deviceCodeResultCallback);
                    }
                }
                else
                {
                    result = await AcquireTokenInteractiveDeviceFlow(app, scopes, parameters.ConnectionId, parameters.UserId, parameters.AuthenticationMethod, deviceCodeResultCallback);
                }
            }
            else
            {
                throw SQL.UnsupportedAuthenticationSpecified(parameters.AuthenticationMethod);
            }

            return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
        });

        private async Task<AuthenticationResult> AcquireTokenInteractiveDeviceFlow(IPublicClientApplication app, string[] scopes, Guid connectionId, string userId,
            SqlAuthenticationMethod authenticationMethod, Func<DeviceCodeResult, Task> deviceCodeResultCallback)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
#if netcoreapp
            /*
             * On .NET Core, MSAL will start the system browser as a separate process. MSAL does not have control over this browser,
             * but once the user finishes authentication, the web page is redirected in such a way that MSAL can intercept the Uri.
             * MSAL cannot detect if the user navigates away or simply closes the browser. Apps using this technique are encouraged
             * to define a timeout (via CancellationToken). We recommend a timeout of at least a few minutes, to take into account
             * cases where the user is prompted to change password or perform 2FA.
             * 
             * https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/System-Browser-on-.Net-Core#system-browser-experience
             */
            cts.CancelAfter(180000);
#endif
            try
            {
                if (authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive)
                {
                    return await app.AcquireTokenInteractive(scopes)
                        /*
                         * We will use the MSAL Embedded or System web browser which changes by Default in MSAL according to this table:
                         * 
                         * Framework        Embedded  System  Default
                         * -------------------------------------------
                         * .NET Classic     Yes       Yes^    Embedded
                         * .NET Core        No        Yes^    System
                         * .NET Standard    No        No      NONE
                         * UWP              Yes       No      Embedded
                         * Xamarin.Android  Yes       Yes     System
                         * Xamarin.iOS      Yes       Yes     System
                         * Xamarin.Mac      Yes       No      Embedded
                         * 
                         * ^ Requires "http://localhost" redirect URI
                         * 
                         * https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/MSAL.NET-uses-web-browser#at-a-glance
                         */
                        //.WithUseEmbeddedWebView(true)
                        .WithCorrelationId(connectionId)
                        .WithLoginHint(userId)
                        .ExecuteAsync(cts.Token);
                }
                else
                {
                    AuthenticationResult result = await app.AcquireTokenWithDeviceCode(scopes,
                    deviceCodeResult => deviceCodeResultCallback(deviceCodeResult)).ExecuteAsync();
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw (authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive) ?
                    SQL.ActiveDirectoryInteractiveTimeout() :
                    SQL.ActiveDirectoryDeviceFlowTimeout();
            }
        }

        private Task DefaultDeviceFlowCallback(DeviceCodeResult result)
        {
            // This will print the message on the console which tells the user where to go sign-in using 
            // a separate browser and the code to enter once they sign in.
            // The AcquireTokenWithDeviceCode() method will poll the server after firing this
            // device code callback to look for the successful login of the user via that browser.
            // This background polling (whose interval and timeout data is also provided as fields in the 
            // deviceCodeCallback class) will occur until:
            // * The user has successfully logged in via browser and entered the proper code
            // * The timeout specified by the server for the lifetime of this code (typically ~15 minutes) has been reached
            // * The developing application calls the Cancel() method on a CancellationToken sent into the method.
            //   If this occurs, an OperationCanceledException will be thrown (see catch below for more details).
            Console.WriteLine(result.Message);
            return Task.FromResult(0);
        }

        /// <summary>
        /// Checks support for authentication type in lower case.
        /// Interactive authentication added.
        /// </summary>
        public override bool IsSupported(SqlAuthenticationMethod authentication)
        {
            return authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated
                || authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
                || authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                || authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                || authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
        }

        public override void BeforeLoad(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeLoad", $"being loaded into SqlAuthProviders for {authentication}.");
        }

        public override void BeforeUnload(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeUnload", $"being unloaded from SqlAuthProviders for {authentication}.");
        }
    }
}
