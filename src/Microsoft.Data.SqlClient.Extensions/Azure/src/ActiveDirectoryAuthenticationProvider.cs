// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ActiveDirectoryAuthenticationProvider/*'/>
public sealed class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
{
    /// <summary>
    /// This is a static cache instance meant to hold instances of "PublicClientApplication" mapping to information available in PublicClientAppKey.
    /// The purpose of this cache is to allow re-use of Access Tokens fetched for a user interactively or with any other mode
    /// to avoid interactive authentication request every-time, within application scope making use of MSAL's userTokenCache.
    /// </summary>
    private static readonly ConcurrentDictionary<PublicClientAppKey, IPublicClientApplication> s_pcaMap = new();
    private static readonly ConcurrentDictionary<TokenCredentialKey, TokenCredentialData> s_tokenCredentialMap = new();
    private static SemaphoreSlim s_pcaMapModifierSemaphore = new(1, 1);
    private static SemaphoreSlim s_tokenCredentialMapModifierSemaphore = new(1, 1);
    private static readonly MemoryCache s_accountPwCache = new MemoryCache(new MemoryCacheOptions()); 
    private const int s_accountPwCacheTtlInHours = 2;
    private const string s_nativeClientRedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";
    private const string s_defaultScopeSuffix = "/.default";
    private readonly string _type = typeof(ActiveDirectoryAuthenticationProvider).Name;
    private readonly SqlClientLogger _logger = new();
    private Func<DeviceCodeResult, Task> _deviceCodeFlowCallback;
    private ICustomWebUi? _customWebUI = null;
    private readonly string _applicationClientId = "2fd908ad-0664-4344-b9be-cd3e8b574c38"; 

