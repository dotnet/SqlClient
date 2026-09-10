# AOT-Safe Authentication Provider Registration

## Problem

Issue [#4193](https://github.com/dotnet/SqlClient/issues/4193): Entra ID authentication is broken under NativeAOT in SqlClient v7.0.

In v6.x, `ActiveDirectoryAuthenticationProvider` lived in the core `Microsoft.Data.SqlClient` assembly and was instantiated with a direct `new` call in `SqlAuthenticationProviderManager.SetDefaultAuthProviders()`. The AOT compiler could trace the entire type hierarchy statically.

In v7.0, the provider was moved to the separate `Microsoft.Data.SqlClient.Extensions.Azure` package. The manager now discovers it at runtime via `Assembly.Load` + `Activator.CreateInstance`. Under NativeAOT, the linker has no static reference to follow and trims the assembly, silently leaving all Active Directory auth methods without a provider.

The public `SqlAuthenticationProvider.SetProvider()` API (in the Abstractions package) also fails under AOT because it uses reflection to call the internal `SqlAuthenticationProviderManager` in the core assembly.

**There is no AOT-safe way to register an authentication provider in v7.0.** None of the reflection code paths carry `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` annotations, so the AOT compiler emits zero warnings.

## PR #4195 Proposal (Callback Bridge)

PR [#4195](https://github.com/dotnet/SqlClient/pull/4195) introduces a callback-based bridge:

1. **`SqlAuthenticationProvider.RegisterProviderManager(getProvider, setProvider)`** — Called by `SqlAuthenticationProviderManager`'s static constructor to wire up direct (non-reflection) delegates. Providers registered via `SetProvider` before the manager initializes are buffered in a pending dictionary and replayed. Marked `[EditorBrowsable(Never)]`.

2. **`ActiveDirectoryAuthenticationProvider.RegisterAsDefault()`** — AOT-safe entry point that registers the MSAL-based provider for all 9 AD auth methods. Applications call this early in `Main()`.

3. **`LoadAzureExtensionProvider()`** — Extracted from the static constructor and annotated with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`.

### AOT usage

```csharp
// Top of Main(), before opening any connection:
ActiveDirectoryAuthenticationProvider.RegisterAsDefault();

// Then connect normally:
using var connection = new SqlConnection(
    "Server=myserver.database.windows.net;Database=mydb;" +
    "Authentication=Active Directory Managed Identity;");
await connection.OpenAsync();
```

### Concerns with this approach

- Adds a new cross-assembly callback/buffering mechanism with subtle ordering requirements (register-before-manager-init gets buffered, register-after goes direct).
- `RegisterProviderManager()` is public API surface (even if hidden with `[EditorBrowsable(Never)]`) that exists only as internal plumbing.
- Thread safety of the pending-provider buffer is informal (plain `Dictionary` without synchronization, safe in practice but not formally).
- The reflection-based `SqlAuthenticationProvider.Internal` class remains the primary code path; the callback bridge is layered on top.
- Does not address the root cause: `SqlAuthenticationProviderManager` is the natural owner of provider registration but is invisible to consumers.

## Alternative Proposal (Make Manager Public)

Instead of adding bridge infrastructure, make the existing `SqlAuthenticationProviderManager` class public and expose its `GetProvider`/`SetProvider` methods directly.

### Changes

#### 1. Make `SqlAuthenticationProviderManager` public

| Current | Proposed |
|---------|----------|
| `internal sealed class SqlAuthenticationProviderManager` | `public sealed class SqlAuthenticationProviderManager` |
| `internal static SqlAuthenticationProvider? GetProvider(...)` | `public static SqlAuthenticationProvider? GetProvider(...)` |
| `internal static bool SetProvider(...)` | `public static bool SetProvider(...)` |

#### 2. Expose `ApplicationClientId`

Add a public read-only property:

```csharp
public static string? ApplicationClientId => Instance._applicationClientId;
```

The manager already reads this from config (`SqlClientAuthenticationProviders` / `SqlAuthenticationProviders` configuration sections). Exposing it lets AOT apps pass it to the `ActiveDirectoryAuthenticationProvider` constructor without reimplementing config parsing.

#### 3. Deprecate `SqlAuthenticationProvider.GetProvider`/`SetProvider`

These static methods on the Abstractions class exist only to bridge into the manager via reflection. With the manager now public, they are redundant:

```csharp
[Obsolete("Use SqlAuthenticationProviderManager.GetProvider() instead.")]
public static SqlAuthenticationProvider? GetProvider(SqlAuthenticationMethod authenticationMethod)

[Obsolete("Use SqlAuthenticationProviderManager.SetProvider() instead.")]
public static bool SetProvider(SqlAuthenticationMethod authenticationMethod, SqlAuthenticationProvider provider)
```

The code comments in the current source already state this intent:
> *"We would like to deprecate this method in favour of SqlAuthenticationProviderManager.GetProvider()."*

#### 4. Guard reflection code with a feature switch

Use `[FeatureSwitchDefinition]` (.NET 9+) to let the trimmer eliminate the reflection-based Azure extension discovery entirely in AOT builds:

```csharp
[FeatureSwitchDefinition(
    "Microsoft.Data.SqlClient.EnableReflectionBasedProviderDiscovery")]
internal static bool EnableReflectionBasedProviderDiscovery =>
    AppContext.TryGetSwitch(
        "Microsoft.Data.SqlClient.EnableReflectionBasedProviderDiscovery",
        out bool enabled)
        ? enabled
        : true; // ON by default for non-AOT
```

Guard the reflection block in the static constructor:

```csharp
if (EnableReflectionBasedProviderDiscovery)
{
    LoadAzureExtensionProvider(); // Assembly.Load + Activator.CreateInstance
}
```

When an AOT app sets the switch to `false` (via `RuntimeHostConfigurationOption` with `Trim="true"`), the trimmer substitutes the property with constant `false` and eliminates the entire reflection branch.

#### 5. Annotate reflection paths

Apply `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` to the extracted `LoadAzureExtensionProvider()` method so the AOT analyzer warns even without the feature switch.

#### 6. Update ref assembly

Add `SqlAuthenticationProviderManager` to `src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.cs`:

```csharp
public sealed class SqlAuthenticationProviderManager
{
    public static string? ApplicationClientId { get { throw null; } }
    public static SqlAuthenticationProvider? GetProvider(
        SqlAuthenticationMethod authenticationMethod) { throw null; }
    public static bool SetProvider(
        SqlAuthenticationMethod authenticationMethod,
        SqlAuthenticationProvider provider) { throw null; }
}
```

### AOT usage

```csharp
// Top of Main(), before opening any connection:
var provider = new ActiveDirectoryAuthenticationProvider(
    SqlAuthenticationProviderManager.ApplicationClientId);

SqlAuthenticationProviderManager.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, provider);
SqlAuthenticationProviderManager.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryDefault, provider);
// ... etc. for each needed auth method
```

No reflection. No `Assembly.Load`. No `Activator.CreateInstance`. Fully AOT-safe.

## Alternative Proposal (Move Registry Into Abstractions)

Both proposals above leave the registry in the core `Microsoft.Data.SqlClient`
assembly and bridge into it — PR #4195 via a callback, "Make Manager Public" via
new public API plus a still-present reflection fallback. This proposal removes the
Abstractions→SqlClient reflection at its root by relocating the *provider registry*
to the assembly both sides already share.

### Root cause

The reflection exists only because the dependency arrow points **SqlClient →
Abstractions**, yet the registry *state* lives in SqlClient. Abstractions therefore
cannot call the manager directly without a cycle, so it reflects. Move the registry
singleton into a layer **both** assemblies reference and both can call concrete,
strongly-typed APIs against the same static state — no `MethodInfo.Invoke`, no
`Assembly.Load`, AOT/trim-safe.

The manager's surface is typed on `SqlAuthenticationProvider` and
`SqlAuthenticationMethod`, which already live in Abstractions. So the registry must
sit *at or above* those types — and Abstractions is exactly that layer.

### Option A — Move the registry core into Abstractions (recommended)

The manager can't move wholesale: its static constructor depends on SqlClient-only
facilities (`System.Configuration` section parsing, the Azure extension
`Assembly.Load` + public-key-token check, `SqlAuthenticationInitializer`,
`SqlClientLogger`, `SQL.*` error helpers). The move is therefore a **split**:

1. **Registry core → Abstractions** (`internal sealed`): the
   `ConcurrentDictionary<method, provider>`, `GetProvider`, `SetProvider`,
   `IsSupported` enforcement, the app-specified-provider tracking set, and the
   singleton `Instance`. Logging switches to `SqlClientEventSource` (already in the
   Logging package that Abstractions references); the unsupported-method error
   throws `SqlAuthenticationProviderException` instead of
   `SQL.UnsupportedAuthenticationByProvider`. A new internal
   `SetAppSpecifiedProvider(method, provider)` records config/Azure-seeded entries
   that user `SetProvider` may not override.

2. **Bootstrap/orchestration → stays in SqlClient** under a new internal
   `SqlAuthenticationProviderBootstrapper` (so there is no duplicate
   `SqlAuthenticationProviderManager` type). It parses config and loads the Azure
   default provider, pushing results into the Abstractions singleton via
   `SetAppSpecifiedProvider`, triggered once on the connection auth path.

3. **Abstractions public wrappers call the concrete manager.**
   `SqlAuthenticationProvider.GetProvider`/`SetProvider` invoke the moved manager
   directly, and `SqlAuthenticationProvider.Internal.cs` (the entire reflection
   bridge) is **deleted**. SqlClient reads providers during connection auth through
   the public `SqlAuthenticationProvider.GetProvider`, and seeds the (overridable)
   Azure default through the public `SqlAuthenticationProvider.SetProvider` — neither
   needs internal access.

4. **`[InternalsVisibleTo("Microsoft.Data.SqlClient")]`** is added to Abstractions for
   **one reason only**: the config-specified-provider precedence. A provider declared
   in `app.config` must be registered as non-overridable (so a later user
   `SetProvider` returns `false` instead of replacing it), and that marking —
   `SetAppSpecifiedProvider` — is intentionally *not* part of the public surface. The
   general get/set path does **not** need `InternalsVisibleTo`; only the bootstrapper's
   app-specified seeding does. If that precedence rule were dropped or reworked,
   `InternalsVisibleTo` would be unnecessary and SqlClient could rely entirely on the
   public API. The manager itself stays internal — no public API or ref-assembly
   change.

> **Note (porting to 7.0):** Adding `[InternalsVisibleTo("Microsoft.Data.SqlClient")]`
> to Abstractions depends on the build/signing changes in PR
> [#4369](https://github.com/dotnet/SqlClient/pull/4369). To take this AOT fix onto the
> 7.0 branch, those changes would need to be ported there first. (This applies only if
> the config-precedence seeding hook is kept; see point 4.)

### Option B — New package *below* Abstractions (rejected)

The literal reading of "put the registry in a Logging-style package beneath
Abstractions" does not pay off. Abstractions consists of **exactly six files, all of
them auth contracts** (`SqlAuthenticationProvider`, `SqlAuthenticationMethod`,
`SqlAuthenticationParameters`, `SqlAuthenticationToken`,
`SqlAuthenticationProviderException`, and the reflection bridge that gets deleted
anyway). Because the manager is typed on those contracts, a package *below*
Abstractions would have to take the contracts down with it — leaving Abstractions an
**empty type-forwarding shim** that re-exports the moved types solely to preserve its
published NuGet surface.

In other words, Abstractions already *is* the low "auth-contracts" package this idea
imagines living beneath; there is no separate higher layer to sit under. Option B is
Option A plus a pointless empty shim and a churn of relocated public types and
`TypeForwards.Abstractions.cs` entries. **Rejected** in favor of Option A.

### Considered: declarations in Abstractions, definitions in a higher intermediate package (rejected)

A tempting variation is to keep `SqlAuthenticationProvider.GetProvider`/`SetProvider`
*declared* in Abstractions but put their *definitions* (the registry) in an
intermediate package that sits **above** Abstractions and **below** SqlClient/Azure —
so the heavy code never ships in the low contract package. This is **not possible** in
.NET for the same type.

- **A method body lives in the same assembly as its declaring type.** There is no
  cross-assembly header/implementation split; `partial` methods and classes must be in
  the same compilation. `SqlAuthenticationProvider` — with all its method bodies —
  exists in exactly one assembly at runtime.

- **Type forwarding only relocates downward.** A forwarder must reference the definer:

  ```csharp
  [assembly: TypeForwardedTo(typeof(SqlAuthenticationProvider))] // needs a ref to the definer
  ```

  Today this works because **SqlClient (high) forwards down to Abstractions (low)** —
  SqlClient already references Abstractions, so it is acyclic. The proposed topology
  needs Abstractions (low) to forward **up** to the intermediate, which would require
  Abstractions to reference the intermediate while the intermediate references
  Abstractions for the contract types — a **circular assembly reference**. Not allowed.

  > **General principle:** a type's definition can live *at or below* Abstractions,
  > never above it.

- **`static abstract` interface members don't apply.** They can be implemented in a
  different assembly, but require a .NET 7+ runtime (not `netstandard2.0`) and dispatch
  only through a generic type parameter — not a plain `SqlAuthenticationProvider.GetProvider()`
  call.

The only way to genuinely place the implementation in a higher package is **dependency
inversion via a registration slot**: keep the public statics defined in Abstractions as
trivial relays to an injected `ISqlAuthProviderRegistry` (or delegates) that a higher
assembly registers at startup. That is exactly the **PR #4195 callback bridge minus the
reflection** — strongly typed, AOT-safe, and `netstandard2.0`-compatible — but it
reintroduces the registration handshake and its ordering/buffering concerns, which is
the complexity Option A exists to avoid.

### AOT impact

This directly eliminates the Abstractions→SqlClient reflection path that the Problem
section calls out (the public `SetProvider` failing under AOT). It does **not** by
itself fix the SqlClient→Azure discovery reflection — that remains and would still be
handled by the feature-switch + `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`
annotations from the "Make Manager Public" proposal, applied to the relocated
bootstrapper. The two proposals compose: this one removes one reflection edge
structurally; the annotations/feature-switch trim the other.

### Changes

| Current | Proposed |
|---------|----------|
| `SqlAuthenticationProviderManager` in core assembly (`internal`) | Registry core moved to Abstractions (`internal`), bootstrap relocated to `SqlAuthenticationProviderBootstrapper` in core |
| `SqlAuthenticationProvider.Internal` reflection bridge | Deleted |
| `SqlAuthenticationProvider.GetProvider`/`SetProvider` reflect into core | Call the concrete manager directly |
| Abstractions `InternalsVisibleTo`: Test only | Add `Microsoft.Data.SqlClient` (only for the config-precedence seeding hook) |
| Default Azure provider force-loaded via reflection | Seeded by SqlClient bootstrap when a connection authenticates |

### Tradeoffs

- **No new public API.** Unlike the other two proposals, nothing new is exposed;
  `SqlAuthenticationProvider.GetProvider`/`SetProvider` keep their signatures.
- **Behavior change (accepted):** user `SetProvider` now works even if the core
  assembly isn't loaded (the provider sits in the shared singleton until SqlClient
  reads it). The default Azure provider appears only once SqlClient's bootstrap runs
  — i.e., when a connection actually authenticates — rather than being eagerly
  force-loaded. This removes the last `Assembly.Load` from the get/set path.
- **Larger structural change** to where the registry lives, but a small, contained
  one (Abstractions is six files). Bootstrap logic and config/Azure semantics are
  unchanged in behavior, only relocated.

### Assembly size & performance

Relocating the registry core shifts code from `Microsoft.Data.SqlClient` into
`Microsoft.Data.SqlClient.Extensions.Abstractions`: the former shrinks by roughly the
amount the latter grows. Because `Abstractions` is a hard dependency of `SqlClient`
and is never deployed without it, the two assemblies always ship together — so the
**total on-disk and in-memory footprint is unchanged** (net zero, minus the deleted
reflection bridge).

There is **no runtime, efficiency, or performance penalty**. Quite the opposite:

- **Faster, not slower** — get/set become direct virtual/static calls instead of
  `MethodInfo.Invoke`, and the one-time `Assembly.Load` + reflection lookup in the
  static constructor is removed.
- **No extra indirection** — the call simply crosses an assembly boundary that the
  JIT already inlines/resolves like any other; there is no marshalling or bridge.
- **No new dependencies** — Abstractions already references the Logging package it
  needs for `SqlClientEventSource`; nothing new is pulled in.
- **Smaller metadata** — deleting `SqlAuthenticationProvider.Internal` and the
  reflection plumbing removes code from the shipped product.

### Scope

- **In scope**: relocate the registry core into Abstractions; delete the reflection
  bridge; add `InternalsVisibleTo`; relocate config/Azure bootstrap into a SqlClient
  `SqlAuthenticationProviderBootstrapper`; preserve app-specified-provider precedence.
- **Out of scope**: making the manager public (covered by the prior proposal),
  the SqlClient→Azure discovery reflection (compose with the feature-switch/annotation
  approach), other AOT issues (#1947).

## Comparison

| Aspect | PR #4195 (Callback Bridge) | Public Manager | Move Into Abstractions |
|--------|---------------------------|----------------|------------------------|
| New public API surface | `RegisterProviderManager()` on Abstractions, `RegisterAsDefault()` on Azure | `GetProvider`/`SetProvider`/`ApplicationClientId` on Manager | None (manager stays internal) |
| Reflection in Abstractions→SqlClient | Remains (deprecated path still used) | Deprecated, no longer needed | Removed (manager moved into Abstractions) |
| Reflection in SqlClient→Azure | Annotated, still runs | Guarded by feature switch, trimmable | Remains; compose with feature switch/annotations |
| Buffering/ordering complexity | Pending-provider dictionary with replay | None | None |
| Thread safety concerns | Informal (plain Dictionary) | None (existing ConcurrentDictionary) | None (existing ConcurrentDictionary) |
| Lines of new code | ~300 | Minimal (visibility changes + feature switch + ref assembly) | Moderate (relocate registry + bootstrapper) |
| Backwards compatible | Yes | Yes (additive public API, deprecations only) | Yes (signatures unchanged; default-seeding timing differs) |
| Works without code changes for non-AOT | Yes | Yes (reflection discovery unchanged by default) | Yes (config/Azure bootstrap unchanged by default) |
| AOT app code | `ActiveDirectoryAuthenticationProvider.RegisterAsDefault()` | Explicit `SetProvider` calls (more verbose but transparent) | Explicit `SetProvider` calls (same as Public Manager) |

## Zero-Code-Change AOT: Why No Proposal Supports It

None of the proposals allow AOT apps to use Entra ID authentication without any startup code changes. All require explicit provider registration:

- **PR #4195**: `ActiveDirectoryAuthenticationProvider.RegisterAsDefault()`
- **Public Manager**: `SqlAuthenticationProviderManager.SetProvider(...)` calls
- **Move Into Abstractions**: `SqlAuthenticationProvider.SetProvider(...)` calls (registry relocation removes the Abstractions→SqlClient reflection, but the app must still touch the provider type)

The fundamental issue is that under NativeAOT, the trimmer removes code that's only reachable via reflection. Without a static reference from the app to `ActiveDirectoryAuthenticationProvider`, the trimmer has no reason to keep it. Some mechanism needs the app to "touch" the provider type.

### Alternatives considered

| Approach | Why it doesn't work |
|----------|-------------------|
| **`[ModuleInitializer]`** in the Azure extension assembly that self-registers | Module initializers of trimmed assemblies don't run — same chicken-and-egg problem. |
| **Source generator** that emits registration code when it detects the Azure extension package reference | Could work, but significantly more complex to ship and maintain. |
| **ILLink/trimmer root descriptor XML** shipped in the Azure extension NuGet package | Preserves the type, but `Assembly.Load`/`Activator.CreateInstance` in the manager's static constructor are still AOT-hostile. Combining with the feature switch left enabled defeats much of the point of AOT — you'd preserve reflection infrastructure and hope it works rather than using statically traceable code. |

One line of startup code is the standard .NET pattern for AOT-compatible service registration (similar to how `System.Text.Json` requires explicit `JsonSerializerContext` registration under AOT). It's the expected tradeoff.

## Scope

- **In scope**: `SqlAuthenticationProviderManager` visibility, `ApplicationClientId` property, deprecations on `SqlAuthenticationProvider`, feature switch for reflection, AOT annotations, ref assembly update.
- **Out of scope**: Removing reflection entirely (backwards compat), changing `SqlAuthenticationProvider.Internal` beyond deprecation (Abstractions targets `netstandard2.0`), other AOT issues (#1947).
