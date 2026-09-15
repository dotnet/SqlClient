// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ConnectionPoolRampStress;

internal interface IConnection : IDisposable
{
    void Open();
    Task OpenAsync(CancellationToken cancellationToken);
}

internal interface IConnectionFactory
{
    IConnection Create();
    void ClearPool();
    void StartMeasurement() { }
    void ObserveColdRound(IReadOnlyList<IConnection> held) { }
    void StopMeasurement() { }
    PoolCreationObservation? PoolCreation => null;
}

internal sealed record SafeFailure(string ExceptionType, int? SqlNumber, string Category);

internal static class Failure
{
    public static SafeFailure Describe(Exception exception, bool setup = false)
    {
        // Never use Message, StackTrace, Data, ToString, or an arbitrary type name.
        string type = exception switch
        {
            SqlException => "SqlException",
            OperationCanceledException => "OperationCanceledException",
            TimeoutException => "TimeoutException",
            ArgumentException => "ArgumentException",
            InvalidOperationException => "InvalidOperationException",
            IOException => "IOException",
            _ => "OtherException"
        };
        return new(type, (exception as SqlException)?.Number, setup ? "setup" :
            exception is OperationCanceledException ? "cancellation" :
            exception is TimeoutException || exception is SqlException { Number: -2 } ? "timeout-unclassified" :
            "unclassified");
    }
}

internal sealed record EffectiveConnection(bool Pooling, int MaximumPoolSize, int MinimumPoolSize,
    int ConnectTimeoutSeconds, int ConnectRetryCount, string PoolBlockingPeriod, string Encryption,
    bool TrustServerCertificate, string Sni, bool Enlist);

internal sealed record RuntimeMetadata(string DriverVersion, string DriverCommit, string Runtime,
    string OS, string Architecture, int ProcessorCount, ThreadPoolSettings ThreadPool,
    string ServerVersion, EffectiveConnection Connection, string ServerResources = "unavailable",
    string ContainerLimits = "unavailable")
{
    public string OSVersion { get; init; } = Environment.OSVersion.Version.ToString();
}

internal sealed record ThreadPoolSettings(int MinimumWorkers, int MinimumIo, int MaximumWorkers, int MaximumIo)
{
    public static ThreadPoolSettings Configure(PoolProfile profile, int minimum)
    {
        ThreadPool.GetMinThreads(out int workers, out int io);
        ThreadPool.GetMaxThreads(out int maxWorkers, out int maxIo);
        if (profile == PoolProfile.Provisioned)
        {
            if (minimum > maxWorkers || !ThreadPool.SetMinThreads(minimum, io))
                throw new InvalidOperationException("Worker minimum could not be set.");
            ThreadPool.GetMinThreads(out workers, out io);
            if (workers != minimum) throw new InvalidOperationException("Worker minimum was not applied.");
        }
        return new(workers, io, maxWorkers, maxIo);
    }
}

internal sealed class ConnectionFactory : IConnectionFactory
{
    private readonly string _connectionString;
    private readonly bool _creationExperiment;
    private readonly int _creationLimit;
    private readonly object _lifetimeGate = new();
    private PoolCreationAdapter? _creationAdapter;
    private int _owners;
    private bool _stopping;
    public EffectiveConnection Effective { get; }
    public PoolCreationObservation? PoolCreation => _creationAdapter?.Snapshot;
    internal PoolCreationAdapter? CreationAdapter => _creationAdapter;

