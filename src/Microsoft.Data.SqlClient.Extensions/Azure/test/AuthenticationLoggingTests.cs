// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Abstractions;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>Verifies non-PII authentication tracing without application logging callbacks.</summary>
[Collection("SqlAuthenticationProvider")]
public class AuthenticationLoggingTests
{
    /// <summary>Authentication diagnostics do not add public callback or PII configuration APIs.</summary>
    [Fact]
    public void Options_DoNotExposeLoggingCallbacksOrPii()
    {
        Assert.Null(typeof(ActiveDirectoryAuthenticationProviderOptions).GetProperty("AuthenticationLoggingCallback"));
        Assert.Null(typeof(ActiveDirectoryAuthenticationProviderOptions).GetProperty("EnableMsalPiiLogging"));
        Assert.Null(typeof(ActiveDirectoryAuthenticationProvider).Assembly.GetType("Microsoft.Data.SqlClient.SqlAuthenticationLogEntry"));
    }

    /// <summary>Authentication events have their own keyword, severity, and stable correlation payload.</summary>
    [Fact]
    public void EventSource_AuthenticationSchemaIsValid()
    {
        Type sourceType = typeof(SqlClientEventSource);
        Assert.Equal((EventKeywords)0x2000, SqlClientEventSource.Keywords.AuthenticationTrace);
        Assert.False(string.IsNullOrEmpty(EventSource.GenerateManifest(sourceType, sourceType.Assembly.Location)));
        foreach ((string name, int id, EventLevel level) in new[]
        {
            ("AuthenticationAlways", 22, EventLevel.LogAlways),
            ("AuthenticationCritical", 23, EventLevel.Critical),
            ("AuthenticationError", 24, EventLevel.Error),
            ("AuthenticationWarning", 25, EventLevel.Warning),
            ("AuthenticationInformational", 26, EventLevel.Informational),
            ("AuthenticationVerbose", 27, EventLevel.Verbose)
        })
        {
            MethodInfo method = sourceType.GetMethod(name)!;
            var attribute = Assert.IsType<EventAttribute>(Assert.Single(method.GetCustomAttributes(typeof(EventAttribute), false)));
            Assert.Equal(id, attribute.EventId);
            Assert.Equal(level, attribute.Level);
            Assert.Equal(SqlClientEventSource.Keywords.AuthenticationTrace, attribute.Keywords);
            Assert.Equal(new[] { "message", "source", "connectionId" }, method.GetParameters().Select(parameter => parameter.Name));
        }
    }

    /// <summary>The dedicated keyword and ordinary Trace can be collected independently or together.</summary>
    [Theory]
    [InlineData(0x2, false, true)]
    [InlineData(0x2000, true, false)]
    [InlineData(0x2002, true, true)]
    public void EventSource_KeywordsAreIndependent(long keywords, bool authentication, bool ordinary)
    {
        using var listener = new AuthenticationListener();
        listener.Enable(EventLevel.Verbose, (EventKeywords)keywords);
        AuthenticationLogger.Write(EventLevel.Informational, "authentication diagnostic", "MSAL", listener.ConnectionId);
        SqlClientEventSource.Log.TryTraceEvent("ordinary diagnostic");

        Assert.Equal(authentication ? 1 : 0, listener.Events.Count);
        Assert.Equal(ordinary ? 1 : 0, listener.TraceEvents);
    }

