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
        using (AuthenticationLogger.Begin(listener.ConnectionId))
        {
            AuthenticationLogger.Write(EventLevel.Informational, "authentication diagnostic", "MSAL");
            SqlClientEventSource.Log.TryTraceEvent("ordinary diagnostic");
        }

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
        using (AuthenticationLogger.Begin(listener.ConnectionId))
        {
            Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
            listener.Enable(EventLevel.Warning, SqlClientEventSource.Keywords.AuthenticationTrace);
            Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
            Assert.True(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Warning));
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = EventLogLevel.Informational, Message = "filtered" });
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = EventLogLevel.Warning, Message = "visible" });
            Assert.Equal("visible", Assert.Single(listener.Events).Payload![0]);
            listener.Disable();
            Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Warning));
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = EventLogLevel.Error, Message = "detached" });
            Assert.Single(listener.Events);
        }
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
        using (AuthenticationLogger.Begin(listener.ConnectionId))
        {
            Assert.True(AuthenticationLogger.MsalLogger.IsEnabled(sdk));
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = sdk, Message = "interface diagnostic" });
        }

        EventWrittenEventArgs entry = Assert.Single(listener.Events);
        Assert.Equal(expected, entry.Level);
        Assert.Equal(new object[] { "interface diagnostic", "MSAL", listener.ConnectionId.ToString() }, entry.Payload!);
    }

    /// <summary>Unknown MSAL severities are never forwarded.</summary>
    [Fact]
    public void MsalLogger_RejectsUndefinedSeverity()
    {
        using var listener = new AuthenticationListener();
        using (AuthenticationLogger.Begin(listener.ConnectionId))
        {
            Assert.False(AuthenticationLogger.MsalLogger.IsEnabled((EventLogLevel)99));
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = (EventLogLevel)99, Message = "unknown" });
        }

        Assert.Empty(listener.Events);
    }

    /// <summary>Reentrant collector work cannot recursively emit authentication diagnostics.</summary>
    [Fact]
    public void EventSource_ReentrantLoggingIsSuppressed()
    {
        using var listener = new AuthenticationListener();
        listener.OnAuthentication = () => AuthenticationLogger.MsalLogger.Log(
            new LogEntry { EventLogLevel = EventLogLevel.Error, Message = "recursive" });
        using (AuthenticationLogger.Begin(listener.ConnectionId))
        {
            AuthenticationLogger.Write(EventLevel.Informational, "outer", "MSAL");
        }

        Assert.Equal("outer", Assert.Single(listener.Events).Payload![0]);
    }

    /// <summary>Nested scopes use the innermost connection ID and restore the outer one.</summary>
    [Fact]
    public void NestedScopes_RestoreOuterScope()
    {
        using var listener = new AuthenticationListener(captureAll: true);
        Guid outerId = Guid.NewGuid();
        Guid innerId = Guid.NewGuid();
        using (AuthenticationLogger.Begin(outerId))
        {
            using (AuthenticationLogger.Begin(innerId))
            {
                AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = EventLogLevel.Informational, Message = "inner" });
            }
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = EventLogLevel.Informational, Message = "outer" });
        }

        Assert.Equal(new[] { innerId.ToString(), outerId.ToString() }, listener.Events.Select(entry => entry.Payload![2]));
        Assert.Null(AuthenticationLogger.CurrentScope);
    }

    /// <summary>A disposed scope invalidates background work that inherited its execution context.</summary>
    [Fact]
    public async Task DisposedScope_DoesNotCaptureInheritedBackgroundWork()
    {
        using var listener = new AuthenticationListener();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task background;
        using (AuthenticationLogger.Begin(listener.ConnectionId))
        {
            background = Task.Run(async () =>
            {
                await gate.Task;
                AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = EventLogLevel.Informational, Message = "after disposal" });
            });
        }
        gate.SetResult(true);
        await background;
        Assert.Empty(listener.Events);
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
        using (AuthenticationLogger.Begin(listener.ConnectionId))
        {
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = EventLogLevel.Informational, Message = "first" });
        }
        listener.Disable();
        Assert.Same(client, await GetClient(first));
        using (AuthenticationLogger.Begin(listener.ConnectionId))
        {
            Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
        }
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
        using var listener = new AuthenticationListener();
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
            using (AuthenticationLogger.Begin(listener.ConnectionId))
            {
                Assert.Single(await reused.GetAccountsAsync());
            }
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

    /// <summary>Canceling the first acquisition wait leaves shared subscription work available to later acquisitions.</summary>
    [Fact]
    public async Task AzureSdkEvents_FirstSubscriptionOwnerHonorsCancellation()
    {
        _ = await AzureAuthenticationEventListener.GetInstanceAsync();
        using var listener = new AuthenticationListener();
        using var source = new ControlledIdentitySource();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        source.Enabling = () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(15)))
            {
                throw new TimeoutException("Subscription owner test was not released.");
            }
        };
        using var cancellation = new CancellationTokenSource();
        Task initial = Task.Run(async () =>
        {
            using AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(Guid.NewGuid(), captureAzure: true);
            await scope.WaitForReadyAsync(cancellation.Token);
        });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CompleteWithinTimeout(initial));
        }
        finally
        {
            source.Enabling = null;
            release.Set();
        }
        using AuthenticationLogger.Scope next = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true);
        await CompleteWithinTimeout(next.WaitForReadyAsync(CancellationToken.None));
        source.LogMsalVerbose("next acquisition");
        Assert.Equal("next acquisition", Assert.Single(listener.Events).Payload![0]);
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

    /// <summary>Real Azure SDK diagnostics are scoped and do not forward raw credential exception text.</summary>
    [Fact]
    public async Task AzureSdkEvents_AreScopedAndNonPii()
    {
        using var handler = new TokenFailureHandler();
        using var httpClient = new HttpClient(handler);
        ClientSecretCredential credential = CreateCredential(httpClient);
        using var listener = new AuthenticationListener();
        using (AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        }
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![1], "Azure-Identity"));
        Assert.All(listener.Events, entry => Assert.DoesNotContain("private SDK exception data", string.Join(" ", entry.Payload!)));
        int count = listener.Events.Count;
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        Assert.Equal(count, listener.Events.Count);
    }

    /// <summary>Real SDK capture follows late attachment and detachment without rebuilding credentials.</summary>
    [Fact]
    public async Task AzureSdkEvents_FollowEventSourceEnablement()
    {
        using var handler = new TokenFailureHandler();
        using var httpClient = new HttpClient(handler);
        ClientSecretCredential credential = CreateCredential(httpClient);
        using var listener = new AuthenticationListener();
        listener.Disable();
        using (AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
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
    }

    /// <summary>Ordinary Trace demand alone must not subscribe to or forward Azure SDK diagnostics.</summary>
    [Theory]
    [InlineData(0x2, false)]
    [InlineData(0x2000, true)]
    [InlineData(0x2002, true)]
    public async Task AzureSdkEvents_RequireAuthenticationKeyword(long keywords, bool expected)
    {
        using var listener = new AuthenticationListener();
        listener.Disable();
        listener.Enable(EventLevel.Verbose, (EventKeywords)keywords);
        using var source = new ControlledIdentitySource();
        using AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true);
        await scope.WaitForReadyAsync(CancellationToken.None);
        Assert.Equal(expected, source.IsEnabled());
        source.LogMsalVerbose("keyword diagnostic");
        Assert.Equal(expected ? 1 : 0, listener.Events.Count);
    }

    /// <summary>SDK-generated provider suffixes retain late tracing attachment and detachment.</summary>
    [Fact]
    public async Task AzureSdkEvents_RenamedSourceFollowsEventSourceEnablement()
    {
        Type sourceType = typeof(ClientSecretCredential).Assembly.GetType("Azure.Identity.AzureIdentityEventSource", throwOnError: true)!;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic;
        _ = sourceType.GetProperty("Singleton", flags | BindingFlags.Static)!.GetValue(null);
        using var source = (EventSource)Activator.CreateInstance(sourceType, nonPublic: true)!;
        Assert.Null(source.ConstructionException);
        Assert.StartsWith("Azure-Identity-", source.Name);
        MethodInfo write = sourceType.GetMethod("LogMsalVerbose", flags | BindingFlags.Instance)!;
        using var listener = new AuthenticationListener();
        listener.Disable();
        using (AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            write.Invoke(source, new object[] { "before tracing" });
            Assert.Empty(listener.Events);
            listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.AuthenticationTrace);
            write.Invoke(source, new object[] { "renamed SDK source" });
            EventWrittenEventArgs entry = Assert.Single(listener.Events);
            Assert.Equal(EventLevel.Verbose, entry.Level);
            Assert.Equal(new object[] { "renamed SDK source", "Azure-Identity", listener.ConnectionId.ToString() }, entry.Payload!);
            listener.Disable();
            write.Invoke(source, new object[] { "after tracing" });
            Assert.Single(listener.Events);
        }
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
    public async Task AzureSdkEvents_IgnoreUnsupportedSourceNames(string sourceName)
    {
        using var source = new ControlledIdentitySource(sourceName);
        Assert.Null(source.ConstructionException);
        using var listener = new AuthenticationListener();
        using (AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            source.LogMsalVerbose("unrecognized provider");
        }
        Assert.Empty(listener.Events);
    }

    /// <summary>Concurrent Azure SDK requests retain their own connection correlation across awaits.</summary>
    [Fact]
    public async Task AzureSdkEvents_ConcurrentProvidersRemainIsolated()
    {
        using var listener = new AuthenticationListener(captureAll: true);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrived = 0;
        async Task Acquire(Guid connectionId)
        {
            using var handler = new TokenFailureHandler();
            using var client = new HttpClient(handler);
            ClientSecretCredential credential = CreateCredential(client, Guid.NewGuid().ToString());
            using AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(connectionId, captureAzure: true);
            await scope.WaitForReadyAsync(CancellationToken.None);
            if (Interlocked.Increment(ref arrived) == 2)
            {
                gate.SetResult(true);
            }
            await gate.Task;
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        }
        await Task.WhenAll(Acquire(firstId), Acquire(secondId));
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![2], firstId.ToString()));
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![2], secondId.ToString()));
        Assert.All(listener.Events, entry => Assert.Contains(entry.Payload![2], new object[] { firstId.ToString(), secondId.ToString() }));
    }

    /// <summary>A Critical collector includes Always diagnostics without enabling Verbose SDK logging.</summary>
    [Fact]
    public async Task AzureSdkEvents_CriticalCollectorIncludesAlways()
    {
        using var listener = new AuthenticationListener(level: EventLevel.Critical);
        using var source = new ControlledIdentitySource();
        using (AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            source.LogMsalVerbose("filtered");
            source.LogMsalAlways("always");
            Assert.False(source.IsEnabled(EventLevel.Verbose, EventKeywords.None));
        }
        Assert.Equal("always", Assert.Single(listener.Events).Payload![0]);
    }

    /// <summary>Independent collectors receive only their own requested authentication levels.</summary>
    [Fact]
    public async Task AzureSdkEvents_CollectorsFilterIndependently()
    {
        using var warning = new AuthenticationListener(captureAll: true, level: EventLevel.Warning);
        using var verbose = new AuthenticationListener();
        using var source = new ControlledIdentitySource();
        using (AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(verbose.ConnectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            source.LogMsalVerbose("verbose");
            Assert.Single(verbose.Events);
            Assert.Empty(warning.Events);
            verbose.Disable();
            source.LogMsalVerbose("after verbose collector detached");
            Assert.Single(verbose.Events);
            Assert.Empty(warning.Events);
        }
    }

    /// <summary>Every acquisition waits for an in-progress subscription and can cancel its own wait.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AzureSdkEvents_ConcurrentSubscriptionReadiness(bool cancel)
    {
        _ = await AzureAuthenticationEventListener.GetInstanceAsync();
        using var listener = new AuthenticationListener();
        using var source = new ControlledIdentitySource();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int once = 1;
        source.Enabling = () =>
        {
            if (Interlocked.Exchange(ref once, 0) == 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new TimeoutException("Subscription readiness test was not released.");
                }
            }
        };
        Task initial = Task.Run(async () =>
        {
            using AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(Guid.NewGuid(), captureAzure: true);
            await scope.WaitForReadyAsync(CancellationToken.None);
            await finished.Task;
        });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            using AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true);
            using var cancellation = new CancellationTokenSource();
            Task ready = scope.WaitForReadyAsync(cancellation.Token);
            Assert.False(ready.IsCompleted);
            var provider = new ActiveDirectoryAuthenticationProvider();
            var parameters = new SqlAuthenticationParameters(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
                "test-server", "test-db", "https://database.windows.net/", "https://login.microsoftonline.com/",
                "", "", Guid.NewGuid(), cancel ? 1 : 30);
            Task acquisition = provider.AcquireTokenAsync(parameters);
            Assert.False(acquisition.IsCompleted);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready);
                AuthenticationException failure = await Assert.ThrowsAsync<AuthenticationException>(() => CompleteWithinTimeout(acquisition));
                Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
            }
            else
            {
                release.Set();
                await CompleteWithinTimeout(ready);
                source.LogMsalVerbose("first diagnostic");
                Assert.Equal("first diagnostic", Assert.Single(listener.Events).Payload![0]);
                await Assert.ThrowsAsync<AuthenticationException>(() => CompleteWithinTimeout(acquisition));
            }
        }
        finally
        {
            source.Enabling = null;
            release.Set();
            finished.TrySetResult(true);
            await CompleteWithinTimeout(initial);
        }
        await CompleteWithinTimeout((await AzureAuthenticationEventListener.GetInstanceAsync()).WaitForRefreshAsync());
        Assert.False(source.IsEnabled());
    }

    /// <summary>Reentrant collector commands and newly constructed SDK sources cannot lose first events.</summary>
    [Fact]
    public async Task AzureSdkEvents_ReentrantCommandsAndNewSourcesRemainReady()
    {
        using var listener = new AuthenticationListener(level: EventLevel.Informational);
        using var source = new ControlledIdentitySource();
        using AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true);
        await scope.WaitForReadyAsync(CancellationToken.None);
        ExecutionContext context = ExecutionContext.Capture()!;
        int once = 1;
        source.Enabling = () =>
        {
            if (Interlocked.Exchange(ref once, 0) == 1)
            {
                ExecutionContext.Run(context, _ =>
                {
                    listener.Disable();
                    listener.Enable(EventLevel.Informational, SqlClientEventSource.Keywords.AuthenticationTrace);
                    using var lateSource = new LateIdentitySource();
                    lateSource.LogMsalInformational("created during refresh");
                }, null);
            }
        };
        try
        {
            listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.AuthenticationTrace);
            Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "created during refresh"));
            Assert.False(source.IsEnabled(EventLevel.Verbose, EventKeywords.None));
        }
        finally
        {
            source.Enabling = null;
        }
    }

    /// <summary>Disposed SDK sources are not kept alive by the shared listener.</summary>
    [Fact]
    public async Task AzureSdkEvents_DoNotRetainDisposedSources()
    {
        _ = await AzureAuthenticationEventListener.GetInstanceAsync();
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
    public async Task AzureListener_RejectsUnknownPayloadsAndSanitizesFailures()
    {
        using var listener = new AuthenticationListener();
        using var source = new InvalidSchemaIdentitySource();
        using AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true);
        await scope.WaitForReadyAsync(CancellationToken.None);
        source.LogMsalVerbose(42);
        source.LogMsalError("wrong event id");
        source.AuthenticatedAccountDetails("private account");
        source.GetTokenFailed("private exception");
        EventWrittenEventArgs entry = Assert.Single(listener.Events);
        Assert.Equal("Azure credential token request failed.", entry.Payload![0]);
        source.DefaultAzureCredentialCredentialSelected("ClientSecretCredential");
        Assert.Contains(listener.Events, e => Equals(e.Payload![0], "DefaultAzureCredential selected ClientSecretCredential."));
    }

    /// <summary>Error and Warning collectors still receive SDK failures that Azure emits at Informational.</summary>
    [Theory]
    [InlineData(EventLevel.Error)]
    [InlineData(EventLevel.Warning)]
    public async Task AzureSdkEvents_LowVerbosityCollectorsReceiveTokenFailures(EventLevel level)
    {
        using var listener = new AuthenticationListener(level: level);
        using var source = new InvalidSchemaIdentitySource();
        using AuthenticationLogger.Scope scope = AuthenticationLogger.Begin(listener.ConnectionId, captureAzure: true);
        await scope.WaitForReadyAsync(CancellationToken.None);
        source.DefaultAzureCredentialCredentialSelected("ClientSecretCredential");
        source.GetTokenFailed("private exception");
        EventWrittenEventArgs entry = Assert.Single(listener.Events);
        Assert.Equal(EventLevel.Error, entry.Level);
        Assert.Equal("Azure credential token request failed.", entry.Payload![0]);
    }

    /// <summary>Collector work during a lifecycle event is not attributed to the acquisition and cannot recurse.</summary>
    [Fact]
    public async Task AcquireToken_ReentrantCollectorWorkIsSuppressed()
    {
        using var listener = new AuthenticationListener(captureAll: true);
        using var source = new ControlledIdentitySource();
        var provider = new ActiveDirectoryAuthenticationProvider();
        Guid nestedId = Guid.NewGuid();
        int reentered = 0;
        listener.OnAuthentication = () =>
        {
            if (Interlocked.Exchange(ref reentered, 1) == 0)
            {
                // Simulates a collector that uses an Azure credential and opens a SqlClient connection.
                source.LogMsalAlways("collector work");
                var nested = new SqlAuthenticationParameters(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
                    "test-server", "test-db", "https://database.windows.net/", "https://login.microsoftonline.com/",
                    "", "", nestedId, 30);
                Assert.ThrowsAsync<AuthenticationException>(() => provider.AcquireTokenAsync(nested)).GetAwaiter().GetResult();
            }
        };
        var parameters = new SqlAuthenticationParameters(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
            "test-server", "test-db", "https://database.windows.net/", "https://login.microsoftonline.com/",
            "", "", listener.ConnectionId, 30);
        await Assert.ThrowsAsync<AuthenticationException>(() => provider.AcquireTokenAsync(parameters));

        Assert.Equal(1, reentered);
        Assert.Single(listener.Events, entry => Equals(entry.Payload![0], "Token acquisition started."));
        Assert.DoesNotContain(listener.Events, entry => Equals(entry.Payload![0], "collector work"));
        Assert.DoesNotContain(listener.Events, entry => Equals(entry.Payload![2], nestedId.ToString()));
        Assert.Null(AuthenticationLogger.CurrentScope);
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
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "Token acquisition started."));
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "Token acquisition failed."));
        Assert.Null(AuthenticationLogger.CurrentScope);
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
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![1], "Azure-Identity") &&
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

    /// <summary>Bounds concurrency regressions without using delays to order operations.</summary>
    private static async Task CompleteWithinTimeout(Task task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))));
        await task;
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
    private static async Task RequestToken(ClientSecretCredential credential) =>
        await credential.GetTokenAsync(new TokenRequestContext(["https://database.windows.net/.default"]));

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
        /// <summary>Emits raw exception text that must be replaced.</summary>
        [Event(3, Level = EventLevel.Informational)]
        public void GetTokenFailed(string message) => WriteEvent(3, message);
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
        /// <summary>Gets or sets a hook used to verify reentrant collector behavior.</summary>
        internal Action? OnAuthentication { get; set; }

        /// <summary>Creates a correlated collector or captures every connection for integration tests.</summary>
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
                eventData.Payload?.Count == 3 && (_captureAll || Equals(eventData.Payload[2], ConnectionId.ToString())))
            {
                Events.Enqueue(eventData);
                OnAuthentication?.Invoke();
            }
        }
    }
}
