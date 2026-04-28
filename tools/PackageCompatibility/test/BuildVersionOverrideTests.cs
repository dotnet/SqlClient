using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Xunit;

namespace Microsoft.Data.SqlClient.Tools.PackageCompatibility.Tests;

/// <summary>
/// Build-focused tests that verify MSBuild property overrides (for package versions) are reflected
/// in generated package-version constants.
/// </summary>
public class BuildVersionOverrideTests
{
    /// <summary>
    /// Ensures building with explicit <c>-p:</c> overrides produces matching version constants in
    /// the generated <c>PackageVersions.g.cs</c> file.
    /// </summary>
    /// <param name="buildProperties">MSBuild properties to override during build.</param>
    /// <param name="expectedVersions">Expected package label-to-version mappings.</param>
    [Theory]
    [MemberData(nameof(GetVersionPropertyTestCases))]
    public void BuildGeneratesExpectedPackageVersionsWhenCustomPropertiesAreSupplied(
        Dictionary<string, string> buildProperties,
        Dictionary<string, string> expectedVersions)
    {
        // Build in an isolated copy so per-test overrides cannot leak into repo obj/bin state.
        BuildArtifacts artifacts = BuildAppWithProperties(buildProperties);

        // The generated source file is the authoritative output of GeneratePackageVersions.targets.
        string generatedVersions = File.ReadAllText(artifacts.GeneratedVersionsFile);

        // Assert each expected package label maps to the generated constant/version pair.
        foreach (var kvp in expectedVersions)
        {
            string constantName = GetPackageVersionsConstantName(kvp.Key);
            string expected = $"public const string {constantName} = \"{kvp.Value}\";";
            Assert.Contains(expected, generatedVersions, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Provides test vectors for single, partial, and full package-version override scenarios.
    /// </summary>
    /// <returns>
    /// Theory data containing MSBuild override properties and expected generated version values.
    /// </returns>
    public static IEnumerable<object[]> GetVersionPropertyTestCases()
    {
        // Override only SqlClient to prove a single-property override flows to generated code.
        yield return new object[]
        {
            new Dictionary<string, string> { { "SqlClientVersion", "7.0.0" } },
            new Dictionary<string, string>
            {
                { "SqlClient", "7.0.0" },
                { "Abstractions", "1.0.0" },
                { "Logging", "1.0.0" },
                { "SqlServer", "1.0.0" }
            }
        };

        // Override a subset of properties to verify mixed default/override behavior.
        yield return new object[]
        {
            new Dictionary<string, string>
            {
                { "SqlClientVersion", "7.0.0" },
                { "LoggingVersion", "1.0.0" },
                { "AbstractionsVersion", "1.0.0" }
            },
            new Dictionary<string, string>
            {
                { "SqlClient", "7.0.0" },
                { "Logging", "1.0.0" },
                { "Abstractions", "1.0.0" },
                { "AKV Provider", "7.0.0" }
            }
        };

        // Override all supported package properties to validate full replacement behavior.
        yield return new object[]
        {
            new Dictionary<string, string>
            {
                { "AbstractionsVersion", "1.0.0" },
                { "AkvProviderVersion", "7.0.0" },
                { "LoggingVersion", "1.0.0" },
                { "SqlClientVersion", "7.0.0" },
                { "SqlServerVersion", "1.0.0" }
            },
            new Dictionary<string, string>
            {
                { "Abstractions", "1.0.0" },
                { "AKV Provider", "7.0.0" },
                { "Logging", "1.0.0" },
                { "SqlClient", "7.0.0" },
                { "SqlServer", "1.0.0" }
            }
        };
    }

    /// <summary>
    /// Builds an isolated copy of the PackageCompatibility project with optional version override
    /// properties and returns paths to relevant build artifacts.
    /// </summary>
    /// <param name="properties">MSBuild properties passed via <c>-p:</c> arguments.</param>
    /// <returns>Paths to the isolated workspace, output folder, and generated versions file.</returns>
    private static BuildArtifacts BuildAppWithProperties(Dictionary<string, string> properties)
    {
        // Locate and clone the package-compatibility subtree so test builds are hermetic.
        string packageCompatibilityDir = GetPackageCompatibilityDirectory();
        string tempProjectDir = Path.Combine(Path.GetTempPath(), $"PackageCompatibilityProject_{Guid.NewGuid():N}");
        string tempOutputDir = Path.Combine(Path.GetTempPath(), $"PackageCompatibility_{Guid.NewGuid():N}");
        CopyDirectory(packageCompatibilityDir, tempProjectDir);
        Directory.CreateDirectory(tempOutputDir);

        string copiedProjectFile = Path.Combine(tempProjectDir, "src", "PackageCompatibility.csproj");
        string generatedVersionsFile = Path.Combine(tempProjectDir, "src", "obj", "Release", "net10.0", "PackageVersions.g.cs");

        // Build with explicit property overrides; MSBuild treats these as highest-priority values.
        var buildArgs = new List<string>
        {
            "build",
            copiedProjectFile,
            "-c", "Release",
            "-f", "net10.0",
            "-o", tempOutputDir
        };

        // Add requested test case overrides as -p:<Name>=<Value> arguments.
        foreach (var kvp in properties)
        {
            buildArgs.Add($"-p:{kvp.Key}={kvp.Value}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = string.Join(" ", buildArgs),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi))
        {
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start build process");
            }

            string stdout = string.Empty;
            string stderr = string.Empty;

#if NET
            bool completed = process.WaitForExit(TimeSpan.FromSeconds(60));
            if (!completed)
            {
                process.Kill();
                throw new InvalidOperationException("Build timed out after 60 seconds");
            }
#else
            process.WaitForExit(60000);
#endif

            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();

            // Fail fast with full command/stdout/stderr context for easy diagnosis.
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Build failed with exit code {process.ExitCode}.\n" +
                    $"Command: dotnet {string.Join(" ", buildArgs)}\n" +
                    $"Output:\n{stdout}\n" +
                    $"Errors:\n{stderr}");
            }
        }

        // Validate that the generator actually produced its output file.
        Assert.True(File.Exists(generatedVersionsFile), $"Generated versions file not found at {generatedVersionsFile}");

        return new BuildArtifacts(tempProjectDir, tempOutputDir, generatedVersionsFile);
    }

    /// <summary>
    /// Maps human-readable package labels used in test vectors to generated constant names in
    /// <c>PackageVersions.g.cs</c>.
    /// </summary>
    /// <param name="packageLabel">Package label used by test case data.</param>
    /// <returns>Generated constant name expected in <c>PackageVersions.g.cs</c>.</returns>
    private static string GetPackageVersionsConstantName(string packageLabel)
    {
        // Keep human-readable labels in test cases while asserting exact generated symbol names.
        return packageLabel switch
        {
            "Abstractions" => "MicrosoftDataSqlClientExtensionsAbstractions",
            "AKV Provider" => "MicrosoftDataSqlClientAlwaysEncryptedAzureKeyVaultProvider",
            "Logging" => "MicrosoftDataSqlClientInternalLogging",
            "SqlClient" => "MicrosoftDataSqlClient",
            "SqlServer" => "MicrosoftSqlServerServer",
            _ => throw new InvalidOperationException($"Unsupported package label '{packageLabel}'.")
        };
    }

    /// <summary>
    /// Recursively copies the source tree into an isolated workspace, excluding generated artifacts
    /// and test folders that can destabilize isolated build behavior.
    /// </summary>
    /// <param name="sourceDirectory">Source directory to copy.</param>
    /// <param name="destinationDirectory">Destination directory for the isolated copy.</param>
    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        // Copy source tree into temp workspace while excluding volatile build/test folders.
        Directory.CreateDirectory(destinationDirectory);

        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            if (ShouldSkip(relativePath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            if (ShouldSkip(relativePath))
            {
                continue;
            }

            string destinationFile = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    /// <summary>
    /// Determines whether a relative path should be excluded from the isolated workspace copy.
    /// </summary>
    /// <param name="relativePath">Path relative to the copy source root.</param>
    /// <returns><see langword="true"/> when the path should be skipped; otherwise <see langword="false"/>.</returns>
    private static bool ShouldSkip(string relativePath)
    {
        // Ignore generated artifacts and the test project itself to avoid recursive/self-copy issues.
        string normalizedPath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string[] segments = normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            if (segment.Equals("bin", StringComparison.Ordinal)
                || segment.Equals("obj", StringComparison.Ordinal)
                || segment.Equals("test", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves and validates the repository path for the PackageCompatibility subtree that contains
    /// the source project and central package-management files.
    /// </summary>
    /// <returns>Absolute path to the PackageCompatibility subtree root.</returns>
    private static string GetPackageCompatibilityDirectory()
    {
        // Resolve the PackageCompatibility folder relative to the running test assembly.
        string testDir = Path.GetDirectoryName(typeof(BuildVersionOverrideTests).Assembly.Location)
            ?? throw new InvalidOperationException("Cannot determine test assembly directory");

        string packageCompatibilityDir = Path.Combine(testDir, "..", "..", "..", "..");

        packageCompatibilityDir = Path.GetFullPath(packageCompatibilityDir);

        if (!Directory.Exists(packageCompatibilityDir))
        {
            throw new InvalidOperationException(
                $"PackageCompatibility directory not found at {packageCompatibilityDir}.\n" +
                $"Test assembly location: {testDir}\n" +
                $"Calculated package compat dir: {Path.GetFullPath(packageCompatibilityDir)}");
        }

        // Ensure the expected project file exists before attempting to build.
        string csprojFile = Path.Combine(packageCompatibilityDir, "src", "PackageCompatibility.csproj");
        if (!File.Exists(csprojFile))
        {
            throw new InvalidOperationException(
                $"Project file not found at {csprojFile}.\n" +
                $"Expected to find PackageCompatibility.csproj in {packageCompatibilityDir}/src");
        }

        return packageCompatibilityDir;
    }

    /// <summary>
    /// Paths to the isolated build workspace and generated version artifacts for a single test run.
    /// </summary>
    /// <param name="ProjectDirectory">Root of the isolated copied project tree.</param>
    /// <param name="OutputDirectory">Build output directory for binaries.</param>
    /// <param name="GeneratedVersionsFile">Path to generated <c>PackageVersions.g.cs</c>.</param>
    private sealed record BuildArtifacts(string ProjectDirectory, string OutputDirectory, string GeneratedVersionsFile);
}
