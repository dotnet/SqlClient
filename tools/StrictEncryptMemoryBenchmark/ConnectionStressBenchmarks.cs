using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Microsoft.Data.SqlClient;

namespace StrictEncryptMemoryBenchmark;

/// <summary>
/// A stress-oriented benchmark that opens many connections per invocation.
/// This amplifies per-connection native memory overhead (SChannel TLS session cache)
/// and makes leaks visible. Uses InvocationCount=1 so each iteration is a single
/// batch of N connections — process memory grows cumulatively across iterations.
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[Config(typeof(Config))]
public class ConnectionStressBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithWarmupCount(1)
                .WithIterationCount(15)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                .WithId("NativeMemoryStress"));

            AddDiagnoser(new NativeMemoryDiagnoser());
        }
    }

    [Params("Strict", "Mandatory")]
    public string EncryptMode { get; set; } = "Strict";

    [Params(1000, 5000)]
    public int ConnectionCount { get; set; }

    private string _connectionString = null!;

    [GlobalSetup]
    public void Setup()
    {
        var server = Environment.GetEnvironmentVariable("BENCHMARK_SERVER")!;
        var database = Environment.GetEnvironmentVariable("BENCHMARK_DATABASE") ?? "master";
        var user = Environment.GetEnvironmentVariable("BENCHMARK_USER")!;
        var password = Environment.GetEnvironmentVariable("BENCHMARK_PASSWORD")!;
        var serverCert = Environment.GetEnvironmentVariable("BENCHMARK_SERVER_CERTIFICATE");
        var hostInCert = Environment.GetEnvironmentVariable("BENCHMARK_HOSTNAME_IN_CERTIFICATE");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = user,
            Password = password,
            Encrypt = EncryptMode switch
            {
                "Strict" => SqlConnectionEncryptOption.Strict,
                "Mandatory" => SqlConnectionEncryptOption.Mandatory,
                "Optional" => SqlConnectionEncryptOption.Optional,
                _ => SqlConnectionEncryptOption.Strict
            },
            TrustServerCertificate = EncryptMode != "Strict", // Not supported with Strict
            Pooling = false, // No pooling to force full TLS handshake each time
            ConnectTimeout = 30,
            Authentication = SqlAuthenticationMethod.SqlPassword,
        };

        if (!string.IsNullOrEmpty(serverCert))
        {
            builder.ServerCertificate = serverCert;
        }

        if (!string.IsNullOrEmpty(hostInCert))
        {
            builder.HostNameInCertificate = hostInCert;
        }

        _connectionString = builder.ConnectionString;

        // Validate connectivity
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
    }

    [Benchmark(Description = "Batch Open+Query+Close (no pooling)")]
    public void BatchOpenQueryClose()
    {
        for (int i = 0; i < ConnectionCount; i++)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
        }
    }

    [Benchmark(Description = "Batch Async Open+Query+Close (no pooling)")]
    public async Task BatchOpenQueryCloseAsync()
    {
        for (int i = 0; i < ConnectionCount; i++)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        SqlConnection.ClearAllPools();
    }
}