    public ConnectionFactory(string input, Sample sample, Settings settings, string? applicationName = null)
    {
        if (sample.Pool == PoolMode.V2 &&
            typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.ConnectionPool.ChannelDbConnectionPool") is null)
            throw new NotSupportedException();
        ValidateExperiment(settings, sample);
        _creationExperiment = sample.Pool == PoolMode.V2 && settings.Rounds == 1;
        _creationLimit = settings.PoolCreationLimit;
        var builder = Normalize(input, sample, settings);
        if (_creationExperiment && (builder.IntegratedSecurity || builder.UserInstance || builder.FailoverPartner.Length != 0))
            throw new ArgumentException("The creation experiment requires non-identity pooling with no user instance or failover partner.");
        if (_creationExperiment) builder.PoolBlockingPeriod = PoolBlockingPeriod.NeverBlock;
        builder.ApplicationName = WorkloadApplicationName(applicationName ??
            (_creationExperiment ? $"ConnectionPoolRampStress_{Guid.NewGuid():N}" : null));
        _connectionString = builder.ConnectionString;
        AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", out bool managed);
        Effective = new(builder.Pooling, builder.MaxPoolSize, builder.MinPoolSize, builder.ConnectTimeout,
            builder.ConnectRetryCount, builder.PoolBlockingPeriod.ToString(), builder.Encrypt.ToString(),
            builder.TrustServerCertificate, !OperatingSystem.IsWindows() || managed ? "managed" : "native",
            builder.Enlist);
    }

    internal static void ValidateExperiment(Settings settings, Sample sample)
    {
        if (settings.Rounds is < 0 or > 1 ||
            settings.Rounds != 0 && sample.Workload != Workload.ColdRamps ||
            settings.PoolCreationLimit is < 0 or > 4096 ||
            settings.PoolCreationLimit != 0 &&
                (sample.Pool != PoolMode.V2 || sample.Workload != Workload.ColdRamps || settings.Rounds != 1 ||
                 settings.MaxConcurrentOpens != 0))
            throw new ArgumentException("Invalid pool creation experiment.");
    }

    internal static SqlConnectionStringBuilder Normalize(string input, Sample sample, Settings settings) => new(input)
    {
        Pooling = sample.Pool != PoolMode.Disabled,
        MaxPoolSize = sample.Concurrency,
        MinPoolSize = 0,
        ConnectTimeout = settings.ConnectTimeoutSeconds,
        ConnectRetryCount = 0,
        ApplicationName = "ConnectionPoolRampStress",
        Enlist = false
    };

    internal static string WorkloadApplicationName(string? name)
    {
        if (name is null) return "ConnectionPoolRampStress";
        if (!Regex.IsMatch(name, @"\AConnectionPoolRampStress_[0-9a-f]{32}\z"))
            throw new ArgumentException("Invalid sample application name.");
        return name;
    }

    internal static SqlConnectionStringBuilder PreflightConnection(string connectionString) => new(connectionString)
    {
        Pooling = false,
        ApplicationName = "ConnectionPoolRampStress-preflight"
    };

    public RuntimeMetadata Preflight(ThreadPoolSettings threadPool)
    {
        SqlConnectionStringBuilder builder = PreflightConnection(_connectionString);
        using SqlConnection connection = new(builder.ConnectionString);
        connection.Open();
        string version = connection.ServerVersion;
        string info = typeof(SqlConnection).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        string commit = info.Contains('+') ? info[(info.LastIndexOf('+') + 1)..] : "";
        return new(typeof(SqlConnection).Assembly.GetName().Version?.ToString() ?? "unavailable",
            Regex.IsMatch(commit, "^[0-9a-fA-F]{40}$") ? commit : "unavailable",
            Environment.Version.ToString(),
            OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux",
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount, threadPool,
            Regex.IsMatch(version, "^[0-9.]{1,40}$") ? version : "unavailable", Effective);
    }

    public void StartMeasurement()
    {
        if (_creationExperiment)
        {
            if (_creationAdapter is not null || _stopping) throw new InvalidOperationException();
            _creationAdapter = PoolCreationAdapter.Install(_connectionString, _creationLimit);
        }
    }

    public IConnection Create()
    {
        if (!_creationExperiment) return new Connection(new SqlConnection(_connectionString));
        lock (_lifetimeGate)
        {
            if (_stopping || _creationAdapter is null) throw new InvalidOperationException();
            SqlConnection connection = new(_connectionString);
            try { _creationAdapter.VerifyRegistered(connection); }
            catch { connection.Dispose(); throw; }
            _owners++;
            return new ExperimentConnection(connection, this);
        }
    }

    public void ObserveColdRound(IReadOnlyList<IConnection> held) =>
        _creationAdapter?.ObserveHeld(held.Cast<ExperimentConnection>().Select(c => c.Value).ToArray());

    public void StopMeasurement()
    {
        lock (_lifetimeGate)
        {
            _stopping = true;
            _creationAdapter?.StopSampling();
            if (_owners == 0) _creationAdapter?.Dispose();
        }
    }

    private void ReleaseOwner()
    {
        lock (_lifetimeGate)
        {
            _owners--;
            if (_stopping && _owners == 0) _creationAdapter?.Dispose();
        }
    }

    public void ClearPool()
    {
        if (_creationAdapter is not null)
        {
            _creationAdapter.Clear();
            return;
        }
        using SqlConnection key = new(_connectionString);
        SqlConnection.ClearPool(key);
    }

    private sealed class Connection(SqlConnection connection) : IConnection
    {
        public void Open() => connection.Open();
        public Task OpenAsync(CancellationToken cancellationToken) => connection.OpenAsync(cancellationToken);
        public void Dispose() => connection.Dispose();
    }

    private sealed class ExperimentConnection(SqlConnection connection, ConnectionFactory owner) : IConnection
    {
        private int _disposed;
        internal SqlConnection Value => connection;
        public void Open()
        {
            connection.Open();
            owner._creationAdapter!.VerifyOpened(connection);
        }
        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            owner._creationAdapter!.VerifyOpened(connection);
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { connection.Dispose(); }
            finally { owner.ReleaseOwner(); }
        }
    }
}
