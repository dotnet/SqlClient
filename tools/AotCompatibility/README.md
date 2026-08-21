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
5. **Fed-auth path is rooted** — The app attempts to open an Active Directory
   authenticated connection (expected to fail at runtime, since no server is
   present). This makes the reflection-based discovery path *statically
   reachable* for the trimmer, so the trimming verification below is meaningful.

> ### Why the Active Directory open attempt matters
>
> The reflection-based discovery in `LoadAzureExtensionProvider()` is only
> reachable through `AuthenticationBootstrapper.Bootstrap()`, which the driver
> calls lazily the first time a federated/Active Directory connection
> authenticates:
>
> ```text
> SqlConnection.Open()
>   -> SqlInternalConnectionTds.GetFedAuthToken()
>     -> AuthenticationBootstrapper.Bootstrap()
>       -> AuthenticationBootstrapper ctor
>         -> LoadAzureExtensionProvider()   // reflection-based discovery
> ```
>
> If the app never roots this call graph (e.g. it only *constructs* a
> `SqlConnection`), the entire bootstrapper path is unreachable and the trimmer
> removes `LoadAzureExtensionProvider()` **regardless of the feature switch** —
> which would make the switch verification a false positive. The app therefore
> issues an Active Directory `Open()` attempt purely to root the path; trimming
> is a static analysis, so the runtime connection failure is irrelevant.

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
two additional warnings rooted at the `AuthenticationBootstrapper` constructor
(which calls that method):

- **IL2026** (trim analysis) — `LoadAzureExtensionProvider()` is annotated with
  `[RequiresUnreferencedCode]` because Azure extension discovery uses
  `Assembly.Load` and `Activator.CreateInstance`.
- **IL3050** (AOT analysis) — the same method is annotated with
  `[RequiresDynamicCode]` because it uses `Activator.CreateInstance`.

This confirms the feature switch is working — setting it to `false` (the test
app's default, configured in the csproj) substitutes the guard with a constant
and removes both warnings along with the method itself. Note that the
*library's* default is `true` (reflection enabled); the test app overrides this
to validate AOT trimming.

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

Active Directory connection open (roots the fed-auth path):
  Open failed as expected: SqlException

Trimming verification (ILC map file):
  Map file: <obj>/.../native/AotCompatibility.map.xml
  Contains LoadAzureExtensionProvider: False
  PASS: Reflection code was successfully trimmed.

All AOT compatibility checks passed.
```

When published with `EnableReflectionBasedAuthProviderDiscovery=true`, the
trimming verification instead reports:

```text
  Contains LoadAzureExtensionProvider: True
  PASS: Reflection code is present (as expected with discovery enabled).
```

> The map-file verification only produces a meaningful result after a Native AOT
> `dotnet publish` (which generates `AotCompatibility.map.xml`). In JIT
> (`dotnet run`) mode the map file is absent and that step is skipped.

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
