# Implementation plan

> Part of the [AOT-safe authentication provider registration](README.md) proposal.
> **Audience:** implementers and PR authors. **Read first:** [README](README.md), then
> [design.md](design.md).

## Phase 1 — Relocation, correctness, gating and enforcement (blocking)

1. Move the registry core (`ConcurrentDictionary`, `GetProvider`, `SetProvider`, `IsSupported`
   enforcement, precedence tracking) into Abstractions as `internal sealed`. Switch the public
   `GetProvider`/`SetProvider` to direct calls and **delete `SqlAuthenticationProvider.Internal.cs`**.
   Change `SqlConnectionInternal.GetFedAuthToken` to call the public
   `SqlAuthenticationProvider.GetProvider`. Delete `SqlAuthenticationProviderManager`.
2. Move the bootstrap logic (config parsing, initializer invocation, Azure discovery,
   `CreateAzureAuthenticationProvider`, the `STRONG_NAME_SIGNING` public-key-token check) into
   Abstractions, and run it on **first registry access** — not lazily on the fed-auth path.
3. Read `<SqlClientAuthenticationProviders>` and `<SqlAuthenticationProviders>` through
   `ConfigurationManager.GetSection` and `ElementInformation.Properties`. Accept a section only if
   its handler's full type name is one of the two SqlClient section types, mirroring today's exact
   type check. Leave the section types in SqlClient, with a comment explaining why they look unused
   and a unit test pinning their names, namespaces and property sets.
4. Move `SqlAuthenticationInitializer` to Abstractions, add it to `TypeForwards.Abstractions.cs`,
   and update the ref assemblies. Publish the registry before invoking the initializer, so its
   registrations succeed (latent bug L1); they rank in the Config tier.
5. Cache any bootstrap failure (bad provider type, bad initializer type, provider rejecting its
   method), preserving public `GetProvider`/`SetProvider` results of `null`/`false`.
   Log the original failure and corrective guidance on every unsuccessful access, so observers
   can diagnose the problem even when tracing is enabled after the initial failure (L2).
   Registration argument and callback failures likewise return `false` with actionable traces.
6. Multi-target Abstractions `netstandard2.0;net10.0`. Add internal polyfills for
   `RequiresUnreferencedCode`, `RequiresDynamicCode`, `FeatureSwitchDefinition` and `FeatureGuard`
   to the `netstandard2.0` build only, following Logging's `UnconditionalSuppressMessageAttribute`
   precedent. Enable `IsAotCompatible` for `net10.0`.
7. Add `System.Configuration.ConfigurationManager` to Abstractions as a compile-excluded
   dependency, as SqlClient already ships it, versioned per
   `3rd-party-package-versions.instructions.md`. On .NET Framework its asset forwards to in-box
   `System.Configuration`.
8. Add `Strings.resx` to Abstractions for the five auth messages. Copy the existing 13 locale
   translations from SqlClient's resource files, since the strings are unchanged, and register the
   new resource set with the localization pipeline and
   `.config/LocalizationValidationAllowlist.json`. Replace `SqlClientLogger` calls with
   `SqlClientEventSource`.
