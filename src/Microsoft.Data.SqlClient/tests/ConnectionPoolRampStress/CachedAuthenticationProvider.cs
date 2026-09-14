// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace ConnectionPoolRampStress;

internal sealed class AuthenticationCacheConfigurationException : ArgumentException
{
    public const string Diagnostic =
        "--cache-authentication requires Authentication=Active Directory Default on the workload " +
        "and observer connections, with an available authentication provider.";

    public AuthenticationCacheConfigurationException() : base(Diagnostic) { }
}

internal sealed class CachedAuthenticationProvider : SqlAuthenticationProvider
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);
    private readonly SqlAuthenticationProvider _provider;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ConcurrentDictionary<CacheKey, Entry> _entries = new();
    private long _acquisitionCount;

    internal long AcquisitionCount => Interlocked.Read(ref _acquisitionCount);
    internal static long? CurrentAcquisitionCount =>
        (GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) as CachedAuthenticationProvider)?.AcquisitionCount;

    internal CachedAuthenticationProvider(SqlAuthenticationProvider provider, Func<DateTimeOffset>? utcNow = null)
    {
        _provider = provider;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    internal static void Configure(Settings settings, string workloadConnection, string? observerConnection = null)
    {
        if (!settings.CacheAuthentication) return;
        ValidateConnection(workloadConnection);
        if (observerConnection is not null) ValidateConnection(observerConnection);

        SqlAuthenticationProvider? provider = GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault);
        if (provider is null || !provider.IsSupported(SqlAuthenticationMethod.ActiveDirectoryDefault))
            throw new AuthenticationCacheConfigurationException();
        if (provider is CachedAuthenticationProvider) return;
        if (!SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, new CachedAuthenticationProvider(provider)))
            throw new AuthenticationCacheConfigurationException();
    }

    private static void ValidateConnection(string connectionString)
    {
        try
        {
            SqlConnectionStringBuilder builder = new(connectionString);
            if (builder.Authentication != SqlAuthenticationMethod.ActiveDirectoryDefault ||
                builder.IntegratedSecurity || !string.IsNullOrEmpty(builder.Password))
                throw new AuthenticationCacheConfigurationException();
        }
        catch (ArgumentException)
        {
            // Connection-string parser messages can contain credentials.
            throw new AuthenticationCacheConfigurationException();
        }
    }

    public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) =>
        authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDefault;

    public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
    {
        if (!IsSupported(parameters.AuthenticationMethod) || !string.IsNullOrEmpty(parameters.Password))
            return Task.FromException<SqlAuthenticationToken>(new AuthenticationCacheConfigurationException());

        // ConnectionId and ConnectionTimeout describe the call, not its identity.
        CacheKey key = new(parameters.AuthenticationMethod, parameters.Authority, parameters.Resource,
            parameters.UserId, parameters.ServerName, parameters.DatabaseName);
        Entry entry = _entries.GetOrAdd(key, _ => new Entry());
        TaskCompletionSource<SqlAuthenticationToken> acquisition;
        lock (entry)
        {
            if (entry.Token is { } token && token.ExpiresOn - _utcNow() > RefreshMargin)
                return Task.FromResult(token);
            if (entry.Acquisition is { } pending) return pending.Task;
            entry.Token = null;
            acquisition = new(TaskCreationOptions.RunContinuationsAsynchronously);
            entry.Acquisition = acquisition;
        }
        _ = AcquireAndPublishAsync(entry, parameters, acquisition);
        return acquisition.Task;
    }

    private async Task AcquireAndPublishAsync(Entry entry, SqlAuthenticationParameters parameters,
        TaskCompletionSource<SqlAuthenticationToken> acquisition)
    {
        try
        {
            Interlocked.Increment(ref _acquisitionCount);
            SqlAuthenticationToken token = await _provider.AcquireTokenAsync(parameters).ConfigureAwait(false);
            lock (entry)
            {
                entry.Token = token.ExpiresOn - _utcNow() > RefreshMargin ? token : null;
                entry.Acquisition = null;
                acquisition.SetResult(token);
            }
        }
        catch (Exception exception)
        {
            lock (entry)
            {
                entry.Acquisition = null;
                acquisition.SetException(exception);
            }
        }
    }

    private readonly record struct CacheKey(SqlAuthenticationMethod Method, string Authority, string Resource,
        string? UserId, string Server, string Database);

    private sealed class Entry
    {
        public SqlAuthenticationToken? Token;
        public TaskCompletionSource<SqlAuthenticationToken>? Acquisition;
    }
}
