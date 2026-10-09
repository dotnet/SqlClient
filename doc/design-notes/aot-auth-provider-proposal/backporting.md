# Backporting to 7.1 and 7.0

> Part of the [AOT-safe authentication provider registration](README.md) proposal.
> **Audience:** release and servicing owners. **Read first:** [README](README.md), then
> [design.md](design.md).

The authentication code on `release/7.1` (7.1.1) and `release/7.0` (7.0.3) is **identical** to
`main`: `SqlAuthenticationProviderManager.cs`, `SqlAuthenticationProvider.Internal.cs` and the Azure
options type do not differ (verified by diff). Both phases therefore apply mechanically,
including the new default-provider factory API (the generator is optional; see BP6). What
differs is the surroundings: older toolchains, a different package history, and servicing rules
that forbid breaking anything that works today.

## What is different on the release branches

| | `release/7.1` | `release/7.0` |
|---|---|---|
| SqlClient TFMs | `net462;net8.0;net9.0` (+ `netstandard2.0` PlatformNotSupported asset) | `net8.0;net9.0` + `net462` on Windows builds; OS-specific builds (`runtimes/unix`, `runtimes/win`) |
| Apps that consume it | `net462`, **.NET 8, .NET 9 and .NET 10** (via the `net9.0` asset) | same |
| Abstractions / Logging | `netstandard2.0` | `netstandard2.0`; versioned through `AbstractionsVersions.props` |
| Azure package | `net462;netstandard2.0`, depends on `Azure.Identity` | same |
| Trim polyfills in the family | none | none |
| SqlClient packaging | `src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.nuspec` | `tools/specs/Microsoft.Data.SqlClient.nuspec` |
| Published family history | 7.1.0, 7.1.1 — all bounded `[x, 8.0.0)` | **7.0.0/7.0.1 shipped with family 1.0.0, unbounded `≥ 1.0.0`**; 7.0.2, 7.0.3 bounded |
| Azure versions without the options type | — | **Azure 1.0.0** (verified from the published package) |

.NET 8 and .NET 9 both leave support on 2026-11-10, but 7.x consumers on those runtimes remain in
scope for any 7.x patch.

## Measured: .NET 8 and .NET 9 toolchains

