---
applyTo: "**/Directory.Packages.props,**/*.csproj,**/Directory.Build.props,**/*.nuspec"
---
# Choosing Third-Party Package Dependency Versions

Guidance for choosing versions of **external (third-party) NuGet package dependencies** in multi-targeted projects (e.g. `net462;net8.0;net9.0`).

> **Scope:** This document covers dependencies consumed from NuGet — packages the SqlClient repo does NOT own. For versioning of SqlClient's own inter-sibling packages (Logging, Abstractions, SqlClient, Azure, AKV Provider, SqlServer.Server), see `sqlclient-package-versions.instructions.md`.

## Rule
For runtime-aligned packages, **the package major must match the target runtime major**: 8.x on `net8.0`, 9.x on `net9.0`, 10.x on `net10.0`, and so on. TFMs that aren't tied to a specific runtime major (`net462`, `netstandard2.0`) get the major of the floor LTS. Other categories are versioned as described below.

Split package references into three categories:

### 1. Runtime-aligned packages — **version per TFM, matching the runtime band**

Packages whose major version ships with (or is tightly coupled to) a specific .NET runtime:

- `Microsoft.Extensions.*` (Logging, DependencyInjection, Configuration, Hosting, Options, Caching, Http, Primitives, ...)
- `Microsoft.AspNetCore.*`
- `Microsoft.EntityFrameworkCore.*`
- `System.Text.Json`, `System.Memory`, `System.IO.Pipelines`, `System.Formats.Asn1`, `System.Security.Cryptography.Pkcs`
- `Microsoft.Bcl.*`

Use the major version that matches the TFM. For TFMs without a corresponding runtime major (`net462`, `netstandard2.0`, etc.), use the major of the **lowest supported modern TFM** — typically the floor LTS (e.g. `8.x` while net8 is supported). This keeps the legacy targets on a long-lived, well-patched band and avoids dragging in transitive deps from a newer major:

```xml
<!-- Defaults: lowest supported runtime band (e.g. net8 LTS); also applies to net462 / netstandard2.0 -->
<ItemGroup>
  <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.1" />
  <PackageVersion Include="System.Text.Json"             Version="8.0.5" />
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net9.0'">
  <PackageVersion Update="Microsoft.Extensions.Logging" Version="9.0.0" />
  <PackageVersion Update="System.Text.Json"             Version="9.0.0" />
</ItemGroup>
```

When the floor LTS drops out of support, bump the default block to the new floor LTS major and drop any conditional block that becomes redundant.

### 2. Independent packages — **single version across all TFMs**

Packages whose versioning is decoupled from the .NET runtime:

- `Newtonsoft.Json`, `Polly.*`, `Serilog.*`
- `Azure.*`, `Microsoft.Identity.*`, `Microsoft.IdentityModel.*`
- `Microsoft.Data.SqlClient`, `Dapper`, `StackExchange.Redis`
- Most third-party packages

Reference one (latest stable) version unconditionally:

```xml
<PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
<PackageVersion Include="Azure.Identity"  Version="1.17.1" />
```

### 3. Polyfills — **conditional presence, single version**

Packages that only exist (or are only needed) on older TFMs. The polyfill major doesn't have to match any runtime band (older TFMs have no in-box equivalent), so pick the latest stable available:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0' OR '$(TargetFramework)' == 'net462'">
  <PackageReference Include="Microsoft.Bcl.AsyncInterfaces" Version="10.0.0" />
