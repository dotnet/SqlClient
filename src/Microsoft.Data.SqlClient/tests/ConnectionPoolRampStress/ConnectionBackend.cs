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
    public EffectiveConnection Effective { get; }

    public ConnectionFactory(string input, Sample sample, Settings settings)
    {
        if (sample.Pool == PoolMode.V2 &&
            typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.ConnectionPool.ChannelDbConnectionPool") is null)
            throw new NotSupportedException();
        var builder = Normalize(input, sample, settings);
        _connectionString = builder.ConnectionString;
        AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", out bool managed);
        Effective = new(builder.Pooling, builder.MaxPoolSize, builder.MinPoolSize, builder.ConnectTimeout,
            builder.ConnectRetryCount, builder.PoolBlockingPeriod.ToString(), builder.Encrypt.ToString(),
            builder.TrustServerCertificate, !OperatingSystem.IsWindows() || managed ? "managed" : "native",
            builder.Enlist);
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

    public RuntimeMetadata Preflight(ThreadPoolSettings threadPool)
    {
        SqlConnectionStringBuilder builder = new(_connectionString) { Pooling = false };
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

    public IConnection Create() => new Connection(new SqlConnection(_connectionString));

    public void ClearPool()
    {
        using SqlConnection key = new(_connectionString);
        SqlConnection.ClearPool(key);
    }

    private sealed class Connection(SqlConnection connection) : IConnection
    {
        public void Open() => connection.Open();
        public Task OpenAsync(CancellationToken cancellationToken) => connection.OpenAsync(cancellationToken);
        public void Dispose() => connection.Dispose();
    }
}
