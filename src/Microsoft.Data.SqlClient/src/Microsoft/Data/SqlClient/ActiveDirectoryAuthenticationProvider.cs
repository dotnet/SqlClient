// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ActiveDirectoryAuthenticationProvider/*'/>
    public sealed class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
    {
        /// <summary>
        /// This is a static cache instance meant to hold instances of "PublicClientApplication" mapping to information available in PublicClientAppKey.
        /// The purpose of this cache is to allow re-use of Access Tokens fetched for a user interactively or with any other mode
        /// to avoid interactive authentication request every-time, within application scope making use of MSAL's userTokenCache.
        /// </summary>
        private static ConcurrentDictionary<PublicClientAppKey, IPublicClientApplication> s_pcaMap
            = new ConcurrentDictionary<PublicClientAppKey, IPublicClientApplication>();
        private static readonly string s_nativeClientRedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        private static readonly string s_defaultScopeSuffix = "/.default";
        private readonly string _type = typeof(ActiveDirectoryAuthenticationProvider).Name;
        private readonly SqlClientLogger _logger = new SqlClientLogger();
        private Func<DeviceCodeResult, Task> _deviceCodeFlowCallback;
        private ICustomWebUi _customWebUI = null;
        private readonly string _applicationClientId = ActiveDirectoryAuthentication.AdoClientId;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor/*'/>
        public ActiveDirectoryAuthenticationProvider() => new ActiveDirectoryAuthenticationProvider(DefaultDeviceFlowCallback);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor2/*'/>
        public ActiveDirectoryAuthenticationProvider(string applicationClientId) => new ActiveDirectoryAuthenticationProvider(DefaultDeviceFlowCallback, applicationClientId);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor3/*'/>
        public ActiveDirectoryAuthenticationProvider(Func<DeviceCodeResult, Task> deviceCodeFlowCallbackMethod, string applicationClientId = null)
        {
            if (applicationClientId != null)
            {
                _applicationClientId = applicationClientId;
            }
            SetDeviceCodeFlowCallback(deviceCodeFlowCallbackMethod);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ClearUserTokenCache/*'/>
        public static void ClearUserTokenCache()
        {
            if (!s_pcaMap.IsEmpty)
            {
                s_pcaMap.Clear();
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetDeviceCodeFlowCallback/*'/>
        public void SetDeviceCodeFlowCallback(Func<DeviceCodeResult, Task> deviceCodeFlowCallbackMethod) => _deviceCodeFlowCallback = deviceCodeFlowCallbackMethod;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetAcquireAuthorizationCodeAsyncCallback/*'/>
        public void SetAcquireAuthorizationCodeAsyncCallback(Func<Uri, Uri, CancellationToken, Task<Uri>> acquireAuthorizationCodeAsyncCallback) => _customWebUI = new CustomWebUi(acquireAuthorizationCodeAsyncCallback);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/IsSupported/*'/>
        public override bool IsSupported(SqlAuthenticationMethod authentication)
        {
            return authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated
                || authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
                || authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                || authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                || authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                || authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                || authentication == SqlAuthenticationMethod.ActiveDirectoryMSI
                || authentication == SqlAuthenticationMethod.ActiveDirectoryDefault;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/BeforeLoad/*'/>
        public override void BeforeLoad(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeLoad", $"being loaded into SqlAuthProviders for {authentication}.");
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/BeforeUnload/*'/>
        public override void BeforeUnload(SqlAuthenticationMethod authentication)
        {
            _logger.LogInfo(_type, "BeforeUnload", $"being unloaded from SqlAuthProviders for {authentication}.");
        }

#if NETSTANDARD
        private Func<object> _parentActivityOrWindowFunc = null;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetParentActivityOrWindowFunc/*'/>
        public void SetParentActivityOrWindowFunc(Func<object> parentActivityOrWindowFunc) => this._parentActivityOrWindowFunc = parentActivityOrWindowFunc;
#endif

#if NETFRAMEWORK
        private Func<System.Windows.Forms.IWin32Window> _iWin32WindowFunc = null;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetIWin32WindowFunc/*'/>
        public void SetIWin32WindowFunc(Func<System.Windows.Forms.IWin32Window> iWin32WindowFunc) => this._iWin32WindowFunc = iWin32WindowFunc;
#endif

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/AcquireTokenAsync/*'/>
        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            // Use Connection timeout value to cancel token acquire request after certain period of time.
            cts.CancelAfter(parameters.ConnectionTimeout * 1000); // Convert to milliseconds

            string scope = parameters.Resource.EndsWith(s_defaultScopeSuffix) ? parameters.Resource : parameters.Resource + s_defaultScopeSuffix;
            string[] scopes = new string[] { scope };

            int seperatorIndex = parameters.Authority.LastIndexOf('/');
            string tenantId = parameters.Authority.Substring(seperatorIndex + 1);
            string authority = parameters.Authority.Remove(seperatorIndex + 1);

            TokenRequestContext tokenRequestContext = new TokenRequestContext(scopes);
            string clientId = string.IsNullOrWhiteSpace(parameters.UserId) ? null : parameters.UserId;

            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDefault)
            {
                DefaultAzureCredentialOptions defaultAzureCredentialOptions = new DefaultAzureCredentialOptions()
                {
                    AuthorityHost = new Uri(authority),
                    ManagedIdentityClientId = clientId,
                    InteractiveBrowserTenantId = tenantId,
                    SharedTokenCacheTenantId = tenantId,
                    SharedTokenCacheUsername = clientId,
                    VisualStudioCodeTenantId = tenantId,
                    VisualStudioTenantId = tenantId,
                    ExcludeInteractiveBrowserCredential = true // Force disabled, even though it's disabled by default to respect driver specifications.
                };
                AccessToken accessToken = await new DefaultAzureCredential(defaultAzureCredentialOptions).GetTokenAsync(tokenRequestContext, cts.Token);
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Default auth mode. Expiry Time: {0}", accessToken.ExpiresOn);
                return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
            }

            TokenCredentialOptions tokenCredentialOptions = new TokenCredentialOptions() { AuthorityHost = new Uri(authority) };

            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity || parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryMSI)
            {
                AccessToken accessToken = await new ManagedIdentityCredential(clientId, tokenCredentialOptions).GetTokenAsync(tokenRequestContext, cts.Token);
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Managed Identity auth mode. Expiry Time: {0}", accessToken.ExpiresOn);
                return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
            }

            AuthenticationResult result;
            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)
            {
                AccessToken accessToken = await new ClientSecretCredential(tenantId, parameters.UserId, parameters.Password, tokenCredentialOptions).GetTokenAsync(tokenRequestContext, cts.Token);
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Active Directory Service Principal auth mode. Expiry Time: {0}", accessToken.ExpiresOn);
                return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
            }

            /*
             * Today, MSAL.NET uses another redirect URI by default in desktop applications that run on Windows
             * (urn:ietf:wg:oauth:2.0:oob). In the future, we'll want to change this default, so we recommend
             * that you use https://login.microsoftonline.com/common/oauth2/nativeclient.
             *
             * https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-desktop-app-registration#redirect-uris
             */
            string redirectUri = s_nativeClientRedirectUri;

#if NETCOREAPP
            if (parameters.AuthenticationMethod != SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
            {
                redirectUri = "http://localhost";
            }
#endif
            PublicClientAppKey pcaKey = new PublicClientAppKey(parameters.Authority, redirectUri, _applicationClientId
#if NETFRAMEWORK
            , _iWin32WindowFunc
#endif
#if NETSTANDARD
            , _parentActivityOrWindowFunc
#endif
                );

            IPublicClientApplication app = GetPublicClientAppInstance(pcaKey);

            if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
            {
                if (!string.IsNullOrEmpty(parameters.UserId))
                {
                    result = await app.AcquireTokenByIntegratedWindowsAuth(scopes)
                        .WithCorrelationId(parameters.ConnectionId)
                        .WithUsername(parameters.UserId)
                        .ExecuteAsync(cancellationToken: cts.Token);
                }
                else
                {
                    result = await app.AcquireTokenByIntegratedWindowsAuth(scopes)
                        .WithCorrelationId(parameters.ConnectionId)
                        .ExecuteAsync(cancellationToken: cts.Token);
                }
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Active Directory Integrated auth mode. Expiry Time: {0}", result?.ExpiresOn);
            }
            else if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryPassword)
            {
                SecureString password = new SecureString();
                foreach (char c in parameters.Password)
                    password.AppendChar(c);
                password.MakeReadOnly();

                result = await app.AcquireTokenByUsernamePassword(scopes, parameters.UserId, password)
                    .WithCorrelationId(parameters.ConnectionId)
                    .ExecuteAsync(cancellationToken: cts.Token);
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Active Directory Password auth mode. Expiry Time: {0}", result?.ExpiresOn);
            }
            else if (parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive ||
                     parameters.AuthenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
            {
                // Fetch available accounts from 'app' instance
                System.Collections.Generic.IEnumerator<IAccount> accounts = (await app.GetAccountsAsync()).GetEnumerator();
                
                IAccount account = default;
                if (accounts.MoveNext())
                {
                    if (!string.IsNullOrEmpty(parameters.UserId))
                    {
                        do
                        {
                            IAccount currentVal = accounts.Current;
                            if (string.Compare(parameters.UserId, currentVal.Username, StringComparison.InvariantCultureIgnoreCase) == 0)
                            {
                                account = currentVal;
                                break;
                            }
                        }
                        while (accounts.MoveNext());
                    }
                    else
                    {
                        account = accounts.Current;
                    }
                }

                if (null != account)
                {
                    try
                    {
                        // If 'account' is available in 'app', we use the same to acquire token silently.
                        // Read More on API docs: https://docs.microsoft.com/dotnet/api/microsoft.identity.client.clientapplicationbase.acquiretokensilent
                        result = await app.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken: cts.Token);
                        SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token (silent) for {0} auth mode. Expiry Time: {1}", parameters.AuthenticationMethod, result?.ExpiresOn);
                    }
                    catch (MsalUiRequiredException)
                    {
                        // An 'MsalUiRequiredException' is thrown in the case where an interaction is required with the end user of the application,
                        // for instance, if no refresh token was in the cache, or the user needs to consent, or re-sign-in (for instance if the password expired),
                        // or the user needs to perform two factor authentication.
                        result = await AcquireTokenInteractiveDeviceFlowAsync(app, scopes, parameters.ConnectionId, parameters.UserId, parameters.AuthenticationMethod, cts);
                        SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token (interactive) for {0} auth mode. Expiry Time: {1}", parameters.AuthenticationMethod, result?.ExpiresOn);
                    }
                }
                else
                {
                    // If no existing 'account' is found, we request user to sign in interactively.
                    result = await AcquireTokenInteractiveDeviceFlowAsync(app, scopes, parameters.ConnectionId, parameters.UserId, parameters.AuthenticationMethod, cts);
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token (interactive) for {0} auth mode. Expiry Time: {1}", parameters.AuthenticationMethod, result?.ExpiresOn);
                }
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | {0} authentication mode not supported by ActiveDirectoryAuthenticationProvider class.", parameters.AuthenticationMethod);
                throw SQL.UnsupportedAuthenticationSpecified(parameters.AuthenticationMethod);
            }

            return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
        }

        private async Task<AuthenticationResult> AcquireTokenInteractiveDeviceFlowAsync(IPublicClientApplication app, string[] scopes, Guid connectionId, string userId,
            SqlAuthenticationMethod authenticationMethod, CancellationTokenSource cts)
        {
            try
            {
                if (authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive)
                {
                    CancellationTokenSource ctsInteractive = new CancellationTokenSource();
#if NETCOREAPP
                    /*
                     * On .NET Core, MSAL will start the system browser as a separate process. MSAL does not have control over this browser,
                     * but once the user finishes authentication, the web page is redirected in such a way that MSAL can intercept the Uri.
                     * MSAL cannot detect if the user navigates away or simply closes the browser. Apps using this technique are encouraged
                     * to define a timeout (via CancellationToken). We recommend a timeout of at least a few minutes, to take into account
                     * cases where the user is prompted to change password or perform 2FA.
                     *
                     * https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/System-Browser-on-.Net-Core#system-browser-experience
                     */
                    ctsInteractive.CancelAfter(180000);
#endif
                    if (_customWebUI != null)
                    {
                        return await app.AcquireTokenInteractive(scopes)
                            .WithCorrelationId(connectionId)
                            .WithCustomWebUi(_customWebUI)
                            .WithLoginHint(userId)
                            .ExecuteAsync(ctsInteractive.Token);
                    }
                    else
                    {
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
                        return await app.AcquireTokenInteractive(scopes)
                            .WithCorrelationId(connectionId)
                            .WithLoginHint(userId)
                            .ExecuteAsync(ctsInteractive.Token);
                    }
                }
                else
                {
                    AuthenticationResult result = await app.AcquireTokenWithDeviceCode(scopes,
                        deviceCodeResult => _deviceCodeFlowCallback(deviceCodeResult))
                        .WithCorrelationId(connectionId)
                        .ExecuteAsync(cancellationToken: cts.Token);
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenInteractiveDeviceFlowAsync | Operation timed out while acquiring access token.");
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
            SqlClientEventSource.Log.TryTraceEvent("AcquireTokenInteractiveDeviceFlowAsync | Callback triggered with Device Code Result: {0}", result.Message);
            Console.WriteLine(result.Message);
            return Task.FromResult(0);
        }

        private class CustomWebUi : ICustomWebUi
        {
            private readonly Func<Uri, Uri, CancellationToken, Task<Uri>> _acquireAuthorizationCodeAsyncCallback;

            internal CustomWebUi(Func<Uri, Uri, CancellationToken, Task<Uri>> acquireAuthorizationCodeAsyncCallback) => _acquireAuthorizationCodeAsyncCallback = acquireAuthorizationCodeAsyncCallback;

            public Task<Uri> AcquireAuthorizationCodeAsync(Uri authorizationUri, Uri redirectUri, CancellationToken cancellationToken)
                => _acquireAuthorizationCodeAsyncCallback.Invoke(authorizationUri, redirectUri, cancellationToken);
        }

        private IPublicClientApplication GetPublicClientAppInstance(PublicClientAppKey publicClientAppKey)
        {
            if (!s_pcaMap.TryGetValue(publicClientAppKey, out IPublicClientApplication clientApplicationInstance))
            {
                clientApplicationInstance = CreateClientAppInstance(publicClientAppKey);
                s_pcaMap.TryAdd(publicClientAppKey, clientApplicationInstance);
            }
            return clientApplicationInstance;
        }

        private IPublicClientApplication CreateClientAppInstance(PublicClientAppKey publicClientAppKey)
        {
            IPublicClientApplication publicClientApplication;

#if NETSTANDARD
            if (_parentActivityOrWindowFunc != null)
            {
                publicClientApplication = PublicClientApplicationBuilder.Create(publicClientAppKey._applicationClientId)
                .WithAuthority(publicClientAppKey._authority)
                .WithClientName(Common.DbConnectionStringDefaults.ApplicationName)
                .WithClientVersion(Common.ADP.GetAssemblyVersion().ToString())
                .WithRedirectUri(publicClientAppKey._redirectUri)
                .WithParentActivityOrWindow(_parentActivityOrWindowFunc)
                .Build();
            }
#endif
#if NETFRAMEWORK
            if (_iWin32WindowFunc != null)
            {
                publicClientApplication = PublicClientApplicationBuilder.Create(publicClientAppKey._applicationClientId)
                .WithAuthority(publicClientAppKey._authority)
                .WithClientName(Common.DbConnectionStringDefaults.ApplicationName)
                .WithClientVersion(Common.ADP.GetAssemblyVersion().ToString())
                .WithRedirectUri(publicClientAppKey._redirectUri)
                .WithParentActivityOrWindow(_iWin32WindowFunc)
                .Build();
            }
#endif
#if !NETCOREAPP
            else
#endif
            {
                publicClientApplication = PublicClientApplicationBuilder.Create(publicClientAppKey._applicationClientId)
                .WithAuthority(publicClientAppKey._authority)
                .WithClientName(Common.DbConnectionStringDefaults.ApplicationName)
                .WithClientVersion(Common.ADP.GetAssemblyVersion().ToString())
                .WithRedirectUri(publicClientAppKey._redirectUri)
                .Build();
            }

            return publicClientApplication;
        }

        internal class PublicClientAppKey
        {
            public readonly string _authority;
            public readonly string _redirectUri;
            public readonly string _applicationClientId;
#if NETFRAMEWORK
            public readonly Func<System.Windows.Forms.IWin32Window> _iWin32WindowFunc;
#endif
#if NETSTANDARD
            public readonly Func<object> _parentActivityOrWindowFunc;
#endif

            public PublicClientAppKey(string authority, string redirectUri, string applicationClientId
#if NETFRAMEWORK
            , Func<System.Windows.Forms.IWin32Window> iWin32WindowFunc
#endif
#if NETSTANDARD
            , Func<object> parentActivityOrWindowFunc
#endif
                )
            {
                _authority = authority;
                _redirectUri = redirectUri;
                _applicationClientId = applicationClientId;
#if NETFRAMEWORK
                _iWin32WindowFunc = iWin32WindowFunc;
#endif
#if NETSTANDARD
                _parentActivityOrWindowFunc = parentActivityOrWindowFunc;
#endif
            }

            public override bool Equals(object obj)
            {
                if (obj != null && obj is PublicClientAppKey pcaKey)
                {
                    return (string.CompareOrdinal(_authority, pcaKey._authority) == 0
                        && string.CompareOrdinal(_redirectUri, pcaKey._redirectUri) == 0
                        && string.CompareOrdinal(_applicationClientId, pcaKey._applicationClientId) == 0
#if NETFRAMEWORK
                        && pcaKey._iWin32WindowFunc == _iWin32WindowFunc
#endif
#if NETSTANDARD
                        && pcaKey._parentActivityOrWindowFunc == _parentActivityOrWindowFunc
#endif
                    );
                }
                return false;
            }

            public override int GetHashCode() => Tuple.Create(_authority, _redirectUri, _applicationClientId
#if NETFRAMEWORK
                , _iWin32WindowFunc
#endif
#if NETSTANDARD
                , _parentActivityOrWindowFunc
#endif
                ).GetHashCode();
        }
    }
}
