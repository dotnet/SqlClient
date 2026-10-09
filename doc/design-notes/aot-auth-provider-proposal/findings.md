# Findings and constraints

> Part of the [AOT-safe authentication provider registration](README.md) proposal.
> **Audience:** reviewers checking the reasoning. **Read first:** [README](README.md).

The facts the design rests on: structural constraints, measured toolchain behaviour, package
history, and latent bugs found in today's code. Methods and raw results are in
[experiments.md](experiments.md).

## SqlClient → Azure reflection is unavoidable

Given R1, this follows structurally:

- Auto-magic discovery means SqlClient must use a type in an assembly it does **not** reference
  at compile time. A static reference would force the Azure package's dependency closure —
  `Azure.Core`, `Microsoft.Identity.Client`, `Microsoft.Identity.Client.Broker` and
  `Microsoft.Extensions.Caching.Memory` — onto every application. That is itself a change to
  existing apps (violating R1), and it would make the package non-optional.
- `Microsoft.Data.SqlClient.nuspec` declares dependencies on Abstractions and Logging, plus
  third-party packages. There is **no dependency edge from SqlClient to Azure**; the application
  references Azure directly.
- Reaching a type in an unreferenced, possibly-absent assembly has exactly one runtime
  mechanism: `Assembly.Load` + `Activator.CreateInstance`.
  ([primer](primer.md#reflection-apis-and-why-they-break-aot) explains these calls;
  [primer](primer.md#assemblies-visibility-and-type-forwarding) explains why the dependency
  direction cannot simply be reversed.)

The only non-reflection alternative is build-time injection (MSBuild targets or a source
generator). That cannot *replace* reflection, because it only works for applications that rebuild
against a cooperating package, and it cannot serve .NET Framework, where trimming never happens
anyway. It can provide a **fast path** — see B7.

## Package lockstep is a release policy, not a NuGet constraint

The family's published dependency ranges (read from the nuspecs on nuget.org):

| Published package | Depends on Abstractions / Logging |
|---|---|
| SqlClient 7.0.0, 7.0.1 | `1.0.0` — i.e. **≥ 1.0.0, no upper bound** |
| Azure 1.0.0 | `1.0.0` — no upper bound |
| SqlClient, Azure 7.0.2 → 7.1.1 | `[<own version>, 8.0.0)` |
| `main` (8.0 builds) | `[<own version>, 9.0.0)` via `$(SqlClientVersionCeiling)` |

NuGet picks the lowest version satisfying everyone, so:

- **Within a major, mixing is silent.** SqlClient 7.1.1 with Azure 7.1.2 resolves Abstractions to
  7.1.2 and satisfies both. No error, no NU1605, no warning. Nothing drags SqlClient forward,
  because SqlClient does not depend on Azure.
- **Across majors, the ceiling usually catches it** — SqlClient 7.1.1 `[7.1.1, 8.0.0)` against
  Azure 8.0 `[8.0.0, 9.0.0)` is a restore conflict — **except for SqlClient 7.0.0/7.0.1**, whose
  unbounded `≥ 1.0.0` accepts Abstractions 8.0.

**Mixes come from routine actions, not deliberate choices.** A single-package update (for example,
Dependabot moving only Azure) or a library's transitive dependency is enough. The policy says such
a graph is unsupported; nothing in NuGet stops it forming. Today a mix is harmless for
authentication, because the reflective bridge finds whichever SqlClient is loaded. After the
relocation it would fail silently, which is why the design enforces R5
([design](design.md#mixed-version-enforcement)).

**The exact version is visible at runtime, but not in `AssemblyName.Version`.** Family assemblies
carry a major-only `AssemblyVersion`: the 7.1.1 Azure assembly loads as `Version=7.0.0.0`
(observed in the discovery trace). The exact family version is in the assembly attributes,
checked on the published 7.0.1 and 7.1.1 packages:

| Attribute | SqlClient 7.1.1 |
|---|---|
| `AssemblyVersion` | `7.0.0.0` |
| `AssemblyFileVersion` | `7.1.1.26272` |
| `AssemblyInformationalVersion` | `7.1.1+29ce2d856cb5a9450f79ea62c0afc744fd337fde` |

So a runtime check (E3) can compare exact family versions, including within one major, by reading
`AssemblyInformationalVersionAttribute`.

## Measured trimmer and ILC behaviour (.NET 10)

The choice of gating shape was determined experimentally, with **real** `PublishTrimmed` and
`PublishAot` builds of a `net10.0` app (ILLink/ILC 10.0.12). The trimmer that runs is the one
matching the **application's** target framework. Every SqlClient 8.0 consumer on modern .NET
targets `net10.0` or later, so it uses this toolchain or a newer one. Method: [A.2](experiments.md#a2-gating-shapes-under-trimming-and-nativeaot);
terminology: [primer](primer.md#feature-switches-and-feature-guards).

"Default" means the application configures nothing. "Switch off" means the application sets the
switch to `false` with `RuntimeHostConfigurationOption … Trim="true"`. PRESENT means the guarded
reflective method survived into the output; TRIMMED means it was removed.

| Gating shape | Library analyzer (`net10.0` build) | Trimmed, default | AOT, default | Switch off |
|---|---|---|---|---|
| **A** — `[FeatureSwitchDefinition]` switch, default `true` | IL2026 + IL3050 at the call site | PRESENT | PRESENT | TRIMMED |
| **B** — switch property **+ separate** guard property with `[FeatureGuard(RUC)]`/`[FeatureGuard(RDC)]` | IL4000 on the guard; **call site clean** | **TRIMMED** | **TRIMMED** | TRIMMED |
| **C** — both attributes on **one** property | IL4000; call site clean | PRESENT | PRESENT | TRIMMED |
| **D** — `[FeatureGuard(RDC)]` returning `RuntimeFeature.IsDynamicCodeSupported` | no IL4000; IL2026 at the call site | PRESENT | TRIMMED | (no switch) |

Each shape was measured twice: from a `netstandard2.0` library using **internal polyfill**
attributes (Abstractions today), and from the `net10.0` asset of a `netstandard2.0;net10.0`
library using the real BCL attributes (C6). **Both gave identical results.**

Conclusions:

1. **Shape B is removed from every .NET 10 trimmed or AOT publish with zero application
   configuration.** ILLink and ILC treat a `RequiresUnreferencedCode` feature guard as `false`
   whenever trimming is enabled. This is what eliminates the silent-failure mode.
2. Shape A works only when the application knows about the switch, and it is not analyzer-clean.
3. The attributes must be on **separate** properties for automatic removal; shape C does not
   trim by default, but does trim with an explicit publish-time `false` switch value.
4. Shape D trims under AOT only, guards only `RequiresDynamicCode`, and leaves IL2026.
5. IL4000 is unavoidable for a `RequiresUnreferencedCode` guard backed by `AppContext`, so each
   guard property needs one justified suppression.
6. With `[FeatureSwitchDefinition]`, the .NET 10 toolchain honours the switch without any
   `ILLink.Substitutions.xml`. That is **not** true for .NET 8 (see
   [Backporting](backporting.md)).

### Combined-property regression experiment (2026-10-09)

Rechecked the actual Abstractions implementation and its existing `PublishTest` fixture on
Linux x64, SDK 10.0.401, ILLink/ILC 10.0.12. Both switch getters retained their lazy caches;
the experiment moved both `FeatureGuard` attributes onto each `FeatureSwitchDefinition`
property, removed the forwarding guards, and changed registry checks to use the combined getters.

| Configuration | Observed result |
|---|---|
| Combined getters without the existing IL4000 suppressions | Strict library build failed with four IL4000 errors, one per capability per getter |
| Combined getters with the existing justified suppressions | All 38 .NET 10 Abstractions tests passed with warnings treated as errors; trim and AOT analyzers were enabled |
| Combined getters, default trimmed publish | Failed with IL2026 at both registry bootstrap calls; configuration reflection remained reachable |
| Combined getters, default NativeAOT publish | Failed with IL2026 and IL3050 at both bootstrap calls |
| Combined getters, trimmed publish with both switches explicitly `false` | Publish and executable passed; decompilation showed all three bootstrap guard branches reduced to `if (false)` |
| Restored separate getters, default trimmed and NativeAOT publishes | Both publishes and executables passed without custom switch values; trimmed decompilation showed all three bootstrap guard branches reduced to `if (false)` |

Default publish checks used `-c Release -r linux-x64 -p:PublishMode=trim` or
`-p:PublishMode=aot`, with `-p:TreatWarningsAsErrors=true` and no explicit custom feature values.
The trimmed control additionally supplied:

```text
-p:_ExtraTrimmerArgs=--feature Switch.Microsoft.Data.SqlClient.EnableAppConfig false --feature Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery false
```

The experiment establishes that combining the attributes is supported with explicit switch
values, but does not preserve this package's automatic removal contract. The separate-property
implementation was restored; the caches were retained.

## `netstandard2.0`: polyfills work, the analyzer does not

- **ILLink and ILC honour internal polyfills.** They match `RequiresUnreferencedCode`,
  `RequiresDynamicCode`, `FeatureSwitchDefinition` and `FeatureGuard` by full type name (table
  above). `main` already relies on this: the `netstandard2.0` Logging package ships an internal
  `UnconditionalSuppressMessageAttribute` (#4703).
- **The Roslyn trim/AOT analyzer does not run for `netstandard2.0`.** Enabling it produces
  `NETSDK1210 … not supported for the target framework` and no IL diagnostics. A
  `netstandard2.0`-only Abstractions gets **no build-time feedback** on its own annotations.
- **A `netstandard2.0;net10.0` build gets full analyzer coverage** on its `net10.0` asset
  (IL2026/IL3050 at unguarded call sites, IL4000 on guards). That asset is the one .NET 10 apps
  consume. This is why C6 multi-targets Abstractions.

See [A.5](experiments.md#a5-netstandard20-polyfill-and-analyzer-experiment).

## Latent bugs on `main`

Found while measuring `main`'s behaviour; all four are fixed by this design.

**L1 — `SqlAuthenticationInitializer` cannot register providers.** The instance constructor runs the
configured `initializerType` **before** the static constructor assigns `Instance`. An initializer
that calls `SqlAuthenticationProvider.SetProvider(...)` — the reason initializers exist — hits a
null `Instance` inside the reflected call. The bridge catches the `TargetInvocationException`,
logs `SetProvider() invocation failed`, and returns `false`. The Azure default then fills that
method. Measured on .NET 10 ([A.4](experiments.md#a4-latent-bugs)). Fix: publish the registry before invoking
the initializer, and rank its registrations in the Config tier.

**L2 — An invalid `app.config` provider silently disables the whole registry.** A provider `type`
that cannot be loaded makes the static constructor throw. Through the public API, the bridge
swallows it: every subsequent `GetProvider` returns `null` and every `SetProvider` returns `false`
for the life of the process, with only a trace to show why (measured). On the fed-auth path the
same failure surfaces as a `TypeInitializationException` (code reading). Fix: cache the failure
and log its details with corrective guidance on each unsuccessful public registry access (G2),
preserving the existing `null`/`false` results rather than introducing new public exceptions.

**L3 — `SetProvider` returns `false` under NativeAOT** ([Problem](README.md#measured)). Fixed by deleting
the bridge.

**L4 — The bridge loads `Microsoft.Data.SqlClient` by name without verifying its signature**, as
its own TODO notes. Fixed by deleting the bridge.

## Related work on `main`

| Item | State | Relevance |
|---|---|---|
| [#4697](https://github.com/dotnet/SqlClient/pull/4697) `Switch.Microsoft.Data.SqlClient.EnableAppConfig` | Open, changes requested | Gates all three `app.config` readers in SqlClient behind a default-`true` switch with a substitution. A reviewer endorsed the `Enable*`/default-`true` naming; a maintainer recommended moving to `Microsoft.Extensions.Configuration`. Measured there: `System.Configuration` carries five fixed-cost trim warnings that any ungated caller keeps |
| [#4684](https://github.com/dotnet/SqlClient/pull/4684) small trim fixes | Open | Library analyzer count 50 → 46 with `-p:IsAotCompatible=true`. SqlClient does not yet enable `IsAotCompatible` |
| #4683, #4688, #4703 | Merged | JSON source generation, DiagnosticSource suppression, and the `netstandard2.0` polyfill precedent in Logging |
| [#1947](https://github.com/dotnet/SqlClient/issues/1947) "NativeAOT: zero warnings" | Open | Umbrella this work contributes to |
| #4195, #4348, #4670, #4573 | Closed, unmerged | Earlier attempts — callback bridge (A1), public manager (A2), registry relocation (A3), lazy-bootstrap feature switch (C1) |
