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
public class EntryPointTests : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly StringWriter _output;

    public EntryPointTests(ConsoleCollectionFixture _)
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _output = new StringWriter();
        Console.SetOut(_output);
        Console.SetError(_output);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        _output.Dispose();
    }

    /// <summary>
    /// Ensures invoking the entry point without the required connection-string argument fails and
    /// reports the missing option to the caller.
    /// </summary>
    [Fact]
    public void EntryPointWithoutConnectionStringReturnsNonZero()
    {
        // Act: invoke with no arguments; --connection-string is required.
        int exitCode = EntryPoint.Main(Array.Empty<string>());

        // Wait for command help/validation text to flush before asserting.
        string commandOutput = WaitForCapturedOutput(_output, "--connection-string");

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
        // Act: request help; this should never fail.
        int exitCode = EntryPoint.Main(new[] { "--help" });
        Assert.Equal(0, exitCode);

        // Ensure final help footer is present so all formatted content has been emitted.
        string helpOutput = WaitForCapturedOutput(_output, "--version");

        // Assert: sample property overrides remain documented in help text.
        Assert.Contains("-p:AbstractionsVersion=1.0.1", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:AkvProviderVersion=7.0.1-preview2", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:AzureVersion=1.0.0-preview1", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:LoggingVersion=1.0.2", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:SqlClientVersion=7.0.0-preview4", helpOutput, StringComparison.Ordinal);
        Assert.Contains("-p:SqlServerVersion=1.0.0", helpOutput, StringComparison.Ordinal);

        // Assert: currently resolved package defaults are visible to aid troubleshooting.
        Assert.Contains($"Abstractions:  {PackageVersions.MicrosoftDataSqlClientExtensionsAbstractions}", helpOutput, StringComparison.Ordinal);
        Assert.Contains($"AKV Provider:  {PackageVersions.MicrosoftDataSqlClientAlwaysEncryptedAzureKeyVaultProvider}", helpOutput, StringComparison.Ordinal);
        Assert.Contains("Azure:         N/A", helpOutput, StringComparison.Ordinal);
        Assert.Contains($"Logging:       {PackageVersions.MicrosoftDataSqlClientInternalLogging}", helpOutput, StringComparison.Ordinal);
        Assert.Contains($"SqlClient:     {PackageVersions.MicrosoftDataSqlClient}", helpOutput, StringComparison.Ordinal);
        Assert.Contains($"SqlServer:     {PackageVersions.MicrosoftSqlServerServer}", helpOutput, StringComparison.Ordinal);
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
