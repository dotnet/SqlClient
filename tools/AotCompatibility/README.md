# AOT Compatibility Test App

This application validates that `Microsoft.Data.SqlClient` can be published as a
Native AOT binary when the reflection-based authentication provider discovery is
disabled via the feature switch.

## What it validates

1. **Feature switch propagation** — `RuntimeHostConfigurationOption` with
   `Trim="true"` correctly disables reflection-based provider discovery.
2. **Native AOT publish** — The app publishes successfully as a fully native
   binary without linker errors.
3. **Explicit provider registration** — `SqlAuthenticationProvider.SetProvider()`
   and `GetProvider()` work correctly at runtime in an AOT context.
4. **SqlConnection construction** — Basic `SqlConnection` object creation works
   without reflection.

## Usage

### Build (JIT mode, for quick iteration)

```bash
dotnet build
dotnet run
```

### Publish as Native AOT

```bash
dotnet publish -c Release -f net9.0 -r linux-x64
./bin/Release/net9.0/linux-x64/publish/AotCompatibility
```

On Windows:

```cmd
dotnet publish -c Release -f net9.0 -r win-x64
bin\Release\net9.0\win-x64\publish\AotCompatibility.exe
```

### Publish with reflection enabled (to confirm trimmer warnings appear)

```bash
dotnet publish -c Release -f net9.0 -r linux-x64 -p:EnableReflectionBasedAuthProviderDiscovery=true
```

When `EnableReflectionBasedAuthProviderDiscovery=true`, the trimmer cannot
eliminate the reflection code in `LoadAzureExtensionProvider()`, so you will see
additional IL2075/IL2026 warnings from that method. This confirms the feature
switch is working — setting it to `false` (the test app's default, configured in
the csproj) removes those warnings. Note that the *library's* default is `true`
(reflection enabled); the test app overrides this to validate AOT trimming.

## Expected output

```text
AOT Compatibility Test
======================

Feature switch checks:
  EnableReflectionBasedAuthenticationProviderDiscovery: False

SqlAuthenticationProvider API checks:
  ApplicationClientId: <random-guid>
  SetProvider(Default): True
  SetProvider(ManagedIdentity): True
  SetProvider(WorkloadIdentity): True
  GetProvider(Default): ActiveDirectoryAuthenticationProvider

SqlConnection construction:
  Created successfully (State=Closed)

All AOT compatibility checks passed.
```

## Trimmer warnings

Some trimmer warnings may appear during publish. These fall into categories:

| Source | Description | Status |
| ------ | ----------- | ------ |
| `AuthenticationBootstrapper` (config section) | `Type.GetType` in configuration-based provider loading | Pre-existing; future work to guard |
| `SqlDiagnosticListener` | `DiagnosticSource.Write<T>` usage | Pre-existing; unrelated to auth |
| `System.Configuration` | Reflection in `ConfigurationManager` | External dependency |

The auth provider **feature switch** correctly eliminates the `LoadAzureExtensionProvider()`
reflection path. The remaining warnings are tracked separately and do not affect
the validity of the AOT auth provider registration pattern.

## Feature switch

The project includes a `RuntimeHostConfigurationOption` in the `.csproj`:

```xml
<RuntimeHostConfigurationOption
  Include="Microsoft.Data.SqlClient.EnableReflectionBasedAuthenticationProviderDiscovery"
  Value="false"
  Trim="true" />
```

This tells the trimmer to substitute
`LocalAppContextSwitches.EnableReflectionBasedAuthenticationProviderDiscovery`
with `false` at compile time, enabling the trimmer to eliminate the entire
`LoadAzureExtensionProvider()` method and its reflection dependencies.

### How trimming works per TFM

| TFM | Mechanism | How it works |
| --- | --------- | ------------ |
| **net9.0+** | `[FeatureSwitchDefinition]` attribute | The attribute on the property tells the trimmer directly that this property is a feature switch. When a `RuntimeHostConfigurationOption` sets it to `false`, the trimmer substitutes the property return value and eliminates guarded code. |
| **net8.0** | `ILLink.Substitutions.xml` | The `[FeatureSwitchDefinition]` attribute does not exist on net8.0. Instead, the `ILLink.Substitutions.xml` file (embedded in the SqlClient assembly) declares the substitution. The trimmer reads this file and performs the same constant substitution, enabling dead-code elimination of the reflection path. |

Both mechanisms produce the same result: the trimmer sees the property as
returning a compile-time constant `false` and removes the unreachable
`LoadAzureExtensionProvider()` call and its transitive reflection dependencies.
