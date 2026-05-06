using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Microsoft.Data.SqlClient;

namespace StrictEncryptMemoryBenchmark;

[MemoryDiagnoser(displayGenColumns: true)]
[Config(typeof(Config))]
public class ConnectionBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(20)
                .WithId("SqlClientMemory"));

            AddColumn(StatisticColumn.AllStatistics);
            AddDiagnoser(new NativeMemoryDiagnoser());
            WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(50));
        }
    }

    [Params("Strict", "Mandatory", "Optional")]
    public string EncryptMode { get; set; } = "Strict";

    [Params(true, false)]
    public bool Pooling { get; set; }

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
            Pooling = Pooling,
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

    [Benchmark(Description = "Open+Close connection")]
    public void OpenClose()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
    }

    [Benchmark(Description = "Open+Query+Close connection")]
    public object? OpenQueryClose()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        return cmd.ExecuteScalar();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        SqlConnection.ClearAllPools();
    }
}
