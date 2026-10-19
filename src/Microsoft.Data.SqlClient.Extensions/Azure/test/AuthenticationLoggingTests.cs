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
using System.Text.Json;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Abstractions;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Covers authentication logging privacy, sink independence, and SDK adaptation without live credentials.
/// </summary>
[Collection("SqlAuthenticationProvider")]
public class AuthenticationLoggingTests
{
    /// <summary>
    /// New options must retain safe defaults and expose only SqlClient-owned logging contracts.
    /// </summary>
    [Fact]
    public void Options_DefaultsUseOwnedNonPiiContract()
    {
        var options = new ActiveDirectoryAuthenticationProviderOptions();
        Assert.Null(options.AuthenticationLoggingCallback);
        Assert.False(options.EnableMsalPiiLogging);
        Assert.Equal(typeof(Action<SqlAuthenticationLogEntry>),
            typeof(ActiveDirectoryAuthenticationProviderOptions).GetProperty("AuthenticationLoggingCallback")!.PropertyType);
        Assert.Null(typeof(ActiveDirectoryAuthenticationProviderOptions).GetProperty("LogLevel"));
        Assert.Null(typeof(ActiveDirectoryAuthenticationProviderOptions).GetProperty("LoggingCallback"));
        Assert.Null(typeof(ActiveDirectoryAuthenticationProviderOptions).GetProperty("EnablePiiLogging"));
        Assert.Equal(new[] { "Always", "Critical", "Error", "Warning", "Informational", "Verbose" },
            Enum.GetNames(typeof(SqlAuthenticationLogLevel)));
        Assert.All(typeof(SqlAuthenticationLogEntry).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    /// <summary>
    /// Undefined SDK severities must not produce callback entries or tracing events.
    /// </summary>
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(99)]
    public void MsalCallback_UndefinedSeverityIsIgnored(int value)
    {
        using var listener = new AuthenticationListener();
        var entries = new List<SqlAuthenticationLogEntry>();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions { AuthenticationLoggingCallback = entries.Add });
        using (logger.Begin(listener.ConnectionId))
        {
            logger.LogMsal((LogLevel)value, "unknown severity", false);
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = (EventLogLevel)value, Message = "unknown severity" });
            Assert.False(AuthenticationLogger.MsalLogger.IsEnabled((EventLogLevel)value));
        }
        Assert.Empty(entries);
        Assert.Empty(listener.Events);
    }

    /// <summary>
    /// Every supported MSAL severity is translated explicitly into the owned contract.
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Always, SqlAuthenticationLogLevel.Always)]
    [InlineData(LogLevel.Error, SqlAuthenticationLogLevel.Error)]
    [InlineData(LogLevel.Warning, SqlAuthenticationLogLevel.Warning)]
    [InlineData(LogLevel.Info, SqlAuthenticationLogLevel.Informational)]
    [InlineData(LogLevel.Verbose, SqlAuthenticationLogLevel.Verbose)]
    public void MsalCallback_MapsSeverityAndCorrelation(LogLevel level, SqlAuthenticationLogLevel expected)
    {
        var entries = new List<SqlAuthenticationLogEntry>();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entries.Add
        });
        Guid connectionId = Guid.NewGuid();
        using (logger.Begin(connectionId))
        {
            logger.LogMsal(level, "diagnostic", false);
        }

        SqlAuthenticationLogEntry entry = Assert.Single(entries);
        Assert.Equal(expected, entry.Level);
        Assert.Equal(connectionId, entry.ConnectionId);
        Assert.Equal("MSAL", entry.Source);
        Assert.Equal("diagnostic", entry.Message);
        Assert.False(entry.ContainsPii);
    }

    /// <summary>
    /// Callback PII opt-in must never change the non-PII EventSource stream.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MsalCallback_PiiNeverReachesEventSource(bool enablePii)
    {
        using var listener = new AuthenticationListener();
        var entries = new List<SqlAuthenticationLogEntry>();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entries.Add,
            EnableMsalPiiLogging = enablePii
        });
        using (logger.Begin(listener.ConnectionId))
        {
            logger.LogMsal(LogLevel.Info, "non-PII diagnostic", false);
            logger.LogMsal(LogLevel.Info, "PII diagnostic", true);
        }

        Assert.Equal(enablePii ? 2 : 1, entries.Count);
        EventWrittenEventArgs written = Assert.Single(listener.Events);
        Assert.Contains("non-PII diagnostic", written.Payload!.OfType<string>());
        Assert.DoesNotContain(listener.Events, e => e.Payload!.OfType<string>().Contains("PII diagnostic"));
    }

    /// <summary>
    /// A failing application logger must not escape or prevent independent EventSource output.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallbackFailure_ReportsSanitizedDiagnostic(bool cancellationException)
    {
        using var listener = new AuthenticationListener();
        int calls = 0;
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = _ =>
            {
                calls++;
                throw cancellationException
                    ? new OperationCanceledException("private callback data")
                    : new InvalidOperationException("private callback data");
            }
        });
        using (logger.Begin(listener.ConnectionId))
        {
            logger.LogMsal(LogLevel.Warning, "authentication diagnostic", false);
            logger.LogMsal(LogLevel.Warning, "subsequent diagnostic", false);
        }

        Assert.Equal(2, calls);
        Assert.Equal(2, listener.Events.Count(e => e.Level == EventLevel.Warning));
        Assert.Equal(2, listener.Events.Count(e => e.Level == EventLevel.Error));
        Assert.DoesNotContain(listener.Events,
            e => e.Payload!.OfType<string>().Any(value => value.Contains("private callback data")));
    }

    /// <summary>
    /// A snapshotted callback receives every eligible severity independently of tracing.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Callback_IsSnapshottedAndReceivesAllSeverities(bool collectTrace)
    {
        using var listener = new AuthenticationListener();
        if (!collectTrace)
            listener.Disable();
        var entries = new List<SqlAuthenticationLogEntry>();
        var options = new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entries.Add
        };
        var logger = new AuthenticationLogger(options);
        options.AuthenticationLoggingCallback = _ => throw new InvalidOperationException("changed options");

        using (logger.Begin(listener.ConnectionId))
        {
            foreach (EventLogLevel level in new[] { EventLogLevel.LogAlways, EventLogLevel.Critical,
                EventLogLevel.Error, EventLogLevel.Warning, EventLogLevel.Informational, EventLogLevel.Verbose })
            {
                Assert.True(AuthenticationLogger.MsalLogger.IsEnabled(level));
                AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = level, Message = "diagnostic" });
            }
        }
        Assert.Equal(Enum.GetValues(typeof(SqlAuthenticationLogLevel)).Cast<SqlAuthenticationLogLevel>(),
            entries.Select(entry => entry.Level));
        Assert.Equal(collectTrace ? 6 : 0, listener.Events.Count);
    }

    /// <summary>Mutating options cannot change an existing provider's callback or PII policy.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProviderConstruction_SnapshotsCallbackAndPiiOptions(bool enablePii)
    {
        var first = new List<SqlAuthenticationLogEntry>();
        var second = new List<SqlAuthenticationLogEntry>();
        var options = new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = first.Add,
            EnableMsalPiiLogging = enablePii
        };
        var original = new ActiveDirectoryAuthenticationProvider(options);
        options.AuthenticationLoggingCallback = second.Add;
        options.EnableMsalPiiLogging = !enablePii;
        var replacement = new ActiveDirectoryAuthenticationProvider(options);
        options.AuthenticationLoggingCallback = null;
        options.EnableMsalPiiLogging = false;

        using (original.Logger.Begin(Guid.NewGuid()))
        {
            AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Verbose, "original provider", false);
            AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "original PII", true);
        }
        using (replacement.Logger.Begin(Guid.NewGuid()))
        {
            AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Verbose, "replacement provider", false);
            AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "replacement PII", true);
        }

        Assert.Equal(enablePii ? 2 : 1, first.Count);
        Assert.Equal(enablePii ? 1 : 2, second.Count);
        Assert.All(first, entry => Assert.StartsWith("original", entry.Message));
        Assert.All(second, entry => Assert.StartsWith("replacement", entry.Message));
        Assert.Equal(enablePii, original.Logger.EnableMsalPiiLogging);
        Assert.Equal(!enablePii, replacement.Logger.EnableMsalPiiLogging);
    }

    /// <summary>
    /// Only the Trace keyword and the requested severity may enable authentication forwarding.
    /// </summary>
    [Fact]
    public void EventSource_RespectsKeywordsSeverityAndDetach()
    {
        using var listener = new AuthenticationListener();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions());
        using (logger.Begin(listener.ConnectionId))
        {
            listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.ExecutionTrace);
            logger.LogMsal(LogLevel.Error, "wrong keyword", false);
            Assert.Empty(listener.Events);

            listener.Enable(EventLevel.Warning, SqlClientEventSource.Keywords.Trace);
            logger.LogMsal(LogLevel.Info, "filtered", false);
            logger.LogMsal(LogLevel.Warning, "visible", false);
            Assert.Single(listener.Events);

            listener.Disable();
            logger.LogMsal(LogLevel.Error, "detached", false);
            Assert.Single(listener.Events);
        }
    }

    /// <summary>
    /// The authentication events have stable, distinct IDs and a valid manifest for external collectors.
    /// </summary>
    [Fact]
    public void EventSource_AuthenticationSchemaIsValid()
    {
        Type sourceType = typeof(SqlClientEventSource);
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
            var method = sourceType.GetMethod(name)!;
            var attribute = Assert.IsType<EventAttribute>(Assert.Single(method.GetCustomAttributes(typeof(EventAttribute), false)));
            Assert.Equal(id, attribute.EventId);
            Assert.Equal(level, attribute.Level);
            Assert.Equal(SqlClientEventSource.Keywords.Trace, attribute.Keywords);
            Assert.Equal(new[] { "message", "source", "connectionId" }, method.GetParameters().Select(parameter => parameter.Name));
        }
    }

    /// <summary>
    /// No callback runs recursively, including logging from a callback with an active acquisition scope.
    /// </summary>
    [Fact]
    public void Callback_ReentrantLoggingIsSuppressed()
    {
        int calls = 0;
        AuthenticationLogger? logger = null;
        logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = _ =>
            {
                calls++;
                logger!.LogMsal(LogLevel.Error, "recursive", false);
            }
        });
        using (logger.Begin(Guid.NewGuid()))
        {
            logger.LogMsal(LogLevel.Info, "initial", false);
        }
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Nested acquisitions restore the outer logger and its connection correlation.
    /// </summary>
    [Fact]
    public void NestedScopes_RestoreOuterLogger()
    {
        var outerEntries = new List<SqlAuthenticationLogEntry>();
        var innerEntries = new List<SqlAuthenticationLogEntry>();
        var outer = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions { AuthenticationLoggingCallback = outerEntries.Add });
        var inner = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions { AuthenticationLoggingCallback = innerEntries.Add });
        Guid outerId = Guid.NewGuid();
        Guid innerId = Guid.NewGuid();
        using (outer.Begin(outerId, captureAzure: true))
        {
            using (inner.Begin(innerId, captureAzure: true))
            {
                outer.LogMsal(LogLevel.Info, "wrong provider", false);
                inner.LogMsal(LogLevel.Info, "inner acquisition", false);
            }
            outer.LogMsal(LogLevel.Info, "outer acquisition", false);
        }
        Assert.Equal(innerId, Assert.Single(innerEntries).ConnectionId);
        Assert.Equal(outerId, Assert.Single(outerEntries).ConnectionId);
        Assert.Null(AuthenticationLogger.CurrentScope);
    }

    /// <summary>
    /// Cached clients share signed-in state while dispatching through the active provider policy.
    /// </summary>
    [Fact]
    public async Task CachedClients_IsolateLoggingPoliciesAndUseRealMsalCallback()
    {
        string clientId = Guid.NewGuid().ToString();
        var first = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions { ApplicationClientId = clientId });
        var equivalent = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions { ApplicationClientId = clientId });
        var entries = new List<SqlAuthenticationLogEntry>();
        var options = new ActiveDirectoryAuthenticationProviderOptions
        {
            ApplicationClientId = clientId,
            AuthenticationLoggingCallback = entries.Add
        };
        var withCallback = new ActiveDirectoryAuthenticationProvider(options);
        options.EnableMsalPiiLogging = true;
        var withPii = new ActiveDirectoryAuthenticationProvider(options);
        options.AuthenticationLoggingCallback = null;
        var withoutCallback = new ActiveDirectoryAuthenticationProvider(options);

        IPublicClientApplication firstApp = await GetClient(first);
        Assert.Same(firstApp, await GetClient(equivalent));
        Assert.Equal(CreateKey(first).GetHashCode(), CreateKey(equivalent).GetHashCode());
        IPublicClientApplication callbackApp = await GetClient(withCallback);
        Assert.Same(firstApp, callbackApp);
        Assert.Equal(CreateKey(first).GetHashCode(), CreateKey(withCallback).GetHashCode());
        IPublicClientApplication piiApp = await GetClient(withPii);
        Assert.NotSame(callbackApp, piiApp);
        Assert.Same(callbackApp, await GetClient(withoutCallback));
        Assert.Null(callbackApp.AppConfig.LoggingCallback);
        Assert.False(callbackApp.AppConfig.EnablePiiLogging);
        Assert.True(piiApp.AppConfig.EnablePiiLogging);
        Assert.Null(piiApp.AppConfig.LoggingCallback.Target);
        Assert.False(piiApp.AppConfig.IsDefaultPlatformLoggingEnabled);

        using (withCallback.Logger.Begin(Guid.NewGuid()))
        {
            AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "configured callback", false);
            await callbackApp.GetAccountsAsync();
        }
        Assert.Contains(entries, entry => entry.Message == "configured callback");
        Assert.Contains(entries, entry => entry.Message != "configured callback" && entry.Source == "MSAL");

        entries.Clear();
        using var listener = new AuthenticationListener();
        using (first.Logger.Begin(listener.ConnectionId))
        {
            AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "late attachment", false);
        }
        Assert.Single(listener.Events);
        Assert.Empty(entries);
    }

    /// <summary>Registering replacement providers preserves accounts and mutations without sharing other identities.</summary>
    [Fact]
    public async Task RegisteredProviders_PreserveSignedInCacheAcrossReplacement()
    {
        const SqlAuthenticationMethod method = SqlAuthenticationMethod.ActiveDirectoryIntegrated;
        SqlAuthenticationProvider original = Assert.IsAssignableFrom<SqlAuthenticationProvider>(SqlAuthenticationProvider.GetProvider(method));
        string clientId = Guid.NewGuid().ToString();
        var first = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions { ApplicationClientId = clientId });
        try
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(method, first));
            IPublicClientApplication primary = await GetClient(
                Assert.IsType<ActiveDirectoryAuthenticationProvider>(SqlAuthenticationProvider.GetProvider(method)));
            ITokenCacheSerializer serializer = Assert.IsAssignableFrom<ITokenCacheSerializer>(primary.UserTokenCache);
            string home = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.11111111-2222-3333-4444-555555555555";
            string json = $$$$"""
            {"Account":{"{{{{home}}}}-login.microsoftonline.com-11111111-2222-3333-4444-555555555555":{
                "home_account_id":"{{{{home}}}}","environment":"login.microsoftonline.com","realm":"11111111-2222-3333-4444-555555555555",
                "local_account_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","username":"user@example.invalid","authority_type":"MSSTS"}},
             "RefreshToken":{"{{{{home}}}}-login.microsoftonline.com-refreshtoken-{{{{clientId}}}}--":{
                "home_account_id":"{{{{home}}}}","environment":"login.microsoftonline.com","client_id":"{{{{clientId}}}}",
                "credential_type":"RefreshToken","secret":"<refresh-token>"}}}
            """;
            serializer.DeserializeMsalV3(System.Text.Encoding.UTF8.GetBytes(json), shouldClearExistingCache: true);
            Assert.Single(await primary.GetAccountsAsync());
            foreach (bool pii in new[] { false, true })
            {
                foreach (bool callback in new[] { false, true })
                {
                    var entries = new List<SqlAuthenticationLogEntry>();
                    var provider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
                    {
                        ApplicationClientId = clientId,
                        EnableMsalPiiLogging = pii,
                        AuthenticationLoggingCallback = callback ? entries.Add : null
                    });
                    Assert.True(SqlAuthenticationProvider.SetProvider(method, provider));
                    var registered = Assert.IsType<ActiveDirectoryAuthenticationProvider>(SqlAuthenticationProvider.GetProvider(method));
                    IPublicClientApplication client = await GetClient(registered);
                    Assert.Single(await client.GetAccountsAsync());
                    var cache = Assert.IsAssignableFrom<ITokenCacheSerializer>(client.UserTokenCache);
                    using JsonDocument snapshot = JsonDocument.Parse(cache.SerializeMsalV3());
                    JsonProperty refreshToken = Assert.Single(snapshot.RootElement.GetProperty("RefreshToken").EnumerateObject());
                    Assert.Equal("<refresh-token>", refreshToken.Value.GetProperty("secret").GetString());
                    if (!pii || !callback)
                        Assert.Same(primary, client);
                    using (registered.Logger.Begin(Guid.NewGuid()))
                    {
                        AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Verbose, "replacement diagnostic", false);
                        AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "replacement PII", true);
                    }
                    Assert.Equal(callback ? (pii ? 2 : 1) : 0, entries.Count);
                }
            }
            var sensitive = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = clientId,
                EnableMsalPiiLogging = true,
                AuthenticationLoggingCallback = _ => { }
            });
            Assert.True(SqlAuthenticationProvider.SetProvider(method, sensitive));
            IPublicClientApplication piiClient = await GetClient(
                Assert.IsType<ActiveDirectoryAuthenticationProvider>(SqlAuthenticationProvider.GetProvider(method)));
            var unrelated = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions { ApplicationClientId = Guid.NewGuid().ToString() });
            Assert.Empty(await (await GetClient(unrelated)).GetAccountsAsync());
            await Task.WhenAll(Enumerable.Range(0, 20).Select(index => Task.Run(async () =>
                Assert.Single(await (index % 2 == 0 ? primary : piiClient).GetAccountsAsync()))));
            byte[] primarySnapshot = serializer.SerializeMsalV3();
            await piiClient.RemoveAsync(Assert.Single(await piiClient.GetAccountsAsync()));
            Assert.Equal(primarySnapshot, serializer.SerializeMsalV3());
            Assert.Empty(await primary.GetAccountsAsync());
        }
        finally
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(method, original));
        }
    }

    /// <summary>AfterAccess following cancellation must neither release another client's lease nor overwrite tokens.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedClients_CanceledBeforeAccessDoesNotReleaseAnotherLease(bool cancelPii)
    {
        var primary = PublicClientApplicationBuilder.Create(Guid.NewGuid().ToString()).Build();
        var secondary = PublicClientApplicationBuilder.Create(primary.AppConfig.ClientId).Build();
        var cached = new ActiveDirectoryAuthenticationProvider.CachedPublicClientApplications(primary);
        cached.AddPiiClient(secondary);
        var primaryCache = Assert.IsAssignableFrom<ITokenCacheSerializer>(primary.UserTokenCache);
        var secondaryCache = Assert.IsAssignableFrom<ITokenCacheSerializer>(secondary.UserTokenCache);
        TokenCacheNotificationArgs Notification(bool pii, CancellationToken token, bool changed = false) =>
            new(pii ? secondaryCache : primaryCache, primary.AppConfig.ClientId, null, changed, false, null, false, null, token);
        TokenCacheNotificationArgs owner = Notification(!cancelPii, CancellationToken.None);
        await cached.BeforeAccessAsync(owner, pii: !cancelPii);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = Notification(cancelPii, cancellation.Token, changed: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cached.BeforeAccessAsync(canceled, cancelPii));
        byte[] before = primaryCache.SerializeMsalV3();
        await cached.AfterAccessAsync(canceled, cancelPii);
        Assert.Equal(before, primaryCache.SerializeMsalV3());
        TokenCacheNotificationArgs next = Notification(cancelPii, CancellationToken.None);
        Task waiting = cached.BeforeAccessAsync(next, cancelPii);
        Assert.False(waiting.IsCompleted);
        await cached.AfterAccessAsync(owner, pii: !cancelPii);
        await CompleteWithinTimeout(waiting);
        await cached.AfterAccessAsync(next, cancelPii);
        Assert.Empty(await primary.GetAccountsAsync());
        Assert.Empty(await secondary.GetAccountsAsync());
    }

    /// <summary>The non-PII interface logger maps SDK severities without exposing SDK contracts publicly.</summary>
    [Theory]
    [InlineData(EventLogLevel.LogAlways, SqlAuthenticationLogLevel.Always)]
    [InlineData(EventLogLevel.Critical, SqlAuthenticationLogLevel.Critical)]
    [InlineData(EventLogLevel.Error, SqlAuthenticationLogLevel.Error)]
    [InlineData(EventLogLevel.Warning, SqlAuthenticationLogLevel.Warning)]
    [InlineData(EventLogLevel.Informational, SqlAuthenticationLogLevel.Informational)]
    [InlineData(EventLogLevel.Verbose, SqlAuthenticationLogLevel.Verbose)]
    public void MsalLogger_MapsInterfaceSeverity(EventLogLevel sdk, SqlAuthenticationLogLevel expected)
    {
        var entries = new List<SqlAuthenticationLogEntry>();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entries.Add
        });
        using (logger.Begin(Guid.NewGuid()))
        {
            Assert.True(AuthenticationLogger.MsalLogger.IsEnabled(sdk));
            AuthenticationLogger.MsalLogger.Log(new LogEntry { EventLogLevel = sdk, Message = "interface diagnostic" });
        }
        SqlAuthenticationLogEntry entry = Assert.Single(entries);
        Assert.Equal(expected, entry.Level);
        Assert.Equal("interface diagnostic", entry.Message);
        Assert.False(entry.ContainsPii);
    }

    /// <summary>The non-PII SDK logger skips formatting when neither output needs the message.</summary>
    [Fact]
    public void MsalLogger_GatesFormattingOnCurrentOutputDemand()
    {
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions());
        using (logger.Begin(Guid.NewGuid()))
            Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
        using var listener = new AuthenticationListener();
        using (logger.Begin(listener.ConnectionId))
        {
            Assert.True(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
            Assert.True(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Verbose));
            listener.Disable();
            Assert.False(AuthenticationLogger.MsalLogger.IsEnabled(EventLogLevel.Informational));
        }
    }

    /// <summary>A cached client must not keep a released provider's application logging target alive.</summary>
    [Fact]
    public void CachedClients_DoNotRetainReleasedCallbackTargets()
    {
        WeakReference callback = CacheReleasedCallback();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(callback.IsAlive);
    }

    /// <summary>Prevents JIT local lifetimes from keeping the callback target alive during collection.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CacheReleasedCallback()
    {
        var entries = new List<SqlAuthenticationLogEntry>();
        var provider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
        {
            ApplicationClientId = Guid.NewGuid().ToString(),
            AuthenticationLoggingCallback = entries.Add
        });
        GetClient(provider).GetAwaiter().GetResult();
        return new WeakReference(entries);
    }

    /// <summary>Shared MSAL clients route callbacks through the current acquisition, not their creator.</summary>
    [Fact]
    public async Task CachedClients_DispatchToCurrentProviderOnly()
    {
        string clientId = Guid.NewGuid().ToString();
        var first = new List<SqlAuthenticationLogEntry>();
        var second = new List<SqlAuthenticationLogEntry>();
        var firstProvider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
        {
            ApplicationClientId = clientId,
            AuthenticationLoggingCallback = first.Add
        });
        var secondProvider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
        {
            ApplicationClientId = clientId,
            AuthenticationLoggingCallback = second.Add
        });
        IPublicClientApplication app = await GetClient(firstProvider);
        Assert.Same(app, await GetClient(secondProvider));
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        using (firstProvider.Logger.Begin(firstId))
        {
            AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "first provider", false);
            using (secondProvider.Logger.Begin(secondId))
            {
                await Task.Yield();
                AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "second provider", false);
            }
            AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "restored provider", false);
        }
        AuthenticationLogger.LogMsalForCurrentScope(LogLevel.Info, "no provider", false);
        Assert.Equal(new[] { "first provider", "restored provider" }, first.Select(entry => entry.Message));
        Assert.All(first, entry => Assert.Equal(firstId, entry.ConnectionId));
        Assert.Equal("second provider", Assert.Single(second).Message);
        Assert.Equal(secondId, second[0].ConnectionId);
    }

    /// <summary>
    /// Every credential option type used by the extension keeps all sensitive SDK logging flags disabled.
    /// </summary>
    [Fact]
    public void AzureCredentialOptions_DisableSensitiveLogging()
    {
        TokenCredentialOptions[] options =
        [
            new DefaultAzureCredentialOptions(),
            new ManagedIdentityCredentialOptions(ManagedIdentityId.SystemAssigned),
            new TokenCredentialOptions(),
            new WorkloadIdentityCredentialOptions()
        ];
        foreach (TokenCredentialOptions option in options)
        {
            option.IsUnsafeSupportLoggingEnabled = true;
            option.Diagnostics.IsAccountIdentifierLoggingEnabled = true;
            option.Diagnostics.IsLoggingContentEnabled = true;
            ActiveDirectoryAuthenticationProvider.ConfigureCredentialLogging(option);
            Assert.False(option.IsUnsafeSupportLoggingEnabled);
            Assert.False(option.Diagnostics.IsAccountIdentifierLoggingEnabled);
            Assert.False(option.Diagnostics.IsLoggingContentEnabled);
        }
    }

    /// <summary>
    /// Real SDK MSAL diagnostics are forwarded only inside an extension-owned Azure acquisition.
    /// </summary>
    [Fact]
    public async Task AzureSdkEvents_AreScopedAndNonPii()
    {
        var entries = new ConcurrentQueue<SqlAuthenticationLogEntry>();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entries.Enqueue,
            EnableMsalPiiLogging = true
        });
        using var handler = new TokenFailureHandler();
        using var httpClient = new HttpClient(handler);
        ClientSecretCredential credential = CreateCredential(httpClient);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        Assert.Empty(entries);
        Guid connectionId = Guid.NewGuid();
        using (AuthenticationLogger.Scope scope = logger.Begin(connectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        }

        Assert.Contains(entries, entry => entry.Source == "Azure-Identity" && entry.Message.Contains("MSAL"));
        Assert.All(entries, entry =>
        {
            Assert.Equal(connectionId, entry.ConnectionId);
            Assert.False(entry.ContainsPii);
            Assert.DoesNotContain("private SDK exception data", entry.Message);
        });
        Assert.True(handler.RequestCount > 0);
        Assert.Contains(entries, entry => entry.Level == SqlAuthenticationLogLevel.Verbose);
        int count = entries.Count;
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
        Assert.Equal(count, entries.Count);
    }

    /// <summary>
    /// SDK listener demand follows late tracing attachment and detachment within an existing request.
    /// </summary>
    [Fact]
    public async Task AzureSdkEvents_FollowEventSourceEnablement()
    {
        using var handler = new TokenFailureHandler();
        using var httpClient = new HttpClient(handler);
        ClientSecretCredential credential = CreateCredential(httpClient);
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions());
        using var listener = new AuthenticationListener();
        listener.Disable();
        using (AuthenticationLogger.Scope scope = logger.Begin(listener.ConnectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
            Assert.Empty(listener.Events);
            listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.Trace);
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
            Assert.Contains(listener.Events, entry => entry.Level == EventLevel.Verbose);
            int count = listener.Events.Count;
            listener.Disable();
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
            Assert.Equal(count, listener.Events.Count);
        }
    }

    /// <summary>
    /// Overlapping SDK operations must keep per-provider callbacks and connection IDs isolated across awaits.
    /// </summary>
    [Fact]
    public async Task AzureSdkEvents_ConcurrentProvidersRemainIsolated()
    {
        var first = new ConcurrentQueue<SqlAuthenticationLogEntry>();
        var second = new ConcurrentQueue<SqlAuthenticationLogEntry>();
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        var firstLogger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions { AuthenticationLoggingCallback = first.Enqueue });
        var secondLogger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = second.Enqueue
        });
        using var handler = new TokenFailureHandler();
        using var httpClient = new HttpClient(handler);
        ClientSecretCredential credential = CreateCredential(httpClient);
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0;
        await Task.WhenAll(Invoke(firstLogger, firstId), Invoke(secondLogger, secondId));
        Assert.NotEmpty(first);
        Assert.NotEmpty(second);
        Assert.All(first, entry => Assert.Equal(firstId, entry.ConnectionId));
        Assert.All(second, entry => Assert.Equal(secondId, entry.ConnectionId));
        Assert.Contains(first, entry => entry.Level == SqlAuthenticationLogLevel.Verbose);
        Assert.Contains(second, entry => entry.Level == SqlAuthenticationLogLevel.Verbose);

        async Task Invoke(AuthenticationLogger logger, Guid id)
        {
            using (AuthenticationLogger.Scope scope = logger.Begin(id, captureAzure: true))
            {
                await scope.WaitForReadyAsync(CancellationToken.None);
                if (Interlocked.Increment(ref started) == 2)
                {
                    ready.SetResult(true);
                }
                await ready.Task;
                await Assert.ThrowsAsync<AuthenticationFailedException>(() => RequestToken(credential));
            }
        }
    }

    /// <summary>The first subscription owner observes the authentication timeout, not only later waiters.</summary>
    [Fact]
    public async Task AzureSdkEvents_FirstSubscriptionOwnerHonorsTimeout()
    {
        _ = await AzureAuthenticationEventListener.GetInstanceAsync();
        using var source = new ControlledIdentitySource();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int once = 1;
        source.Enabling = () =>
        {
            if (Interlocked.Exchange(ref once, 0) == 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Subscription timeout test was not released.");
            }
        };
        var provider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions { AuthenticationLoggingCallback = _ => { } });
        var parameters = new SqlAuthenticationParameters(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
            "test-server", "test-db", "https://database.windows.net/", "not-an-authority", "", "", Guid.NewGuid(), 1);
        Task acquisition = Task.Run(() => provider.AcquireTokenAsync(parameters));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.Same(acquisition, await Task.WhenAny(acquisition, Task.Delay(TimeSpan.FromSeconds(3))));
            AuthenticationException failure = await Assert.ThrowsAsync<AuthenticationException>(() => acquisition);
            Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
        }
        finally
        {
            source.Enabling = null;
            release.Set();
            await Assert.ThrowsAsync<AuthenticationException>(() => acquisition);
        }
    }

    /// <summary>A newly attached collector receives its first diagnostic even inside an older refresh.</summary>
    [Fact]
    public async Task AzureSdkEvents_LateCollectorRetainsFirstDiagnostic()
    {
        using var source = new ControlledIdentitySource();
        using var listener = new AuthenticationListener();
        listener.Disable();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions());
        using AuthenticationLogger.Scope outer = logger.Begin(listener.ConnectionId, captureAzure: true);
        await outer.WaitForReadyAsync(CancellationToken.None);
        ExecutionContext context = ExecutionContext.Capture()!;
        int once = 1;
        source.Enabling = () =>
        {
            if (Interlocked.Exchange(ref once, 0) == 1)
            {
                ExecutionContext.Run(context, _ =>
                {
                    listener.Enable(EventLevel.Verbose, SqlClientEventSource.Keywords.Trace);
                    source.LogMsalVerbose("first diagnostic after attachment");
                }, null);
            }
        };
        try
        {
            var other = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions { AuthenticationLoggingCallback = _ => { } });
            using AuthenticationLogger.Scope inner = other.Begin(Guid.NewGuid(), captureAzure: true);
            await inner.WaitForReadyAsync(CancellationToken.None);
        }
        finally
        {
            source.Enabling = null;
        }
        Assert.Contains(listener.Events, e => Equals(e.Payload![0], "first diagnostic after attachment"));
    }

    /// <summary>Observing a disposed SDK source must not keep its object or defining assembly alive.</summary>
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

    /// <summary>Keeps the disposed source out of the collecting method's JIT local roots.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedSource()
    {
        using var source = new ControlledIdentitySource();
        Assert.Null(source.ConstructionException);
        return new WeakReference(source);
    }

    /// <summary>A null callback disables callback PII production without changing EventSource collection.</summary>
    [Fact]
    public void NullCallback_PreservesEventSourceAndDisablesPii()
    {
        using var listener = new AuthenticationListener();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            EnableMsalPiiLogging = true
        });
        int tracesBefore = listener.TraceEvents;
        using (logger.Begin(listener.ConnectionId, captureAzure: true))
        {
            SqlClientEventSource.Log.TryTraceEvent("Other SqlClient tracing remains enabled.");
            logger.LogMsal(LogLevel.Always, "callback disabled", false);
            logger.LogMsal(LogLevel.Info, "trace retained", false);
            logger.LogMsal(LogLevel.Info, "disabled PII", true);
        }
        Assert.Equal(2, listener.Events.Count);
        Assert.DoesNotContain(listener.Events, e => e.Payload!.OfType<string>().Contains("disabled PII"));
        Assert.False(logger.EnableMsalPiiLogging);
        Assert.True(listener.TraceEvents > tracesBefore);
    }

    /// <summary>Always diagnostics remain visible to a Critical collector without requesting Verbose capture.</summary>
    [Fact]
    public async Task AzureSdkEvents_CriticalCollectorIncludesAlways()
    {
        using var listener = new AuthenticationListener();
        listener.Disable();
        listener.Enable(EventLevel.Critical, SqlClientEventSource.Keywords.Trace);
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions());
        using var source = new ControlledIdentitySource();
        using AuthenticationLogger.Scope scope = logger.Begin(listener.ConnectionId, captureAzure: true);
        await scope.WaitForReadyAsync(CancellationToken.None);
        Assert.False(source.IsEnabled(EventLevel.Verbose, EventKeywords.None));
        source.LogMsalAlways("always diagnostic");
        Assert.Equal(EventLevel.LogAlways, Assert.Single(listener.Events).Level);
    }

    /// <summary>Callbacks receive all SDK severities independently of collector filtering and detachment.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AzureSdkEvents_CollectorFilteringIsIndependentOfCallback(bool hasCallback)
    {
        var entries = new List<SqlAuthenticationLogEntry>();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = hasCallback ? entries.Add : null
        });
        using var listener = new AuthenticationListener();
        using var source = new ControlledIdentitySource();
        using AuthenticationLogger.Scope scope = logger.Begin(listener.ConnectionId, captureAzure: true);
        await scope.WaitForReadyAsync(CancellationToken.None);
        Assert.True(source.IsEnabled(EventLevel.Verbose, EventKeywords.None));
        source.LogMsalVerbose("trace detail");
        source.LogMsalInformational("information");
        Assert.Equal(2, listener.Events.Count);
        Assert.Equal(hasCallback ? 2 : 0, entries.Count);

        listener.Enable(EventLevel.Warning, SqlClientEventSource.Keywords.Trace);
        source.LogMsalVerbose("collector filtered detail");
        source.LogMsalInformational("callback only");
        Assert.Equal(2, listener.Events.Count);
        Assert.Equal(hasCallback ? 4 : 0, entries.Count);

        listener.Disable();
        source.LogMsalVerbose("callback after collector detachment");
        Assert.Equal(2, listener.Events.Count);
        Assert.Equal(hasCallback ? 5 : 0, entries.Count);
    }

    /// <summary>Consumer filtering must neither suppress independent tracing nor admit PII into tracing.</summary>
    [Fact]
    public void MsalCallback_ConsumerFilteringPreservesPiiBoundary()
    {
        var entries = new List<SqlAuthenticationLogEntry>();
        int calls = 0;
        using var listener = new AuthenticationListener();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            EnableMsalPiiLogging = true,
            AuthenticationLoggingCallback = entry =>
            {
                calls++;
                if (entry.Level <= SqlAuthenticationLogLevel.Error)
                    entries.Add(entry);
            }
        });
        using (logger.Begin(listener.ConnectionId))
        {
            logger.LogMsal(LogLevel.Error, "callback PII", true);
            logger.LogMsal(LogLevel.Info, "filtered PII", true);
            logger.LogMsal(LogLevel.Verbose, "trace detail", false);
        }
        Assert.Equal(3, calls);
        Assert.Equal("callback PII", Assert.Single(entries).Message);
        Assert.Equal("trace detail", Assert.Single(listener.Events).Payload![0]);
    }

    /// <summary>SDK work waits for capture even when another acquisition owns subscription refresh.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AzureSdkEvents_ConcurrentSubscriptionReadiness(bool cancel)
    {
        _ = await AzureAuthenticationEventListener.GetInstanceAsync();
        using var source = new ControlledIdentitySource();
        Assert.Null(source.ConstructionException);
        Assert.Equal("Azure-Identity", source.Name);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entries = new ConcurrentQueue<SqlAuthenticationLogEntry>();
        var firstLogger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions { AuthenticationLoggingCallback = _ => { } });
        var provider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entries.Enqueue
        });
        AuthenticationLogger secondLogger = provider.Logger;
        int blockNext = 1;
        source.Enabling = () =>
        {
            if (Interlocked.Exchange(ref blockNext, 0) == 1)
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
            started.SetResult(true);
            using AuthenticationLogger.Scope scope = firstLogger.Begin(Guid.NewGuid(), captureAzure: true);
            await scope.WaitForReadyAsync(CancellationToken.None);
            await finished.Task;
        });
        try
        {
            await CompleteWithinTimeout(started.Task);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            using AuthenticationLogger.Scope scope = secondLogger.Begin(Guid.NewGuid(), captureAzure: true);
            using var cancellation = new CancellationTokenSource();
            Task ready = scope.WaitForReadyAsync(cancellation.Token);
            Assert.False(ready.IsCompleted);
            var parameters = new SqlAuthenticationParameters(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
                "test-server", "test-db", "https://database.windows.net/", "https://login.microsoftonline.com/",
                "", "", Guid.NewGuid(), cancel ? 1 : 30);
            Task acquisition = provider.AcquireTokenAsync(parameters);
            Assert.False(acquisition.IsCompleted);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready);
                AuthenticationException failure = await Assert.ThrowsAsync<AuthenticationException>(
                    () => CompleteWithinTimeout(acquisition));
                Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
            }
            else
            {
                release.Set();
                await CompleteWithinTimeout(ready);
                source.LogMsalVerbose("first diagnostic");
                SqlAuthenticationLogEntry entry = Assert.Single(entries, entry => entry.Source == "Azure-Identity");
                Assert.Equal("first diagnostic", entry.Message);
                Assert.Equal(scope.ConnectionId, entry.ConnectionId);
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
        using (AuthenticationLogger.Scope scope = firstLogger.Begin(Guid.NewGuid(), captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            Assert.True(source.IsEnabled(EventLevel.Informational, EventKeywords.None));
            Assert.True(source.IsEnabled(EventLevel.Verbose, EventKeywords.None));
        }
        await CompleteWithinTimeout((await AzureAuthenticationEventListener.GetInstanceAsync()).WaitForRefreshAsync());
        Assert.False(source.IsEnabled());
    }

    /// <summary>EventSource callbacks can change tracing and create SDK sources during a refresh.</summary>
    [Fact]
    public async Task AzureSdkEvents_ReentrantCommandsAndNewSourcesRemainReady()
    {
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions());
        using var source = new ControlledIdentitySource();
        using var listener = new AuthenticationListener();
        listener.Disable();
        listener.Enable(EventLevel.Informational, SqlClientEventSource.Keywords.Trace);
        using AuthenticationLogger.Scope outer = logger.Begin(listener.ConnectionId, captureAzure: true);
        await outer.WaitForReadyAsync(CancellationToken.None);
        ExecutionContext context = ExecutionContext.Capture()!;
        int once = 1;
        source.Enabling = () =>
        {
            if (Interlocked.Exchange(ref once, 0) != 1)
                return;
            ExecutionContext.Run(context, _ =>
            {
                listener.Enable(EventLevel.Informational, SqlClientEventSource.Keywords.Trace);
                using var lateSource = new LateIdentitySource();
                lateSource.LogMsalInformational("created during refresh");
            }, null);
        };
        var withCallback = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = _ => { }
        });
        using (AuthenticationLogger.Scope inner = withCallback.Begin(Guid.NewGuid(), captureAzure: true))
        {
            await inner.WaitForReadyAsync(CancellationToken.None);
            source.Enabling = null;
        }
        Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "created during refresh") &&
            Equals(entry.Payload[2], outer.ConnectionId.ToString()));
    }

    /// <summary>Bounds concurrency regressions without relying on a delay to order the tested operations.</summary>
    private static async Task CompleteWithinTimeout(Task task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))));
        await task;
    }

    /// <summary>Allows deterministic subscription interleavings without changing the real SDK source.</summary>
    [EventSource(Name = "Azure-Identity", Guid = "A02D753B-4CBB-4D93-A496-6B613D3E1080")]
    private sealed class ControlledIdentitySource : EventSource
    {
        /// <summary>Runs a test hook while an SDK enable command is in progress.</summary>
        internal Action? Enabling;

        /// <summary>Executes the test hook under the framework's EventSource command lock.</summary>
        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                Enabling?.Invoke();
            }
        }

        /// <summary>Emits the reviewed SDK Verbose schema.</summary>
        [Event(7, Level = EventLevel.Verbose)]
        public void LogMsalVerbose(string message) => WriteEvent(7, message);

        /// <summary>Emits the reviewed SDK Always schema.</summary>
        [Event(24, Level = EventLevel.LogAlways)]
        public void LogMsalAlways(string message) => WriteEvent(24, message);

        /// <summary>Emits the reviewed SDK Informational schema.</summary>
        [Event(8, Level = EventLevel.Informational)]
        public void LogMsalInformational(string message) => WriteEvent(8, message);
    }

    /// <summary>Represents an SDK source created reentrantly while another source is being configured.</summary>
    [EventSource(Name = "Azure-Identity", Guid = "BE467792-4AF4-43B8-A1B4-AB8BCB48F7F9")]
    private sealed class LateIdentitySource : EventSource
    {
        /// <summary>Emits the reviewed SDK Informational schema.</summary>
        [Event(8, Level = EventLevel.Informational)]
        public void LogMsalInformational(string message) => WriteEvent(8, message);
    }

    /// <summary>
    /// A disposed acquisition must not receive SDK work that inherited its execution context.
    /// </summary>
    [Fact]
    public async Task DisposedScope_DoesNotCaptureInheritedBackgroundWork()
    {
        var entries = new List<SqlAuthenticationLogEntry>();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions { AuthenticationLoggingCallback = entries.Add });
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task background;
        using (AuthenticationLogger.Scope scope = logger.Begin(Guid.NewGuid(), captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            background = Task.Run(async () =>
            {
                await gate.Task;
                logger.LogMsal(LogLevel.Info, "after disposal", false);
            });
        }
        gate.SetResult(true);
        await background;
        Assert.Empty(entries);
    }

    /// <summary>Creates the real cached MSAL client without acquiring a token or opening a browser.</summary>
    private static Task<IPublicClientApplication> GetClient(ActiveDirectoryAuthenticationProvider provider) =>
        provider.GetPublicClientAppInstanceAsync(CreateKey(provider), CancellationToken.None);

    /// <summary>Uses a deterministic authority and the provider's immutable logging policy for cache tests.</summary>
    private static ActiveDirectoryAuthenticationProvider.PublicClientAppKey CreateKey(ActiveDirectoryAuthenticationProvider provider) =>
        new("https://login.microsoftonline.com/11111111-2222-3333-4444-555555555555", "http://localhost",
            provider.ApplicationClientId
#if NETFRAMEWORK
            , null
#endif
        );

    /// <summary>Creates a real SDK credential with a local transport and the extension's safe logging configuration.</summary>
    /// <param name="client">The HTTP client that intercepts every SDK request.</param>
    /// <param name="clientId">An optional unique client identity for provider cache tests.</param>
    /// <returns>A credential that cannot send HTTP outside the supplied transport.</returns>
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

    /// <summary>Requests a token through the real SDK while all HTTP is intercepted by the test transport.</summary>
    private static async Task RequestToken(ClientSecretCredential credential) =>
        await credential.GetTokenAsync(new TokenRequestContext(["https://database.windows.net/.default"]));

    /// <summary>Returns controlled OAuth failures without contacting an identity service or using credentials.</summary>
    private sealed class TokenFailureHandler : HttpMessageHandler
    {
        /// <summary>Counts locally intercepted token requests.</summary>
        internal int RequestCount;

        /// <summary>Returns a controlled failure without sending any request over the network.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref RequestCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_client\",\"error_description\":\"private SDK exception data\"}")
            });
        }
    }

    /// <summary>Rejects malformed SDK payloads at the actual listener boundary, not just the severity mapper.</summary>
    [Fact]
    public async Task AzureListener_RejectsUnknownPayloadsAndSanitizesFailures()
    {
        using var listener = new AuthenticationListener();
        using var source = new InvalidSchemaIdentitySource();
        var entries = new List<SqlAuthenticationLogEntry>();
        var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entries.Add
        });
        using (AuthenticationLogger.Scope scope = logger.Begin(listener.ConnectionId, captureAzure: true))
        {
            await scope.WaitForReadyAsync(CancellationToken.None);
            Assert.True(source.IsEnabled(EventLevel.Verbose, EventKeywords.None));
            source.LogMsalVerbose(42);
            source.LogMsalInformational("private payload", "extra payload");
            source.LogMsalWarning();
            source.UnknownSchema("private payload");
            source.LogMsalError("private payload");
            source.AuthenticatedAccountDetails("private payload");
            Assert.Empty(entries);
            Assert.Empty(listener.Events);

            source.DefaultAzureCredentialCredentialSelected("ClientSecretCredential");
            SqlAuthenticationLogEntry selected = Assert.Single(entries);
            Assert.Equal("DefaultAzureCredential selected ClientSecretCredential.", selected.Message);
            Assert.Equal(SqlAuthenticationLogLevel.Informational, selected.Level);
            source.GetTokenFailed("private SDK exception data");
        }
        Assert.Equal(2, entries.Count);
        Assert.Equal("Azure credential token request failed.", entries[1].Message);
        Assert.Equal(SqlAuthenticationLogLevel.Error, entries[1].Level);
        Assert.All(entries, entry =>
        {
            Assert.False(entry.ContainsPii);
            Assert.Equal("Azure-Identity", entry.Source);
            Assert.Equal(listener.ConnectionId, entry.ConnectionId);
            Assert.DoesNotContain("private", entry.Message);
        });
        Assert.Equal(entries.Select(entry => entry.Message), listener.Events.Select(entry => entry.Payload![0]));
        Assert.Equal(new[] { EventLevel.Informational, EventLevel.Error }, listener.Events.Select(entry => entry.Level));
    }

    /// <summary>Simulates SDK schema drift while retaining valid EventSource metadata.</summary>
    [EventSource(Name = "Azure-Identity", Guid = "402F7956-73A1-4279-830E-8AB1D15DA526")]
    private sealed class InvalidSchemaIdentitySource : EventSource
    {
        /// <summary>Emits the known event name with a non-string payload.</summary>
        /// <param name="value">The unsupported numeric payload.</param>
        [Event(7, Level = EventLevel.Verbose)]
        public void LogMsalVerbose(int value) => WriteEvent(7, value);

        /// <summary>Emits the known event name with too many payload values.</summary>
        /// <param name="message">The first unsupported payload value.</param>
        /// <param name="extra">The extra payload value.</param>
        [Event(8, Level = EventLevel.Informational)]
        public void LogMsalInformational(string message, string extra) => WriteEvent(8, message, extra);

        /// <summary>Emits the known event name without its required payload.</summary>
        [Event(9, Level = EventLevel.Warning)]
        public void LogMsalWarning() => WriteEvent(9);

        /// <summary>Emits a known ID with an unrecognized name.</summary>
        /// <param name="message">The diagnostic that must not be forwarded.</param>
        [Event(10, Level = EventLevel.Error)]
        public void UnknownSchema(string message) => WriteEvent(10, message);

        /// <summary>Emits a known name with the wrong ID.</summary>
        /// <param name="message">The diagnostic that must not be forwarded.</param>
        [Event(20, Level = EventLevel.Error)]
        public void LogMsalError(string message) => WriteEvent(20, message);

        /// <summary>Emits a non-allowlisted account-information event.</summary>
        /// <param name="message">The diagnostic that must not be forwarded.</param>
        [Event(19, Level = EventLevel.Informational)]
        public void AuthenticatedAccountDetails(string message) => WriteEvent(19, message);

        /// <summary>Emits the supported credential-selection schema.</summary>
        /// <param name="credentialType">The selected credential class.</param>
        [Event(13, Level = EventLevel.Informational)]
        public void DefaultAzureCredentialCredentialSelected(string credentialType) => WriteEvent(13, credentialType);

        /// <summary>Emits exception text that the bridge must replace with a fixed diagnostic.</summary>
        /// <param name="message">The sensitive diagnostic that must not be copied.</param>
        [Event(3, Level = EventLevel.Error)]
        public void GetTokenFailed(string message) => WriteEvent(3, message);
    }

    /// <summary>Successful public provider acquisition survives throwing callbacks without disclosing token or exception text.</summary>
    [Fact]
    public async Task AcquireTokenSuccess_ThrowingCallbackDoesNotChangeToken()
    {
        using var listener = new AuthenticationListener();
        var handler = new TokenSuccessHandler();
        using var client = new HttpClient(handler);
        string clientId = Guid.NewGuid().ToString();
        var key = new ActiveDirectoryAuthenticationProvider.TokenCredentialKey(typeof(ClientSecretCredential),
            "https://login.microsoftonline.com/", "https://database.windows.net/.default",
            "11111111-2222-3333-4444-555555555555", clientId);
        // Reuse the existing cache seam; only this unique key is changed and every HTTP request stays local.
        var cache = Assert.IsType<ConcurrentDictionary<ActiveDirectoryAuthenticationProvider.TokenCredentialKey,
            ActiveDirectoryAuthenticationProvider.TokenCredentialData>>(typeof(ActiveDirectoryAuthenticationProvider)
            .GetField("s_tokenCredentialMap", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null));
        using SHA256 hash = SHA256.Create();
        Assert.True(cache.TryAdd(key, new ActiveDirectoryAuthenticationProvider.TokenCredentialData(
            CreateCredential(client, clientId), hash.ComputeHash(Encoding.Unicode.GetBytes("<secret>")))));
        try
        {
            var entries = new ConcurrentQueue<SqlAuthenticationLogEntry>();
            var provider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
            {
                AuthenticationLoggingCallback = entry =>
                {
                    entries.Enqueue(entry);
                    throw new InvalidOperationException("private callback data");
                }
            });
            var parameters = new SqlAuthenticationParameters(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
                "test-server", "test-db", "https://database.windows.net/.default",
                "https://login.microsoftonline.com/11111111-2222-3333-4444-555555555555",
                clientId, "<secret>", listener.ConnectionId, 30);
            SqlAuthenticationToken token = await provider.AcquireTokenAsync(parameters);
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal("<access-token>", token.AccessToken);
            Assert.True(token.ExpiresOn > DateTime.UtcNow);
            Assert.Contains(entries, entry => entry.Source == "Azure-Identity");
            Assert.Contains(entries, entry => entry.Message == "Token acquisition started.");
            Assert.DoesNotContain(entries, entry => entry.Message == "Token acquisition failed.");
            Assert.Contains(listener.Events, entry => Equals(entry.Payload![0], "Authentication logging callback failed."));
            Assert.All(entries, entry =>
            {
                Assert.Equal(listener.ConnectionId, entry.ConnectionId);
                Assert.False(entry.ContainsPii);
                Assert.DoesNotContain("<access-token>", entry.Message);
                Assert.DoesNotContain("private callback data", entry.Message);
            });
            Assert.All(listener.Events, entry =>
            {
                Assert.DoesNotContain("<access-token>", string.Join(" ", entry.Payload!));
                Assert.DoesNotContain("private callback data", string.Join(" ", entry.Payload!));
            });
            Assert.Null(AuthenticationLogger.CurrentScope);
        }
        finally
        {
            Assert.True(cache.TryRemove(key, out _));
        }
    }

    /// <summary>Returns an opaque synthetic access token without contacting an identity service.</summary>
    private sealed class TokenSuccessHandler : HttpMessageHandler
    {
        /// <summary>Counts requests handled locally so the test cannot pass using a different credential.</summary>
        internal int RequestCount;

        /// <summary>Completes each intercepted token request with a controlled OAuth success response.</summary>
        /// <param name="request">The intercepted request, which is never sent to the network.</param>
        /// <param name="cancellationToken">The acquisition cancellation token.</param>
        /// <returns>The synthetic token response.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref RequestCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"<access-token>\",\"token_type\":\"Bearer\",\"expires_in\":3600}",
                    Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// SDK severity mapping must reject unknown schemas rather than misclassifying their privacy or level.
    /// </summary>
    [Theory]
    [InlineData(7, "LogMsalVerbose", SqlAuthenticationLogLevel.Verbose)]
    [InlineData(8, "LogMsalInformational", SqlAuthenticationLogLevel.Informational)]
    [InlineData(9, "LogMsalWarning", SqlAuthenticationLogLevel.Warning)]
    [InlineData(10, "LogMsalError", SqlAuthenticationLogLevel.Error)]
    [InlineData(23, "LogMsalCritical", SqlAuthenticationLogLevel.Critical)]
    [InlineData(24, "LogMsalAlways", SqlAuthenticationLogLevel.Always)]
    [InlineData(8, "UnknownSchema", null)]
    [InlineData(19, "AuthenticatedAccountDetails", null)]
    [InlineData(0, null, null)]
    public void AzureSdkSeverity_MapsOnlyKnownEvents(int id, string? name, SqlAuthenticationLogLevel? expected)
    {
        Assert.Equal(expected, AzureAuthenticationEventListener.GetMsalLevel(id, name));
        if (expected.HasValue)
        {
            using var listener = new AuthenticationListener();
            var entries = new List<SqlAuthenticationLogEntry>();
            var logger = new AuthenticationLogger(new ActiveDirectoryAuthenticationProviderOptions
            {
                AuthenticationLoggingCallback = entries.Add
            });
            using (logger.Begin(listener.ConnectionId))
            {
                logger.Write(expected.Value, "SDK diagnostic", false, "Azure-Identity");
            }
            Assert.Equal(AuthenticationLogger.ToEventLevel(expected.Value), Assert.Single(listener.Events).Level);
            Assert.Equal(expected.Value, Assert.Single(entries).Level);
        }
    }

    /// <summary>
    /// Authentication failures retain their original shape even if the application logger throws.
    /// </summary>
    [Theory]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
    public async Task AcquireTokenFailure_LogsWithoutExposingExceptionOrChangingFailure(SqlAuthenticationMethod method)
    {
        using var listener = new AuthenticationListener();
        var entries = new List<SqlAuthenticationLogEntry>();
        var provider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entry =>
            {
                entries.Add(entry);
                throw new InvalidOperationException("private callback data");
            }
        });
        var parameters = new SqlAuthenticationParameters(method, "test-server", "test-db",
            "https://database.windows.net/", "https://login.microsoftonline.com/", "", "",
            listener.ConnectionId, 30);
        await Assert.ThrowsAsync<AuthenticationException>(() => provider.AcquireTokenAsync(parameters));
        Assert.Contains(entries, entry => entry.Message == "Token acquisition failed.");
        Assert.All(entries, entry => Assert.Equal(listener.ConnectionId, entry.ConnectionId));
        Assert.Null(AuthenticationLogger.CurrentScope);
    }

    /// <summary>
    /// The default provider emits authentication events for both open paths without callback registration.
    /// </summary>
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
        Assert.Contains(listener.Events,
            entry => Equals(entry.Payload![2], connection.ClientConnectionId.ToString()));
    }

    /// <summary>
    /// A direct MSAL flow delivers owned callback entries for synchronous and asynchronous connections.
    /// </summary>
    [ConditionalTheory(typeof(Config), nameof(Config.IsAzureSqlConnectionString), nameof(Config.SupportsEntraIntegrated))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IntegratedProvider_LogsBothOpenPaths(bool async)
    {
        SqlAuthenticationProvider original = Assert.IsAssignableFrom<SqlAuthenticationProvider>(
            SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated));
        var entries = new ConcurrentQueue<SqlAuthenticationLogEntry>();
        var provider = new ActiveDirectoryAuthenticationProvider(new ActiveDirectoryAuthenticationProviderOptions
        {
            AuthenticationLoggingCallback = entries.Enqueue
        });
        try
        {
            Assert.True(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated, provider));
            var builder = new SqlConnectionStringBuilder(Config.TCPConnectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryIntegrated,
                IntegratedSecurity = false,
                Pooling = false,
                ApplicationName = $"AuthenticationLogging-{Guid.NewGuid()}"
            };
            builder.Remove("User ID");
            builder.Remove("Password");
            using var connection = new SqlConnection(builder.ConnectionString);
            if (async)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }
            Assert.Contains(entries, entry => entry.Source == "MSAL" && entry.ConnectionId == connection.ClientConnectionId);
            Assert.All(entries, entry => Assert.False(entry.ContainsPii));
        }
        finally
        {
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated, original);
        }
    }

    /// <summary>
    /// Captures only the additive authentication events without changing other trace consumers.
    /// </summary>
    private sealed class AuthenticationListener : EventListener
    {
        /// <summary>Gets the matching authentication events captured by this listener.</summary>
        internal ConcurrentQueue<EventWrittenEventArgs> Events { get; } = new();

        /// <summary>Gets a unique correlation identifier to exclude unrelated test activity.</summary>
        internal Guid ConnectionId { get; } = Guid.NewGuid();
        /// <summary>Counts ordinary Trace events to verify authentication-only opt-out.</summary>
        internal int TraceEvents;
        private EventSource? _source;
        private readonly bool _captureAll;

        /// <summary>Creates an isolated listener or captures all IDs for live-connection assertions.</summary>
        internal AuthenticationListener(bool captureAll = false) => _captureAll = captureAll;

        /// <summary>Changes current collector demand without recreating the authentication client.</summary>
        internal void Enable(EventLevel level, EventKeywords keywords) => EnableEvents(_source!, level, keywords);

        /// <summary>Detaches collection while preserving captured events for assertions.</summary>
        internal void Disable() => DisableEvents(_source!);

        /// <summary>Disables events before disposing so later tests do not inherit enabled state.</summary>
        /// <remarks>On .NET Framework, Dispose only detaches the listener and leaves the source enabled.</remarks>
        public override void Dispose()
        {
            if (_source is not null)
                DisableEvents(_source);
            base.Dispose();
        }

        /// <summary>Enables the shared SqlClient source when it becomes available.</summary>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                _source = eventSource;
                EnableEvents(eventSource, EventLevel.Verbose, SqlClientEventSource.Keywords.Trace);
            }
        }

        /// <summary>Captures only the additive authentication payload and requested correlation.</summary>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName == "Trace")
                Interlocked.Increment(ref TraceEvents);
            if (eventData.EventName?.StartsWith("Authentication", StringComparison.Ordinal) == true &&
                eventData.Payload?.Count == 3 && (_captureAll || Equals(eventData.Payload[2], ConnectionId.ToString())))
            {
                Events.Enqueue(eventData);
            }
        }
    }
}
