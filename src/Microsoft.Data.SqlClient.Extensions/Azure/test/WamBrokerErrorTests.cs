// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.Identity.Client;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Verifies missing-window diagnostics without opening UI or contacting Entra ID.
/// </summary>
[Collection(SqlAuthenticationProviderCollection.Name)]
public class WamBrokerErrorTests
{
    /// <summary>
    /// Both first-party and custom applications using WAM receive actionable guidance on Windows.
    /// Other platforms retain the original MSAL diagnostic.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("11111111-2222-3333-4444-555555555555")]
    public async Task AcquireTokenAsync_MissingWamWindow_ExplainsSupportedAlternatives(string? applicationClientId)
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = applicationClientId,
                UseWamBroker = true,
            });
        provider.SetParentActivityOrWindowFunc(
            () => throw new InvalidOperationException("Window discovery must not precede MSAL token acquisition."));
        var cause = new MsalClientException("window_handle_required", "Original MSAL diagnostic.");

        using var listener = new FailureListener();
        AuthenticationException exception = await AcquireWithMsalExceptionAsync(
            provider, SqlAuthenticationMethod.ActiveDirectoryInteractive, cause);

        Assert.Contains(listener.Events, entry => entry.Level == EventLevel.Error &&
            Equals(entry.Payload![0], "Token acquisition failed for ActiveDirectoryInteractive.") && Equals(entry.Payload[1], "SqlClient"));

        Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryInteractive, exception.Method);
        Assert.Equal(cause.ErrorCode, exception.FailureCode);
        Assert.Same(cause, exception.InnerException);
        Assert.False(exception.ShouldRetry);
        Assert.Equal(0, exception.RetryPeriod);
        Assert.Contains(cause.Message, exception.Message);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Contains("parent window handle", exception.Message);
            Assert.Contains("SetParentActivityOrWindowFunc", exception.Message);
            Assert.Contains("Active Directory Default", exception.Message);
            Assert.Contains("ApplicationClientId", exception.Message);
            Assert.Contains("UseWamBroker", exception.Message);
            Assert.Contains("first-party", exception.Message);
        }
        else
        {
            Assert.Equal($"Failed to acquire access token for {exception.Method}: {cause.Message}", exception.Message);
        }
    }

    /// <summary>
    /// Non-broker flows, other authentication methods, and unrelated MSAL failures are not
    /// relabeled as missing WAM windows.
    /// </summary>
    [Theory]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive, false, "window_handle_required")]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, true, "window_handle_required")]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryIntegrated, true, "window_handle_required")]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive, true, "authentication_canceled")]
    public async Task AcquireTokenAsync_OtherMsalFailures_PreservesDiagnostic(
        SqlAuthenticationMethod method, bool useWamBroker, string errorCode)
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = "11111111-2222-3333-4444-555555555555",
                UseWamBroker = useWamBroker,
            });
        var cause = new MsalClientException(errorCode, "Original MSAL diagnostic.");

        AuthenticationException exception = await AcquireWithMsalExceptionAsync(provider, method, cause);

        Assert.Equal(method, exception.Method);
        Assert.Equal(errorCode, exception.FailureCode);
        Assert.Same(cause, exception.InnerException);
        Assert.False(exception.ShouldRetry);
        Assert.Equal($"Failed to acquire access token for {method}: {cause.Message}", exception.Message);
    }

    /// <summary>Collects SqlClient authentication trace events.</summary>
    private sealed class FailureListener : EventListener
    {
        /// <summary>Gets the captured authentication events.</summary>
        internal ConcurrentQueue<EventWrittenEventArgs> Events { get; } = new();

        /// <summary>Starts collecting authentication events.</summary>
        internal FailureListener() =>
            EnableEvents(SqlClientEventSource.Log, EventLevel.Verbose, SqlClientEventSource.Keywords.AuthenticationTrace);

        /// <summary>Disables explicitly so later tests do not inherit this demand.</summary>
        public override void Dispose()
        {
            DisableEvents(SqlClientEventSource.Log);
            base.Dispose();
        }

        /// <inheritdoc />
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource == SqlClientEventSource.Log &&
                (eventData.Keywords & SqlClientEventSource.Keywords.AuthenticationTrace) != 0)
            {
                Events.Enqueue(eventData);
            }
        }
    }

    /// <summary>
    /// Installs an isolated MSAL client whose cache callback throws the supplied exception,
    /// exercising the provider's real exception translation without a network request.
    /// The cache entry is removed even if an assertion fails.
    /// </summary>
    /// <param name="provider">The provider configuration under test.</param>
    /// <param name="method">The authentication method under test.</param>
    /// <param name="cause">The MSAL failure to propagate through token acquisition.</param>
    /// <returns>The translated authentication failure.</returns>
    private static async Task<AuthenticationException> AcquireWithMsalExceptionAsync(
        ActiveDirectoryAuthenticationProvider provider, SqlAuthenticationMethod method, MsalClientException cause)
    {
        string authority = $"https://login.microsoftonline.com/{Guid.NewGuid()}";
        string redirectUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? provider.UseWamBroker
                ? $"ms-appx-web://microsoft.aad.brokerplugin/{provider.ApplicationClientId}"
#if NETFRAMEWORK
                : "https://login.microsoftonline.com/common/oauth2/nativeclient"
#else
                : "http://localhost"
#endif
            : "http://localhost";

        var key = new ActiveDirectoryAuthenticationProvider.PublicClientAppKey(
            authority, redirectUri, provider.ApplicationClientId
#if NETFRAMEWORK
            , null
#endif
        );
        IPublicClientApplication app = PublicClientApplicationBuilder.Create(provider.ApplicationClientId)
            .WithAuthority(authority)
            .WithRedirectUri(redirectUri)
            .Build();
        app.UserTokenCache.SetBeforeAccess(_ => throw cause);

        var cache = ActiveDirectoryAuthenticationProvider.s_pcaMap;
        Assert.True(cache.TryAdd(key, app));
        try
        {
            var parameters = new SqlAuthenticationParameters(
                method, "test-server", "test-database", "https://database.windows.net/",
                authority, userId: null, password: null, connectionId: Guid.NewGuid(), connectionTimeout: 30);
            return await Assert.ThrowsAsync<AuthenticationException>(() => provider.AcquireTokenAsync(parameters));
        }
        finally
        {
            cache.TryRemove(key, out _);
        }
    }
}
