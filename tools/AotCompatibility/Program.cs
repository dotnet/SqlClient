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
bool switchFound = AppContext.TryGetSwitch(
    "Microsoft.Data.SqlClient.EnableReflectionBasedAuthenticationProviderDiscovery",
    out bool reflectionEnabled);
if (!switchFound)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  WARNING: Feature switch not found in AppContext.");
    Console.WriteLine("  The switch is set via RuntimeHostConfigurationOption at publish time.");
    Console.WriteLine("  If running via 'dotnet run' (JIT mode), this is expected.");
    Console.ResetColor();
}
Console.WriteLine($"  EnableReflectionBasedAuthenticationProviderDiscovery: {reflectionEnabled}");
Console.WriteLine();

Console.WriteLine("SqlAuthenticationProvider API checks:");

// Register a provider explicitly (the AOT-safe way).
string clientId = Guid.NewGuid().ToString();
var provider = new ActiveDirectoryAuthenticationProvider(clientId);
Console.WriteLine($"  ApplicationClientId: {clientId}");

bool registered = SqlAuthenticationProvider.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryDefault, provider);
Console.WriteLine($"  SetProvider(Default): {registered}");

registered = SqlAuthenticationProvider.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, provider);
Console.WriteLine($"  SetProvider(ManagedIdentity): {registered}");

registered = SqlAuthenticationProvider.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity, provider);
Console.WriteLine($"  SetProvider(WorkloadIdentity): {registered}");

// Verify we can retrieve the registered provider.
var retrieved = SqlAuthenticationProvider.GetProvider(
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
    return 1;
}

Console.WriteLine();

// Force the federated-authentication code path to be statically reachable so the
// trimmer's treatment of reflection-based provider discovery is actually exercised.
//
// In a real AOT app this path is reached when an Active Directory connection
// authenticates:
//
//   SqlConnection.Open()
//     -> SqlInternalConnectionTds.GetFedAuthToken()
//       -> AuthenticationBootstrapper.Bootstrap()
//         -> AuthenticationBootstrapper ctor
//           -> LoadAzureExtensionProvider()   (the reflection-based discovery)
//
// We model that here with an Open() attempt using an Active Directory
// authentication mode. The attempt is EXPECTED to fail at runtime (there is no
// server listening), but trimming is a static analysis: the mere presence of this
// call roots the fed-auth call graph, so the trimmer must decide whether to keep
// or eliminate LoadAzureExtensionProvider() based on the feature switch.
//
// Without this call the entire bootstrapper path is unreachable and would be
// trimmed regardless of the switch, making the trimming verification meaningless.
Console.WriteLine("Active Directory connection open (roots the fed-auth path):");
try
{
    using var adConnection = new SqlConnection(
        "Server=localhost;Database=test;Encrypt=false;" +
        "Authentication=Active Directory Default;Connect Timeout=1;");
    adConnection.Open();
    Console.WriteLine("  Opened unexpectedly (no server was expected to be present).");
}
catch (Exception ex)
{
    // Expected: there is no server to connect to. The point is static
    // reachability for the trimmer, not a successful connection.
    Console.WriteLine($"  Open failed as expected: {ex.GetType().Name}");
}

Console.WriteLine();

// Check the ILC map file for trimming verification.
// The map file is generated alongside the native binary during publish.
// At runtime we can look for it relative to the executable.
Console.WriteLine("Trimming verification (ILC map file):");

// The ILC map file is generated only by a Native AOT publish, at
//   <proj>/obj/<Config>/<TFM>/<RID>/native/<ExeName>.map.xml
// while the native binary lives at
//   <proj>/bin/<Config>/<TFM>/<RID>/publish/<ExeName>
//
// We derive the map path deterministically from the running binary's location so
// we only ever read the map for THIS exact configuration. Under the JIT
// (dotnet run) the process path is the shared 'dotnet' host rather than our
// native binary, so no map is found and the verification is skipped - this avoids
// reading a stale map left over from a previous publish (possibly built with a
// different feature-switch value).
var exePath = Environment.ProcessPath;
if (exePath is not null)
{
    var exeDir = Path.GetDirectoryName(exePath)!;
    var exeName = Path.GetFileNameWithoutExtension(exePath);

    // Candidate 1: alongside the binary, in case it is ever copied there.
    var beside = Path.Combine(exeDir, $"{exeName}.map.xml");

    // Candidate 2: the well-known obj/native path derived from the publish layout.
    // exeDir is expected to be <proj>/bin/<Config>/<TFM>/<RID>/publish.
    string? derived = null;
    if (Path.GetDirectoryName(exeDir) is { } ridDir)
    {
        var sep = Path.DirectorySeparatorChar;
        derived = Path.Combine(ridDir, "native", $"{exeName}.map.xml")
            .Replace($"{sep}bin{sep}", $"{sep}obj{sep}");
    }

    string? mapFile =
        File.Exists(beside) ? beside :
        derived is not null && File.Exists(derived) ? derived :
        null;

    if (mapFile is not null)
    {
        // Stream the file line-by-line to avoid loading a potentially huge ILC map
        // file entirely into memory.
        bool hasLoadAzure = false;
        foreach (var line in File.ReadLines(mapFile))
        {
            if (line.Contains("LoadAzureExtensionProvider", StringComparison.Ordinal))
            {
                hasLoadAzure = true;
                break;
            }
        }

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
        Console.WriteLine("  Skipped (no map file for this build; it is generated only by a Native AOT 'dotnet publish').");
    }
}

Console.WriteLine();
Console.WriteLine("All AOT compatibility checks passed.");
return 0;