The same harnesses as [A.1](experiments.md#a1-reproduction-of-4193) and
[A.2](experiments.md#a2-gating-shapes-under-trimming-and-nativeaot), with `net9.0` apps (ILLink/ILC 9.0.20) and `net8.0`
apps (ILLink/ILC 8.0.31).

**Reproduction of #4193 (7.1.1 packages).** Behaviour is identical to .NET 10: trimmed →
`GetProvider` `null`, `SetProvider` `true`; AOT → `null`/**`false`** (`MDS SetProvider() method not
found`); trimmed + `TrimmerRootAssembly` → Azure provider found. Warnings:

| App | Trimmed, default | AOT, default | Auth-specific (with `TrimmerSingleWarn=false`) |
|---|---|---|---|
| `net10.0` | 4× IL2104 | 4× IL2104 + IL3053 | 15 |
| `net9.0` | 4× IL2104 | 4× IL2104 + IL3053 | 15 |
| `net8.0` | 5× IL2104 (adds `System.Text.Json`) | 5× IL2104 + IL3053 | 15 |

**Gating shapes.** "Polyfill + XML" is a `netstandard2.0` library with internal polyfill attributes
*and* an `ILLink.Substitutions.xml`. "Attributes only" is the `netstandard2.0` asset of the
multi-targeted library — polyfill attributes, no substitution file. Shape **E** is shape B plus a
substitution entry that stubs the guard to `false` when
`System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported` is `false`.

| App | Library | Mode | A | B | C | E | Switch off |
|---|---|---|---|---|---|---|---|
| `net9.0` | Polyfill + XML | trimmed | P | **T** | P | **T** | all T |
| `net9.0` | Polyfill + XML | AOT | P | **T** | P | **T** | all T |
| `net9.0` | Attributes only | trimmed / AOT | P | **T** | P | — | all T |
| `net8.0` | Polyfill + XML | trimmed | P | P | P | P | all T |
| `net8.0` | Polyfill + XML | AOT | P | P | P | **T** | all T |
| `net8.0` | Attributes only | trimmed / AOT | P | P | P | — | **A, B, C still P** |

P = PRESENT, T = TRIMMED. Publish warnings for the guarded call sites: `net9.0` 4× IL2026 trimmed,
4× IL2026 + 4× IL3050 AOT; `net8.0` 7× IL2026 trimmed, 6× IL2026 + 6× IL3050 AOT.

What this shows:

1. **.NET 9 behaves like .NET 10.** `[FeatureGuard]` and `[FeatureSwitchDefinition]` are honoured,
   including from polyfills, with or without a substitution file.
2. **.NET 8 ignores both attributes.** The guard does nothing, and even an explicit switch-off is
   honoured at runtime but **not trimmed** unless an `ILLink.Substitutions.xml` entry exists.
3. **Shape E closes the .NET 8 AOT gap with no app configuration.** ILC sets
   `IsDynamicCodeSupported=false` for every AOT publish, so the substitution fires.
4. **.NET 8 `PublishTrimmed` (non-AOT) has no automatic signal.** Those apps must set the
   switches. They have no Azure provider and no `app.config` today either (measured), so this is
   not a regression.

**Toolchain note.** One run of ILC 9.0.20 (`net9.0` app, AOT, switches off, shape E entry present)
crashed with `IL1013: Error processing 'name'`: a `NullReferenceException` in ILC's linker-XML
processing on a parallel scanner thread. It occurred once in eight identical-configuration runs,
never on ILC 8.0.31 or 10.0.12. Treat it as an intermittent toolchain race to watch in CI.

## Additional changes for the backport

**BP1 — Abstractions stays `netstandard2.0` (C6a).** Adding a TFM in servicing is avoidable risk.
Use internal polyfills, plus a CI-only `net8.0`/`net9.0` analysis build, because the analyzer does
not run for `netstandard2.0` (NETSDK1210).

**BP2 — Ship `ILLink.Substitutions.xml` entries for both switches.** Required for .NET 8, which
ignores `[FeatureSwitchDefinition]` (measured above).

**BP3 — Add the `IsDynamicCodeSupported`-keyed substitution on each guard (C5).** One XML line per
guard covers .NET 8 AOT. Document that .NET 8 `PublishTrimmed` apps must set the switches:

```xml
<method signature="System.Boolean get_IsAzureExtensionDiscoverySupported()" body="stub" value="false"
        feature="System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported" featurevalue="false" />
```

**BP4 — Mixed versions are reported, not supported.** Mixing is unsupported (R5), so the backport
enforces the policy exactly as 8.0 does rather than keeping old pairings working. In servicing,
the mix is a *likely* outcome: updating only the Azure package to a patched 7.1.x pulls the patched
Abstractions alongside SqlClient 7.1.1, because all of 7.0.0–7.1.1 accept a newer 7.x
Abstractions. So:

- **E2 is a build error,** with the documented opt-out property, as in 8.0. It doesn't break a
  build that works today; it fires when an update introduces the mix. The error names each
  mismatched package and explains that the packages shipped with SqlClient 7.0.0/7.0.1 were
  numbered 1.0.0.
- **E3 compares exact family versions** (`AssemblyInformationalVersionAttribute`), so it detects
  same-major mixes. With an older SqlClient, the old SqlClient's own Azure discovery keeps
  working, but `GetProvider`/`SetProvider` return `null`/`false` and emit descriptive diagnostics
  instead of silently using a registry the old SqlClient never reads.
- **Azure 1.0.0 with a patched SqlClient** fails at discovery with descriptive diagnostics (E3)
  instead of falling back to the legacy `(string)` constructor. The version-tolerant probing is
  removed, as in 8.0.
- **`SqlAuthenticationInitializer` relocates behind a type forward,** as in 8.0. A compilation
  that sees both an older SqlClient and the patched Abstractions fails with CS0433, but that
  graph is already reported by E2.

These checks reject an unsupported configuration while preserving the public API's existing
failure results. Release notes must call out the enforcement together with the upgrade
instruction: update every family package together.

**BP5 — Scope the latent-bug fixes for servicing.**

| Bug | Servicing |
|---|---|
| L1 initializer cannot register | Fix — calls that failed now succeed |
| L2 invalid `app.config` provider disables the registry | Preserve public `GetProvider`/`SetProvider` return values, as in 8.0. Cache the bootstrap failure and emit actionable Trace-keyword diagnostics on each unsuccessful registry access. |
| L3 AOT `SetProvider` returns `false` | Fix |
| L4 bridge loads unverified SqlClient | Fix — the bridge is deleted |

**BP6 — Ship the default-provider factory; the generator is optional.** New public API is
acceptable in a patch, so `SetDefaultProviderFactory`, `TrySetDefaultProviderFactory`,
`SqlAuthenticationProviderFactoryContext` and `ActiveDirectoryAuthenticationProvider.CreateDefault`
backport as designed. The patched Azure package's floor requires the patched Abstractions, so the API
is always present when `CreateDefault` is. AOT and trimmed apps on 7.x then need one line:

```csharp
SqlAuthenticationProvider.SetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault);
```

The generator is the only new *packaging* surface (an analyzer payload in the Azure package).
Release owners can include it; if they do, it emits for `net8.0+` consumers, since 7.x serves
.NET 8 and .NET 9 apps. On .NET 8 AOT it relies on BP3 to remove discovery, and it roots the
provider as on .NET 10.

**BP7 — New dependency and resources in a patch.** Abstractions acquires
`System.Configuration.ConfigurationManager` and a `Strings.resx` whose
translations are copied from SqlClient. Both release branches run the localization pipeline
(`release/7.0`'s head is a OneLocBuild commit). The dependency's version follows the branch's
existing SqlClient choice for the floor LTS (8.x).
If the public section-handler relocation is backported, include its type forwards and allow
configuration dependency compile assets to flow to consumers of those public APIs.

**BP8 — 7.0 specifics.**

- The SqlClient-side edits (delete the manager, change `GetFedAuthToken`, add the
  `SqlAuthenticationInitializer` type forward) must land in both OS-specific builds. `net462`
  builds only on Windows.
- The nuspec lives under `tools/specs/`.
- Abstractions' version comes from `AbstractionsVersions.props`. Confirm the floor arithmetic
  still makes a patched SqlClient require the patched Abstractions.

## Resulting behaviour on 7.x after the backport

| Scenario | Provider source | Reflection |
|---|---|---|
| `net10.0`/`net9.0` app, AOT or trimmed | Generated `TrySetDefaultProviderFactory` if the generator ships; otherwise the one-line `SetDefaultProviderFactory` | Removed by the feature guard |
| `net8.0` app, AOT | As above | Removed by the `IsDynamicCodeSupported` substitution (BP3) |
| `net8.0` app, trimmed (non-AOT) | As above | Present unless the app sets the switches |
| Any JIT app, `net462` | Reflective discovery | Used — behaviour unchanged |
| `app.config` providers, JIT | Config parsing | Unchanged |
| Older 7.x SqlClient + patched Abstractions | Build error (E2, unless opted out). At runtime the old SqlClient's own discovery still works; `GetProvider`/`SetProvider`/the default-provider factory throw (E3) | As today in the old SqlClient |
| Patched SqlClient + Azure 1.0.0 | Build error (E2, unless opted out); descriptive exception at discovery (E3) | — |

## Backport test matrix

- `net8.0`, `net9.0`, `net10.0` apps × trimmed/AOT × switches default/off: behaviour and absence
  of the guarded methods.
- `net462` JIT: `app.config` providers, initializer (L1), precedence.
- Default-provider factory ordering, as on `main`.
- Mixed graphs (BP4), each with `PackageReference` and `packages.config`, and with the opt-out
  property set (E3 must still fire):
  - SqlClient 7.1.1 + patched Abstractions (via an Azure-only update): E2 error; with the opt-out,
    E3 exception from `SetProvider`, and the old SqlClient's discovery still works;
  - SqlClient 7.0.0 + patched 7.0.x Abstractions (the unbounded range);
  - patched SqlClient + Azure 1.0.0: E2 error naming the 1.0.0 numbering; E3 at discovery.
- An app that references `SqlAuthenticationInitializer`, built against the patched packages
  (resolves through the type forward).