9. Implement the gates: `EnableAppConfig` (shared name) and `EnableAzureExtensionDiscovery` switch
   properties with `[FeatureSwitchDefinition]`, plus separate guard properties carrying both
   `[FeatureGuard]` attributes, with justified IL4000 suppressions. Annotate the reflective methods
   `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. An `ILLink.Substitutions.xml` is not
   required for .NET 10 apps (measured), but is cheap to add for the `netstandard2.0` asset.
10. Make precedence order-independent (D3). The Azure default never overwrites an existing
    registration.
11. Ship mixed-version enforcement in Abstractions (E2, E3), as specified in
    [design.md](design.md#mixed-version-enforcement): the `build/`/`buildTransitive/` check with
    its `packages.config` branch and opt-out property, and the runtime comparison of exact family
    versions for SqlClient at bootstrap and Azure at discovery. It belongs in Phase 1 because
    Phase 1 is what turns an old-SqlClient/new-Abstractions pairing into a silent failure. Add
    tests for each mixed graph listed in [backporting.md](backporting.md#backport-test-matrix)
    that applies to 8.0.
12. Add CI coverage that does not exist today:
    - a **NativeAOT and trimmed publish test** for a `net10.0` app, asserting behaviour
      (`SetProvider` succeeds, `GetProvider` returns it, discovery is absent) and warnings-as-errors
      for the auth assemblies. Linux agents can link with gcc (`CppCompilerAndLinker=gcc`,
      `StripSymbols=false`) if clang is unavailable, as these experiments did;
    - `net462` functional tests for `app.config` providers, initializer and precedence, since
      .NET Framework keeps the reflective path permanently.
13. Document the switches in `.github/instructions/features.instructions.md`, including that
    trimming requires the publish-time `RuntimeHostConfigurationOption … Trim="true"` form.

No new public API in Phase 1. The ref-assembly change is limited to relocating
`SqlAuthenticationInitializer`.

### Phase 1 validation commands

The implementation includes repeatable process-isolated tests of real configuration handlers,
initializer registration, precedence, bootstrap errors, and disabled gates. The runtime checks
also replace the driver with an older-version fixture and verify that disabling both discovery
switches cannot bypass exact family version validation:

```bash
dotnet tool run pwsh -- -File src/Microsoft.Data.SqlClient.Extensions/Abstractions/test/RunAuthenticationTests.ps1 -Publish
dotnet tool run pwsh -- -File src/Microsoft.Data.SqlClient.Extensions/Abstractions/test/RunVersionChecks.ps1
```

The first command requires Linux and a native linker (gcc); it publishes and executes both
trimmed and NativeAOT .NET 10 apps. On Windows, use `-Framework net462` without `-Publish`
to exercise the permanently reflective .NET Framework path. Both commands are wired into CI.
The second covers exact family version checks in PackageReference metadata and
`packages.config`, including the emergency `SqlClientEnforceFamilyVersions=false` build opt-out.

SqlClient 8.0 implementation assets target `net462;net10.0`, with reference and unsupported
assets additionally targeting `netstandard2.0`. All family packages must be upgraded together.
The phase 2 factory API and generator are not implemented by phase 1.

## Phase 2 — Default-provider factory and generated fast path

1. Add `SqlAuthenticationProviderFactoryContext`, `SqlAuthenticationProvider.SetDefaultProviderFactory`
   and `SqlAuthenticationProvider.TrySetDefaultProviderFactory` to Abstractions, with the
   precedence, late-registration and trace behaviour in
   [design.md](design.md#default-provider-factory). Document them in
   `src/Microsoft.Data.SqlClient.Extensions/Abstractions/doc/`.
2. Make the bootstrap prefer a registered factory, invoke it once with the context, and fall back
   to reflective discovery only when none is registered.
3. Add `ActiveDirectoryAuthenticationProvider.CreateDefault(SqlAuthenticationProviderFactoryContext)`
   to Azure, reproducing today's constructor selection with typed calls. Document it in
   `src/Microsoft.Data.SqlClient.Extensions/Azure/doc/`.
4. Collapse `CreateAzureAuthenticationProvider`'s probing on the fallback path. The legacy
   `(string)` constructor fallback exists only for Azure 1.0.0, which has no options type
   (verified from the published package). E2 makes that pairing impossible for 8.0, so the
   fallback can go.
5. Ship a Roslyn generator in the Azure package (`analyzers/dotnet/cs`) that emits
   `TrySetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault)` for
   `net10.0+` consumers, with an opt-out MSBuild property. Cover signing, design-time builds and
   packaging (open question 8).
6. Tests:
   - ordering: `Set` before and after the generated `TrySet`, `Set` after bootstrap (Default-tier
     entries replaced; Config and User untouched), a late `TrySet` with and without an explicit
     factory, last-wins between two `Set` calls;
   - a factory that throws or supports no fed-auth method surfaces as a bootstrap failure;
   - the NativeAOT/trimmed CI app from Phase 1, run with the generator and with the one-line
     explicit registration instead of `SetProvider`.
7. Add a sample to `doc/samples/` showing explicit registration and overriding the Azure default.
8. Publish the supported-configuration policy (R5) alongside the build-time check.

**Sequencing.** Phase 1 alone makes AOT apps *correct but provider-less unless they register
explicitly* — which they now can, with `SetProvider`. Phase 2 makes that one line, and zero lines
with the generator. Trimmed apps lose nothing they have today at any point. Shipping both phases in
8.0 is preferable.

---

## Changes relative to main

| Area | `main` today | This proposal |
|---|---|---|
| Registry location | SqlClient (`SqlAuthenticationProviderManager`, internal) | Abstractions (internal) |
| Abstractions → SqlClient reflection | Present (`SqlAuthenticationProvider.Internal`) | **Deleted** |
| AOT `SetProvider` | Silently returns `false` | Works |
| Bootstrap location | SqlClient static constructor | Abstractions, first registry access |
| `InternalsVisibleTo` between production assemblies | None | None |
| Config section types | SqlClient | **Unchanged** — still the handlers `app.config` binds to |
| Config parsing | SqlClient, typed | Abstractions, untyped |
| `SqlAuthenticationInitializer` | SqlClient | Abstractions + type forward |
| Initializer's `SetProvider` calls | Silently fail | Work |
| Invalid `app.config` provider | Public API returns `null`/`false` for the process lifetime | Same results, with cached failure details and actionable traces on every access |
| Precedence | Permanent flag, order-dependent | Explicit tiers |
| Gating | None | Two switches + feature guards |
| Trim annotations | None on auth paths | `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` on reflective methods |
| Abstractions TFMs | `netstandard2.0` | `netstandard2.0;net10.0` |
| Azure provider acquisition | Reflection only | Default-provider factory (explicit or generated), reflection fallback |
| New public API | — | `SetDefaultProviderFactory`, `TrySetDefaultProviderFactory`, `SqlAuthenticationProviderFactoryContext` (Abstractions); `ActiveDirectoryAuthenticationProvider.CreateDefault` (Azure) |
| Mixed family versions | Silently accepted | Build error + logged runtime failure (`GetProvider`/`SetProvider` return `null`/`false`) |
| AOT validation | None | CI publish tests |

Useful salvage exists in two closed, unmerged PRs: the registry relocation and its tests from
#4670 (commit `943678b3f`), and the feature-switch plumbing and AOT test app from #4573 (commit
`e52b2b53c`). #4573's lazy fed-auth bootstrap and its `SetPermanentProvider` IVT seam should not be
reused (see [Ordering hazards](design.md#ordering-hazards-any-implementation-must-avoid)).
