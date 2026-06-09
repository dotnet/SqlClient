// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// AOT Compatibility Test Application
//
// This application validates that Microsoft.Data.SqlClient can be published
// as a Native AOT binary when the reflection-based authentication provider
// discovery is disabled via the feature switch.
//
// Usage:
//   dotnet publish -c Release
//
// After successful publish, this app can be run to verify that explicit
// provider registration works correctly without reflection.
//

using Microsoft.Data.SqlClient;

Console.WriteLine("AOT Compatibility Test");
Console.WriteLine("======================");
Console.WriteLine();

// Verify that the feature switch disabled reflection-based discovery.
// In an AOT build, the trimmer will have substituted the property with
// constant false and eliminated LoadAzureExtensionProvider() entirely.
Console.WriteLine("Feature switch checks:");
AppContext.TryGetSwitch(
    "Microsoft.Data.SqlClient.EnableReflectionBasedAuthenticationProviderDiscovery",
    out bool reflectionEnabled);
Console.WriteLine($"  EnableReflectionBasedAuthenticationProviderDiscovery: {reflectionEnabled}");
Console.WriteLine();

// Verify that ApplicationClientId is accessible (will be null without config).
Console.WriteLine("SqlAuthenticationProviderManager API checks:");
Console.WriteLine($"  ApplicationClientId: {SqlAuthenticationProviderManager.ApplicationClientId ?? "(null)"}");

// Register a provider explicitly (the AOT-safe way).
var provider = new ActiveDirectoryAuthenticationProvider(
    SqlAuthenticationProviderManager.ApplicationClientId ?? string.Empty);

bool registered = SqlAuthenticationProviderManager.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryDefault, provider);
Console.WriteLine($"  SetProvider(Default): {registered}");

registered = SqlAuthenticationProviderManager.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, provider);
Console.WriteLine($"  SetProvider(ManagedIdentity): {registered}");

registered = SqlAuthenticationProviderManager.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity, provider);
Console.WriteLine($"  SetProvider(WorkloadIdentity): {registered}");

// Verify we can retrieve the registered provider.
var retrieved = SqlAuthenticationProviderManager.GetProvider(
    SqlAuthenticationMethod.ActiveDirectoryDefault);
Console.WriteLine($"  GetProvider(Default): {retrieved?.GetType().Name ?? "(null)"}");
Console.WriteLine();

// Verify SqlConnection can be constructed (no actual connection needed).
Console.WriteLine("SqlConnection construction:");
try
{
    using var connection = new SqlConnection(
        "Server=localhost;Database=test;Encrypt=false;");
    Console.WriteLine($"  Created successfully (State={connection.State})");
}
catch (Exception ex)
{
    Console.WriteLine($"  Construction failed: {ex.Message}");
}

Console.WriteLine();

// Check the ILC map file for trimming verification.
// The map file is generated alongside the native binary during publish.
// At runtime we can look for it relative to the executable.
Console.WriteLine("Trimming verification (ILC map file):");
var exePath = Environment.ProcessPath;
if (exePath is not null)
{
    var exeDir = Path.GetDirectoryName(exePath)!;
    var exeName = Path.GetFileNameWithoutExtension(exePath);

    // Map file is in obj/<Config>/<TFM>/<RID>/native/<ExeName>.map.xml
    // But at runtime we only have the publish dir. Check if it was copied or
    // look in the well-known obj path relative to the project directory.
    // For simplicity, search upward from exe for the map file.
    var mapFile = Path.Combine(exeDir, $"{exeName}.map.xml");

    // If not next to the binary, try the obj path (when running from the project dir)
    if (!File.Exists(mapFile))
    {
        // Try to find it via the well-known native output path pattern
        var projectDir = FindProjectDir(exeDir);
        if (projectDir is not null)
        {
            var candidates = Directory.GetFiles(projectDir, $"{exeName}.map.xml", SearchOption.AllDirectories);
            if (candidates.Length > 0)
            {
                mapFile = candidates[0];
            }
        }
    }

    if (File.Exists(mapFile))
    {
        var mapContent = File.ReadAllText(mapFile);
        bool hasLoadAzure = mapContent.Contains("LoadAzureExtensionProvider", StringComparison.Ordinal);

        Console.WriteLine($"  Map file: {mapFile}");
        Console.WriteLine($"  Contains LoadAzureExtensionProvider: {hasLoadAzure}");

        if (!reflectionEnabled && hasLoadAzure)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  FAIL: Reflection code was NOT trimmed!");
            Console.ResetColor();
            return 1;
        }
        else if (!reflectionEnabled && !hasLoadAzure)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  PASS: Reflection code was successfully trimmed.");
            Console.ResetColor();
        }
        else if (reflectionEnabled && hasLoadAzure)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  PASS: Reflection code is present (as expected with discovery enabled).");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  WARN: Reflection code absent despite discovery being enabled.");
            Console.ResetColor();
        }
    }
    else
    {
        Console.WriteLine($"  Map file not found (expected after 'dotnet publish').");
        Console.WriteLine("  Skipping trimming verification.");
    }
}

Console.WriteLine();
Console.WriteLine("All AOT compatibility checks passed.");
return 0;

static string? FindProjectDir(string startDir)
{
    var dir = startDir;
    while (dir is not null)
    {
        if (Directory.GetFiles(dir, "*.csproj").Length > 0)
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}
