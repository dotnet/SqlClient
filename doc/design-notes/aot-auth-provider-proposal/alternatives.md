# Approaches considered

> Part of the [AOT-safe authentication provider registration](README.md) proposal.
> **Audience:** reviewers asking "why not X?". **Read first:** [README](README.md).

Every approach considered, grouped by the question it answers and scored against the
[goals and requirements](README.md#goals). Selected approaches are summarised here; their
mechanics are specified in [design.md](design.md).

| ID | Approach | Verdict |
|---|---|---|
| A0 | Reflective bridge (`main` today) | Removed |
| A1 | Callback bridge (#4195) | Rejected |
| A2 | Public `SqlAuthenticationProviderManager` (#4348) | Rejected |
| A3 | Move only the registry (#4670) | Subsumed by A4 |
| A4 | Move the authentication pipeline into Abstractions | **Selected (A4-Full)** |
| B1 | Reflective Azure discovery | Retained as the fallback |
| B2 | Explicit application registration | Escape hatch only (one line via B8) |
| B3 | Separate integration package | Rejected |
| B4 | SqlClient references Azure directly | Rejected |
| B5 | `[ModuleInitializer]` in the Azure assembly | Rejected |
| B6 | Keep the Azure assembly alive for the trimmer | Rejected |
| B7 | Source generator in the Azure package | **Selected** |
| B8 | Public default-provider factory | **Selected** |
| C1 | Single feature switch (#4573) | Mechanism kept, shape changed |
| C2 | Feature-guard shape | **Selected** |
| C3 | Split gates aligned with `EnableAppConfig` | **Selected** |
| C4 | Package targets flipping the switch off | Rejected |
| C5 | Substitution keyed on `IsDynamicCodeSupported` | Backport only |
| C6 | Multi-target Abstractions | **Selected (C6b)** |
| D1 | Permanent flag ordered by the static constructor | Superseded by D3 |
| D2 | Public permanent-registration API | Rejected |
| D3 | Order-independent tiers | **Selected** |
| E1 | Document the version policy only | Rejected |
| E2 | Build-time version check | **Selected** |
| E3 | Runtime version check (exact family version) | **Selected** |
| E4 | Keep the reflective bridge for old SqlClient | Rejected |
| E5 | Azure package depends on the SqlClient package | Worth evaluating |

Approaches are grouped by the question they answer and scored against the requirements.

> Terminology used throughout this section — `InternalsVisibleTo`, type forwarding, module
> initializers, feature switches, source generators — is explained in
> the [primer](primer.md).

## Group A — How does Abstractions reach the registry?

### A0. Reflective bridge (`main` today)

`SqlAuthenticationProvider.Internal` loads `Microsoft.Data.SqlClient`, finds the internal manager
and its non-public static `GetProvider`/`SetProvider`, and invokes them.

**Verdict: remove.** Violates R3, makes NativeAOT registration impossible (L3), and loads an
unverified assembly (L4). It is removed in 8.0 and in the [backports](backporting.md); a mixed
graph is reported instead (E2, E3).

### A1. Callback bridge ([#4195](https://github.com/dotnet/SqlClient/pull/4195))

`SqlAuthenticationProvider.RegisterProviderManager(getProvider, setProvider)` is called by the
manager's static constructor to wire up delegates. Earlier registrations are buffered and
replayed. `ActiveDirectoryAuthenticationProvider.RegisterAsDefault()` gives AOT apps an entry
point.

- Adds a cross-assembly callback with subtle ordering rules.
- `RegisterProviderManager()` is public API that exists only as internal plumbing (violates G3).
- The pending-provider buffer is an unsynchronised `Dictionary`.
- The reflective bridge remains the primary path — R3 not met.

**Verdict: rejected.**

### A2. Make `SqlAuthenticationProviderManager` public ([#4348](https://github.com/dotnet/SqlClient/pull/4348))

Promote the manager and its `GetProvider`/`SetProvider` to public, expose `ApplicationClientId`,
deprecate the Abstractions statics, and guard reflective discovery with a feature switch.

- Three public members plus a ref-assembly change (violates G3).
- The bridge is deprecated but still present for existing callers — R3 not met.
- AOT apps must make explicit registration calls — violates R1 as the primary path.

**Verdict: rejected.** Its feature-switch and annotation ideas are retained in Group C.

### A3. Move only the registry into Abstractions ([#4670](https://github.com/dotnet/SqlClient/pull/4670))

The reflection exists only because the dependency arrow points SqlClient → Abstractions while the
registry *state* lives in SqlClient. A3 moves the registry core into Abstractions as
`internal sealed`, deletes the bridge, and leaves the bootstrap in SqlClient.

**Verdict: necessary but insufficient — subsumed by A4.** It satisfies R3 structurally. But
because the bootstrap stays behind, the registry cannot start discovery itself. That forces
either a cross-assembly hook plus an `InternalsVisibleTo` grant for config seeding plus a residual
ordering gap, or a lazy fed-auth bootstrap with both
[ordering hazards](design.md#ordering-hazards-any-implementation-must-avoid).

#### A3 sub-variants rejected

- **New package *below* Abstractions.** Abstractions is six source files of auth contracts plus
  the bridge. A package beneath it would take the contracts with it, leaving Abstractions an empty
  forwarding shim. **Rejected** as churn.
- **Declarations in Abstractions, definitions in a higher package.** Not possible in .NET. A
  method body lives in the same assembly as its declaring type, and type forwarding only relocates
  *downward*. `static abstract` interface members need .NET 7+ and generic dispatch. The only way to
  place an implementation higher is dependency inversion via a registration slot, which is A1
  minus the reflection.

### A4. Move the authentication pipeline into Abstractions — **selected**

Moving the bootstrap logic down as well collapses the hook, the IVT and the ordering gap. The
registry calls the bootstrap directly on first access — same assembly, no delegate, no module
initializer — which **preserves today's semantics**: any registry access triggers discovery first.

#### What A4 avoids, compared with A3

| A3 would require | Under A4 |
|---|---|
| Bootstrap hook + SqlClient `[ModuleInitializer]` | **Not needed** — direct in-assembly call |
| "Abstractions touched before SqlClient loads" gap | **Not possible** — discovery no longer involves SqlClient |
| `InternalsVisibleTo("Microsoft.Data.SqlClient")` on Abstractions | **Not needed** |
| Signed-IVT plumbing (#4369) | **Not needed** |
| Order-independent tiers (D3) | Recommended, not required |
| Default-provider factory (B8) | **Simpler** — it lives beside the bootstrap that consumes it |

#### Variants

- **A4-Full — move the registry, Azure discovery, `app.config` *parsing* and
  `SqlAuthenticationInitializer`; leave the two section *types* in SqlClient as the schema.**
  Preserves today's semantics exactly, including `SetProvider` returning `false` for a method
  claimed by configuration (measured, [A.3](experiments.md#a3-what-the-section-handler-does)). Cost: Abstractions
  acquires `System.Configuration.ConfigurationManager` (compile-excluded) and localized resources.
- **A4-Conservative — move the registry and Azure discovery; leave `app.config` parsing in
  SqlClient.** Abstractions gains **no new dependency**. But config seeding needs IVT or a narrow
  public seam, D3 tiers become mandatory, and one divergence appears: if `app.config` claims a
  method *and* the app calls `SetProvider` for it before SqlClient bootstraps, `SetProvider`
  returns `true` where today it returns `false`.

#### Costs and counter-arguments

- **Abstractions changes character** from "auth contracts" to "the auth layer".
- **It runs against the direction given on #4697** (move toward `Microsoft.Extensions.Configuration`).
  A4-Full makes Abstractions a second assembly that depends on `System.Configuration`.
  `Microsoft.Extensions.Configuration` cannot replace `app.config` under R1 by itself; it could
  only be an additional source.
- In deployment terms the cost is close to zero. Azure is never deployed without SqlClient, and
  SqlClient already ships `System.Configuration.ConfigurationManager` (compile-excluded) and uses
  in-box `System.Configuration` on `net462`. No real application's dependency closure grows.

**Verdict: selected — A4-Full**, with A4-Conservative as the fallback **if** the team decides
Abstractions must not depend on `System.Configuration` (open question 1).

## Group B — How is the Azure provider obtained?

### B1. Reflective discovery (`main` today)

`Assembly.Load` (with public-key-token enforcement under `STRONG_NAME_SIGNING`) +
`Assembly.GetType` + `CreateAzureAuthenticationProvider`'s constructor/property probing.
[primer](primer.md#reflection-apis-and-why-they-break-aot) shows the code.

**Verdict: retained as the compatibility path.** Required by R1 and R4. Annotated and gated
(Group C).

### B2. Explicit application registration

e.g. `ActiveDirectoryAuthenticationProvider.RegisterDefaults(options)` called from `Main()`.

**Verdict: rejected as primary** (R1). Available as an escape hatch once A4 lands: one line with
B8's `SetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault)`, or the
existing per-method `SetProvider`.

### B3. Separate integration package

A `…Extensions.Azure.Integration` package exposing `SqlClientAzureAuthentication.Register(options)`.

**Verdict: rejected.** Same R1 violation as B2, plus another package.

### B4. SqlClient references Azure directly

**Verdict: rejected.** Forces `Azure.Core`, MSAL, the MSAL broker and `Caching.Memory` onto every
application. That enlarges deployment, AOT binary size and vulnerability surface for apps that
never use Entra auth, reverses the v7 packaging decision, and violates R1.

### B5. `[ModuleInitializer]` inside the Azure assembly

**Verdict: rejected.** If nothing statically references the Azure assembly, it is removed before
its initializer could run. ([primer](primer.md#trimming-and-nativeaot-explained).)

### B6. Keep the Azure assembly alive for the trimmer

- **B6a — ILLink descriptor embedded in the Azure assembly.** Same chicken-and-egg as B5: an
  embedded descriptor is processed only if its assembly is already kept.
- **B6b — `TrimmerRootAssembly` added by the Azure package's `buildTransitive` targets.**
  **Measured to work for `PublishTrimmed`**: discovery succeeds and `GetProvider` returns the
  Azure provider ([A.1](experiments.md#a1-reproduction-of-4193)).

**Verdict: rejected.** B6b roots the entire Azure assembly, keeps the reflective path and its
warnings alive (contrary to R2/G4), and does nothing for AOT, where the bridge still fails. Once
C2 lands, the discovery code itself is trimmed, so rooting Azure is moot.

### B7. Source generator in the Azure package — **selected**

Ship a Roslyn generator in the Azure package that emits, into the *application's* compilation, a
module initializer calling
`SqlAuthenticationProvider.TrySetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault)`
(B8). The application assembly is always rooted, so the direct reference keeps the provider alive
through trimming. Nothing is constructed until the bootstrap runs, so MSAL load timing is
unchanged. Because it uses `TrySet`, any explicit registration by the application or a library
wins.

Why the Azure package hosts it: apps that do not use Entra auth pay no analyzer cost, and Azure
packs from its csproj, where adding a generator is one item. R5 means the generator ships from the
same commit as the provider and needs no version-adaptive probing. It emits only for `net10.0+`
consumers; .NET Framework keeps reflective discovery, which also avoids C# 7.3/`packages.config`
analyzer issues. It uses only public API.

It is a fast path, not a replacement: reflection remains for .NET Framework, analyzer-disabled
builds and direct DLL references. Mechanics: [design.md](design.md#source-generator).

**Verdict: selected** — ships with B8 in Phase 2.

#### B7 variant rejected: targets injecting a `.cs` file

`buildTransitive` `<Compile Include="…AzureAutoRegister.cs" />` is cheaper but injects source into
the user's compilation (namespace collisions, `ImplicitUsings`) and cannot produce diagnostics.

### B8. Public default-provider factory — **selected**

A public registration point in Abstractions —
`SqlAuthenticationProvider.SetDefaultProviderFactory`/`TrySetDefaultProviderFactory`, with a
`SqlAuthenticationProviderFactoryContext` carrying the config-derived `applicationClientId` and
`useWamBroker` — supplies the factory for the default provider. The bootstrap prefers a registered
factory and falls back to reflective discovery. Applications, libraries and the generated Azure
code (B7) all use it, and Azure's `ActiveDirectoryAuthenticationProvider.CreateDefault` makes
explicit registration a single line. The string-keyed constructor/property probing shrinks to
typed calls. Mechanics: [design.md](design.md#default-provider-factory).

Variants rejected:

- **An internal slot.** Generated code and the Azure package cannot reach an internal member of
  Abstractions without a hidden public member or an `InternalsVisibleTo` grant. Public API serves
  more callers for the same cost.
- **First caller takes the slot.** The generated registration is a module initializer in the
  application assembly, and it runs before `Main`. Applications could never register first, so
  Azure would always win.
- **Registration closes at bootstrap.** A library touching the registry early would silently
  make a later registration fail, costing an AOT app its Azure provider. Late registration
  instead replaces Default-tier entries only.

Cost: two methods and a context type in Abstractions, plus one method in Azure (G3). The public
surface is justified because it replaces today's per-method workaround for apps without the
generator, and it lets third-party providers become a default without overriding explicit
choices.

**Verdict: selected.**

## Group C — How is AOT-hostile code gated?

> [primer](primer.md#feature-switches-and-feature-guards) explains switches, guards,
> substitution and the IL warning codes referenced below.

### C1. Single feature switch, default `true` ([#4573](https://github.com/dotnet/SqlClient/pull/4573))

One switch gating both config and Azure discovery, with `[FeatureSwitchDefinition]` and
`ILLink.Substitutions.xml` (shape A).

**Verdict: mechanism kept, shape changed.** Correct only when the app knows about the switch, so
#4193's silent failure remains for uninformed AOT apps. Not analyzer-clean.

### C2. Feature guard shape — **selected**

Keep a named switch for runtime opt-out, and add a **separate** guard property annotated
`[FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]` and
`[FeatureGuard(typeof(RequiresDynamicCodeAttribute))]` (shape B). Measured: removed from every
.NET 10 trimmed or AOT publish with zero app configuration, and guarded call sites stop emitting
IL2026/IL3050. Cost: one documented IL4000 suppression per guard property.

**Verdict: selected.** Serves G1 and G2.

### C3. Split the gates, aligned with `EnableAppConfig` — **selected**

Config parsing and Azure discovery are independent reflection sources with different weights.
Config drags in `System.Configuration` (five fixed-cost trim warnings) and `Type.GetType`; Azure
uses `Assembly.Load`. Each gets its own switch:

- the `app.config` gate reuses `Switch.Microsoft.Data.SqlClient.EnableAppConfig` from #4697, so one
  setting turns off every `app.config` reader;
- Azure discovery gets `Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery`.

Both default to `true` and follow the repo's `Switch.Microsoft.Data.SqlClient.*` convention, with
the `Enable*`/default-`true` form a reviewer endorsed on #4697. The "default to `false`" guideline
in `features.instructions.md` targets opt-in behaviour changes, whereas these gate behaviour that
exists today. If #4697 does not merge, the config gate is introduced here under the same name.
Mechanics: [design.md](design.md#gating).

**Verdict: selected.**

### C4. Package targets flipping the switch off — **rejected (trap)**

The Azure package's targets could set `RuntimeHostConfigurationOption … Value="false"` on the
grounds that generated registration replaced discovery.

**Verdict: rejected.** If the generator is skipped for any reason — analyzers disabled, an
unanticipated TFM, an unusual build — discovery is disabled *and* no registration exists,
recreating #4193 for non-AOT apps. Factory precedence (B8) plus the guard (C2) is self-correcting.

### C5. Substitution keyed on `IsDynamicCodeSupported` — **backport only**

An `ILLink.Substitutions.xml` entry that stubs each guard to `false` whenever ILC reports
`RuntimeFeature.IsDynamicCodeSupported=false`. It exists for toolchains that ignore
`[FeatureGuard]`, i.e. .NET 8. Every SqlClient 8.0 consumer on modern .NET uses a .NET 10 or
later toolchain, so 8.0 does not need it.

**Verdict: not needed for 8.0; required for the backports** (BP3).

### C6. Where do the annotations get checked? — **multi-target, selected**

| Option | Effect |
|---|---|
| **C6a** — Abstractions stays `netstandard2.0`; polyfills; a CI-only `net10.0` analysis build | Works (polyfills measured), but the analyzed binary is not the shipped one |
| **C6b** — Abstractions targets `netstandard2.0;net10.0`; polyfills only in the `netstandard2.0` build | .NET 10 apps consume the `net10.0` asset, built with real BCL attributes and analyzed with `IsAotCompatible=true`. Measured identical trimming to C6a |

SqlClient 8.0 already targets `net10.0`, so C6b adds one TFM to a six-file project and makes the
shipped asset the checked one. `netstandard2.0` stays for .NET Framework consumers and third-party
provider authors.

**Verdict: C6b selected.** (The servicing branches use C6a; see
[Backporting](backporting.md).)

## Group D — How is registration precedence expressed?

> [primer](primer.md#the-sqlclient-authentication-pipeline) describes the three provider
> sources and the precedence rules being preserved.

### D1. Permanent flag, ordered by the static constructor (`main` today)

`_authenticationsWithAppSpecifiedProvider` marks config-supplied methods non-overridable.
`SetProvider` refuses them and returns `false`, and otherwise overwrites. Correctness rests on the
static constructor seeding config before any other access.

**Verdict: sound, superseded by D3.** A4 keeps the ordering guarantee, but D3 removes the
dependence on it.

### D2. Public permanent-registration API

e.g. `SetProvider(method, provider, SqlAuthenticationProviderRegistrationMode.Permanent)`.

**Verdict: rejected.** Exposes an implementation concept and lets any library lock a registration
against the application (G3).

### D3. Order-independent tiers — **selected**

Three tiers — Default (default-provider factory or discovery; never overwrites Config or User), User (public `SetProvider`),
Config (`app.config` and the initializer; wins over both) — reproduce today's observable behaviour
without depending on initialisation order. Hazard 1 becomes impossible by construction. Mandatory
under A4-Conservative, where config seeding could arrive late. Mechanics:
[design.md](design.md#precedence-tiers).

**Verdict: selected.** No new public API.

## Group E — How are mixed family versions handled?

Mixing family versions is unsupported (R5), but NuGet produces mixes silently from routine
actions such as updating one package. Today a mix is harmless for authentication. After the
relocation, an **old SqlClient** (which keeps its own registry) with a **new Abstractions** (which
no longer bridges to it) would accept the app's `SetProvider` into a registry SqlClient never
reads. The question is therefore how to *report* a mix, not how to support one. In 8.0 the pairing
is reachable across majors only through SqlClient 7.0.0/7.0.1; in a patch, within one major, any
single-package update reaches it
([lockstep](findings.md#package-lockstep-is-a-release-policy-not-a-nuget-constraint)).

### E1. Document the policy only

**Verdict: rejected.** Leaves exactly the silent failure G2 forbids.

### E2. Build-time check in Abstractions' `build/` and `buildTransitive/` — **selected**

Abstractions is in every family graph — SqlClient, Azure and third-party providers all depend on
it — and packs from its csproj, so it hosts a `.targets` check in `build/` and `buildTransitive/`.
The check compares the exact versions of the family packages and fails the build, naming them. It
has a separate branch for `packages.config` projects, which have no resolved-package metadata.
Mechanics: [design.md](design.md#build-time-e2).

**Verdict: selected.** It catches mixes earliest, but only for builds that resolve through NuGet
and haven't opted out.

### E3. Runtime check in Abstractions — **selected**

At bootstrap, Abstractions compares the loaded SqlClient's exact family version, read from
`AssemblyInformationalVersionAttribute`, with its own. Discovery compares the Azure assembly's
version the same way. A mismatch fails the bootstrap, so `GetProvider`, `SetProvider` and the
default-provider factory throw a descriptive exception — exactly the calls a mix breaks.
`AssemblyName.Version` is major-only and could not detect a mix within one major. Mechanics:
[design.md](design.md#runtime-e3).

**Verdict: selected.** It covers what E2 cannot see: direct DLL references, replaced files,
opted-out builds, and processes composed at runtime (plugin hosts, PowerShell modules, custom
`AssemblyLoadContext`s, the GAC).

### E4. Keep the reflective bridge as a fallback for old SqlClient

Detect an older SqlClient and route `GetProvider`/`SetProvider` through today's bridge, so the
mix keeps working.

**Verdict: rejected**, for 8.0 and the backports alike. It supports a configuration the policy
says is unsupported, keeps reflection into SqlClient (R3), and needs a second code path, a
legacy mode for the default-provider factory, and its own tests. E2 and E3 report the mix
instead.

### E5. Make Azure depend on the SqlClient package

Give the Azure package a compile-excluded dependency on `Microsoft.Data.SqlClient` with the family
range. Then updating Azure alone would pull SqlClient forward, so the most common mix could not
form. There is no cycle, because SqlClient does not depend on Azure.

**Verdict: worth evaluating, not required.** It doesn't cover direct Abstractions updates or
third-party provider packages, so E2 and E3 are still needed. Check its interaction with
SqlClient's `netstandard2.0` PlatformNotSupported asset before adopting it.

## Approaches considered for zero-code-change AOT

| Approach | Outcome |
|---|---|
| `[ModuleInitializer]` in the Azure extension | Rejected — B5; trimmed assemblies' initializers never run |
| ILLink descriptor in the Azure assembly | Rejected — B6a; same chicken-and-egg |
| `TrimmerRootAssembly` from Azure package targets | Works for `PublishTrimmed` (measured) but keeps reflection alive and fails AOT — B6b |
| Source generator | **Accepted** — B7; viable once R5 removes version adaptivity |