    // The MSAL error code that indicates the action should be retried.
    //
    // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/retry-after#simple-retry-for-errors-with-http-error-codes-500-600
    private const int MsalRetryStatusCode = 429;

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor/*'/>
    public ActiveDirectoryAuthenticationProvider()
        : this(DefaultDeviceFlowCallback)
    {
    }

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor2/*'/>
    public ActiveDirectoryAuthenticationProvider(string applicationClientId)
        : this(DefaultDeviceFlowCallback, applicationClientId)
    {
    }

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ctor3/*'/>
    public ActiveDirectoryAuthenticationProvider(Func<DeviceCodeResult, Task> deviceCodeFlowCallbackMethod, string? applicationClientId = null)
    {
        _deviceCodeFlowCallback = deviceCodeFlowCallbackMethod;
        if (applicationClientId is not null)
        {
            _applicationClientId = applicationClientId;
        }
    }

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/ClearUserTokenCache/*'/>
    public static void ClearUserTokenCache()
    {
        if (!s_pcaMap.IsEmpty)
        {
            s_pcaMap.Clear();
        }

        if (!s_tokenCredentialMap.IsEmpty)
        {
            s_tokenCredentialMap.Clear();
        }
    }

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetDeviceCodeFlowCallback/*'/>
    public void SetDeviceCodeFlowCallback(Func<DeviceCodeResult, Task> deviceCodeFlowCallbackMethod) => _deviceCodeFlowCallback = deviceCodeFlowCallbackMethod;

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetAcquireAuthorizationCodeAsyncCallback/*'/>
    public void SetAcquireAuthorizationCodeAsyncCallback(Func<Uri, Uri, CancellationToken, Task<Uri>> acquireAuthorizationCodeAsyncCallback) => _customWebUI = new CustomWebUi(acquireAuthorizationCodeAsyncCallback);

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/IsSupported/*'/>
    public override bool IsSupported(SqlAuthenticationMethod authentication)
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        return authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated
        #pragma warning restore CS0618 // Type or member is obsolete
            #pragma warning disable CS0618 // Type or member is obsolete
            || authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
            #pragma warning restore CS0618 // Type or member is obsolete
            || authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
            || authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
            || authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
            || authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
            || authentication == SqlAuthenticationMethod.ActiveDirectoryMSI
            || authentication == SqlAuthenticationMethod.ActiveDirectoryDefault
            || authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;
    }

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/BeforeLoad/*'/>
    public override void BeforeLoad(SqlAuthenticationMethod authentication)
    {
        _logger.LogInfo(_type, "BeforeLoad", $"being loaded into SqlAuthProviders for {authentication}.");
    }

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/BeforeUnload/*'/>
    public override void BeforeUnload(SqlAuthenticationMethod authentication)
    {
        _logger.LogInfo(_type, "BeforeUnload", $"being unloaded from SqlAuthProviders for {authentication}.");
    }

    #if NETFRAMEWORK
    private Func<System.Windows.Forms.IWin32Window> _iWin32WindowFunc = null;

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/SetIWin32WindowFunc/*'/>
    public void SetIWin32WindowFunc(Func<System.Windows.Forms.IWin32Window> iWin32WindowFunc) => this._iWin32WindowFunc = iWin32WindowFunc;
    #endif

    /// <include file='../doc/ActiveDirectoryAuthenticationProvider.xml' path='docs/members[@name="ActiveDirectoryAuthenticationProvider"]/AcquireTokenAsync/*'/>
    public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
    {
        try
        {
            using CancellationTokenSource cts = new();

            // Use the authentication timeout value to cancel token acquire
            // request after certain period of time.
            if (parameters.ConnectionTimeout > 0)
            {
                // Safely convert to milliseconds.
                if (parameters.ConnectionTimeout > int.MaxValue / 1000)
                {
                    cts.CancelAfter(int.MaxValue);
                }
                else
                {
                    cts.CancelAfter(parameters.ConnectionTimeout * 1000);
                }
            }

            string scope = parameters.Resource.EndsWith(s_defaultScopeSuffix, StringComparison.Ordinal) ? parameters.Resource : parameters.Resource + s_defaultScopeSuffix;
            string[] scopes = [scope];
            TokenRequestContext tokenRequestContext = new(scopes);

            // We split audience from Authority URL here. Audience can be one of
            // the following:
            //
            //   - The Azure AD authority audience enumeration
            //   - The tenant ID, which can be:
            //     - A GUID (the ID of your Azure AD instance), for
            //       single-tenant applications
            //     - A domain name associated with your Azure AD instance (also
            //       for single-tenant applications)
            //   - One of these placeholders as a tenant ID in place of the
            //     Azure AD authority audience enumeration:
            //     - `organizations` for a multitenant application
            //     - `consumers` to sign in users only with their personal
            //       accounts
            //     - `common` to sign in users with their work and school
            //       accounts or their personal Microsoft accounts
            //
            // MSAL will throw a meaningful exception if you specify both the
            // Azure AD authority audience and the tenant ID.
            //
            // If you don't specify an audience, your app will target Azure AD
            // and personal Microsoft accounts as an audience.  (That is, it
            // will behave as though `common` were specified.)
            //
            // More information:
            //
            //   https://docs.microsoft.com/azure/active-directory/develop/msal-client-application-configuration

            int separatorIndex = parameters.Authority.LastIndexOf('/');
            string authority = parameters.Authority.Remove(separatorIndex + 1);
            string audience = parameters.Authority.Substring(separatorIndex + 1);
            string? clientId = string.IsNullOrWhiteSpace(parameters.UserId) ? null : parameters.UserId;

            switch (parameters.AuthenticationMethod)
            {
                case SqlAuthenticationMethod.ActiveDirectoryDefault:
                {
                    // Cache DefaultAzureCredenial based on scope, authority, audience, and clientId
                    TokenCredentialKey tokenCredentialKey = new(typeof(DefaultAzureCredential), authority, scope, audience, clientId);
                    AccessToken accessToken = await GetTokenAsync(tokenCredentialKey, string.Empty, tokenRequestContext, cts.Token).ConfigureAwait(false);
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Default auth mode. Expiry Time: {0}", accessToken.ExpiresOn);
                    return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
                }

                case SqlAuthenticationMethod.ActiveDirectoryManagedIdentity:
                case SqlAuthenticationMethod.ActiveDirectoryMSI:
                {
                    // Cache ManagedIdentityCredential based on scope, authority, and clientId
                    TokenCredentialKey tokenCredentialKey = new(typeof(ManagedIdentityCredential), authority, scope, string.Empty, clientId);
                    AccessToken accessToken = await GetTokenAsync(tokenCredentialKey, string.Empty, tokenRequestContext, cts.Token).ConfigureAwait(false);
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Managed Identity auth mode. Expiry Time: {0}", accessToken.ExpiresOn);
                    return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
                }

                case SqlAuthenticationMethod.ActiveDirectoryServicePrincipal:
                {
                    // Cache ClientSecretCredential based on scope, authority, audience, and clientId
                    TokenCredentialKey tokenCredentialKey = new(typeof(ClientSecretCredential), authority, scope, audience, clientId);
                    string password = parameters.Password is null ? string.Empty : parameters.Password;
                    AccessToken accessToken = await GetTokenAsync(tokenCredentialKey, password, tokenRequestContext, cts.Token).ConfigureAwait(false);
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Active Directory Service Principal auth mode. Expiry Time: {0}", accessToken.ExpiresOn);
                    return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
                }

                case SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity:
                {
                    // Cache WorkloadIdentityCredential based on authority and clientId
                    TokenCredentialKey tokenCredentialKey = new(typeof(WorkloadIdentityCredential), authority, string.Empty, string.Empty, clientId);
                    // If either tenant id, client id, or the token file path are not specified when fetching the token,
                    // a CredentialUnavailableException will be thrown instead
                    AccessToken accessToken = await GetTokenAsync(tokenCredentialKey, string.Empty, tokenRequestContext, cts.Token).ConfigureAwait(false);
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Workload Identity auth mode. Expiry Time: {0}", accessToken.ExpiresOn);
                    return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
                }

                #pragma warning disable CS0618 // Type or member is obsolete
                case SqlAuthenticationMethod.ActiveDirectoryIntegrated:
                #pragma warning restore CS0618 // Type or member is obsolete
                {
                    IPublicClientApplication app = await GetPublicClientAppInstanceAsync(parameters, _applicationClientId, cts.Token).ConfigureAwait(false);
    
                    AuthenticationResult? cachedResult = await TryAcquireTokenSilent(app, parameters, scopes, cts).ConfigureAwait(false);

                    if (cachedResult is not null)
                    {
                        return new SqlAuthenticationToken(cachedResult.AccessToken, cachedResult.ExpiresOn);
                    }

                    // The AcquireTokenByIntegratedWindowsAuth method is marked
                    // as obsolete in MSAL.NET but it is still a supported way
                    // to acquire tokens for Active Directory Integrated
                    // authentication.
                    var builder =
                        #pragma warning disable CS0618 // Type or member is obsolete
                        app.AcquireTokenByIntegratedWindowsAuth(scopes)
                        #pragma warning restore CS0618 // Type or member is obsolete
                        .WithCorrelationId(parameters.ConnectionId);

                    if (!string.IsNullOrEmpty(parameters.UserId))
                    {
                        builder = builder.WithUsername(parameters.UserId);
                    }

                    AuthenticationResult result = await builder
                        .ExecuteAsync(cancellationToken: cts.Token)
                        .ConfigureAwait(false);

                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Active Directory Integrated auth mode. Expiry Time: {0}", result.ExpiresOn);
                    return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
                }
                #pragma warning disable CS0618 // Type or member is obsolete
                case SqlAuthenticationMethod.ActiveDirectoryPassword:
                #pragma warning restore CS0618 // Type or member is obsolete
                {
                    IPublicClientApplication app = await GetPublicClientAppInstanceAsync(parameters, _applicationClientId, cts.Token).ConfigureAwait(false);

                    string pwCacheKey = GetAccountPwCacheKey(parameters);
                    object? previousPw = s_accountPwCache.Get(pwCacheKey);
                    string password = parameters.Password is null ? string.Empty : parameters.Password;
                    byte[] currPwHash = GetHash(password);

                    if (previousPw is not null &&
                        previousPw is byte[] previousPwBytes &&
                        // Only get the cached token if the current password hash matches the previously used password hash
                        AreEqual(currPwHash, previousPwBytes))
                    {
                        AuthenticationResult? cachedResult = await TryAcquireTokenSilent(app, parameters, scopes, cts).ConfigureAwait(false);

                        if (cachedResult is not null)
                        {
                            return new SqlAuthenticationToken(cachedResult.AccessToken, cachedResult.ExpiresOn);
                        }
                    }

                    #pragma warning disable CS0618 // Type or member is obsolete
                    AuthenticationResult result = await app.AcquireTokenByUsernamePassword(scopes, parameters.UserId, parameters.Password)
                    #pragma warning restore CS0618 // Type or member is obsolete
                        .WithCorrelationId(parameters.ConnectionId)
                        .ExecuteAsync(cancellationToken: cts.Token)
                        .ConfigureAwait(false);

                    // We cache the password hash to ensure future connection requests include a validated password
                    // when we check for a cached MSAL account. Otherwise, a connection request with the same username
                    // against the same tenant could succeed with an invalid password when we re-use the cached token.
                    using (ICacheEntry entry = s_accountPwCache.CreateEntry(pwCacheKey))
                    {
                        entry.Value = currPwHash;
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(s_accountPwCacheTtlInHours);
                    }

                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token for Active Directory Password auth mode. Expiry Time: {0}", result.ExpiresOn);
                    return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
                }
                case SqlAuthenticationMethod.ActiveDirectoryInteractive:
                {
                    AuthenticationResult result = await AcquireTokenInteractiveAsync(
                        parameters,
                        _applicationClientId,
                        scopes,
                        parameters.ConnectionId,
                        parameters.UserId,
                        parameters.AuthenticationMethod,
                        cts,
                        _customWebUI,
                        _deviceCodeFlowCallback).ConfigureAwait(false);
                    return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
                }
                case SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow:
                {
                    IPublicClientApplication app = await GetPublicClientAppInstanceAsync(parameters, _applicationClientId, cts.Token).ConfigureAwait(false);

                    try
                    {
                        AuthenticationResult? cachedResult = await TryAcquireTokenSilent(app, parameters, scopes, cts).ConfigureAwait(false);

                        if (cachedResult is not null)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token (silent) for {0} auth mode. Expiry Time: {1}", parameters.AuthenticationMethod, cachedResult .ExpiresOn);
                            return new SqlAuthenticationToken(cachedResult.AccessToken, cachedResult.ExpiresOn);
                        }
                    }
                    catch (MsalUiRequiredException)
                    {
                        // An 'MsalUiRequiredException' is thrown in the case where an interaction is required with the end user of the application,
                        // for instance, if no refresh token was in the cache, or the user needs to consent, or re-sign-in (for instance if the password expired),
                        // or the user needs to perform two factor authentication.
                        //
                        // result should be null here, but we make sure of that.
                    }

                    // If no existing 'account' is found, we request user to sign in interactively.
                    AuthenticationResult result = await AcquireTokenDeviceFlowAsync(app, scopes, parameters.ConnectionId, parameters.UserId, parameters.AuthenticationMethod, cts, _customWebUI, _deviceCodeFlowCallback).ConfigureAwait(false);
                    SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token (interactive) for {0} auth mode. Expiry Time: {1}", parameters.AuthenticationMethod, result.ExpiresOn);
                    return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
                }
            }
        }
        catch (MsalException ex)
        {
            // Check for an explicitly retryable error.
            if (ex is MsalServiceException svcEx &&
                svcEx.StatusCode == MsalRetryStatusCode)
            {
                int retryPeriod = 0;

                var retryAfter = svcEx.Headers.RetryAfter;
                if (retryAfter is not null)
                {
                    if (retryAfter.Delta.HasValue)
                    {
                        retryPeriod = retryAfter.Delta.Value.Milliseconds;
                    }
                    else if (retryAfter.Date.HasValue)
                    {
                        retryPeriod = Convert.ToInt32(retryAfter.Date.Value.Offset.TotalMilliseconds);
                    }

                    throw new Extensions.Azure.AuthenticationException(
                        parameters.AuthenticationMethod,
                        ex.ErrorCode,
                        true,
                        retryPeriod,
                        ex.Message,
                        ex);
                }

                // Fall through to check the ErrorCode...
            }

            // Check for an unknown error, which we will treat as implicitly
            // retryable, but without a suggested period.
            if (ex.ErrorCode == MsalError.UnknownError)
            {
                throw new Extensions.Azure.AuthenticationException(
                    parameters.AuthenticationMethod,
                    ex.ErrorCode,
                    true,
                    // Don't suggest a retry period.
                    0,
                    ex.Message,
                    ex);
            }

            // The error isn't retryable.
            throw new Extensions.Azure.AuthenticationException(
                parameters.AuthenticationMethod,
                ex.ErrorCode,
                false,
                0,
                ex.Message,
                ex);
        }
        catch (Exception ex)
        when (ex is
            AuthenticationFailedException or
            AuthenticationRequiredException or
            CredentialUnavailableException)
        {
            // These errors aren't retryable.
            throw new Extensions.Azure.AuthenticationException(
                parameters.AuthenticationMethod,
                "Unknown",
                false,
                0,
                $"Azure.Identity error: {ex.Message}",
                ex);
        }
        catch (Exception ex)
        {
            // These errors aren't retryable.
            throw new Extensions.Azure.AuthenticationException(
                parameters.AuthenticationMethod,
                "Unknown",
                false,
                0,
                $"Unexpected error: {ex.Message}",
                ex);
        }
    }

    private static async Task<AuthenticationResult?> TryAcquireTokenSilent(IPublicClientApplication app, SqlAuthenticationParameters parameters,
        string[] scopes, CancellationTokenSource cts)
    {
        AuthenticationResult? result = null;

        // Fetch available accounts from 'app' instance
        System.Collections.Generic.IEnumerator<IAccount> accounts = (await app.GetAccountsAsync().ConfigureAwait(false)).GetEnumerator();

        IAccount? account = default;
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

        if (account != null)
        {
            // If 'account' is available in 'app', we use the same to acquire token silently.
            // Read More on API docs: https://docs.microsoft.com/dotnet/api/microsoft.identity.client.clientapplicationbase.acquiretokensilent
            result = await app.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken: cts.Token).ConfigureAwait(false);
            SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token (silent) for {0} auth mode. Expiry Time: {1}", parameters.AuthenticationMethod, result?.ExpiresOn);
        }

        return result;
    }

    private static async Task<AuthenticationResult> AcquireTokenDeviceFlowAsync(IPublicClientApplication app, string[] scopes, Guid connectionId, string? userId,
        SqlAuthenticationMethod authenticationMethod, CancellationTokenSource cts, ICustomWebUi? customWebUI, Func<DeviceCodeResult, Task> deviceCodeFlowCallback)
    {
        try
        {
            return await app.AcquireTokenWithDeviceCode(scopes,
                deviceCodeResult => deviceCodeFlowCallback(deviceCodeResult))
                .WithCorrelationId(connectionId)
                .ExecuteAsync(cancellationToken: cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            SqlClientEventSource.Log.TryTraceEvent("AcquireTokenInteractiveDeviceFlowAsync | Operation timed out while acquiring access token.");

            throw new Extensions.Azure.AuthenticationException(
                authenticationMethod,
                "OperationCanceled",
                false,
                0,
                // TODO: This used to use the following localized strings
                // depending on the method:
                //
                // Strings.SQL_Timeout_Active_Directory_Interactive_Authentication
                // Strings.SQL_Timeout_Active_Directory_DeviceFlow_Authentication
                ex.Message,
                ex);
        }
    }

    private async Task<AuthenticationResult> AcquireTokenInteractiveAsync(
        SqlAuthenticationParameters parameters,
        string applicationClientId,
        string[] scopes, 
        Guid connectionId,
        string? userId,
        SqlAuthenticationMethod authenticationMethod, 
        CancellationTokenSource cts, 
        ICustomWebUi? customWebUI, 
        Func<DeviceCodeResult, Task> deviceCodeFlowCallback)
    {
        IPublicClientApplication app = await GetPublicClientAppInstanceAsync(parameters, applicationClientId, cts.Token).ConfigureAwait(false);

        try
        {
            AuthenticationResult? cachedResult = await TryAcquireTokenSilent(app, parameters, scopes, cts).ConfigureAwait(false);

            if (cachedResult is not null)
            {
                SqlClientEventSource.Log.TryTraceEvent("AcquireTokenAsync | Acquired access token (silent) for {0} auth mode. Expiry Time: {1}", parameters.AuthenticationMethod, cachedResult .ExpiresOn);
                return cachedResult;
            }
        }
        catch (MsalUiRequiredException)
        {
            // An 'MsalUiRequiredException' is thrown in the case where an interaction is required with the end user of the application,
            // for instance, if no refresh token was in the cache, or the user needs to consent, or re-sign-in (for instance if the password expired),
            // or the user needs to perform two factor authentication.
            //
            // result should be null here, but we make sure of that.
        }

        // If no existing 'account' is found, we request user to sign in interactively.
        


        try
        {
            CancellationTokenSource ctsInteractive = new();
            #if NET
            // On .NET Core, MSAL will start the system browser as a
            // separate process. MSAL does not have control over this
            // browser, but once the user finishes authentication, the web
            // page is redirected in such a way that MSAL can intercept the
            // Uri.  MSAL cannot detect if the user navigates away or simply
            // closes the browser. Apps using this technique are encouraged
            // to define a timeout (via CancellationToken). We recommend a
            // timeout of at least a few minutes, to take into account cases
            // where the user is prompted to change password or perform 2FA.
            //
            // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/System-Browser-on-.Net-Core#system-browser-experience
            //
            // Wait up to 3 minutes.
            ctsInteractive.CancelAfter(180000);
            #endif
            if (customWebUI != null)
            {
                return await app.AcquireTokenInteractive(scopes)
                    .WithCorrelationId(connectionId)
                    .WithCustomWebUi(customWebUI)
                    .WithLoginHint(userId)
                    .ExecuteAsync(ctsInteractive.Token)
                    .ConfigureAwait(false);
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
                    .ExecuteAsync(ctsInteractive.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            SqlClientEventSource.Log.TryTraceEvent("AcquireTokenInteractiveAsync | Operation timed out while acquiring access token.");

            throw new Extensions.Azure.AuthenticationException(
                authenticationMethod,
                "OperationCanceled",
                false,
                0,
                // TODO: This used to use the following localized strings
                // depending on the method:
                //
                // Strings.SQL_Timeout_Active_Directory_Interactive_Authentication
                // Strings.SQL_Timeout_Active_Directory_DeviceFlow_Authentication
                ex.Message,
                ex);
        }
    }

    private static Task DefaultDeviceFlowCallback(DeviceCodeResult result)
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

    private async Task<IPublicClientApplication> GetPublicClientAppInstanceAsync(
        SqlAuthenticationParameters parameters,
        string _applicationClientId,
        CancellationToken cancellationToken)
    {
        /*
        * Today, MSAL.NET uses another redirect URI by default in desktop applications that run on Windows
        * (urn:ietf:wg:oauth:2.0:oob). In the future, we'll want to change this default, so we recommend
        * that you use https://login.microsoftonline.com/common/oauth2/nativeclient.
        *
        * https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-desktop-app-registration#redirect-uris
        */
        string redirectUri = s_nativeClientRedirectUri;