    /// <summary>Collector severity and detachment dynamically gate the cached MSAL logger.</summary>
    [Fact]
    public void MsalLogger_GatesFormattingOnCurrentOutputDemand()
    {
        Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
        using var listener = new AuthenticationListener();
        listener.Disable();
        Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
        listener.Enable(EventLevel.Warning, SqlClientEventSource.Keywords.AuthenticationTrace);
        Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
        Assert.True(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Warning));
        AuthenticationLogger.MsalLogger.Log(listener.Entry(EventLogLevel.Informational, "filtered"));
        AuthenticationLogger.MsalLogger.Log(listener.Entry(EventLogLevel.Warning, "visible"));
        Assert.Equal("visible", Assert.Single(listener.Events).Payload![0]);
        listener.Disable();
        Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Warning));
        AuthenticationLogger.MsalLogger.Log(listener.Entry(EventLogLevel.Error, "detached"));
        Assert.Single(listener.Events);
    }

    /// <summary>The SDK interface maps every supported severity and retains source and connection correlation.</summary>
    [Theory]
    [InlineData(EventLogLevel.LogAlways, EventLevel.LogAlways)]
    [InlineData(EventLogLevel.Critical, EventLevel.Critical)]
    [InlineData(EventLogLevel.Error, EventLevel.Error)]
    [InlineData(EventLogLevel.Warning, EventLevel.Warning)]
    [InlineData(EventLogLevel.Informational, EventLevel.Informational)]
    [InlineData(EventLogLevel.Verbose, EventLevel.Verbose)]
    public void MsalLogger_MapsInterfaceSeverity(EventLogLevel sdk, EventLevel expected)
    {
        using var listener = new AuthenticationListener();
        Assert.True(AuthenticationLogger.MsalLogger.IsEnabled(sdk));
        AuthenticationLogger.MsalLogger.Log(listener.Entry(sdk, "interface diagnostic"));

        EventWrittenEventArgs entry = Assert.Single(listener.Events);
        Assert.Equal(expected, entry.Level);
        Assert.Equal(new object[] { "interface diagnostic", "MSAL", listener.ConnectionId.ToString() }, entry.Payload!);
    }

    /// <summary>MSAL entries without a GUID correlation ID are written with an empty connection ID.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void MsalLogger_UncorrelatedEntriesUseEmptyConnectionId(string? correlationId)
    {
        using var listener = new AuthenticationListener();
        string message = $"uncorrelated {Guid.NewGuid()}";
        AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = EventLogLevel.Informational, Message = message, CorrelationId = correlationId });

        EventWrittenEventArgs entry = Assert.Single(listener.Events, e => Equals(e.Payload![0], message));
        Assert.Equal(Guid.Empty.ToString(), entry.Payload![2]);
    }

    /// <summary>Unknown MSAL severities are never forwarded.</summary>
    [Fact]
    public void MsalLogger_RejectsUndefinedSeverity()
    {
        using var listener = new AuthenticationListener();
        Assert.False(AuthenticationLogger.MsalLogger.IsEnabled((EventLogLevel)99));
        AuthenticationLogger.MsalLogger.Log(listener.Entry((EventLogLevel)99, "unknown"));

        Assert.Empty(listener.Events);
    }

    /// <summary>Only parent request IDs created by SqlClient map back to a connection ID.</summary>
    [Fact]
    public void ParentRequestId_RoundTripsOnlySqlClientRequests()
    {
        Guid connectionId = Guid.NewGuid();
        string requestId = AuthenticationLogger.GetParentRequestId(connectionId);
        Assert.True(AuthenticationLogger.TryGetConnectionId(requestId, out Guid parsed));
        Assert.Equal(connectionId, parsed);
        foreach (string? other in new[] { null, "", connectionId.ToString(), "SqlClient-", "SqlClient-not-a-guid", "other-" + connectionId })
        {
            Assert.False(AuthenticationLogger.TryGetConnectionId(other, out Guid empty));
            Assert.Equal(Guid.Empty, empty);
        }
    }

    /// <summary>Collector changes preserve one SDK client and its token cache across provider instances.</summary>
    [Fact]
    public async Task CachedClients_ReuseClientAcrossCollectorChanges()
    {
        var first = new ActiveDirectoryAuthenticationProvider();
        var second = new ActiveDirectoryAuthenticationProvider();
        IPublicClientApplication client = await GetClient(first);
        using var listener = new AuthenticationListener();
        Assert.Same(client, await GetClient(second));
        AuthenticationLogger.MsalLogger.Log(listener.Entry(EventLogLevel.Informational, "first"));
        listener.Disable();
        Assert.Same(client, await GetClient(first));
        Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
        Assert.Equal("first", Assert.Single(listener.Events).Payload![0]);
    }

    /// <summary>Real cached clients use the non-PII identity logger rather than an application callback.</summary>
    [Fact]
    public async Task CachedClients_KeepPiiDisabled()
    {
        IPublicClientApplication client = await GetClient(new ActiveDirectoryAuthenticationProvider());
        Assert.False(client.AppConfig.EnablePiiLogging);
        Assert.False(client.AppConfig.IsDefaultPlatformLoggingEnabled);
    }

    /// <summary>Provider replacement and collector changes retain identity-local signed-in account state.</summary>
    [Fact]
    public async Task RegisteredProviders_PreserveSignedInCacheAcrossReplacement()
    {
        const SqlAuthenticationMethod method = SqlAuthenticationMethod.ActiveDirectoryIntegrated;
        SqlAuthenticationProvider original = Assert.IsAssignableFrom<SqlAuthenticationProvider>(SqlAuthenticationProvider.GetProvider(method));
        string clientId = Guid.NewGuid().ToString();
        var first = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions { ApplicationClientId = clientId });
        using var listener = new AuthenticationListener(captureAll: true);
        try
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(method, first));
            IPublicClientApplication primary = await GetClient(first);
            ITokenCacheSerializer serializer = Assert.IsAssignableFrom<ITokenCacheSerializer>(primary.UserTokenCache);
            const string home = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.11111111-2222-3333-4444-555555555555";
            string json = $$$$"""
            {"Account":{"{{{{home}}}}-login.microsoftonline.com-11111111-2222-3333-4444-555555555555":{
                "home_account_id":"{{{{home}}}}","environment":"login.microsoftonline.com","realm":"11111111-2222-3333-4444-555555555555",
                "local_account_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","username":"user@example.invalid","authority_type":"MSSTS"}},
             "RefreshToken":{"{{{{home}}}}-login.microsoftonline.com-refreshtoken-{{{{clientId}}}}--":{
                "home_account_id":"{{{{home}}}}","environment":"login.microsoftonline.com","client_id":"{{{{clientId}}}}",
                "credential_type":"RefreshToken","secret":"<refresh-token>"}}}
            """;
            serializer.DeserializeMsalV3(Encoding.UTF8.GetBytes(json), shouldClearExistingCache: true);
            Assert.Single(await primary.GetAccountsAsync());
            var replacement = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions { ApplicationClientId = clientId });
            Assert.True(SqlAuthenticationProvider.SetProvider(method, replacement));
            IPublicClientApplication reused = await GetClient(
                Assert.IsType<ActiveDirectoryAuthenticationProvider>(SqlAuthenticationProvider.GetProvider(method)));
            Assert.Same(primary, reused);
            listener.Disable();
            Assert.Single(await reused.GetAccountsAsync());
            listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.AuthenticationTrace);
            Assert.Single(await reused.GetAccountsAsync());
            Assert.Contains(listener.Events, entry => Equals(entry.Payload![1], "MSAL"));
            Assert.All(listener.Events, entry =>
            {
                Assert.DoesNotContain("<refresh-token>", string.Join(" ", entry.Payload!));
                Assert.DoesNotContain("user@example.invalid", string.Join(" ", entry.Payload!));
            });
            var unrelated = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions { ApplicationClientId = Guid.NewGuid().ToString() });
            Assert.Empty(await (await GetClient(unrelated)).GetAccountsAsync());
            await reused.RemoveAsync(Assert.Single(await reused.GetAccountsAsync()));
            Assert.Empty(await primary.GetAccountsAsync());
        }
        finally
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(method, original));
        }
    }

    /// <summary>Azure SDK options never enable account identifiers, unsafe support logging, or HTTP content.</summary>
    [Fact]
    public void AzureCredentialOptions_DisableSensitiveLogging()
    {
        var options = new ClientSecretCredentialOptions();
        ActiveDirectoryAuthenticationProvider.ConfigureCredentialLogging(options);
        Assert.False(options.IsUnsafeSupportLoggingEnabled);
        Assert.False(options.Diagnostics.IsAccountIdentifierLoggingEnabled);
        Assert.False(options.Diagnostics.IsLoggingContentEnabled);
        Assert.True(options.Diagnostics.IsLoggingEnabled);
    }

    /// <summary>Real Azure SDK failures are correlated by parent request ID and do not forward raw exception text.</summary>
    [Fact]
    public async Task AzureSdkEvents_CorrelateFailuresAndAreNonPii()
    {
        _ = AzureAuthenticationEventListener.Instance;
        using var handler = new TokenFailureHandler();
        using var httpClient = new HttpClient(handler);
        ClientSecretCredential credential = CreateCredential(httpClient);
        using var listener = new AuthenticationListener();
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            RequestToken(credential, AuthenticationLogger.GetParentRequestId(listener.ConnectionId)));

        EventWrittenEventArgs failure = Assert.Single(listener.Events, entry => Equals(entry.Payload![0], "Azure credential token request failed."));
        Assert.Equal(listener.ConnectionId.ToString(), failure.Payload![2]);
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![1], "Azure-Identity") && Equals(entry.Payload[2], Guid.Empty.ToString()));
        Assert.All(listener.Events, entry => Assert.DoesNotContain("private SDK exception data", string.Join(" ", entry.Payload!)));
    }

    /// <summary>Concurrent Azure SDK failures keep their own connection correlation.</summary>
    [Fact]
    public async Task AzureSdkEvents_ConcurrentFailuresRemainIsolated()
    {
        _ = AzureAuthenticationEventListener.Instance;
        using var listener = new AuthenticationListener(captureAll: true);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        async Task Acquire(Guid connectionId)
        {
            using var handler = new TokenFailureHandler();
            using var client = new HttpClient(handler);
            ClientSecretCredential credential = CreateCredential(client, Guid.NewGuid().ToString());
            await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
                RequestToken(credential, AuthenticationLogger.GetParentRequestId(connectionId)));
        }
        await Task.WhenAll(Acquire(firstId), Acquire(secondId));
        Assert.Single(listener.Events, entry => Equals(entry.Payload![2], firstId.ToString()));
        Assert.Single(listener.Events, entry => Equals(entry.Payload![2], secondId.ToString()));
    }

    /// <summary>Real SDK capture follows late attachment and detachment without rebuilding credentials.</summary>
    [Fact]
    public async Task AzureSdkEvents_FollowEventSourceEnablement()
    {
        _ = AzureAuthenticationEventListener.Instance;
        using var handler = new TokenFailureHandler();
        using var httpClient = new HttpClient(handler);
        ClientSecretCredential credential = CreateCredential(httpClient);
        using var listener = new AuthenticationListener();
        listener.Disable();
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        Assert.Empty(listener.Events);
        listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.AuthenticationTrace);
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        Assert.Contains(listener.Events, entry => entry.Level == EventLevel.Verbose);
        int count = listener.Events.Count;
        listener.Disable();
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        Assert.Equal(count, listener.Events.Count);
    }

    /// <summary>Ordinary Trace demand alone must not subscribe to or forward Azure SDK diagnostics.</summary>
    [Theory]
    [InlineData(0x2, false)]
    [InlineData(0x2000, true)]
    [InlineData(0x2002, true)]
    public void AzureSdkEvents_RequireAuthenticationKeyword(long keywords, bool expected)
    {
        AzureAuthenticationEventListener shared = AzureAuthenticationEventListener.Instance;
        using var listener = new AuthenticationListener();
        listener.Disable();
        listener.Enable(EventLevel.Verbose, (EventKeywords)keywords);
        using var source = new ControlledIdentitySource();
        Assert.Equal(expected, shared.GetAppliedLevel(source).HasValue);
        string message = $"keyword diagnostic {Guid.NewGuid()}";
        source.LogMsalVerbose(message);
        Assert.Equal(expected ? 1 : 0, listener.Count(message));
    }

    /// <summary>SDK-generated provider suffixes retain late tracing attachment and detachment.</summary>
    [Fact]
    public void AzureSdkEvents_RenamedSourceFollowsEventSourceEnablement()
    {
        _ = AzureAuthenticationEventListener.Instance;
        Type sourceType = typeof(ClientSecretCredential).Assembly.GetType("Azure.Identity.AzureIdentityEventSource", throwOnError: true)!;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic;
        _ = sourceType.GetProperty("Singleton", flags | BindingFlags.Static)!.GetValue(null);
        using var source = (EventSource)Activator.CreateInstance(sourceType, nonPublic: true)!;
        Assert.Null(source.ConstructionException);
        Assert.StartsWith("Azure-Identity-", source.Name);
        MethodInfo write = sourceType.GetMethod("LogMsalVerbose", flags | BindingFlags.Instance)!;
        using var listener = new AuthenticationListener();
        listener.Disable();
        write.Invoke(source, new object[] { "before tracing" });
        Assert.Equal(0, listener.Count("before tracing"));
        listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.AuthenticationTrace);
        write.Invoke(source, new object[] { "renamed SDK source" });
        EventWrittenEventArgs entry = Assert.Single(listener.Events, e => Equals(e.Payload![0], "renamed SDK source"));
        Assert.Equal(EventLevel.Verbose, entry.Level);
        Assert.Equal(new object[] { "renamed SDK source", "Azure-Identity", Guid.Empty.ToString() }, entry.Payload!);
        listener.Disable();
        write.Invoke(source, new object[] { "after tracing" });
        Assert.Equal(0, listener.Count("after tracing"));
    }

    /// <summary>Unrelated providers and invalid suffixes cannot supply authentication diagnostics.</summary>
    [Theory]
    [InlineData("Other-Identity")]
    [InlineData("Azure-Identity-")]
    [InlineData("Azure-Identity-other")]
    [InlineData("Azure-Identity-0")]
    [InlineData("Azure-Identity--1")]
    [InlineData("Azure-Identity-+1")]
    [InlineData("Azure-Identity-1-extra")]
    [InlineData("Azure-Identity-2147483648")]
    public void AzureSdkEvents_IgnoreUnsupportedSourceNames(string sourceName)
    {
        _ = AzureAuthenticationEventListener.Instance;
        using var source = new ControlledIdentitySource(sourceName);
        Assert.Null(source.ConstructionException);
        using var listener = new AuthenticationListener();
        source.LogMsalVerbose("unrecognized provider");
        Assert.Equal(0, listener.Count("unrecognized provider"));
    }

    /// <summary>A Critical collector includes Always diagnostics without enabling Verbose SDK logging.</summary>
    [Fact]
    public void AzureSdkEvents_CriticalCollectorIncludesAlways()
    {
        AzureAuthenticationEventListener shared = AzureAuthenticationEventListener.Instance;
        using var listener = new AuthenticationListener(level: EventLevel.Critical);
        using var source = new ControlledIdentitySource();
        source.LogMsalVerbose("filtered");
        source.LogMsalAlways("always");
        Assert.Equal(EventLevel.Informational, shared.GetAppliedLevel(source));
        Assert.Equal(0, listener.Count("filtered"));
        Assert.Equal(1, listener.Count("always"));
    }

    /// <summary>Independent collectors receive only their own requested authentication levels.</summary>
    [Fact]
    public void AzureSdkEvents_CollectorsFilterIndependently()
    {
        _ = AzureAuthenticationEventListener.Instance;
        using var warning = new AuthenticationListener(level: EventLevel.Warning);
        using var verbose = new AuthenticationListener();
        using var source = new ControlledIdentitySource();
        source.LogMsalVerbose("verbose");
        Assert.Equal(1, verbose.Count("verbose"));
        Assert.Equal(0, warning.Count("verbose"));
        verbose.Disable();
        source.LogMsalVerbose("after verbose collector detached");
        Assert.Equal(0, verbose.Count("after verbose collector detached"));
        Assert.Equal(0, warning.Count("after verbose collector detached"));
    }

    /// <summary>Removing all collector demand unsubscribes from SDK sources.</summary>
    [Fact]
    public void AzureSdkEvents_UnsubscribeWithoutCollectors()
    {
        AzureAuthenticationEventListener shared = AzureAuthenticationEventListener.Instance;
        using var source = new ControlledIdentitySource();
        using (var listener = new AuthenticationListener())
        {
            Assert.Equal(EventLevel.Verbose, shared.GetAppliedLevel(source));
        }
#if NET
        // .NET Framework keeps SqlClientEventSource.IsEnabled raised while any other listener is attached,
        // so the subscription stays on there; Write still filters each event by collector demand.
        Assert.Null(shared.GetAppliedLevel(source));
#endif
    }

    /// <summary>Reentrant collector commands and newly constructed SDK sources cannot lose first events.</summary>
    [Fact]
    public void AzureSdkEvents_ReentrantCommandsAndNewSourcesRemainReady()
    {
        AzureAuthenticationEventListener shared = AzureAuthenticationEventListener.Instance;
        using var listener = new AuthenticationListener(level: EventLevel.Informational);
        using var source = new ControlledIdentitySource();
        int once = 1;
        source.Enabling = () =>
        {
            if (Interlocked.Exchange(ref once, 0) == 1)
            {
                listener.Disable();
                listener.Enable(EventLevel.Informational, SqlClientEventSource.Keywords.AuthenticationTrace);
                using var lateSource = new LateIdentitySource();
                lateSource.LogMsalInformational("created during refresh");
            }
        };
        try
        {
            listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.AuthenticationTrace);
            Assert.Equal(1, listener.Count("created during refresh"));
            // Check this listener's subscription; source-wide IsEnabled keeps the highest level on .NET Framework.
            Assert.Equal(EventLevel.Informational, shared.GetAppliedLevel(source));
        }
        finally
        {
            source.Enabling = null;
        }
    }

    /// <summary>Disposed SDK sources are not kept alive by the shared listener.</summary>
    [Fact]
    public void AzureSdkEvents_DoNotRetainDisposedSources()
    {
        _ = AzureAuthenticationEventListener.Instance;
        WeakReference source = CreateDisposedSource();
        for (int i = 0; i < 5 && source.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(source.IsAlive);
    }

    /// <summary>Unsupported events are excluded and raw SDK exceptions are replaced by fixed text.</summary>
    [Fact]
    public void AzureListener_RejectsUnknownPayloadsAndSanitizesFailures()
    {
        _ = AzureAuthenticationEventListener.Instance;
        using var listener = new AuthenticationListener(captureAll: true);
        using var source = new InvalidSchemaIdentitySource();
        Guid connectionId = Guid.NewGuid();
        source.LogMsalVerbose(42);
        source.LogMsalError("wrong event id");
        source.AuthenticatedAccountDetails("private account");
        source.GetTokenFailed("ClientSecretCredential.GetToken", "[ scope ]", "other-request", "app failure");
        source.GetTokenFailed("ClientSecretCredential.GetToken", "[ scope ]", connectionId.ToString(), "app failure");
        source.GetTokenFailed("ClientSecretCredential.GetToken", "[ scope ]", AuthenticationLogger.GetParentRequestId(connectionId), "private exception");
        Assert.Equal(0, listener.Count("wrong event id"));
        Assert.Equal(0, listener.Count("private account"));
        Assert.DoesNotContain(listener.Events, e => string.Join(" ", e.Payload!).Contains("failure") || string.Join(" ", e.Payload!).Contains("exception"));
        EventWrittenEventArgs entry = Assert.Single(listener.Events, e => Equals(e.Payload![2], connectionId.ToString()));
        Assert.Equal("Azure credential token request failed.", entry.Payload![0]);
        source.DefaultAzureCredentialCredentialSelected("ClientSecretCredential");
        Assert.Equal(1, listener.Count("DefaultAzureCredential selected ClientSecretCredential."));
    }

    /// <summary>Error and Warning collectors still receive SDK failures that Azure emits at Informational.</summary>
    [Theory]
    [InlineData(EventLevel.Error)]
    [InlineData(EventLevel.Warning)]
    public void AzureSdkEvents_LowVerbosityCollectorsReceiveTokenFailures(EventLevel level)
    {
        _ = AzureAuthenticationEventListener.Instance;
        using var listener = new AuthenticationListener(level: level);
        using var source = new InvalidSchemaIdentitySource();
        source.DefaultAzureCredentialCredentialSelected("ClientSecretCredential");
        source.GetTokenFailed("ClientSecretCredential.GetToken", "[ scope ]", AuthenticationLogger.GetParentRequestId(listener.ConnectionId), "private exception");
        Assert.Equal(0, listener.Count("DefaultAzureCredential selected ClientSecretCredential."));
        EventWrittenEventArgs entry = Assert.Single(listener.Events, e => Equals(e.Payload![2], listener.ConnectionId.ToString()));
        Assert.Equal(EventLevel.Error, entry.Level);
        Assert.Equal("Azure credential token request failed.", entry.Payload![0]);
    }

    /// <summary>The SDK allowlist requires a reviewed event ID and name together.</summary>
    [Theory]
    [InlineData(7, "LogMsalVerbose", EventLevel.Verbose)]
    [InlineData(8, "LogMsalInformational", EventLevel.Informational)]
    [InlineData(9, "LogMsalWarning", EventLevel.Warning)]
    [InlineData(10, "LogMsalError", EventLevel.Error)]
    [InlineData(23, "LogMsalCritical", EventLevel.Critical)]
    [InlineData(24, "LogMsalAlways", EventLevel.LogAlways)]
    [InlineData(8, "UnknownSchema", null)]
    [InlineData(19, "AuthenticatedAccountDetails", null)]
    [InlineData(0, null, null)]
    public void AzureSdkSeverity_MapsOnlyKnownEvents(int id, string? name, EventLevel? expected) =>
        Assert.Equal(expected, AzureAuthenticationEventListener.GetMsalLevel(id, name));

    /// <summary>Public acquisition emits correlated failure diagnostics without changing exception behavior.</summary>
    [Theory]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
    public async Task AcquireTokenFailure_LogsWithoutChangingFailure(SqlAuthenticationMethod method)
    {
        using var listener = new AuthenticationListener();
        var provider = new ActiveDirectoryAuthenticationProvider();
        var parameters = new SqlAuthenticationParameters(method, "test-server", "test-db",
            "https://database.windows.net/", "https://login.microsoftonline.com/", "", "", listener.ConnectionId, 30);
        await Assert.ThrowsAsync<AuthenticationException>(() => provider.AcquireTokenAsync(parameters));
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "Token acquisition started.") && Equals(entry.Payload[2], listener.ConnectionId.ToString()));
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "Token acquisition failed.") && Equals(entry.Payload[2], listener.ConnectionId.ToString()));
    }

    /// <summary>Real SDK success preserves the token while emitting sanitized provider and Azure events.</summary>
    [Fact]
    public async Task AcquireTokenSuccess_LogsWithoutApplicationCallbacks()
    {
        using var listener = new AuthenticationListener();
        using var handler = new TokenSuccessHandler();
        using var client = new HttpClient(handler);
        string clientId = Guid.NewGuid().ToString();
        var key = new ActiveDirectoryAuthenticationProvider.TokenCredentialKey(typeof(ClientSecretCredential),
            "https://login.microsoftonline.com/", "https://database.windows.net/.default",
            "11111111-2222-3333-4444-555555555555", clientId);
        var cache = Assert.IsType<ConcurrentDictionary<ActiveDirectoryAuthenticationProvider.TokenCredentialKey,
            ActiveDirectoryAuthenticationProvider.TokenCredentialData>>(typeof(ActiveDirectoryAuthenticationProvider)
            .GetField("s_tokenCredentialMap", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null));
        using SHA256 hash = SHA256.Create();
        Assert.True(cache.TryAdd(key, new ActiveDirectoryAuthenticationProvider.TokenCredentialData(
            CreateCredential(client, clientId), hash.ComputeHash(Encoding.Unicode.GetBytes("<secret>")))));
        try
        {
            var provider = new ActiveDirectoryAuthenticationProvider();
            var parameters = new SqlAuthenticationParameters(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
                "test-server", "test-db", "https://database.windows.net/.default",
                "https://login.microsoftonline.com/11111111-2222-3333-4444-555555555555",
                clientId, "<secret>", listener.ConnectionId, 30);
            SqlAuthenticationToken token = await provider.AcquireTokenAsync(parameters);
            Assert.Equal("<access-token>", token.AccessToken);
            Assert.Contains(listener.Events, entry => Equals(entry.Payload![1], "Azure-Identity"));
            Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "Token acquisition succeeded."));
            Assert.DoesNotContain(listener.Events, entry => Equals(entry.Payload![0], "Token acquisition failed."));
            Assert.All(listener.Events, entry => Assert.DoesNotContain("<access-token>", string.Join(" ", entry.Payload!)));
        }
        finally
        {
            Assert.True(cache.TryRemove(key, out _));
        }
    }

    /// <summary>The default provider traces Azure credential authentication on sync and async open paths.</summary>
    [ConditionalTheory(typeof(Config), nameof(Config.IsAzureSqlConnectionString), nameof(Config.HasServicePrincipal))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultProvider_ServicePrincipalLogsBothOpenPaths(bool async)
    {
        var builder = new SqlConnectionStringBuilder(Config.TCPConnectionString)
        {
            Authentication = SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
            UserID = Config.ServicePrincipalId,
            Password = Config.ServicePrincipalSecret,
            IntegratedSecurity = false,
            Pooling = false,
            ApplicationName = $"AuthenticationLogging-{Guid.NewGuid()}"
        };
        using var listener = new AuthenticationListener(captureAll: true);
        using var connection = new SqlConnection(builder.ConnectionString);
        if (async)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![1], "Azure-Identity"));
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "Token acquisition succeeded.") &&
            Equals(entry.Payload[2], connection.ClientConnectionId.ToString()));
    }

    /// <summary>The default provider traces direct MSAL authentication on sync and async open paths.</summary>
    [ConditionalTheory(typeof(Config), nameof(Config.IsAzureSqlConnectionString), nameof(Config.SupportsEntraIntegrated))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IntegratedProvider_LogsBothOpenPaths(bool async)
    {
        var builder = new SqlConnectionStringBuilder(Config.TCPConnectionString)
        {
            Authentication = SqlAuthenticationMethod.ActiveDirectoryIntegrated,
            IntegratedSecurity = false,
            Pooling = false,
            ApplicationName = $"AuthenticationLogging-{Guid.NewGuid()}"
        };
        builder.Remove("User ID");
        builder.Remove("Password");
        using var listener = new AuthenticationListener(captureAll: true);
        using var connection = new SqlConnection(builder.ConnectionString);
        if (async)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![1], "MSAL") &&
            Equals(entry.Payload[2], connection.ClientConnectionId.ToString()));
    }

    /// <summary>Obtains a cached MSAL client without contacting an authentication service.</summary>
    private static Task<IPublicClientApplication> GetClient(ActiveDirectoryAuthenticationProvider provider) =>
        provider.GetPublicClientAppInstanceAsync(new("https://login.microsoftonline.com/11111111-2222-3333-4444-555555555555",
            "http://localhost", provider.ApplicationClientId
#if NETFRAMEWORK
            , null
#endif
        ), CancellationToken.None);

    /// <summary>Keeps disposed sources outside the collecting method's JIT roots.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedSource()
    {
        using var source = new ControlledIdentitySource();
        return new WeakReference(source);
    }

    /// <summary>Creates a credential whose entire HTTP transport is intercepted locally.</summary>
    private static ClientSecretCredential CreateCredential(HttpClient client, string? clientId = null)
    {
        var options = new ClientSecretCredentialOptions
        {
            Transport = new HttpClientTransport(client),
            DisableInstanceDiscovery = true
        };
        ActiveDirectoryAuthenticationProvider.ConfigureCredentialLogging(options);
        return new ClientSecretCredential("11111111-2222-3333-4444-555555555555",
            clientId ?? "11111111-2222-3333-4444-555555555555", "<secret>", options);
    }

    /// <summary>Requests an SDK token through a local test transport.</summary>
    private static async Task RequestToken(ClientSecretCredential credential, string? requestId = null) =>
        await credential.GetTokenAsync(new TokenRequestContext(["https://database.windows.net/.default"], parentRequestId: requestId));

    /// <summary>Returns a local OAuth failure containing text that must not enter the trace.</summary>
    private sealed class TokenFailureHandler : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_client\",\"error_description\":\"private SDK exception data\"}")
            });
    }

    /// <summary>Returns a synthetic token without using credentials or contacting Azure.</summary>
    private sealed class TokenSuccessHandler : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"<access-token>\",\"expires_in\":3600,\"token_type\":\"Bearer\"}")
            });
    }

    /// <summary>Provides deterministic subscription interleavings using reviewed SDK schemas.</summary>
    [EventSource(Name = "Azure-Identity", Guid = "A02D753B-4CBB-4D93-A496-6B613D3E1080")]
    private sealed class ControlledIdentitySource : EventSource
    {
        /// <summary>Creates the named fixture with a distinct provider GUID.</summary>
        internal ControlledIdentitySource() { }
        /// <summary>Creates a named provider to exercise source-name filtering.</summary>
        internal ControlledIdentitySource(string name) : base(name) { }
        /// <summary>Gets or sets the hook executed under the EventSource subscription lock.</summary>
        internal Action? Enabling;
        /// <inheritdoc />
        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                Enabling?.Invoke();
            }
        }
        /// <summary>Emits the SDK Verbose schema.</summary>
        [Event(7, Level = EventLevel.Verbose)]
        public void LogMsalVerbose(string message) => WriteEvent(7, message);
        /// <summary>Emits the SDK Always schema.</summary>
        [Event(24, Level = EventLevel.LogAlways)]
        public void LogMsalAlways(string message) => WriteEvent(24, message);
    }

    /// <summary>Represents a new source constructed during a reentrant subscription command.</summary>
    [EventSource(Name = "Azure-Identity", Guid = "BE467792-4AF4-43B8-A1B4-AB8BCB48F7F9")]
    private sealed class LateIdentitySource : EventSource
    {
        /// <summary>Emits the SDK Informational schema.</summary>
        [Event(8, Level = EventLevel.Informational)]
        public void LogMsalInformational(string message) => WriteEvent(8, message);
    }

    /// <summary>Emits malformed and sensitive schemas to exercise bridge filtering.</summary>
    [EventSource(Name = "Azure-Identity", Guid = "02B917EC-CB06-4B1C-A40B-CE1469D30B13")]
    private sealed class InvalidSchemaIdentitySource : EventSource
    {
        /// <summary>Emits the right event name with the wrong payload type.</summary>
        [Event(7, Level = EventLevel.Verbose)]
        public void LogMsalVerbose(int message) => WriteEvent(7, message);
        /// <summary>Emits the right event name with the wrong ID.</summary>
        [Event(20, Level = EventLevel.Error)]
        public void LogMsalError(string message) => WriteEvent(20, message);
        /// <summary>Emits an unsupported account-information event.</summary>
        [Event(19, Level = EventLevel.Informational)]
        public void AuthenticatedAccountDetails(string message) => WriteEvent(19, message);
        /// <summary>Emits credential selection with the reviewed SDK schema.</summary>
        [Event(13, Level = EventLevel.Informational)]
        public void DefaultAzureCredentialCredentialSelected(string credentialType) => WriteEvent(13, credentialType);
        /// <summary>Emits the SDK failure schema with raw exception text that must be replaced.</summary>
        [Event(3, Level = EventLevel.Informational)]
        public void GetTokenFailed(string fullyQualifiedMethodName, string scopes, string parentRequestId, string exception) =>
            WriteEvent(3, fullyQualifiedMethodName, scopes, parentRequestId, exception);
    }

    /// <summary>Collects authentication events without affecting unrelated diagnostic consumers.</summary>
    private sealed class AuthenticationListener : EventListener
    {
        private EventSource? _source;
        private readonly bool _captureAll;
        /// <summary>Gets the correlated authentication events.</summary>
        internal ConcurrentQueue<EventWrittenEventArgs> Events { get; } = new();
        /// <summary>Gets the connection identifier isolated to this collector.</summary>
        internal Guid ConnectionId { get; } = Guid.NewGuid();
        /// <summary>Counts ordinary Trace events independently of authentication events.</summary>
        internal int TraceEvents;

        /// <summary>Creates a collector for this connection and uncorrelated events, or for every connection.</summary>
        internal AuthenticationListener(bool captureAll = false, EventLevel level = EventLevel.Verbose)
        {
            _captureAll = captureAll;
            _source = SqlClientEventSource.Log;
            Enable(level, SqlClientEventSource.Keywords.AuthenticationTrace);
        }
        /// <summary>Changes keyword and severity demand without changing authentication clients.</summary>
        internal void Enable(EventLevel level, EventKeywords keywords) => EnableEvents(_source!, level, keywords);
        /// <summary>Stops collection without discarding previously captured events.</summary>
        internal void Disable() => DisableEvents(_source!);
        /// <summary>Creates an MSAL entry correlated to this collector's connection.</summary>
        internal LogEntry Entry(EventLogLevel level, string message) =>
            new() { EventLogLevel = level, Message = message, CorrelationId = ConnectionId.ToString() };
        /// <summary>Counts captured events with the given message.</summary>
        internal int Count(string message) => Events.Count(entry => Equals(entry.Payload![0], message));
        /// <summary>Disables the subscription explicitly, including on .NET Framework.</summary>
        public override void Dispose()
        {
            if (_source is not null)
            {
                DisableEvents(_source);
            }
            base.Dispose();
        }
        /// <inheritdoc />
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName == "Trace")
            {
                Interlocked.Increment(ref TraceEvents);
            }
            if (eventData.EventName?.StartsWith("Authentication", StringComparison.Ordinal) == true &&
                eventData.Payload?.Count == 3 && (_captureAll || Equals(eventData.Payload[2], ConnectionId.ToString()) ||
                Equals(eventData.Payload[2], Guid.Empty.ToString())))
            {
                Events.Enqueue(eventData);
            }
        }
    }
}