</ItemGroup>
```

Condition the *presence* of the reference, not the version.

## How to categorize a package

The named lists above aren't exhaustive. To classify a package you don't recognize, work through these steps in order:

### 1. Read the nuget.org description

Pure polyfills almost always say so explicitly. For example, `Microsoft.Bcl.TimeProvider`'s page reads: *"For apps targeting .NET 8 and newer versions, referencing this package is unnecessary, as the types it contains are already included in the .NET 8 and higher platform versions."*

If the description says "for apps targeting .NET X and earlier" or "unnecessary on .NET X+", treat as polyfill candidate and continue to step 2 to confirm. If it makes no such claim and the package owner is Microsoft + a major number tracks the .NET release train, treat as runtime-aligned candidate.

### 2. Inspect the package's `lib/` layout

Look at the "Frameworks" tab on nuget.org or open the `.nupkg`:

| `lib/` layout | Category |
|---|---|
| Only older TFM folders (`netstandard2.0`, `net462`) — no modern TFMs | Pure polyfill |
| `lib/net8.0/_._`, `lib/net9.0/_._` placeholders + real DLL only on older TFMs | Pure polyfill (no-op on modern TFMs) |
| Real DLLs in `lib/net8.0/`, `lib/net9.0/`, `lib/net10.0/`, differing per band | Runtime-aligned |
| Real DLLs on every TFM including older ones, single major doesn't track .NET releases | Independent |

### 3. Check the release cadence

- Runtime-aligned: new major every November in lockstep with .NET (8.0, 9.0, 10.0, ...) plus monthly servicing patches.
- Independent: releases on its own schedule, major doesn't correlate with .NET versions.
- Pure polyfill: usually freezes at one major and rarely bumps; new majors only to ride the build train.

### 4. Functional test — remove the reference and rebuild

The decisive test for the polyfill-vs-runtime-aligned boundary: remove the `PackageReference` on a modern TFM (e.g. net8) and build.

- Builds clean → package was acting as a polyfill on that TFM. Confirm category 3.
- Fails with `CS0246`/`CS1061` (missing type or method) → the package contributes API the in-box BCL doesn't have. Treat as runtime-aligned (category 1), even if the description sounds polyfill-ish.

### Beware hybrids

Some packages look like polyfills but add API beyond the in-box BCL even on modern TFMs. Treat these like runtime-aligned packages.

### When in doubt

Treat as **runtime-aligned** (category 1) and reference on every TFM with per-TFM majors.

- Cost of mis-classifying a true polyfill this way: a redundant `_._` asset at restore. Harmless.
- Cost of mis-classifying a hybrid as a pure polyfill: a compile break on the TFMs where you dropped the reference.

## Why

### Why latest minor/patch always

`PackageReference` versions are minimums (`[X, ∞)`), so writing a stale minor or patch buys nothing for downgrade-safety and only loses fixes:

- **Security**: BCL and Extensions packages ship CVE patches in minor/patch bumps. Pinning to an older patch means a customer who doesn't transitively pull a newer version stays on the vulnerable floor.
- **Bug fixes**: Same logic for non-security fixes. We have no reason to anchor customers to an older `8.0.0` when `8.0.5` is available.
- **NU1605 risk is small and easy to fix**: NU1605 fires on *any* downgrade, including minor/patch within the same major (e.g. our `8.0.5` transitive vs. a customer's direct `8.0.0`). In practice this is rare and trivial to resolve — the customer bumps their direct reference to a current patch. The cost of *not* tracking latest (stale security/bug fixes for every consumer who doesn't override) is larger than the cost of an occasional one-line bump in a consumer project.
- **Reduces noise from automated bumps**: Dependabot/Renovate PRs disappear if we already track latest.

This rule applies to all three categories. The category decides the major; "latest" decides the minor and patch.

### NuGet `PackageReference` versions are minimums, not pins

`Version="X"` means `[X, ∞)`. The resolver picks the highest version requested across the graph, with one critical exception: a **direct** reference wins over a transitive one (nearest-wins).

### NU1605 fires when the customer's direct version is *lower* than your transitive version

If your library transitively requires `Microsoft.Extensions.Logging 10.0.0` and the consuming app has a direct `<PackageReference Version="8.0.0" />`, NuGet detects a downgrade and emits **NU1605**. In modern SDKs this is an **error**, not a warning — the customer's build fails.

The reverse (customer's direct version higher than your transitive) resolves cleanly with no warning.

### The asymmetry drives the rule

| Your transitive version | Customer's direct version | Result |
|---|---|---|
| 10.x | 8.x | **NU1605 error** |
| 10.x | 10.x or higher | Clean |
| 8.x  | 8.x or higher | Clean |

Pinning runtime-aligned packages to the **band matching each TFM** means a net8 consumer transitively gets 8.x (no friction with their own 8.x reference), and a net10 consumer transitively gets 10.x.

Pinning everything to the latest major (e.g. `10.x` unconditionally) forces every net8 customer to roll their direct references forward or hit NU1605.

### Independent packages don't have this problem

`Newtonsoft.Json 13.x`, `Azure.Identity 1.17.x`, etc. aren't tied to a runtime version. Customers don't have a "matching" version in mind, and the package's own multi-targeted assets handle TFM selection internally. One version is simpler and avoids needless conditional blocks.

### Framework-provided assemblies win at runtime anyway

On .NET 8+, packages like `System.Text.Json` and `System.Memory` are part of the shared framework. Even if you reference `System.Text.Json 9.0.0`, a net8 app uses the in-box net8 copy at runtime. Referencing 10.x on net8 just creates restore-graph noise with no runtime benefit — another reason to match the band.

## Tradeoffs and alternatives considered

### Per-TFM (the rule) vs. single lowest-LTS version

A "pin everything to the lowest supported LTS major (e.g. 8.x everywhere) and bump only when that LTS drops" policy is also downgrade-safe and simpler in `Directory.Packages.props`. We rejected it because:

- Customers on newer runtimes lose access to perf/feature work that landed in later package majors for packages that *aren't* fully overridden by the in-box shared framework (e.g. `Microsoft.Extensions.Caching.Memory`, `System.Configuration.ConfigurationManager`).
- The simplification is small: one extra conditional `ItemGroup` per runtime-aligned package.
- A coordinated bump when the floor LTS drops is a larger, riskier change than incremental per-TFM updates.

Per-TFM keeps each runtime on its matching band and confines change to one `Update` line at a time.

### Why no upper bounds

Customers can transitively pull in a *higher* major than your reference. NuGet resolves nearest-wins with no warning, so a major that broke API can produce `MissingMethodException`/`TypeLoadException` at runtime rather than a restore-time failure.

An upper bound (e.g. `Version="[8.0.0, 10.0.0)"`) would convert that runtime failure into a restore-time `NU1107` and is the only way to guarantee compatibility. We still avoid it because:

- It blocks customers from rolling forward to fix CVEs or take perf wins in the newer major.
- It propagates conflicts deep into customer dependency graphs that we can't see.
- Microsoft's [library guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/dependencies#nuget-dependency-version-ranges) explicitly says **AVOID upper bounds**, and the foundational Microsoft libraries (EF Core, ASP.NET Core, Aspire, Orleans, the BCL itself, Azure SDK) follow it.
- Exact pins (`[X]`) in Microsoft repos are reserved for host-coupled scenarios: Roslyn analyzers (`Microsoft.CodeAnalysis.*`), MSBuild API consumers (`Microsoft.Build`), VS extensibility. None apply to runtime libraries like SqlClient.

We accept the SemVer-break risk in exchange for not breaking customer rollforward. If a specific package is known to break compatibility at a future major, document it in code review and revisit — don't blanket-bound.

### Downgrade direction matters

| Your transitive | Customer's direct | Result |
|---|---|---|
| Higher | Lower | **NU1605 error** (we cause this) |
| Lower | Higher | Clean restore, customer's wins |
| Equal | Equal | Clean |

The asymmetry is the entire reason per-TFM matching works: it makes us the *lower* or *equal* for any customer who has aligned their own references with their runtime.

## Checklist before changing a package version

1. Is the package in the runtime-aligned list above? → use per-TFM conditional `PackageVersion Update`, latest minor/patch in each band.
2. Is it independent? → single `PackageVersion`, latest stable.
3. Is it a polyfill? → conditional `PackageReference` only on TFMs that need it, latest stable.
4. Always pick the latest available minor/patch within the chosen major. Don't carry forward a stale minor when bumping or adding.
5. Prefer Central Package Management (`Directory.Packages.props`) over per-project versions.
6. Never use exact-version (`[X]`) or upper-bound (`[X, Y)`) ranges on `PackageReference` unless you have a documented compatibility reason.

## Sources

- [NU1605 — Detected package downgrade](https://learn.microsoft.com/nuget/reference/errors-and-warnings/nu1605)
- [NuGet Package versioning — version ranges](https://learn.microsoft.com/nuget/concepts/package-versioning#version-ranges)
- [NuGet dependency resolution](https://learn.microsoft.com/nuget/concepts/dependency-resolution)
- [Library guidance — Dependencies](https://learn.microsoft.com/dotnet/standard/library-guidance/dependencies)
- [Library guidance — Cross-platform targeting](https://learn.microsoft.com/dotnet/standard/library-guidance/cross-platform-targeting)
