# Experiments

> Part of the [AOT-safe authentication provider registration](README.md) proposal.
> **Audience:** anyone auditing the measurements. The harness is in [experiments/](experiments/);
> see [Reproducing](#reproducing).

**Environment.** Linux x64 (Debian), .NET SDK 10.0.401. The primary measurements use `net10.0`
apps with ILLink/ILC **10.0.12**. The backport measurements use `net9.0` (9.0.20) and `net8.0`
(8.0.31) apps. NativeAOT linked with gcc 14.2 (`CppCompilerAndLinker=gcc`, `StripSymbols=false`),
because clang is not installed. **Every AOT row is a real `PublishAot`.** All publishes were
`-r linux-x64`, self-contained, Release, with `bin`/`obj` deleted between configurations so
ILLink/ILC output is never reused.

## A.1 Reproduction of #4193

**Subject.** NuGet packages `Microsoft.Data.SqlClient` 7.1.1 and
`Microsoft.Data.SqlClient.Extensions.Azure` 7.1.1 — signed, so the `STRONG_NAME_SIGNING`
public-key-token path is exercised. A `net10.0` app consumes SqlClient's `net9.0` asset. Between
`v7.1.1` and `main`, the manager and the bridge are byte-identical.

**App.**

```csharp
using var conn = new SqlConnection("Server=localhost;Authentication=Active Directory Default");
if (args.Length > 0) conn.Open();               // statically reachable, never executed
var p = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault);
bool ok = SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, new MyProvider());
```

An `EventListener` on `Microsoft.Data.SqlClient.EventSource` (with `EventSourceSupport=true`)
captured the manager's and bridge's trace messages.

**Results (`net10.0`).**

| Publish | `GetProvider(AD Default)` | `SetProvider(user)` | Key trace |
|---|---|---|---|
| JIT | Azure provider | `true` | `Azure extension assembly=…, Version=7.0.0.0, …, PublicKeyToken=23ec7fc2d6eaa4a5 found` |
| `PublishTrimmed` | `null` | `true` | `ConfigurationManager failed to load … Configuration system failed to initialize`; `Azure extension … not found or not usable … FileNotFoundException` |
| `PublishAot` | `null` | **`false`** | `MDS GetProvider() method not found`; `MDS SetProvider() method not found` |
| `PublishTrimmed` + `<TrimmerRootAssembly Include="Microsoft.Data.SqlClient.Extensions.Azure" />` | Azure provider | `true` | discovery succeeds |

**Warnings.** With the default `TrimmerSingleWarn=true`: trimmed → IL2104 for
`Microsoft.Data.SqlClient`, `…Extensions.Abstractions`, `…Internal.Logging` and
`System.Configuration.ConfigurationManager`; AOT → the same plus IL3053 for
`Microsoft.Data.SqlClient`. With `TrimmerSingleWarn=false`: trimmed → 16× IL2026, 6× IL2057,
4× IL2067, 6× IL2070, 3× IL2072, 2× IL2075; AOT → 16× IL2026, 7× IL2057, 4× IL2067, 6× IL2070,
4× IL2072, 4× IL2075, 1× IL3050. The 15 auth-specific warnings, identical in both modes:

| Method | Warnings |
|---|---|
| `SqlAuthenticationProvider.Internal..cctor()` | 1× IL2026 (`Assembly.GetType`), 2× IL2075 (`Type.GetMethod`) |
| `SqlAuthenticationProviderManager..cctor()` | 2× IL2026 (`Assembly.GetType`) |
| `SqlAuthenticationProviderManager..ctor(…ConfigurationSection)` | 2× IL2057 (`Type.GetType(string, bool)`) |
| `SqlAuthenticationProviderManager.CreateAzureAuthenticationProvider(…)` | 3× IL2067 (`Activator.CreateInstance`), 5× IL2070 (`GetConstructor`, `GetProperty`) |

The `net9.0`/`net8.0` results are in [Backporting](backporting.md#measured-net-8-and-net-9-toolchains).

## A.2 Gating shapes under trimming and NativeAOT

**Libraries.**

- `PolyLib` — `netstandard2.0` with **internal polyfills** of `RequiresUnreferencedCodeAttribute`,
  `RequiresDynamicCodeAttribute`, `FeatureSwitchDefinitionAttribute` and `FeatureGuardAttribute`,
  plus an embedded `ILLink.Substitutions.xml` stubbing each switch to `false` when the app sets it
  `false`. Shapes A, B, C and E (E adds a guard stub keyed on
  `System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported=false`).
- `MultiLib` — `netstandard2.0;net10.0`, polyfills compiled into the `netstandard2.0` build only,
  real BCL attributes and `IsAotCompatible=true` on `net10.0`, **no** substitution file. Shapes A,
  B, C, and D (`net10.0` only).

Each shape guards its own `[MethodImpl(NoInlining)]` method annotated
`[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`, which performs `Assembly.Load` +
`Activator.CreateInstance` and returns a unique marker string.

**Apps.** `net10.0`, `net9.0` and `net8.0`, each referencing both libraries. `TrimMode=full`,
`TrimmerSingleWarn=false`. Each was published `PublishTrimmed` and `PublishAot`, with and without
`RuntimeHostConfigurationOption … Value="false" Trim="true"` for every switch: 12 publishes.

**Detection.**

- Trimmed: the marker literal searched as UTF-16 across the publish folder. ILLink folded
  switched-off calls to constants and sometimes dropped a library entirely; absence counts as
  TRIMMED.
- AOT: the per-shape method symbol searched with `nm` in the unstripped native binary, since
  NativeAOT does not store these literals as UTF-16.
- The runtime output of every binary confirmed which branch executed.

**Results (`net10.0`, ILLink/ILC 10.0.12).**

| Mode | Switches | PolyLib A / B / C / E | MultiLib (`net10.0` asset) A / B / C / D | Publish warnings |
|---|---|---|---|---|
| trimmed | default | P / **T** / P / **T** | P / **T** / P / P | 7× IL2026, 1× IL3050, 4× IL4000 |
| trimmed | off | T / T / T / T | T / T / T / P | 3× IL2026, 1× IL3050, 4× IL4000 |
| AOT | default | P / **T** / P / **T** | P / **T** / P / T | 6× IL2026, 5× IL3050, 4× IL4000 |
| AOT | off | T / T / T / T | T / T / T / T | 2× IL2026, 1× IL3050, 4× IL4000 |

P = PRESENT, T = TRIMMED. Shape D has no switch, so its "off" result equals its default.
`MultiLib` honoured its switches without a substitution file. The four IL4000 warnings come from
`MultiLib`'s `net10.0` guards (shapes B and C).

The `net9.0`/`net8.0` results are in [Backporting](backporting.md#measured-net-8-and-net-9-toolchains). The ILC
9.0.20 crash noted there occurred in this harness.

## A.3 What the section handler does

**Method.** The A.1 app (JIT, `net10.0`, SqlClient 7.1.1) was extended with a probe that reads the
section exactly as Abstractions would — no reference to SqlClient's internal handler type:

```csharp
var sec = ConfigurationManager.GetSection("SqlClientAuthenticationProviders") as ConfigurationSection;
var props = sec!.ElementInformation.Properties;
var providers = (ProviderSettingsCollection)props["providers"]!.Value;
// plus props["applicationClientId"], props["useWamBroker"], props["initializerType"]
```

Each variant below was placed in `app.dll.config`.

| # | Variant | Probe result | `main`'s behaviour |
|---|---|---|---|
| 1 | Valid: `applicationClientId`, `useWamBroker="false"`, provider `MyProvider, app` for AD Interactive | `handler=…SqlClientAuthenticationProviderConfigurationSection; clientId='1111…'; useWamBroker='false'; providers=[active directory interactive=>MyProvider, app]` | Config provider installed; app's `SetProvider` for that method returns `false`; Azure default refused for it |
| 2 | `initializerType="MyInit, app"` | `initializerType='MyInit, app'` | See [A.4](#a4-latent-bugs) |
| 3 | Provider `type="No.Such.Provider, app"` | Read succeeds; the type is not resolved at parse time | See [A.4](#a4-latent-bugs) |
| 4 | `<section … type="No.Such.Section, No.Such.Assembly">` | `ConfigurationErrorsException: An error occurred creating the configuration section handler … Could not load file or assembly 'No.Such.Assembly'` | Logged and ignored |
| 5 | Real handler, extra attribute `notAnAttribute="y"` | `ConfigurationErrorsException: Unrecognized attribute 'notAnAttribute'` | Logged and ignored |
| 6 | Section present but not declared in `<configSections>` | `ConfigurationErrorsException: Configuration system failed to initialize` | Logged and ignored |

Variants 4–6 show that the handler type is what loads, validates and parses the XML. Variant 1
shows that its output is fully readable without referencing it.

**Forwarding check.** An `internal` type forwarded with
`[assembly: TypeForwardedTo(typeof(Other.InternalType))]` fails with
`CS0122: '…' is inaccessible due to its protection level` (`net10.0`).

**Not verified:** the same untyped read on .NET Framework (Linux host).

## A.4 Latent bugs

**L1.** Variant 2 of A.3, where `MyInit.Initialize()` calls
`SqlAuthenticationProvider.SetProvider(ActiveDirectoryDeviceCodeFlow, new MyProvider())`.
Result: `SetProvider` returned `false` and no exception reached the initializer. Afterwards
`GetProvider(ActiveDirectoryDeviceCodeFlow)` returned `ActiveDirectoryAuthenticationProvider`.
Trace: `SqlAuthenticationProvider.Internal | SetProvider() invocation failed: TargetInvocationException`.

**L2.** Variant 3 of A.3. Result: `GetProvider(ActiveDirectoryDefault)` returned `null` (the Azure
default was never installed), and the app's `SetProvider` returned `false`. Every call traced
`GetProvider()/SetProvider() invocation failed: TargetInvocationException`, and the state persisted
for the life of the process.

## A.5 `netstandard2.0` polyfill and analyzer experiment

**Question.** `[FeatureSwitchDefinition]` and `[FeatureGuard]` are .NET 9+ types, and Abstractions
targets `netstandard2.0`. Can the gating live there, and does the library get build-time
diagnostics?

**Trimming and ILC.** `PolyLib` in [A.2](#a2-gating-shapes-under-trimming-and-nativeaot) is exactly
this case. On `net10.0` apps its results matched the `net10.0` asset built with BCL attributes, shape
for shape, under both ILLink and ILC. Both tools resolve these attributes **by full type name**, so
internal polyfills are honoured. `main` already relies on this for
`UnconditionalSuppressMessageAttribute` in Logging (#4703).

**Analyzer.** Building `PolyLib` with `EnableTrimAnalyzer=true` and `EnableAotAnalyzer=true`
produced only:

```
warning NETSDK1210: IsAotCompatible and EnableAotAnalyzer are not supported for the target
framework. Consider multi-targeting to a supported framework ...
```

No IL2026/IL3050/IL4000 diagnostics. Building `MultiLib` (`netstandard2.0;net10.0`) produced, for
its `net10.0` build: IL2026 + IL3050 at shape A's call site, IL4000 ×2 on shape B's guard and on
shape C's property with clean call sites, and IL2026 at shape D's call site.

**Conclusion.** A `netstandard2.0`-only library can host the switches, guards and annotated
reflective code, but its annotations are checked only when an application publishes. Adding a
`net10.0` TFM (C6b) puts the analyzer on the asset .NET 10 apps actually consume.

---

## Reproducing

The harness sources are in [experiments/](experiments/). Each script copies its harness to a work
directory **outside the repository** (default `${TMPDIR:-/tmp}/aot-auth-experiments`; override
with `AOT_EXPERIMENTS_WORK`) and builds there, so no build output lands in the repository. The
copies use your own NuGet configuration.

`experiments/` also contains a `Directory.Packages.props` that turns off the repository's central
package management, and a `NuGet.config` that restores every package from the repository's
governed feed instead of the local `packages/` feed. With these, an IDE can load the projects in
place; the scripts do not depend on them.

**Prerequisites:** Linux x64; .NET SDK 10.0.4xx with the .NET 8 and .NET 9 runtimes for the
backport rows; gcc or clang plus `zlib1g-dev` for NativeAOT; `nm` and `python3`; access to a
NuGet feed carrying the published SqlClient 7.1.1 packages.

| Script | Measures | Section |
|---|---|---|
| `baseline/run-matrix.sh [tfm…]` | #4193 reproduction: JIT, trimmed, AOT, with and without `TrimmerSingleWarn`, and with `TrimmerRootAssembly` | [A.1](#a1-reproduction-of-4193); net9.0/net8.0 in [backporting](backporting.md#measured-net-8-and-net-9-toolchains) |
| `baseline/run.sh <tfm> <jit\|trim\|aot> <true\|false> [msbuild args]` | One cell of the above | A.1 |
| `baseline/run-config-variants.sh` | The `app.config` variants in `baseline/cfgs/` against SqlClient's real section handler | [A.3](#a3-what-the-section-handler-does), [A.4](#a4-latent-bugs) |
| `gating/run-matrix.sh [10\|9\|8 …]` | Gating shapes A–E, trimmed and AOT, switches default/off | [A.2](#a2-gating-shapes-under-trimming-and-nativeaot) |
| `gating/run.sh <10\|9\|8> <trim\|aot> <0\|1>` | One cell of the above | A.2 |
| `gating/analyzer.sh` | Library analyzer output for `PolyLib` and `MultiLib` | A.5 |
| `forwarding/run.sh` | Untyped section read across assemblies, and the CS0122 forwarding check | A.3 |

The apps link NativeAOT output with gcc (`CppCompilerAndLinker=gcc`). Remove that property where
clang is available.

`experiments/results/` holds the raw output of the runs reported here. Paths in it refer to the
original work directory.
