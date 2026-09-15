// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.Data.SqlClient;

namespace ConnectionPoolRampStress;

internal sealed record PoolCreationObservation(int ConfiguredLimit, string PoolType,
    string DriverAssemblyVersion, string DriverModuleId, int PoolId)
{
    public string AdapterContract { get; init; } = "channel-constructor-registration-v1";
    public int? VerifiedHeldConnections { get; init; }
    public int? DistinctPhysicalConnectionIds { get; init; }
    public int? PoolConnectionsAtSettlement { get; init; }
    public int? SampledPeakActivePermits { get; init; }
    public long PermitSamples { get; init; }
    public int PermitSamplingIntervalMilliseconds { get; init; } = 1;
    public long? SuccessfulLeases { get; init; }
    public long? FailedLeases { get; init; }
    public long? AvailablePermitsAfterDrain { get; init; }
    public int? PeakPhysicalCreations { get; init; }
    public bool PoolCleared { get; init; }
    public bool? LimiterDisposed { get; init; }
}

/// <summary>
/// Registers a real, constructor-injected pool before first use. This experiment-only adapter
/// reads private state but never writes fields or replaces a previously constructed pool.
/// </summary>
internal sealed class PoolCreationAdapter : IDisposable
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly SqlConnection _key;
    private readonly object _group;
    private readonly object _pool;
    private readonly object _identity;
    private readonly IDictionary _collection;
    private readonly FieldInfo _collectionField;
    private readonly FieldInfo _limiterField;
    private readonly PropertyInfo _poolGroup;
    private readonly PropertyInfo _innerConnection;
    private readonly PropertyInfo _innerPool;
    private readonly PropertyInfo _count;
    private readonly ManualResetEventSlim _stop = new();
    private readonly Thread? _sampler;
    private int _peak;
    private long _samples;
    private volatile bool _disposed;
    private int _disposeStarted;
    private PoolCreationObservation _snapshot;

    internal ConcurrencyLimiter? Limiter { get; }
    internal int PoolCount => (int)_count.GetValue(_pool)!;
    public PoolCreationObservation Snapshot => Volatile.Read(ref _snapshot);

    internal static PoolCreationAdapter Install(string connectionString, int limit) => new(connectionString, limit);

    internal static void ValidateAssembly(Assembly assembly)
    {
        AssemblyName expected = typeof(PoolCreationAdapter).Assembly.GetReferencedAssemblies()
            .Single(a => a.Name == "Microsoft.Data.SqlClient");
        AssemblyName actual = assembly.GetName();
        if (assembly != typeof(SqlConnection).Assembly || actual.Name != expected.Name ||
            actual.Version != expected.Version ||
            !actual.GetPublicKeyToken()!.SequenceEqual(expected.GetPublicKeyToken()!))
            throw new NotSupportedException("The driver does not match the harness assembly reference.");
    }

    private PoolCreationAdapter(string connectionString, int limit)
    {
        if (limit is < 0 or > 4096) throw new ArgumentOutOfRangeException(nameof(limit));
        Assembly driver = typeof(SqlConnection).Assembly;
        ValidateAssembly(driver);
        Type Type(string name) => driver.GetType(name, throwOnError: false) ?? throw new NotSupportedException();
        Type poolType = Type("Microsoft.Data.SqlClient.ConnectionPool.ChannelDbConnectionPool");
        Type groupType = Type("Microsoft.Data.SqlClient.ConnectionPool.DbConnectionPoolGroup");
        Type factoryType = Type("Microsoft.Data.SqlClient.SqlConnectionFactory");
        Type identityType = Type("Microsoft.Data.SqlClient.ConnectionPool.DbConnectionPoolIdentity");
        Type providerType = Type("Microsoft.Data.SqlClient.ConnectionPool.DbConnectionPoolProviderInfo");
        Type optionsType = Type("Microsoft.Data.SqlClient.SqlConnectionOptions");
        Type metricsType = Type("Microsoft.Data.SqlClient.Diagnostics.ISqlClientMetrics");
        Type poolInterface = Type("Microsoft.Data.SqlClient.ConnectionPool.IDbConnectionPool");
        _poolGroup = Property(typeof(SqlConnection), "PoolGroup");
        _innerConnection = Property(typeof(SqlConnection), "InnerConnection");
        _innerPool = Property(_innerConnection.PropertyType, "Pool");
        _count = Property(poolType, "Count");
        _collectionField = groupType.GetField("_poolCollection", Instance) ?? throw new NotSupportedException();
        _limiterField = poolType.GetField("_connectionCreationRateLimiter", Instance) ?? throw new NotSupportedException();
        if (!_limiterField.IsInitOnly || _limiterField.FieldType != typeof(ConcurrencyLimiter) ||
            _collectionField.FieldType != typeof(System.Collections.Concurrent.ConcurrentDictionary<,>)
                .MakeGenericType(identityType, poolInterface))
            throw new NotSupportedException();
        ConstructorInfo constructor = poolType.GetConstructor(Instance, null,
            [factoryType, groupType, identityType, providerType, typeof(ConcurrencyLimiter), typeof(TimeProvider), metricsType],
            null) ?? throw new NotSupportedException();
        MethodInfo startup = Method(poolType, "Startup");
        MethodInfo markActive = Method(groupType, "MarkPoolGroupAsActive");
        MethodInfo getPool = Method(groupType, "GetConnectionPool", factoryType);
        MethodInfo provider = Method(factoryType, "CreateConnectionPoolProviderInfo", optionsType);
        MethodInfo enterMetric = Method(metricsType, "EnterActiveConnectionPool");
        _identity = identityType.GetField("NoIdentity", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new NotSupportedException();
        _key = new(connectionString);
        try
        {
            _group = _poolGroup.GetValue(_key) ?? throw new NotSupportedException();
            object options = Property(groupType, "PoolGroupOptions").GetValue(_group) ?? throw new NotSupportedException();
            if ((bool)Property(options.GetType(), "PoolByIdentity").GetValue(options)! ||
                (int)Property(options.GetType(), "MinPoolSize").GetValue(options)! != 0)
                throw new NotSupportedException("Only empty non-identity pools are supported.");
            object factory = Property(typeof(SqlConnection), "ConnectionFactory").GetValue(_key)!;
            object connectionOptions = Property(groupType, "ConnectionOptions").GetValue(_group)!;
            _collection = (IDictionary)(_collectionField.GetValue(_group) ?? throw new NotSupportedException());
            lock (_group)
            {
                if (_collection.Count != 0 || !(bool)markActive.Invoke(_group, null)!)
                    throw new NotSupportedException("The experiment requires a fresh pool group.");
                Limiter = limit == 0 ? null : new(new ConcurrencyLimiterOptions
                {
                    PermitLimit = limit,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
                _pool = constructor.Invoke([factory, _group, _identity,
                    provider.Invoke(factory, [connectionOptions]), Limiter, null, null]);
                try
                {
                    if (!ReferenceEquals(_limiterField.GetValue(_pool), Limiter) || PoolCount != 0)
                        throw new NotSupportedException();
                    startup.Invoke(_pool, null);
                    // Same lock, startup ordering, dictionary operation and metric as GetConnectionPool.
                    if (!(bool)Method(_collection.GetType(), "TryAdd", identityType, poolInterface)
                        .Invoke(_collection, [_identity, _pool])!)
                        throw new NotSupportedException();
                    enterMetric.Invoke(Property(poolType, "Metrics").GetValue(_pool), null);
                    if (!ReferenceEquals(getPool.Invoke(_group, [factory]), _pool))
                        throw new NotSupportedException();
                }
                catch
                {
                    SqlConnection.ClearPool(_key);
                    ((IDisposable)_pool).Dispose();
                    throw;
                }
            }
            _snapshot = new(limit, poolType.FullName!, driver.GetName().Version!.ToString(),
                driver.ManifestModule.ModuleVersionId.ToString(), (int)Property(poolType, "Id").GetValue(_pool)!)
            {
                SampledPeakActivePermits = limit == 0 ? null : 0,
                LimiterDisposed = limit == 0 ? null : false
            };
            if (Limiter is not null)
            {
                _sampler = new Thread(() =>
                {
                    do { SamplePermits(); } while (!_stop.Wait(1));
                }) { IsBackground = true, Name = "pool-permit-sampler" };
                _sampler.Start();
            }
        }
        catch
        {
            Limiter?.Dispose();
            _key.Dispose();
            _stop.Dispose();
            throw;
        }
    }

    public void VerifyRegistered(SqlConnection connection)
    {
        if (_disposed || _snapshot.PoolCleared || !ReferenceEquals(_poolGroup.GetValue(connection), _group) ||
            !ReferenceEquals(_collectionField.GetValue(_group), _collection) ||
            !ReferenceEquals(_collection[_identity], _pool) ||
            !ReferenceEquals(_limiterField.GetValue(_pool), Limiter))
            throw new NotSupportedException("The experiment pool was replaced or cleared.");
    }

    public void VerifyOpened(SqlConnection connection)
    {
        VerifyRegistered(connection);
        if (connection.State != System.Data.ConnectionState.Open ||
            !ReferenceEquals(_innerPool.GetValue(_innerConnection.GetValue(connection)), _pool))
            throw new NotSupportedException("A measured open used a different pool.");
    }

    public void ObserveHeld(IReadOnlyList<SqlConnection> held)
    {
        foreach (SqlConnection connection in held) VerifyOpened(connection);
        int distinct = held.Select(c => c.ClientConnectionId).Distinct().Count();
        if (distinct != held.Count || held.Any(c => c.ClientConnectionId == Guid.Empty) || PoolCount != held.Count)
            throw new NotSupportedException("Held physical connections did not match the experiment pool.");
        _snapshot = _snapshot with
        {
            VerifiedHeldConnections = held.Count,
            DistinctPhysicalConnectionIds = distinct,
            PoolConnectionsAtSettlement = PoolCount
        };
    }

    private void SamplePermits()
    {
        RateLimiterStatistics stats = Limiter!.GetStatistics()!;
        int active = _snapshot.ConfiguredLimit - (int)stats.CurrentAvailablePermits;
        _peak = Math.Max(_peak, active);
        _samples++;
    }

    public void StopSampling()
    {
        if (_disposed || _stop.IsSet) return;
        _stop.Set();
        _sampler?.Join();
        RateLimiterStatistics? stats = Limiter?.GetStatistics();
        _snapshot = _snapshot with
        {
            SampledPeakActivePermits = Limiter is null ? null : _peak,
            PermitSamples = _samples,
            SuccessfulLeases = stats?.TotalSuccessfulLeases,
            FailedLeases = stats?.TotalFailedLeases,
            AvailablePermitsAfterDrain = stats?.CurrentAvailablePermits
        };
    }

    public void Clear()
    {
        if (_snapshot.PoolCleared) return;
        SqlConnection.ClearPool(_key);
        _snapshot = _snapshot with { PoolCleared = true };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0) return;
        StopSampling();
        Clear();
        if (!AllPermitsReturned)
        {
            // An outer OpenAsync can cancel while its synchronous physical creation is still
            // unwinding. Do not dispose that creation's caller-owned limiter before its lease.
            new Thread(() =>
            {
                while (!AllPermitsReturned) Thread.Sleep(10);
                FinishDispose();
            }) { IsBackground = true, Name = "pool-limiter-cleanup" }.Start();
            return;
        }
        FinishDispose();
    }

    private bool AllPermitsReturned =>
        Limiter is null || Limiter.GetStatistics()!.CurrentAvailablePermits == _snapshot.ConfiguredLimit;

    private void FinishDispose()
    {
        long? available = Limiter?.GetStatistics()?.CurrentAvailablePermits;
        Limiter?.Dispose();
        _snapshot = _snapshot with
        {
            AvailablePermitsAfterDrain = available,
            LimiterDisposed = Limiter is null ? null : true
        };
        _key.Dispose();
        _stop.Dispose();
        _disposed = true;
    }

    private static PropertyInfo Property(Type type, string name) =>
        type.GetProperty(name, Instance) ?? throw new NotSupportedException();

    private static MethodInfo Method(Type type, string name, params Type[] parameters) =>
        type.GetMethod(name, Instance, null, parameters, null) ?? throw new NotSupportedException();
}
