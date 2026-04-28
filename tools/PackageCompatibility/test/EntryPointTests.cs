using System;
using System.IO;
using System.Threading;

using Microsoft.Data.SqlClient.Tools.PackageCompatibility;

using Xunit;

namespace Microsoft.Data.SqlClient.Tools.PackageCompatibility.Tests;

/// <summary>
/// Verifies command-line entry-point behavior, including required argument handling and help-text
/// content that documents default package versions and override examples.
/// </summary>
[Collection(ConsoleCollection.Name)]
public class EntryPointTests
{
    /// <summary>
    /// Ensures invoking the entry point without the required connection-string argument fails and
    /// reports the missing option to the caller.
    /// </summary>
    [Fact]
    public void EntryPointWithoutConnectionStringReturnsNonZero()
    {
        // Redirect console streams so we can assert on CLI output without polluting test logs.
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter output = new();

        int exitCode;

        try
        {
            Console.SetOut(output);
            Console.SetError(output);

            // Act: invoke with no arguments; --connection-string is required.
            exitCode = EntryPoint.Main(Array.Empty<string>());
        }
        finally
        {
            // Always restore global console streams for subsequent tests.
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        // Wait for command help/validation text to flush before asserting.
        string commandOutput = WaitForCapturedOutput(output, "--connection-string");

        // Assert: parser should reject the command and mention the missing option.
        Assert.NotEqual(0, exitCode);
        Assert.Contains("--connection-string", commandOutput, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures help output includes both version-override examples and currently resolved default
    /// package versions so users can understand and troubleshoot package selection.
    /// </summary>
    [Fact]
    public void HelpOutputContainsDefaultVersions()
    {
        // Capture help output for contract validation of documented build parameters.
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter output = new();

        string helpOutput;

        try
        {
            Console.SetOut(output);
            Console.SetError(output);

            // Act: request help; this should never fail.
            int exitCode = EntryPoint.Main(new[] { "--help" });
            Assert.Equal(0, exitCode);

            helpOutput = output.ToString();
        }
        finally
        {
            // Restore global console state to keep tests isolated.
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        // Ensure final help footer is present so all formatted content has been emitted.
        helpOutput = WaitForCapturedOutput(output, "--version");

        // Assert: sample property overrides remain documented in help text.
        Assert.Contains("-p:AbstractionsVersion=1.0.1", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:AkvProviderVersion=7.0.1-preview2", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:AzureVersion=1.0.0-preview1", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:LoggingVersion=1.0.2", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:SqlClientVersion=7.0.0-preview4", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:SqlServerVersion=1.0.0", helpOutput, StringComparison.Ordinal);

        // Assert: currently resolved package defaults are visible to aid troubleshooting.
        Assert.Contains("Abstractions:  1.0.0", helpOutput, StringComparison.Ordinal);
        Assert.Contains("AKV Provider:  7.0.0", helpOutput, StringComparison.Ordinal);
        Assert.Contains("Azure:         N/A", helpOutput, StringComparison.Ordinal);
        Assert.Contains("Logging:       1.0.0", helpOutput, StringComparison.Ordinal);
        Assert.Contains("SqlClient:     7.0.1", helpOutput, StringComparison.Ordinal);
        Assert.Contains("SqlServer:     1.0.0", helpOutput, StringComparison.Ordinal);
    }

    /// <summary>
    /// Waits briefly for asynchronously emitted command output to include a marker substring before
    /// assertions run, reducing flakiness in console-capture tests.
    /// </summary>
    /// <param name="output">Captured console output writer.</param>
    /// <param name="expectedSubstring">Marker text that indicates output emission has completed.</param>
    /// <returns>The current captured output text.</returns>
    private static string WaitForCapturedOutput(StringWriter output, string expectedSubstring)
    {
        // CommandLine writes asynchronously; wait briefly for expected marker text.
        SpinWait.SpinUntil(
            () => output.ToString().Contains(expectedSubstring, StringComparison.Ordinal),
            TimeSpan.FromSeconds(1));

        return output.ToString();
    }
}
