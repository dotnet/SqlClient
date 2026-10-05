# Design

> Part of the [AOT-safe authentication provider registration](README.md) proposal.
> **Audience:** implementers and code reviewers. **Read first:** [README](README.md).

The authoritative description of what gets built. [alternatives.md](alternatives.md) explains why
each mechanism was chosen over the others, and [implementation-plan.md](implementation-plan.md)
orders the work.

| Mechanism | Section | Approach |
|---|---|---|
| Relocate the registry, bootstrap and config parsing into Abstractions | [What moves and what stays](#what-moves-and-what-stays) | [A4-Full](alternatives.md#a4-move-the-authentication-pipeline-into-abstractions--selected) |
| Keep the config section types in SqlClient as schema | [Role of the configuration section types](#role-of-the-configuration-section-types) | A4-Full |
| Bootstrap on first registry access | [Bootstrap](#bootstrap) | A4 |
| Order-independent precedence | [Precedence tiers](#precedence-tiers) | [D3](alternatives.md#d3-order-independent-tiers--selected) |
| Switches and feature guards | [Gating](#gating) | [C2](alternatives.md#c2-feature-guard-shape--selected), [C3](alternatives.md#c3-split-the-gates-aligned-with-enableappconfig--selected) |
| Public default-provider factory, preferred over reflection | [Default-provider factory](#default-provider-factory) | [B8](alternatives.md#b8-public-default-provider-factory--selected) |
| Generated registration | [Source generator](#source-generator) | [B7](alternatives.md#b7-source-generator-in-the-azure-package--selected) |
| `netstandard2.0;net10.0` Abstractions | [Abstractions target frameworks](#abstractions-target-frameworks) | [C6b](alternatives.md#c6-where-do-the-annotations-get-checked--multi-target-selected) |
| Build-time and runtime version checks | [Mixed-version enforcement](#mixed-version-enforcement) | [E2](alternatives.md#e2-build-time-check-in-abstractions-build-and-buildtransitive--selected), [E3](alternatives.md#e3-runtime-check-in-abstractions--selected) |
| Fixes for L1–L4 | [Latent-bug fixes](#latent-bug-fixes) | — |

## What moves and what stays

On `main` there is no separate bootstrapper. `SqlAuthenticationProviderManager`'s static
constructor and instance constructor do config parsing, initializer invocation and Azure
discovery. Each dependency was audited for a move into Abstractions
([type forwarding and visibility rules: primer](primer.md#assemblies-visibility-and-type-forwarding)):

| Dependency | Status on `main` | Consequence for the move |
|---|---|---|
| `SqlAuthenticationProviderConfigurationSection`, `SqlClientAuthenticationProviderConfigurationSection` | `internal`, in SqlClient | **Stay in SqlClient** — see [the next section](#role-of-the-configuration-section-types) |
| `SqlAuthenticationInitializer` | **public**, in SqlClient and its ref assembly | Moves with a `[TypeForwardedTo]` (public, downward). `TypeForwards.Abstractions.cs` already forwards five types this way |
| `SqlClientLogger` | public, SqlClient-only | Does not move; replaced with `SqlClientEventSource` from Logging, which Abstractions already references |
| `SQL.CannotCreateAuthProvider`, `CannotCreateSqlAuthInitializer`, `UnsupportedAuthenticationByProvider`, `UnsupportedAuthentication`, `UseWamBrokerRequiresAzureExtensionUpgrade` | SqlClient; five `Strings` entries localized into 13 locales | Abstractions has no resources today. Add `Strings.resx` and copy the existing translations. Exception *types* are BCL (`ArgumentException`, `NotSupportedException`, `InvalidOperationException`), so behaviour is unchanged |
| `System.Configuration` (`net462`) / `System.Configuration.ConfigurationManager` (shipped `exclude="Compile"`) | SqlClient references both | **The only genuinely new dependency** for Abstractions. Ship it compile-excluded so it does not leak into third-party provider authors' compilations |
| `STRONG_NAME_SIGNING` public-key-token check | SqlClient | Moves as-is; `src/Directory.Build.props` defines the constant for every signed project |
| Feature switches, guards, polyfills | None exist for auth | New in Abstractions |
| `CreateAzureAuthenticationProvider` unit tests | `tests/UnitTests/.../SqlAuthenticationProviderManagerTests.cs` | Move to `Abstractions.Test` with the code |

`ConfigurationManager.GetSection` reads process-level configuration, so moving the reading code
does not change which configuration file is read.

## Role of the configuration section types

Customers' configuration files name the section types explicitly:

```xml
<configSections>
  <section name="SqlClientAuthenticationProviders"
           type="Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient" />
</configSections>
```

That string is a contract. An internal type cannot be type-forwarded (`typeof` of another
assembly's internal type is CS0122, measured), so the types cannot leave SqlClient without either
breaking every such file or becoming public API.

After the move **no SqlClient code calls them**, but `System.Configuration` still depends on them
entirely. They are the **section handlers**, and they remain the parser and schema for the XML:

1. **Instantiation.** `ConfigurationManager.GetSection` loads the type named in `<configSections>`
   and constructs it. If that type cannot be loaded, the read fails —
   `ConfigurationErrorsException: An error occurred creating the configuration section handler…`
   (measured, [A.3](experiments.md#a3-what-the-section-handler-does)).
2. **Schema and validation.** Their `[ConfigurationProperty]` declarations define the legal
   attributes and child elements (`applicationClientId`, `useWamBroker`, `initializerType`,
   `<providers>`). An undeclared attribute is rejected —
   `Unrecognized attribute 'notAnAttribute'` (measured).
3. **Deserialization.** They turn `<providers>` into a `ProviderSettingsCollection` and the
   attributes into typed property values, including the empty-string defaults that today's code
   relies on to tell "absent" from "set".

Abstractions consumes the result without referencing the type. It casts to the base
`ConfigurationSection` and reads `ElementInformation.Properties["applicationClientId" | "useWamBroker"
| "initializerType" | "providers"].Value`. Measured on .NET 10 against SqlClient 7.1.1's real
handler: every value was returned, including the provider list.

The split is therefore: **SqlClient owns the schema that config files bind to; Abstractions owns
the behaviour.** That keeps today's file format working unchanged on `net462` and `net10.0`.

Alternatives rejected:

- **Move the types and forward them** — impossible while they are internal (CS0122).
- **Make them public and forward them** — new public types that exist only as configuration
  schema (G3).
- **New handler types in Abstractions, keep SqlClient's for old files** — two schemas to maintain
  for one feature, and new files would be gratuitously different.

Under trimming the config path is gated off, so it does not matter that nothing roots these types.

## Bootstrap

The bootstrap runs **once, on the first access to the registry**, in Abstractions. Both entry
points reach it: the public `SqlAuthenticationProvider.GetProvider`/`SetProvider`, and SqlClient's
fed-auth path, which now calls the public `GetProvider`. This keeps today's guarantee — provided
by `SqlAuthenticationProviderManager`'s static constructor — that configuration and discovery
complete before any registry read or write. It needs no hook, module initializer or
`InternalsVisibleTo`, because the bootstrap and the registry live in the same assembly.

### The sequence after this proposal

```
first touch of the registry in Abstractions — from EITHER:
  • SqlAuthenticationProvider.GetProvider/SetProvider (direct call), OR
  • SqlConnection.Open() → … → GetFedAuthToken() → SqlAuthenticationProvider.GetProvider
      └─ bootstrap (once, same assembly)
          ├─ mixed-version check: SqlClient's exact family version (E3)
          ├─ if EnableAppConfig: read sections untyped; publish the registry;
          │     run the initializer (Config tier); install config providers (Config tier)
          ├─ if a default-provider factory is registered: invoke it (Default tier)
          └─ else if EnableAzureExtensionDiscovery: reflective discovery, checking Azure's
                family version (E3) (Default tier)
then:
  registry.GetProvider(method) → provider.AcquireTokenAsync(...)
```

Any bootstrap failure is cached and rethrown from every later registry access (L2).

### Ordering hazards any implementation must avoid

On `main`, `GetProvider`/`SetProvider` reach the manager and run its static constructor, so
**any** registry access runs config + Azure discovery first. Any design that defers the bootstrap
to the fed-auth path (as the closed PR #4573 did) introduces two regressions:

**Hazard 1 — user-registered providers silently clobbered.**

```csharp
SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, myProvider); // true
using var c = new SqlConnection("...Authentication=Active Directory Default;");
c.Open(); // a lazy bootstrap runs NOW and overwrites myProvider with the Azure default
```

`main`'s comment — *"SetProvider() will refuse to clobber an application specified provider"* —
is true only because of ordering. `SetProvider`'s update path replaces any entry that config did
not claim.

**Hazard 2 — `GetProvider` stops discovering.** On `main`,
`GetProvider(ActiveDirectoryDefault)` returns the Azure provider before any connection (measured).
A lazy bootstrap returns `null` until a fed-auth connection opens.

A4 avoids both by bootstrapping on first registry access, and D3 makes Hazard 1 impossible by
construction.

## Precedence tiers

Every registration carries a tier, and precedence depends only on tiers, never on timing:

| Tier | Set by | Semantics |
|---|---|---|
| Default | Default-provider factory, or reflective Azure discovery | Fills methods nobody else claimed; **never** overwrites Config or User |
| User | Public `SetProvider` | Overwrites Default; refused (returns `false`) if Config exists |
| Config | `app.config` providers and the configured `SqlAuthenticationInitializer` | Wins over both |

This reproduces today's observable behaviour, measured on .NET 10: a config-claimed method refuses
the app's `SetProvider`, and the Azure default never replaces an earlier registration
([A.3](experiments.md#a3-what-the-section-handler-does)). Today those outcomes depend on the static
constructor running first; with tiers they cannot change if the order does, so
[Hazard 1](#ordering-hazards-any-implementation-must-avoid) is impossible by construction.
Registrations made from inside the initializer rank as Config, matching the intent of configuring
an initializer in `app.config` ([L1](findings.md#latent-bugs-on-main)).

`IsSupported` enforcement and the `BeforeLoad`/`BeforeUnload` callbacks behave as today whenever a
registration is accepted.

## Gating

Each reflective path sits behind its own switch, and each switch is read through a separate
feature-guard property:

| Gate | Switch | Default | Guards |
|---|---|---|---|
| `app.config` providers, initializer, `applicationClientId`, `useWamBroker` | `Switch.Microsoft.Data.SqlClient.EnableAppConfig` (shared with #4697) | `true` | `ConfigurationManager.GetSection`, `Type.GetType`, `Activator.CreateInstance` |
| Azure extension discovery | `Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery` | `true` | `Assembly.Load`, `Assembly.GetType`, constructor/property probing |

The shape, measured as shape B in [findings](findings.md#measured-trimmer-and-ilc-behaviour-net-10):

```csharp
[FeatureSwitchDefinition("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery")]
internal static bool EnableAzureExtensionDiscovery =>
    AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", out bool v) ? v : true;

// A separate property: putting all three attributes on one property is never trimmed (shape C).
[FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
[FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
[UnconditionalSuppressMessage("Trimming", "IL4000",
    Justification = "Guard is backed by an AppContext switch; trimming treats it as false.")]
internal static bool IsAzureExtensionDiscoverySupported => EnableAzureExtensionDiscovery;

// Bootstrap
if (IsAzureExtensionDiscoverySupported)
{
    LoadAzureExtensionProvider();   // [RequiresUnreferencedCode] + [RequiresDynamicCode]
}
```

- The .NET 10 trimmer and AOT compiler treat the guard as `false` in **every** trimmed or AOT
  publish, so the branch and everything only it uses are removed with no application
  configuration. Guarded call sites produce no IL2026/IL3050.
- Applications can still turn either path off explicitly. Trimming honours this only in the
  publish-time form:

  ```xml
  <RuntimeHostConfigurationOption Include="Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery"
                                  Value="false" Trim="true" />
  ```

- `[FeatureSwitchDefinition]` is enough for .NET 10 apps (measured). An `ILLink.Substitutions.xml`
  is optional in 8.0 and required in the [backports](backporting.md).
- Reading the `EnableAppConfig` switch in Abstractions, under the same name SqlClient uses, means
  one setting turns off every `app.config` reader. That is the only setting that removes
  `System.Configuration` from a trimmed application.

## Default-provider factory

A public registration point in Abstractions lets anyone supply the function that builds the
**default provider**: the provider used for every authentication method that neither
`app.config` nor `SetProvider` has claimed. Applications, libraries and the generated Azure code
all use the same API. Reflective discovery is what runs when nobody registers anything.

### API

```csharp
// Abstractions
public abstract partial class SqlAuthenticationProvider
{
    // An explicit choice by an application or library. Replaces any earlier registration,
    // including a TrySet one; the last call wins.
    public static void SetDefaultProviderFactory(
        Func<SqlAuthenticationProviderFactoryContext, SqlAuthenticationProvider> factory);

    // A package default. Registers only if no factory has been registered; returns false otherwise.
    public static bool TrySetDefaultProviderFactory(
        Func<SqlAuthenticationProviderFactoryContext, SqlAuthenticationProvider> factory);
}

public sealed class SqlAuthenticationProviderFactoryContext
{
    // From <SqlClientAuthenticationProviders> in app.config; null when absent or when
    // Switch.Microsoft.Data.SqlClient.EnableAppConfig is off.
    public string? ApplicationClientId { get; }
    public bool? UseWamBroker { get; }
}

// Azure
public partial class ActiveDirectoryAuthenticationProvider
{
    // Builds the provider exactly as reflective discovery does today, with typed calls.
    public static SqlAuthenticationProvider CreateDefault(SqlAuthenticationProviderFactoryContext context);
}
```

`CreateDefault` reproduces today's `CreateAzureAuthenticationProvider` selection:

- the parameterless constructor when neither value is configured;
- the options constructor otherwise;
- `UseWamBrokerRequiresAzureExtensionUpgrade` no longer applies, because lockstep guarantees the
  options type exists.

The context class leaves room for future configuration values without breaking the delegate
signature.

### Precedence and ordering

The registry decides **at bootstrap** which registered factory to use, so precedence depends on
*how* something registered, not *when*:

| Source | Typically runs | Precedence |
|---|---|---|
| `app.config` providers and initializer | Bootstrap | Config tier — wins over every default |
| `SetProvider(method, provider)` | Any time | User tier — wins over every default |
| `SetDefaultProviderFactory` (app or library) | Any time | The highest default; the last call wins |
| `TrySetDefaultProviderFactory` (generated Azure code) | The application's module initializer, before `Main` | Used only when no explicit factory exists |
| Reflective discovery | Bootstrap, when no factory is registered and discovery is enabled | The lowest default |

A "first caller wins" rule would not work. The generated registration is a `[ModuleInitializer]`
in the application assembly, and the runtime runs it before any other code in that module,
including `Main`; NativeAOT runs module initializers at startup. Application code could never
register first. With `Set`/`TrySet`, an application or library overrides the generated Azure
default whichever runs first: a later `Set` replaces it, and a `TrySet` after an earlier `Set`
does nothing.

### Behaviour

- **At bootstrap,** the chosen factory is invoked once with the context. The provider it returns
  is registered in the Default tier for every fed-auth method its `IsSupported` accepts; Config
  and User registrations are untouched. If no factory is registered, reflective discovery runs as
  before. Construction happens at bootstrap, not at registration, so MSAL load timing does not
  change.
- **After bootstrap,** a `SetDefaultProviderFactory` call invokes the new factory immediately and
  replaces only Default-tier entries, with the usual `BeforeUnload`/`BeforeLoad` callbacks — the
  same "any time" behaviour as `SetProvider`. A late `TrySetDefaultProviderFactory` succeeds only
  if no factory has been registered, in which case it replaces the reflective-discovery result.
  So a library that touches the registry early cannot cost an AOT app its Azure provider.
- **Diagnostics:** each registration, replacement and refusal emits a trace event naming the
  factories involved (G2). A factory that throws, or returns a provider that supports no fed-auth
  method, is a bootstrap failure and is surfaced like any other (L2).
- Registration is thread-safe (an `Interlocked` exchange on the factory, and the registry's
  existing concurrency for the Default-tier swap).

### Applications without the generator

One line, lazy and config-aware, replaces today's per-method `SetProvider` loop:

```csharp
SqlAuthenticationProvider.SetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault);
```

A third-party provider can become the default the same way, without overriding explicit
application or configuration choices.

## Source generator

A Roslyn generator in the Azure package (`analyzers/dotnet/cs`) emits a module initializer into the
**application's** compilation when the consuming project targets `net10.0` or later (read via
`CompilerVisibleProperty`):

```csharp
// <auto-generated/>
[ModuleInitializer]
internal static void __SqlClientAzureDefaultProvider() =>
    SqlAuthenticationProvider.TrySetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault);
```

The application assembly is always rooted, so this direct reference keeps
`ActiveDirectoryAuthenticationProvider` alive through trimming. Because it uses `TrySet`, any
explicit `SetDefaultProviderFactory` call from the application or a library wins, and nothing is
constructed until the bootstrap runs. An MSBuild property opts out. .NET Framework never runs the
generator and keeps reflective discovery.

The generator uses only public API, so it needs no hidden member and no `InternalsVisibleTo`.

## Abstractions target frameworks

Abstractions targets `netstandard2.0;net10.0`:

- The `net10.0` asset is what .NET 10 applications consume. It is built against the real BCL
  attributes with `IsAotCompatible=true`, so the trim/AOT analyzer checks the shipped binary.
- The `netstandard2.0` asset serves .NET Framework consumers and third-party provider authors. It
  carries internal polyfills of `RequiresUnreferencedCode`, `RequiresDynamicCode`,
  `FeatureSwitchDefinition` and `FeatureGuard`. The trimmer matches these by name (measured), but
  the analyzer does not run for this TFM.
- Both assets take a compile-excluded dependency on `System.Configuration.ConfigurationManager`,
  versioned per `3rd-party-package-versions.instructions.md`.

## Mixed-version enforcement

Mixing family package versions is unsupported (R5). The design doesn't make mixed graphs work; it
makes them **fail loudly**. Enforcement matters because NuGet produces mixes silently from
routine actions, and the relocation turns a mix that is harmless today into a silent failure:

- **How mixes arise.** A single-package update (for example, Dependabot moving only Azure from
  7.1.1 to 7.1.2) raises Abstractions through Azure's version floor while SqlClient stays put,
  because nothing depends on SqlClient. A library's transitive dependency does the same. Across
  majors, SqlClient 7.0.0/7.0.1 accept any Abstractions `≥ 1.0.0`
  ([findings](findings.md#package-lockstep-is-a-release-policy-not-a-nuget-constraint)).
- **What breaks.** An **older SqlClient** keeps its own registry and its own discovery, so
  automatic Azure discovery still works. What breaks is every call into the **new Abstractions**:
  `GetProvider`, `SetProvider` and the default-provider factory. They write to and read from a
  registry the old SqlClient never consults, so `SetProvider` would succeed while the connection
  fails later with "no provider".

The dangerous direction is always an older SqlClient with a newer Abstractions. A newer SqlClient
cannot resolve against an older Abstractions, because SqlClient's version floor forbids it. So
whenever the dangerous mix exists, the newer Abstractions is in the process and carries both
checks.

Both checks compare the **exact family version**. That is the package version at build time, and
at runtime the assembly's `AssemblyInformationalVersionAttribute` without its `+commit` suffix
(e.g. `7.1.1` from `7.1.1+29ce2d85…`). `AssemblyName.Version` can't be used, because family
assemblies carry a major-only `AssemblyVersion` (`7.0.0.0` for every 7.x).

### Build time (E2)

Abstractions ships a `.targets` file in `build/` and `buildTransitive/`. Abstractions is in every
family graph, so the check reaches every consuming project.

- **`PackageReference` projects.** A target after `ResolvePackageAssets` compares the resolved
  versions of the family packages (`NuGetPackageId`/`NuGetPackageVersion` item metadata) with
  Abstractions' own, and fails the build naming each mismatched package and both versions.
- **`packages.config` projects** don't run `ResolvePackageAssets` and have no package metadata. A
  second branch reads the project's `packages.config` (or the version segment of each family
  `Reference`'s `HintPath`) and applies the same comparison.
- **The family list** is the packages that share `SqlClientPackageVersion`:
  `Microsoft.Data.SqlClient`, `…Extensions.Abstractions`, `…Extensions.Azure`,
  `…Internal.Logging` and `…AlwaysEncrypted.AzureKeyVaultProvider`. It excludes the separately
  versioned `Microsoft.SqlServer.Server`.
- **The error message** notes that the family packages shipped alongside SqlClient 7.0.0/7.0.1 were
  numbered 1.0.0. Otherwise "Azure 1.0.0" doesn't look like a family member that needs upgrading.
- **A documented property** opts out ([open question 7](README.md#risks-and-open-questions)).

### Runtime (E3)

E2 sees only what NuGet resolved for a `PackageReference` build. E3 checks the assemblies actually
loaded in the process. That covers direct DLL references, hand-copied or replaced files, opted-out
builds, and processes composed at runtime: plugin hosts, PowerShell modules, custom
`AssemblyLoadContext`s and the .NET Framework GAC.

- **At bootstrap,** Abstractions loads `Microsoft.Data.SqlClient` by name and compares its exact
  family version with its own. On a mismatch, the bootstrap fails with an
  `InvalidOperationException` naming both versions. Like any bootstrap failure (L2), it is
  rethrown from `GetProvider`, `SetProvider` and the default-provider factory — exactly the calls a
  mix breaks.
- **When discovery loads the Azure assembly,** it compares that assembly's family version too and
  fails the same way. This catches a current SqlClient paired with Azure 1.0.0, a combination in
  which SqlClient and Abstractions agree with each other.
- **If `Microsoft.Data.SqlClient` cannot be loaded,** the check is skipped. Abstractions is useful
  only alongside SqlClient, so this happens only in unusual hosts.
- **The check sits inside the trimming guard,** because it loads an assembly by name. Trimmed and
  NativeAOT images are fixed at publish time, which E2 has already checked.

## Latent-bug fixes

The bugs are described, with evidence, in [findings](findings.md#latent-bugs-on-main).

| Bug | Fix |
|---|---|
| L1 — `SqlAuthenticationInitializer` cannot register providers | Publish the registry before invoking the initializer; its registrations rank as Config |
| L2 — an invalid `app.config` provider silently disables the registry | Cache the bootstrap failure; rethrow the original exception from `GetProvider`, `SetProvider` and the fed-auth path |
| L3 — `SetProvider` returns `false` under NativeAOT | Delete the reflective bridge; the public API calls the registry directly |
| L4 — the bridge loads `Microsoft.Data.SqlClient` without verifying its signature | Delete the reflective bridge |
