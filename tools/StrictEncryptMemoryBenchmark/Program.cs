using BenchmarkDotNet.Running;
using StrictEncryptMemoryBenchmark;

// Connection parameters are read from environment variables:
//   BENCHMARK_SERVER   - SQL Server hostname (required)
//   BENCHMARK_DATABASE - Database name (default: master)
//   BENCHMARK_USER     - SQL Auth username (required)
//   BENCHMARK_PASSWORD - SQL Auth password (required)
//   BENCHMARK_SERVER_CERTIFICATE - CA cert path for Strict mode (optional)
//   BENCHMARK_HOSTNAME_IN_CERTIFICATE - Expected cert hostname (optional)
//
// Usage:
//   dotnet run -c Release                       # Run all BenchmarkDotNet benchmarks
//   dotnet run -c Release -- --filter *Stress*  # Run only stress benchmarks
//   dotnet run -c Release -- --long-running     # Run long-running native memory leak detector

var server = Environment.GetEnvironmentVariable("BENCHMARK_SERVER");
var user = Environment.GetEnvironmentVariable("BENCHMARK_USER");
var password = Environment.GetEnvironmentVariable("BENCHMARK_PASSWORD");

if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
{
    Console.WriteLine("ERROR: Set environment variables before running:");
    Console.WriteLine("  BENCHMARK_SERVER   - SQL Server hostname");
    Console.WriteLine("  BENCHMARK_USER     - SQL Auth username");
    Console.WriteLine("  BENCHMARK_PASSWORD - SQL Auth password");
    Console.WriteLine();
    Console.WriteLine("Optional:");
    Console.WriteLine("  BENCHMARK_DATABASE - Database (default: master)");
    Console.WriteLine("  BENCHMARK_SERVER_CERTIFICATE - CA cert path for Strict mode");
    Console.WriteLine("  BENCHMARK_HOSTNAME_IN_CERTIFICATE - Expected cert hostname");
    Console.WriteLine();
    Console.WriteLine("Then run:");
    Console.WriteLine("  dotnet run -c Release                        # BenchmarkDotNet mode");
    Console.WriteLine("  dotnet run -c Release -- --long-running      # Long-running native memory tracker");
    Console.WriteLine("  dotnet run -c Release -- --long-running --connections 10000 --encrypt Strict");
    return;
}

// Check if user wants the long-running native memory monitor mode
if (args.Any(a => a.Equals("--long-running", StringComparison.OrdinalIgnoreCase)))
{
    await NativeMemoryLeakDetector.RunAsync(args);
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(ConnectionBenchmarks).Assembly).Run(args);
}
