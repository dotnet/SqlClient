# AOT-safe authentication provider registration

| | |
|---|---|
| **Issue** | [#4193](https://github.com/dotnet/SqlClient/issues/4193) — Entra ID authentication broken under NativeAOT |
| **Target** | 8.0.0 (`net462`, `net10.0`); backports to 7.1 and 7.0 |
| **Status** | Proposal |
| **Code** | `SqlAuthenticationProviderManager` (SqlClient), `SqlAuthenticationProvider` (Abstractions), `ActiveDirectoryAuthenticationProvider` (Azure) |

This is the entry point. It explains the problem, the chosen solution and how it works, in enough
detail to evaluate it. Supporting detail lives in the [companion documents](#documents).

## Summary

Entra ID authentication is broken under NativeAOT in SqlClient v7.x, and the failure is
effectively silent. In trimmed and AOT publishes the Azure provider is never found. Under
NativeAOT, applications cannot even register a provider themselves: the public `SetProvider`
silently returns `false`.

**Decision: move the authentication pipeline into the Abstractions package — the provider
registry, Azure extension discovery, `app.config` parsing and `SqlAuthenticationInitializer` —
then add guarded reflection, mixed-version enforcement, a public default-provider factory and a
generated fast path.** Delivered in two phases, the design:

- keeps every existing non-AOT behaviour, and fixes four latent bugs found along the way;
- removes the Abstractions → SqlClient reflection structurally;
- makes the remaining SqlClient → Azure reflection disappear automatically from every trimmed
  and AOT publish, with no application configuration;
- makes the existing public `SqlAuthenticationProvider.SetProvider` work under NativeAOT;
- adds a public default-provider factory shared by applications, libraries and a generated Azure
  registration, so AOT apps get the Azure provider with no code, or with one line where the
  generator is not used.

Phase 1 needs **no new public API** (one public type relocates behind a type forward). Phase 2
adds a small one: the default-provider factory, which the generator uses too. Neither phase needs
`InternalsVisibleTo` or a cross-assembly bootstrap hook. Every trimming and AOT claim is
backed by measurement with real `PublishTrimmed` and `PublishAot` builds
([experiments](experiments.md)).

> **New to trimming, NativeAOT, or the .NET assembly-loading rules?** Start with the
> [glossary](glossary.md) and the [primer](primer.md).

---

## Problem

In v6.x, `ActiveDirectoryAuthenticationProvider` lived in the core `Microsoft.Data.SqlClient`
assembly and was instantiated with a direct `new` call. The AOT compiler could trace the entire
type hierarchy statically.

In v7.x the provider lives in the separate, optional `Microsoft.Data.SqlClient.Extensions.Azure`
package. The static constructor of the internal `SqlAuthenticationProviderManager` discovers it at
runtime via `Assembly.Load` + `Assembly.GetType` + `Activator.CreateInstance` /
`ConstructorInfo.Invoke`. In a trimmed or AOT publish nothing statically references the Azure
assembly, so it is removed, and every Active Directory method is left without a provider.
(Why this happens: [primer](primer.md#trimming-and-nativeaot-explained). What these calls
do: [primer](primer.md#reflection-apis-and-why-they-break-aot).)

The public `SqlAuthenticationProvider.GetProvider()`/`SetProvider()` API lives in the
Abstractions package, but the registry lives in SqlClient. Abstractions cannot reference SqlClient
(that would be a cycle), so `SqlAuthenticationProvider.Internal` reaches the registry by reflection:
`Assembly.Load("Microsoft.Data.SqlClient")` → `GetType("…SqlAuthenticationProviderManager")` →
`GetMethod("GetProvider"/"SetProvider", NonPublic | Static)` → `Invoke`.

Two independent reflection boundaries therefore exist:

1. **Abstractions → SqlClient** — the public `GetProvider`/`SetProvider` reaching the registry.
2. **SqlClient → Azure** — discovery of the optional extension assembly.

### Measured

A `net10.0` console app referencing `Microsoft.Data.SqlClient` 7.1.1 and
`Microsoft.Data.SqlClient.Extensions.Azure` 7.1.1 (the #4193 shape: the app references Azure but
never uses a type from it, and `SqlConnection.Open()` is statically reachable) was published four
ways with the .NET 10.0.12 toolchain. Method: [A.1](experiments.md#a1-reproduction-of-4193).

| Publish | What a developer sees by default | `GetProvider(ActiveDirectoryDefault)` | App's own `SetProvider(...)` |
|---|---|---|---|
| JIT | nothing | `ActiveDirectoryAuthenticationProvider` | `true`, takes effect |
| `PublishTrimmed` | 4× IL2104 (SqlClient, Abstractions, Logging, System.Configuration.ConfigurationManager) | **`null`** — Azure assembly removed | `true`, takes effect |
| `PublishAot` | 4× IL2104 + 1× IL3053 | **`null`** | **`false`, silently discarded** |
| `PublishTrimmed` + `TrimmerRootAssembly` for Azure | 4× IL2104 | `ActiveDirectoryAuthenticationProvider` | `true` |

- **Under NativeAOT there is no working registration path at all.** The bridge's
  `GetMethod("GetProvider"/"SetProvider")` returns `null` because ILC emits no reflection metadata
  for those internal methods. So even an application that constructs the provider itself cannot
  register it. The trace reads `MDS SetProvider() method not found; SetProvider() will not function`.
- **The failure is effectively silent.** By default the publish collapses everything into one
  generic "assembly produced trim warnings" line per assembly, three of which are unrelated to
  auth. The 15 auth-specific warnings (IL2026/IL2057/IL2067/IL2070/IL2075 in four methods) appear
  only with `TrimmerSingleWarn=false`.
- **Trimmed (non-AOT) apps are broken too, and also lose `app.config`.** The Azure assembly is
  removed, and `ConfigurationManager` throws `Configuration system failed to initialize` in the
  trimmed app. The manager logs and swallows it.

Neither reflective path carries `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]`.
**There is no AOT-safe way to register an authentication provider in v7.x or on `main`.**

## Goals

| ID | Goal |
|----|------|
| G1 | Entra ID authentication works under NativeAOT and `PublishTrimmed` |
| G2 | No silent failures — a misconfiguration produces a diagnosable error, not a missing provider |
| G3 | Minimise new public API surface |
| G4 | Reflection that cannot be removed must at least be annotated and trimmable |
| G5 | Keep the driver's reflective surface small and version-robust |

## Requirements (hard constraints)

These are non-negotiable and every approach in [alternatives.md](alternatives.md) is scored against them.

| ID | Requirement |
|----|-------------|
| **R1** | **No changes to existing non-AOT apps.** Auto-magic discovery and use of the Azure package must be preserved. `app.config` configuration of third-party providers — including their reflective loading and registration — must be preserved. Fixing latent bugs is permitted. ([What these are: primer](primer.md#the-sqlclient-authentication-pipeline)) |
| **R2** | **All code that would cause AOT problems must be gated behind flags** that allow those paths to be trimmed. ([How gating works: primer](primer.md#feature-switches-and-feature-guards)) |
| **R3** | **No reflection *into* Abstractions.** ([Why it exists today: primer](primer.md#reflection-apis-and-why-they-break-aot)) |
| **R4** | **Reflection from SqlClient to Azure is accepted as unavoidable** (see [findings](findings.md#sqlclient--azure-reflection-is-unavoidable)). |
| **R5** | **All SqlClient family packages upgrade together.** Mixed family versions (e.g. Azure 8.0 with SqlClient 7.1) are **unsupported by policy**, stated in public docs and enforced at build time. ([Why NuGet permits it: primer](primer.md#nuget-packaging-and-versioning)) |

R1 rules out any design whose primary path requires application code changes.

---

## The solution

**Selected: A4-Full + B8 + B7 + C2 + C3 + C6 + D3 + E2 + E3** — *the authentication pipeline in
Abstractions, with guarded reflection, mixed-version enforcement, a public default-provider
factory and a generated fast path.* The lettered approaches are described in
[Approaches considered](alternatives.md).

Concretely:

1. **Relocation (A4-Full).** The registry, Azure discovery, `app.config` parsing and
   `SqlAuthenticationInitializer` move into Abstractions. SqlClient calls only the public
   `SqlAuthenticationProvider.GetProvider`. The two `app.config` section *types* stay in SqlClient
   as the configuration schema that existing config files bind to by name. Abstractions reads the
   parsed values untyped (see [Role of the configuration section types](design.md#role-of-the-configuration-section-types)).
   No IVT, no hook, no module initializer.
2. **Bootstrap on first registry access**, in the same assembly, which keeps today's guarantee
   that configuration and Azure discovery run before any registry read or write.
3. **Precedence as explicit tiers (D3)** — Config > User > Default — so correctness no longer
   depends on initialisation order.
4. **Public default-provider factory (B8).** `SqlAuthenticationProvider.SetDefaultProviderFactory`
   and `TrySetDefaultProviderFactory` let applications, libraries and generated code supply the
   default provider. The bootstrap prefers a registered factory and falls back to reflective
   discovery. An explicit `Set` always beats the generated `TrySet`, whichever runs first.
5. **Gating (C2, C3).** Two switches — the `app.config` gate shared with
   `Switch.Microsoft.Data.SqlClient.EnableAppConfig` (#4697) and a new
   `Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery` — each paired with a separate
   `[FeatureGuard]` property. Measured: the reflection is removed from every .NET 10 trimmed or AOT
   publish with no application configuration.
6. **Abstractions multi-targets `netstandard2.0;net10.0` (C6)**, so the asset that .NET 10 apps
   consume is built with the real BCL attributes and checked by the trim/AOT analyzer.
   `netstandard2.0` keeps internal polyfills.
7. **Mixed-version enforcement (E2, E3).** Mixing family versions stays unsupported; the design
   reports it rather than supporting it. A build-time check shipped in Abstractions' `build/` and
   `buildTransitive/` folders fails the build when the family's package versions differ. A
   runtime check compares the exact family versions of the loaded SqlClient and Azure assemblies
   and makes `GetProvider`/`SetProvider` throw a descriptive exception on a mismatch.
8. **A source generator in the Azure package (B7)** registers
   `ActiveDirectoryAuthenticationProvider.CreateDefault` through `TrySetDefaultProviderFactory` for
   `net10.0+` consumers, rooting the provider statically.
9. **Four latent bugs are fixed** as part of the move (see [Latent bugs](findings.md#latent-bugs-on-main)).

### How it works

**Today**, the registry lives in SqlClient, the public API lives in Abstractions, and both
boundaries are crossed by reflection:

```
                      reflection (GetMethod/Invoke)
   Abstractions  ─────────────────────────────────────▶  SqlClient
   GetProvider/SetProvider                               SqlAuthenticationProviderManager
                                                          ├─ registry
                                                          ├─ app.config parsing
                                                          └─ Azure discovery ── reflection ──▶ Azure
```

**After**, everything except the configuration *schema* lives in Abstractions, which both
SqlClient and Azure already reference:

```
   SqlClient ──────── direct call ────────▶  Abstractions
   (fed-auth path)                            ├─ registry (Config > User > Default tiers)
   config section types (schema only)         ├─ bootstrap, run on first registry access
                                              │    ├─ app.config parsing      [gated: EnableAppConfig]
                                              │    ├─ default-provider factory ◀── Set… (app/library) / TrySet… (generated)
                                              │    └─ Azure discovery         [gated: EnableAzureExtensionDiscovery]
                                              │                                     └── reflection ──▶ Azure
                                              └─ public GetProvider/SetProvider
```

Five mechanisms make this work. Each is specified in [design.md](design.md):

- **Relocation.** The registry and bootstrap move down a layer, so the public API and SqlClient
  both call them directly. The bootstrap still runs on the first registry access, as today's static
  constructor does, so configuration and discovery always happen before any read or write. The two
  `app.config` section types stay in SqlClient because customers' config files name them; they
  remain the schema that `System.Configuration` parses, and Abstractions reads the parsed values
  without referencing them ([details](design.md#role-of-the-configuration-section-types)).
- **Gating.** Each reflective path sits behind a switch property *and* a separate
  `[FeatureGuard]` property. The .NET 10 trimmer and AOT compiler treat a guard as `false`
  whenever trimming is on, so the reflection disappears from every trimmed or AOT publish with no
  application configuration (measured, [findings](findings.md#measured-trimmer-and-ilc-behaviour-net-10)).
- **Default-provider factory and generator.** A public factory registration is preferred over
  reflective discovery. Explicit registrations beat the generated one regardless of order. A source
  generator in the Azure package registers from the application's own compilation, which keeps the
  provider alive through trimming.
- **Explicit precedence.** Config beats User beats Default, independent of the order things happen.
- **Mixed-version enforcement.** A build-time check and a runtime backstop stop an older SqlClient
  (which keeps its own registry) from being paired with the new Abstractions.

### Why this wins

**It is the only combination that satisfies R1 exactly.** B2, B3 and B4 change what existing
applications must do or depend on. A0, A1 and A2 keep the Abstractions reflection. A3 alone forces
either a lazy bootstrap (with the [ordering hazards](design.md#ordering-hazards-any-implementation-must-avoid)) or a hook plus IVT. This design
changes only the *mechanism* by which a provider is obtained. The registry semantics, first-touch
timing, `app.config` precedence and `app.config` file format are unchanged.

**It removes the silent-failure mode that defines #4193.** The guard trims the reflective path in
trimmed and AOT publishes without any application configuration, and the default-provider
factory supplies the provider there. G2 is met by construction rather than by documentation.

**It makes registration work under AOT at all.** After Phase 1, the existing public `SetProvider`
works under NativeAOT with no new API. Phase 2 reduces that to one line,
`SetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault)`, and to none with
the generator.

**It satisfies R3 without `InternalsVisibleTo`, and keeps new public API small and useful.**
`GetProvider`/`SetProvider` keep their signatures, and the one public type that relocates does so
behind a type forward. The only additions are the default-provider factory (two methods and a
context type) and `ActiveDirectoryAuthenticationProvider.CreateDefault`. These are the same
entry points the generator uses, and they replace today's per-method workaround.

**It accepts R4 honestly while shrinking it.** Reflection remains where it must — .NET Framework,
analyzer-disabled builds, direct DLL references, and `app.config` third-party providers by
definition. The string-keyed constructor/property probing collapses into a typed factory (G5).

**It makes R5 enforceable.** NuGet forms mixed graphs silently, from something as routine as
updating one package, and moving the registry turns such a mix from harmless into a silent
failure. Rather than supporting mixes, the design reports them: at build time from Abstractions,
the one package present in every family graph, and at runtime by comparing exact family versions.
In 8.0 the cross-major case is reachable through SqlClient 7.0.0/7.0.1, whose dependency on
Abstractions has no upper bound.

### Resulting behaviour

| Scenario | Provider source | Reflection |
|---|---|---|
| `net10.0` app, AOT or trimmed | Generated `TrySetDefaultProviderFactory` | Removed by the feature guard |
| `net10.0` app, JIT | Generated `TrySetDefaultProviderFactory` | Present, unused |
| App or library calls `SetDefaultProviderFactory` | That factory, regardless of order relative to the generated registration | Removed (trimmed/AOT) or unused (JIT) |
| `net10.0`, generator skipped, JIT | Reflective discovery | Used — behaviour unchanged |
| `net10.0`, generator skipped, trimmed/AOT | One line: `SetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault)` | Removed |
| `net462` | Reflective discovery | Used — behaviour unchanged |
| `app.config` providers, JIT (`net10.0`, `net462`) | Config parsing | Always reflective, by R1 |
| `app.config` under trimming/AOT | Not available; non-functional on `main` today as well | Removed cleanly |
| `net8.0`/`net9.0` app | SqlClient 8.0 resolves to its `netstandard2.0` PlatformNotSupported asset | — |
| Any mixed family versions (e.g. SqlClient 7.0.1 with 8.x packages) | Build error (E2); runtime exception from `GetProvider`/`SetProvider` (E3) | — |

---

## Delivery

| Phase | Content | Outcome |
|---|---|---|
| **1 — Relocation, correctness, gating, enforcement** (blocking) | Move the registry and bootstrap; delete the reflective bridge; untyped config reading; fix latent bugs L1–L4; gates and guards; multi-target Abstractions; mixed-version enforcement; AOT CI tests | AOT and trimmed apps are correct and can register providers explicitly; nothing changes for JIT apps |
| **2 — Default-provider factory and generated fast path** | Public `SetDefaultProviderFactory`/`TrySetDefaultProviderFactory` and `ActiveDirectoryAuthenticationProvider.CreateDefault`; bootstrap prefers a registered factory; collapse the version-tolerant probing; source generator in the Azure package | AOT and trimmed apps get the Azure provider automatically, or with one line where the generator is not used; applications and libraries can supply their own default |

Step-by-step detail, the full change list relative to `main`, and the CI plan are in
[implementation-plan.md](implementation-plan.md).

## Backporting to 7.1 and 7.0

The authentication code on both release branches is identical to `main`, so the core change
applies mechanically. The servicing context adds constraints, detailed in
[backporting.md](backporting.md):

- **.NET 8 ignores `[FeatureGuard]` and `[FeatureSwitchDefinition]`.** The backport needs
  `ILLink.Substitutions.xml` entries, including one keyed on `IsDynamicCodeSupported` for
  .NET 8 AOT (measured).
- **Mixed versions are reported, not supported.** Same-major mixing is likely in patches (one
  package updated alone), so the build-time and runtime checks matter most there. They compare
  exact family versions and fail loudly; no compatibility fallback is kept.
- **The default-provider factory API is backported**, since new API is acceptable in a patch.
  The generator is optional there.
- **Invalid `app.config` providers don't start throwing** from the existing public API in a
  patch (that change ships in 8.0).

---

## Risks and open questions

1. **Should Abstractions depend on `System.Configuration.ConfigurationManager`?** A4-Full requires
   it. It runs against the direction a maintainer gave on #4697 (move toward
   `Microsoft.Extensions.Configuration`). If the answer is "no", fall back to A4-Conservative:
   config parsing stays in SqlClient, seeding needs IVT or a narrow seam, D3 becomes mandatory,
   and one `SetProvider` return-value divergence remains.
2. **IL4000 suppressions.** One per guard property; ensure they do not mask other guard mistakes.
   The `net10.0` build of Abstractions is where they are checked.
3. **Default-provider factory API review.** Names, last-wins for `SetDefaultProviderFactory`,
   replacing Default-tier entries after bootstrap, and what `SqlAuthenticationProviderFactoryContext`
   should carry need API review. See [design.md](design.md#default-provider-factory).
4. **Untyped config reading on .NET Framework.** Measured on .NET 10 against SqlClient's real
   handler; not yet run on .NET Framework (Linux host). Covered by [Phase 1 step 12](implementation-plan.md#phase-1--relocation-correctness-gating-and-enforcement-blocking).
5. **The section types look unused** once parsing moves. Someone may delete or "tidy" them, which
   would break every existing `app.config`. Mitigated by the pinning test and a comment.
6. **Relocating `SqlAuthenticationInitializer`** is binary-compatible, but a graph containing both
   an old SqlClient (which still defines the type) and the new Abstractions sees two definitions
   and fails to compile (CS0433). Only reachable with SqlClient 7.0.0/7.0.1, which E2 blocks.
   Validate with the ref-assembly comparison tooling.
7. **Version-check opt-out.** E2 fails on any family version mismatch, with a documented opt-out
   property for emergencies. E3 has no build-level opt-out; decide whether it needs an `AppContext`
   switch. E5 (Azure depending on the SqlClient package) could stop the most common mix forming
   at restore and is worth evaluating.
8. **Generator packaging.** No analyzer-packaging precedent exists in this repo; signing,
   design-time builds and the opt-out property need validation.
9. **Overlap with #4697.** Whichever lands first defines `EnableAppConfig`; the other adopts it.

## Scope

**In scope:** registry relocation; moving the bootstrap logic and `SqlAuthenticationInitializer`
into Abstractions; untyped config reading; fixes for latent bugs L1–L4; tiered precedence; the public
default-provider factory; split gates aligned with `EnableAppConfig`; feature guards and polyfills; Abstractions
multi-targeting; mixed-version enforcement; AOT CI tests; documentation of switches and policy;
the source generator; the [backport plan](backporting.md).

**Out of scope:** making the manager public (A2); removing reflection entirely (R1/R4); changing
`SqlAuthenticationProvider`'s public signatures; migrating `app.config` to
`Microsoft.Extensions.Configuration` (tracked on #4697); other AOT warnings
([#1947](https://github.com/dotnet/SqlClient/issues/1947)).

---

## Documents

| Document | Audience | Contents |
|---|---|---|
| **README.md** (this file) | Everyone | Problem, requirements, decision, how it works, delivery, risks |
| [design.md](design.md) | Implementers, code reviewers | What moves where, the role of the config section types, the bootstrap sequence, tiers, gates, default-provider factory, generator, target frameworks, version enforcement, latent-bug fixes |
| [implementation-plan.md](implementation-plan.md) | Implementers, PR authors | Phase 1–3 steps, changes relative to `main`, salvage from earlier PRs, CI coverage |
| [findings.md](findings.md) | Reviewers checking the reasoning | Established constraints and measured facts: why reflection is unavoidable, NuGet range facts, trimmer behaviour, the `netstandard2.0` analyzer gap, latent bugs L1–L4, related work |
| [alternatives.md](alternatives.md) | Reviewers asking "why not X?" | Every approach considered (A0–E5) with its verdict |
| [backporting.md](backporting.md) | Release and servicing owners | 7.1/7.0 differences, .NET 8/9 measurements, extra changes BP1–BP8, test matrix |
| [experiments.md](experiments.md) | Anyone auditing the measurements | Method, environment and raw results (A.1–A.5); how to reproduce with [experiments/](experiments/) |
| [glossary.md](glossary.md) | Newcomers | One-line definitions of every term |
| [primer.md](primer.md) | Newcomers | Trimming and NativeAOT, feature switches and guards, reflection, assemblies, NuGet, and the SqlClient authentication pipeline |

### Identifiers

Identifiers are defined in one document and linked from the others.

| Identifier | Meaning | Defined in |
|---|---|---|
| G1–G5, R1–R5 | Goals and requirements | [README.md](#goals) |
| A0–A4, B1–B8, C1–C6, D1–D3, E1–E5 | Approaches | [alternatives.md](alternatives.md) |
| L1–L4 | Latent bugs on `main` | [findings.md](findings.md#latent-bugs-on-main) |
| Phase 1–3 steps | Implementation steps | [implementation-plan.md](implementation-plan.md) |
| BP1–BP8 | Backport-specific changes | [backporting.md](backporting.md#additional-changes-for-the-backport) |
| A.1–A.5 | Experiments | [experiments.md](experiments.md) |