        #if NET
        if (parameters.AuthenticationMethod != SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
        {
            redirectUri = "http://localhost";
        }
        #endif
        PublicClientAppKey pcaKey =
        #if NETFRAMEWORK
            new(parameters.Authority, redirectUri, _applicationClientId, _iWin32WindowFunc);
        #else
            new(parameters.Authority, redirectUri, _applicationClientId);
        #endif

        if (!s_pcaMap.TryGetValue(pcaKey, out IPublicClientApplication clientApplicationInstance))
        {
            await s_pcaMapModifierSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Double-check in case another thread added it while we waited for the semaphore
                if (!s_pcaMap.TryGetValue(pcaKey, out clientApplicationInstance))
                {
                    clientApplicationInstance = CreateClientAppInstance(pcaKey);
                    s_pcaMap.TryAdd(pcaKey, clientApplicationInstance);
                }
            }
            finally
            {
                s_pcaMapModifierSemaphore.Release();
            }
        }

        return clientApplicationInstance;
    }

    private static async Task<AccessToken> GetTokenAsync(TokenCredentialKey tokenCredentialKey, string secret,
        TokenRequestContext tokenRequestContext, CancellationToken cancellationToken)
    {
        if (!s_tokenCredentialMap.TryGetValue(tokenCredentialKey, out TokenCredentialData tokenCredentialInstance))
        {
            await s_tokenCredentialMapModifierSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Double-check in case another thread added it while we waited for the semaphore
                if (!s_tokenCredentialMap.TryGetValue(tokenCredentialKey, out tokenCredentialInstance))
                {
                    tokenCredentialInstance = CreateTokenCredentialInstance(tokenCredentialKey, secret);
                    s_tokenCredentialMap.TryAdd(tokenCredentialKey, tokenCredentialInstance);
                }
            }
            finally
            {
                s_tokenCredentialMapModifierSemaphore.Release();
            }
        }

        if (!AreEqual(tokenCredentialInstance._secretHash, GetHash(secret)))
        {
            // If the secret hash has changed, we need to remove the old token credential instance and create a new one.
            await s_tokenCredentialMapModifierSemaphore.WaitAsync(cancellationToken);
            try
            {
                s_tokenCredentialMap.TryRemove(tokenCredentialKey, out _);
                tokenCredentialInstance = CreateTokenCredentialInstance(tokenCredentialKey, secret);
                s_tokenCredentialMap.TryAdd(tokenCredentialKey, tokenCredentialInstance);
            }
            finally
            {
                s_tokenCredentialMapModifierSemaphore.Release();
            }
        }

        return await tokenCredentialInstance._tokenCredential.GetTokenAsync(tokenRequestContext, cancellationToken);
    }

    private static string GetAccountPwCacheKey(SqlAuthenticationParameters parameters)
    {
        return parameters.Authority + "+" + parameters.UserId;
    }

    private static byte[] GetHash(string input)
    {
        byte[] unhashedBytes = Encoding.Unicode.GetBytes(input);
        SHA256 sha256 = SHA256.Create();
        byte[] hashedBytes = sha256.ComputeHash(unhashedBytes);
        return hashedBytes;
    }

    private static bool AreEqual(byte[] a1, byte[] a2)
    {
        if (ReferenceEquals(a1, a2))
        {
            return true;
        }
        else if (a1 is null || a2 is null)
        {
            return false;
        }
        else if (a1.Length != a2.Length)
        {
            return false;
        }

        return a1.AsSpan().SequenceEqual(a2.AsSpan());
    }

    private IPublicClientApplication CreateClientAppInstance(PublicClientAppKey publicClientAppKey)
    {
        PublicClientApplicationBuilder builder = PublicClientApplicationBuilder
            .CreateWithApplicationOptions(new PublicClientApplicationOptions
            {
                ClientId = publicClientAppKey._applicationClientId,
                ClientName = typeof(ActiveDirectoryAuthenticationProvider).FullName,
                ClientVersion = Extensions.Azure.ThisAssembly.InformationalVersion,
                RedirectUri = publicClientAppKey._redirectUri,
            })
            .WithAuthority(publicClientAppKey._authority);
        
        #if NETFRAMEWORK
        if (_iWin32WindowFunc is not null)
        {
            builder.WithParentActivityOrWindow(_iWin32WindowFunc);
        }
        #endif

        return builder.Build();
    }

    private static TokenCredentialData CreateTokenCredentialInstance(TokenCredentialKey tokenCredentialKey, string secret)
    {
        if (tokenCredentialKey._tokenCredentialType == typeof(DefaultAzureCredential))
        {
            DefaultAzureCredentialOptions defaultAzureCredentialOptions = new()
            {
                AuthorityHost = new Uri(tokenCredentialKey._authority),
                TenantId = tokenCredentialKey._audience,
                ExcludeInteractiveBrowserCredential = true // Force disabled, even though it's disabled by default to respect driver specifications.
            };

            // Optionally set clientId when available
            if (tokenCredentialKey._clientId is not null)
            {
                defaultAzureCredentialOptions.ManagedIdentityClientId = tokenCredentialKey._clientId;
                defaultAzureCredentialOptions.SharedTokenCacheUsername = tokenCredentialKey._clientId;
                defaultAzureCredentialOptions.WorkloadIdentityClientId = tokenCredentialKey._clientId;
            }

            // SqlClient is a library and provides support to acquire access
            // token using 'DefaultAzureCredential' on user demand when they
            // specify 'Authentication = Active Directory Default' in
            // connection string.
            //
            // Default Azure Credential is instantiated by the calling
            // application when using "Active Directory Default"
            // authentication code to connect to Azure SQL instance.
            // SqlClient is a library, doesn't instantiate the credential
            // without running application instructions.
            //
            // Note that CodeQL suppression support can only detect
            // suppression comments that appear immediately above the
            // flagged statement, or appended to the end of the statement.
            // Multi-line justifications are not supported.
            //
            // https://eng.ms/docs/cloud-ai-platform/devdiv/one-engineering-system-1es/1es-docs/codeql/codeql-semmle#guidance-on-suppressions
            //
            // CodeQL [SM05137] See above for justification.
            DefaultAzureCredential cred = new(defaultAzureCredentialOptions);

            return new TokenCredentialData(cred, GetHash(secret));
        }

        TokenCredentialOptions tokenCredentialOptions = new() { AuthorityHost = new Uri(tokenCredentialKey._authority) };

        if (tokenCredentialKey._tokenCredentialType == typeof(ManagedIdentityCredential))
        {
            return new TokenCredentialData(new ManagedIdentityCredential(tokenCredentialKey._clientId, tokenCredentialOptions), GetHash(secret));
        }
        else if (tokenCredentialKey._tokenCredentialType == typeof(ClientSecretCredential))
        {
            return new TokenCredentialData(new ClientSecretCredential(tokenCredentialKey._audience, tokenCredentialKey._clientId, secret, tokenCredentialOptions), GetHash(secret));
        }
        else if (tokenCredentialKey._tokenCredentialType == typeof(WorkloadIdentityCredential))
        {
            // The WorkloadIdentityCredentialOptions object initialization populates its instance members
            // from the environment variables AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_FEDERATED_TOKEN_FILE,
            // and AZURE_ADDITIONALLY_ALLOWED_TENANTS. AZURE_CLIENT_ID may be overridden by the User Id.
            WorkloadIdentityCredentialOptions options = new() { AuthorityHost = new Uri(tokenCredentialKey._authority) };

            if (tokenCredentialKey._clientId is not null)
            {
                options.ClientId = tokenCredentialKey._clientId;
            }

            return new TokenCredentialData(new WorkloadIdentityCredential(options), GetHash(secret));
        }

        // This should never be reached, but if it is, throw an exception that will be noticed during development
        throw new ArgumentException(nameof(ActiveDirectoryAuthenticationProvider));
    }

    internal class PublicClientAppKey
    {
        public readonly string _authority;
        public readonly string _redirectUri;
        public readonly string _applicationClientId;
        #if NETFRAMEWORK
        public readonly Func<System.Windows.Forms.IWin32Window> _iWin32WindowFunc;
        #endif

        public PublicClientAppKey(string authority, string redirectUri, string applicationClientId
        #if NETFRAMEWORK
        , Func<System.Windows.Forms.IWin32Window> iWin32WindowFunc
        #endif
            )
        {
            _authority = authority;
            _redirectUri = redirectUri;
            _applicationClientId = applicationClientId;
            #if NETFRAMEWORK
            _iWin32WindowFunc = iWin32WindowFunc;
            #endif
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is PublicClientAppKey pcaKey)
            {
                return string.CompareOrdinal(_authority, pcaKey._authority) == 0
                    && string.CompareOrdinal(_redirectUri, pcaKey._redirectUri) == 0
                    && string.CompareOrdinal(_applicationClientId, pcaKey._applicationClientId) == 0
                    #if NETFRAMEWORK
                    && pcaKey._iWin32WindowFunc == _iWin32WindowFunc
                    #endif
                ;
            }
            return false;
        }

        public override int GetHashCode() => Tuple.Create(_authority, _redirectUri, _applicationClientId
        #if NETFRAMEWORK
            , _iWin32WindowFunc
            #endif
            ).GetHashCode();
    }

    internal class TokenCredentialData
    {
        public TokenCredential _tokenCredential;
        public byte[] _secretHash;

        public TokenCredentialData(TokenCredential tokenCredential, byte[] secretHash)
        {
            _tokenCredential = tokenCredential;
            _secretHash = secretHash;
        }
    }

    internal class TokenCredentialKey
    {
        public readonly Type _tokenCredentialType;
        public readonly string _authority;
        public readonly string _scope;
        public readonly string _audience;
        public readonly string? _clientId;

        public TokenCredentialKey(Type tokenCredentialType, string authority, string scope, string audience, string? clientId)
        {
            _tokenCredentialType = tokenCredentialType;
            _authority = authority;
            _scope = scope;
            _audience = audience;
            _clientId = clientId;
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is TokenCredentialKey tcKey)
            {
                return string.CompareOrdinal(nameof(_tokenCredentialType), nameof(tcKey._tokenCredentialType)) == 0
                    && string.CompareOrdinal(_authority, tcKey._authority) == 0
                    && string.CompareOrdinal(_scope, tcKey._scope) == 0
                    && string.CompareOrdinal(_audience, tcKey._audience) == 0
                    && string.CompareOrdinal(_clientId, tcKey._clientId) == 0
                ;
            }
            return false;
        }

        public override int GetHashCode() => Tuple.Create(_tokenCredentialType, _authority, _scope, _audience, _clientId).GetHashCode();
    }

}

internal class SqlClientLogger
{
    public void LogInfo(string type, string method, string message)
    {
        SqlClientEventSource.Log.TryTraceEvent(
            "<sc|{0}|{1}|{2}>{3}", type, method, LogLevel.Info, message);
    }
}

internal class SqlClientEventSource
{
    internal class Logger
    {
        public void TryTraceEvent(string message, params object?[] args)
        {
        }
    }

    public static readonly Logger Log = new();
}
