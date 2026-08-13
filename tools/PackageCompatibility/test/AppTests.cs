using System;
using System.IO;

using Microsoft.Data.SqlClient.Tools.PackageCompatibility;

using Xunit;

namespace Microsoft.Data.SqlClient.Tools.PackageCompatibility.Tests;

/// <summary>
/// Verifies <see cref="App.Run(App.RunOptions)"/> control flow and diagnostics for common failure
/// and instrumentation paths without requiring a live SQL Server.
/// </summary>
[Collection(ConsoleCollection.Name)]
public class AppTests
{
    /// <summary>
    /// Ensures malformed connection strings produce a parse error and a non-zero exit code in
    /// non-verbose mode, without leaking full exception details.
    /// </summary>
    [Fact]
    public void AppRunWithMalformedConnectionStringReturnsOneAndWritesParseError()
    {
        // Use an intentionally invalid connection string to force parse failure branch.
        CommandResult result = ExecuteApp(
            connectionString: "Server",
            verbose: false);

        // Non-verbose mode should report a friendly error without full exception type details.
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Failed to parse connection string:", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", result.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures malformed connection strings produce detailed exception output when verbose mode is
    /// enabled.
    /// </summary>
    [Fact]
    public void AppRunWithMalformedConnectionStringAndVerboseIncludesExceptionDetails()
    {
        // Same malformed input, but verbose mode should include full exception diagnostics.
        CommandResult result = ExecuteApp(
            connectionString: "Server",
            verbose: true);

        // Verbose mode intentionally includes type/stack-friendly exception text.
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Failed to parse connection string:", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("System.", result.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures enabling event logging emits the expected logging banner before the eventual
    /// connection failure for an unreachable endpoint.
    /// </summary>
    [Fact]
    public void AppRunWithLogEventsEmitsLoggingMessageBeforeConnectionFailure()
    {
        // Use an unreachable endpoint to keep this deterministic while exercising log-events path.
        CommandResult result = ExecuteApp(
            connectionString: "Server=127.0.0.1,1;Database=master;User ID=sa;Password=invalid;Connect Timeout=1;Encrypt=False",
            logEvents: true,
            verbose: false);

        // App should announce event logging before eventually failing the connection attempt.
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("SqlClient event logging enabled; events will be prefixed with [EVENT]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Connection failed:", result.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    /// Executes <see cref="App.Run(App.RunOptions)"/> with redirected console streams and returns
    /// captured output and exit code for assertions.
    /// </summary>
    /// <param name="connectionString">Connection string supplied to <see cref="App.RunOptions.ConnectionString"/>.</param>
    /// <param name="logEvents">Whether to enable event listener wiring during the run.</param>
    /// <param name="trace">Whether to enable trace-attach pause behavior.</param>
    /// <param name="verbose">Whether to emit verbose diagnostic details.</param>
    /// <returns>Captured stdout, stderr, and process-equivalent exit code.</returns>
    private static CommandResult ExecuteApp(string connectionString, bool logEvents = false, bool trace = false, bool verbose = false)
    {
        // Redirect process-wide console streams so assertions can inspect output.
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter standardOutput = new();
        using StringWriter standardError = new();

        // Construct the same internal options object EntryPoint passes to App.Run.
        using App app = new();
        App.RunOptions runOptions = new()
        {
            ConnectionString = connectionString,
            LogEvents = logEvents,
            Trace = trace,
            Verbose = verbose
        };

        try
        {
            Console.SetOut(standardOutput);
            Console.SetError(standardError);

            // Act: execute one App.Run invocation for the provided scenario.
            int exitCode = app.Run(runOptions);

            // Capture both streams because App writes info and errors separately.
            return new CommandResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            // Restore global console streams to prevent cross-test interference.
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    /// Represents the observed result of a single in-process <see cref="App.Run(App.RunOptions)"/>
    /// invocation.
    /// </summary>
    /// <param name="ExitCode">Return code from <see cref="App.Run(App.RunOptions)"/>.</param>
    /// <param name="StandardOutput">Captured standard output stream contents.</param>
    /// <param name="StandardError">Captured standard error stream contents.</param>
    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
