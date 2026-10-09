# Primer

> Part of the [AOT-safe authentication provider registration](README.md) proposal.
> **Audience:** readers new to trimming, NativeAOT or the .NET assembly-loading rules. Assumes no
> prior knowledge. One-line definitions are in the [glossary](glossary.md).

| Section | Covers |
|---|---|
| [Trimming and NativeAOT explained](#trimming-and-nativeaot-explained) | What trimming does, why reflection breaks, what "silent failure" means |
| [Feature switches and feature guards](#feature-switches-and-feature-guards) | The gating mechanism; the difference between the two, and why it matters |
| [Reflection APIs and why they break AOT](#reflection-apis-and-why-they-break-aot) | The specific calls used, and how hostile each one is |
| [Assemblies, visibility and type forwarding](#assemblies-visibility-and-type-forwarding) | `InternalsVisibleTo`, type forwarding, module initializers, dependency direction |
| [NuGet packaging and versioning](#nuget-packaging-and-versioning) | Version ranges, package layout, source generators, why lockstep isn't enforced |
| [The SqlClient authentication pipeline](#the-sqlclient-authentication-pipeline) | The packages, the registry, the static constructor, and who calls what |

---

## Trimming and NativeAOT explained

### The one-sentence version

Publishing with trimming or NativeAOT **deletes every piece of code the build cannot prove is
used** — and reflection is invisible to that proof.

### How normal .NET differs

In a normal (JIT) app, the whole library ships. At runtime you can ask for any type by name and
it will be there:

```csharp
var t = Type.GetType("Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider, Microsoft.Data.SqlClient.Extensions.Azure");
var provider = Activator.CreateInstance(t);   // works — the type is present
```

Trimming and NativeAOT change the deal. At publish time the tool starts from `Main()` and follows
every method call and type reference it can see. Whatever it reaches is **rooted** and kept.
Everything else is deleted.

The tool reads *code*, not strings. So this is traceable:

```csharp
var provider = new ActiveDirectoryAuthenticationProvider();  // kept — a real reference
```

and this is not:

```csharp
var asm = Assembly.Load("Microsoft.Data.SqlClient.Extensions.Azure");  // just a string
var t   = asm.GetType("Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider");
var provider = Activator.CreateInstance(t);   // the assembly was deleted — Load throws
```

Nothing references the provider type, so the whole Azure assembly is removed — measured with the
7.1.1 packages:
the trimmed publish folder contains no `Microsoft.Data.SqlClient.Extensions.Azure.dll`. **That is
issue #4193.**

### Why it failed "silently"

The trimmer *does* warn — but by default it collapses every warning in a library that is not
marked trimmable into a single line per assembly:

```
warning IL2104: Assembly 'Microsoft.Data.SqlClient.Extensions.Abstractions' produced trim warnings.
```

Measured with the 7.1.1 packages, a trimmed publish prints four such lines (SqlClient, Abstractions, Logging,
`System.Configuration.ConfigurationManager`). An AOT publish adds one IL3053. None mentions
authentication, and they appear for every app that uses SqlClient, auth or not. The eight
auth-specific warnings only appear with `TrimmerSingleWarn=false`. So:

- the app published without an actionable message,
- and the failure appeared only when someone opened an Entra-authenticated connection in
  production.

Hence goal **G2** (no silent failures) and requirement **R2** (gate the AOT-hostile code), and
hence the `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` labels this proposal applies.

### Trimming versus NativeAOT

| | `PublishTrimmed` | `PublishAot` |
|---|---|---|
| Output | IL, with unused code deleted | A native executable |
| Deletes unused code | Yes | Yes (always) |
| Runtime code generation | Still available | **Not** available |
| `RuntimeFeature.IsDynamicCodeSupported` | `true` | `false` |
| Reflection over kept methods | Works | Works only if ILC emitted metadata |

That last row is why main's two modes fail differently: trimmed apps keep the bridge working (the
manager's methods survive with metadata), while AOT apps lose it.

### Two distinct problems

1. **Reachability** — "the type may have been deleted." Applies to trimming *and* AOT.
   Flagged by `[RequiresUnreferencedCode]` → warning IL2026.
2. **Code generation** — "this needs to build new code at runtime." Applies to AOT only.
   Flagged by `[RequiresDynamicCode]` → warning IL3050.

### How code gets "rooted"

Only a real reference roots something. This is why approach **B7** works: a source generator puts
a genuine `new ActiveDirectoryAuthenticationProvider(...)` into the *application's* own code, and
the application is always rooted. It is also why **B5** (a module initializer inside the Azure
assembly) does **not** work: if nothing references that assembly, it is deleted before its
initializer could ever run. `TrimmerRootAssembly` (B6b) roots an assembly by fiat, which works,
but keeps all of it.

### Verifying AOT output

`main` has no AOT test. Grepping ILC's `.map.xml` for a method name is tempting, but silently
passes if the method is renamed or inlined. These experiments used behavioural output plus `nm`
checks for non-inlined methods in an unstripped binary. The recommended CI gate ([Phase 1 step 12](implementation-plan.md#phase-1--relocation-correctness-gating-and-enforcement-blocking))
is behavioural assertions plus warnings-as-errors, with symbol checks only as a supplement.

---

## Feature switches and feature guards

This is the mechanism behind requirement **R2**, approach **C2** (and **C5** in the backports),
and the "shape A–D" comparison in
[Measured trimmer and ILC behaviour](findings.md#measured-trimmer-and-ilc-behaviour-net-10).

### The problem both solve

We have code that must exist for normal apps but must vanish for trimmed ones:

```csharp
if (IsAzureExtensionDiscoverySupported)
{
    LoadAzureExtensionProvider();   // Assembly.Load + Activator.CreateInstance
}
```

If the trimmer can be convinced that the condition is always `false`, it deletes the branch, the
method, and everything only that method used. The question is *what convinces it*.

### Feature switch — the app decides

A feature switch is a named flag. The library declares it:

```csharp
[FeatureSwitchDefinition("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery")]
internal static bool EnableAzureExtensionDiscovery =>
    AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", out bool v)
        ? v
        : true;   // default: on
```

and the **application** turns it off in its `.csproj`:

```xml
<RuntimeHostConfigurationOption
    Include="Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery"
    Value="false"
    Trim="true" />
```

`Trim="true"` makes the value visible to the trimmer. Setting the switch with
`AppContext.SetSwitch` at run time changes behaviour but trims nothing.

**The catch:** nothing happens unless the application does this. That is shape **A**.

### Feature guard — the trimmer decides

A feature guard says **"the code behind this needs something trimming takes away."**

```csharp
[FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
[FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
internal static bool IsAzureExtensionDiscoverySupported => EnableAzureExtensionDiscovery;
```

The .NET 9+ trimmer and ILC conclude the guard must be `false` **whenever trimming is enabled**,
with no configuration from the app. That is shape **B**, measured TRIMMED for .NET 10 apps (and
.NET 9 apps, see [Backporting](backporting.md#measured-net-8-and-net-9-toolchains)) in both trimmed and AOT
publishes. It also silences IL2026/IL3050 at guarded call sites.

### Why keep both

| | Feature switch | Feature guard |
|---|---|---|
| Who acts | The application, at publish time | The trimmer/ILC, automatically (.NET 9+) |
| Effect in a normal JIT app | Can turn discovery off at runtime | No effect — guard returns the switch value |
| Effect in a trimmed/AOT app | Only if the app opts in | Always removes the branch (.NET 9+ apps) |
| Silences IL2026/IL3050 | No | Yes |

### Measured subtleties

**The attributes must be on separate properties for automatic removal.** Shape **C** (one
property) stayed PRESENT in default trimmed/AOT publishes. It does trim when the application
explicitly supplies a publish-time `false` switch value; combining the attributes is not
inherently invalid, but loses the no-configuration behaviour required here.

**IL4000 is unavoidable here.** A guard body reading `AppContext` is not a recognised shape. One
documented suppression per guard. Shape **D** (bare `RuntimeFeature.IsDynamicCodeSupported`)
avoids IL4000 but guards only `RequiresDynamicCode` and does nothing for `PublishTrimmed`.

**The toolchain follows the application, not the library.** A `net8.0` application uses the .NET 8
ILLink and ILC, which predate `[FeatureGuard]` and `[FeatureSwitchDefinition]` and ignore both. That
does not affect SqlClient 8.0, whose modern-.NET consumers all target `net10.0`+, but it shapes the
[backports](backporting.md).

### Substitution files

`ILLink.Substitutions.xml` works on every toolchain. Its entries fire when a named feature has a
given value. For .NET 10 apps the attributes alone suffice (measured), so a substitution file is
optional in 8.0. The backports need two kinds of entry:

```xml
<!-- App opted out explicitly (all toolchains) -->
<method signature="System.Boolean get_EnableAzureExtensionDiscovery()" body="stub" value="false"
        feature="Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery" featurevalue="false" />

<!-- Any NativeAOT publish, including .NET 8 (shape E / approach C5) -->
<method signature="System.Boolean get_IsAzureExtensionDiscoverySupported()" body="stub" value="false"
        feature="System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported" featurevalue="false" />
```

ILC sets `IsDynamicCodeSupported=false` for every AOT publish, so the second entry removes the
branch for .NET 8 AOT apps with no configuration (measured). It cannot help .NET 8
`PublishTrimmed`, where that feature is not set.
[A.5](experiments.md#a5-netstandard20-polyfill-and-analyzer-experiment) shows the attributes and
substitutions also work from `netstandard2.0`.

### A consequence reviewers should weigh

Because the guard is `false` for **any** trimmed publish, shape B also removes reflective
discovery and `app.config` reading from `PublishTrimmed` apps that are **not** using NativeAOT.
Measured with the 7.1.1 packages, those apps **already** have neither: the Azure assembly is trimmed away and
`ConfigurationManager` fails to initialize. The only apps that lose something are ones that rooted
the Azure assembly themselves (e.g. with `TrimmerRootAssembly`). They get their provider back from
the generated default-provider registration, or with one line:
`SetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault)`.

---

## Reflection APIs and why they break AOT

"Reflection" means inspecting or using types by *name at runtime* rather than by compile-time
reference.

### The calls in use

| Call | What it does | Why it is a problem |
|---|---|---|
| `Assembly.Load("Name")` / `Assembly.Load(AssemblyName)` | Loads an assembly by name | Nothing references the assembly; the trimmer may have deleted it entirely |
| `Assembly.GetType("Full.Name")` / `Type.GetType("Full.Name, Asm")` | Finds a type by name | The type may be gone. Used for Azure discovery and `app.config` provider/initializer entries |
| `Activator.CreateInstance(type)` | Constructs an object from a `Type` | Requires that the type *and its constructor* survived |
| `type.GetConstructor(...)` / `GetProperty("X")` / `GetMethod("X")` | Finds a member by signature or name | Members can be trimmed individually; under AOT they may lack reflection metadata even when compiled |
| `ConstructorInfo.Invoke` / `MethodInfo.Invoke` / `PropertyInfo.SetValue` | Calls a member found reflectively | Same reachability problem; exceptions arrive wrapped in `TargetInvocationException` |

### What this looks like on `main`

Azure discovery in `SqlAuthenticationProviderManager`'s static constructor (simplified):

```csharp
#if STRONG_NAME_SIGNING
var qualifiedName = new AssemblyName("Microsoft.Data.SqlClient.Extensions.Azure");
qualifiedName.SetPublicKeyToken(s_azurePublicKeyToken);    // 23ec7fc2d6eaa4a5
var assembly = Assembly.Load(qualifiedName);                 // netfx enforces the token at bind time
// on .NET the token is compared manually after loading
#else
var assembly = Assembly.Load("Microsoft.Data.SqlClient.Extensions.Azure");
#endif
Type? type        = assembly.GetType("Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider");
Type? optionsType = assembly.GetType("Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProviderOptions");
var instance = CreateAzureAuthenticationProvider(type, optionsType, applicationClientId, useWamBroker);
```

and `CreateAzureAuthenticationProvider` picks among the parameterless constructor, the
`(ActiveDirectoryAuthenticationProviderOptions)` constructor with `ApplicationClientId` /
`UseWamBroker` set via `GetProperty(...).SetValue`, and a legacy `(string)` constructor.

Beyond the AOT question:

- **Everything is a string.** Rename `UseWamBroker` and nothing fails to compile; it fails at
  runtime during authentication.
- **The probing exists for version tolerance** across Azure package versions. Requirement **R5**
  removes the need for that, which is what makes the typed factory in **B8** possible.

### The public key token check

Before using the discovered assembly, the driver verifies its **public key token**. This prevents
a hostile `.dll` with the right filename from being loaded *and used*. .NET Framework enforces the
token while binding. On .NET (Core) the runtime ignores it, so main compares the token after
`Assembly.Load` and before touching any type in the assembly. The check moves unchanged with the
bootstrap. The generated path (B7/B8) never loads by name at all.

### What replaces it

```csharp
// Registered by generated code (or one line of app code); reflection is only the fallback.
SqlAuthenticationProvider.TrySetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault);

// Inside CreateDefault: ordinary typed code.
var options = new ActiveDirectoryAuthenticationProviderOptions { ApplicationClientId = context.ApplicationClientId };
return new ActiveDirectoryAuthenticationProvider(options);
```

Renaming `UseWamBroker` now breaks the build, not the customer.

### The other reflection: Abstractions → SqlClient

`SqlAuthenticationProvider.SetProvider` — a public method in Abstractions — reaches a registry
that lives in SqlClient. Abstractions cannot reference SqlClient (a cycle; see
[primer](#assemblies-visibility-and-type-forwarding)), so it reflects:

```csharp
var assembly = Assembly.Load("Microsoft.Data.SqlClient");     // signature not verified (TODO in code)
Type? manager = assembly.GetType("Microsoft.Data.SqlClient.SqlAuthenticationProviderManager");
_setProvider = manager.GetMethod("SetProvider", BindingFlags.NonPublic | BindingFlags.Static);
...
_setProvider.Invoke(null, [authenticationMethod, provider]);
```

Under NativeAOT, `GetMethod` returns `null` (measured), so every public `SetProvider` call returns
`false`. Requirement **R3** forbids this bridge, and Phase 1 removes it.

---

## Assemblies, visibility and type forwarding

### Dependency direction

An assembly may only use types from assemblies it references, and references cannot form a cycle:

```
Microsoft.Data.SqlClient ───────────┐
                                    ├──> Extensions.Abstractions ──> Internal.Logging
Extensions.Azure ───────────────────┘
```

Abstractions sits at the bottom, so **both** SqlClient and Azure can call into it, while it can
call neither. A shared thing both sides must reach has to live at or below Abstractions.

**SqlClient does not reference Azure.** The application references Azure directly. That is why
discovery must be reflective (**R4**).

### `internal` and `InternalsVisibleTo`

`internal` members are visible only inside their own assembly. `InternalsVisibleTo` grants one
named friend access; with strong naming the grant must carry the friend's public key.

On `main`, Abstractions grants IVT only to its own test assembly (unsigned builds). SqlClient grants
IVT to `UnitTests` (with the test key in signed Package mode). **No production assembly sees
another's internals.** A4 keeps it that way. A3 would have needed an
`Abstractions → SqlClient` IVT grant for config seeding.

### Type forwarding

When a public type moves between assemblies, already-compiled applications still look for it in
the old one. A **type forward** is a signpost left behind:

```csharp
// in Microsoft.Data.SqlClient (TypeForwards.Abstractions.cs already has five of these)
[assembly: TypeForwardedTo(typeof(SqlAuthenticationInitializer))]   // "it lives in Abstractions now"
```

Two rules shape this proposal:

> 1. A forward can only point at an assembly the forwarder **already references** — so types move
>    *down* the dependency chain, never *up*.
> 2. The forwarder must be able to **see** the type: `typeof(SomeInternalType)` from another
>    assembly is CS0122 (measured).

Rule 1 is why `SqlAuthenticationInitializer` (public) can move into Abstractions, and why
"declarations in Abstractions, definitions higher up" is impossible. Rule 2 means the section
handlers must become public for this move without a production friend-assembly relationship.
Their type forwards preserve existing `app.config` names ending in
`…, Microsoft.Data.SqlClient` (see [Role of the configuration section types](design.md#role-of-the-configuration-section-types)).

### Module initializers

A `[ModuleInitializer]` method runs automatically the first time its assembly is loaded:

```csharp
[ModuleInitializer]
internal static void Init() => /* register something */;
```

Caveat: **if the assembly was trimmed away, it never runs.** That is why B5 fails and B7
(generated code in the *application*) succeeds.

### Polyfills

`[FeatureGuard]` and friends are .NET 9 types, but Abstractions targets `netstandard2.0`. A
**polyfill** declares them yourself:

```csharp
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class FeatureGuardAttribute : Attribute
    {
        public FeatureGuardAttribute(Type featureType) => FeatureType = featureType;
        public Type FeatureType { get; }
    }
}
```

ILLink and ILC identify attributes by **full type name**, so an internal copy is treated exactly
like the real one (measured, [A.5](experiments.md#a5-netstandard20-polyfill-and-analyzer-experiment)).
`main`'s Logging package already ships `UnconditionalSuppressMessageAttribute` this way (#4703).
**The Roslyn analyzer, however, does not run on `netstandard2.0` at all**, so polyfilled
annotations are checked only by an app publish. That is why C6 adds a `net10.0` TFM to
Abstractions, and why the backports need a separate analysis build.

### Reference assemblies

The `ref/` projects describe the public API surface. This proposal relocates
`SqlAuthenticationInitializer` behind a forward and adds the default-provider factory
(`SetDefaultProviderFactory`, `TrySetDefaultProviderFactory`, `SqlAuthenticationProviderFactoryContext`)
plus `ActiveDirectoryAuthenticationProvider.CreateDefault`. The extension packages document their
API in their own `doc/` folders.

---

## NuGet packaging and versioning

### Version ranges

A dependency is a **range**, not a single version. The SqlClient family emits:

```xml
Version="[7.1.0, 8.0.0)"
```

which reads "at least 7.1.0, and below 8.0.0". NuGet picks the lowest version satisfying everyone.
A bare version such as `1.0.0` means "at least 1.0.0" with **no** upper bound — which is what
SqlClient 7.0.0/7.0.1 and Azure 1.0.0 shipped with.

If an app references SqlClient 7.1 and Azure 7.0:

| Package | Wants Abstractions |
|---|---|
| SqlClient 7.1 | `[7.1.0, 8.0.0)` |
| Azure 7.0 | `[7.0.0, 8.0.0)` |

Abstractions 7.1.0 satisfies both, so restore succeeds silently. There is no downgrade, so not
even NU1605 fires — and because SqlClient does not depend on Azure at all, nothing can drag Azure
forward. Requirement **R5** closes this by policy; E2 enforces it at build time and E3 at
runtime.

### Assembly versions carry only the major

Family assemblies use a major-only `AssemblyVersion` (`$(SqlClientAssemblyVersion)` =
`<major>.0.0.0`): the 7.1.1 Azure assembly binds as `Version=7.0.0.0`, so `AssemblyName.Version`
can't tell 7.0.3 from 7.1.1. The exact version is in
`AssemblyInformationalVersionAttribute` (`7.1.1+<commit>`) and `AssemblyFileVersionAttribute`
(`7.1.1.26272`), which is what the runtime check (E3) reads. The build-time check (E2) compares
package versions.

### What lives inside a package

| Path | Purpose |
|---|---|
| `lib/<tfm>/` | The DLLs the app compiles and runs against |
| `build/<tfm>/` | MSBuild `.props`/`.targets` run in the **directly** referencing project |
| `buildTransitive/<tfm>/` | Same, but also flows to projects that reference that project |
| `analyzers/dotnet/cs/` | Roslyn analyzers and source generators |

Relevant details on `main`:

- SqlClient's package is built from a **hand-maintained `.nuspec`**
  (`src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.nuspec`) that lists files explicitly,
  including 13 per-locale resource assemblies per TFM. It ships **no** `build/` folder. It declares
  `System.Configuration.ConfigurationManager` with `exclude="Compile"` on `net8.0`/`net9.0`
  (`net10.0` in 8.0). This relocation removes that compile exclusion so the public configuration
  APIs can be consumed.
- Abstractions and Azure pack from their `.csproj`. Adding a generator to Azure is one `<None
  Pack="true" …>` item. Abstractions' configuration dependency flows compile assets because its
  public section handlers expose types from that dependency.
- The repo has no analyzer-packaging precedent today.
- Abstractions is the one package present in every family graph, which is why E2's version
  check ships in its `build/` and `buildTransitive/` folders.

### Source generators

A source generator is a compiler plug-in that *adds* C# to the application's compilation during
build. Shipped in `analyzers/dotnet/cs`, it is picked up automatically by anyone referencing the
package. For this proposal it would emit something like:

```csharp
// <auto-generated/> — added to the application's own compilation
[ModuleInitializer]
internal static void __SqlClientAzureDefaultProvider() =>
    SqlAuthenticationProvider.TrySetDefaultProviderFactory(ActiveDirectoryAuthenticationProvider.CreateDefault);
```

The key property is *where it lands*: the application's own code, so it is always rooted, and the
direct method reference keeps the provider alive through trimming. Further benefits over reflection are
compile-time errors instead of runtime ones, and the ability to emit a clear build diagnostic
(e.g. WAM broker requested with an incompatible package).

The rejected cheaper variant — MSBuild targets that inject a `.cs` file — achieves the rooting but
cannot produce diagnostics and risks colliding with the user's own code.

---

## The SqlClient authentication pipeline

### The packages (`main`)

| Package | Contains | Targets | Notes |
|---|---|---|---|
| `Microsoft.Data.SqlClient` | The driver, the registry (`SqlAuthenticationProviderManager`), the config section types, `SqlAuthenticationInitializer` | `net462`, `net8.0`, `net9.0` (8.0: `net462`, `net10.0`) | Packs from a nuspec |
| `…Extensions.Abstractions` | `SqlAuthenticationProvider`, `SqlAuthenticationMethod`, parameters, token, exception, and the reflective bridge | `netstandard2.0` (8.0: + `net10.0`) | Six source files; no resources |
| `…Extensions.Azure` | `ActiveDirectoryAuthenticationProvider` and its options | `net462`, `netstandard2.0` | Depends on `Azure.Core`, MSAL, MSAL Broker, `Caching.Memory`; optional, referenced by the app |
| `…Internal.Logging` | `SqlClientEventSource` | `netstandard2.0` | Bottom of the stack |

The optional split is deliberate: apps that never use Entra ID should not carry MSAL and the
Azure SDK. Undoing it was approach **B4**, rejected for that reason.

### The moving parts

- **Provider** — knows how to get a token. `ActiveDirectoryAuthenticationProvider` is one; an
  application or third party may supply its own.
- **Registry** — a `ConcurrentDictionary` from auth method to provider, plus a
  `HashSet` of methods claimed by `app.config`.
- **Bootstrap** — on `main`, the manager's static constructor (reads config, constructs the
  instance, then discovers Azure) and instance constructor (reads `applicationClientId`,
  `useWamBroker`, runs the initializer, loads config providers).
- **Bridge** — `SqlAuthenticationProvider.Internal` in Abstractions, reflecting into the manager.
- **Default-provider factory** (proposed) — a public registration point for the function that builds
  the default provider, used by apps, libraries and generated code.

### Where providers come from

**1. The Azure package, discovered automatically.** No app code; the auto-magic behaviour **R1**
protects.

**2. The application, programmatically:**

```csharp
SqlAuthenticationProvider.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryDefault, myProvider);
```

**3. `app.config`, declaratively** — loaded reflectively by type name. Note that the section
*handler* is also named by type, which pins it to the SqlClient assembly:

```xml
<configSections>
  <section name="SqlClientAuthenticationProviders"
           type="Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient" />
</configSections>
<SqlClientAuthenticationProviders applicationClientId="..." useWamBroker="true" initializerType="MyCompany.Init, MyCompany.Auth">
  <providers>
    <add name="Active Directory Interactive" type="MyCompany.MyProvider, MyCompany.Auth" />
  </providers>
</SqlClientAuthenticationProviders>
```

Measured on .NET 10 with the 7.1.1 packages: config wins over everything, `SetProvider` returns `false` for a config-claimed
method, and the Azure default never overwrites a provider registered earlier. Preserving that is
what D1/D3 formalise.

### The sequence on `main`

```
first touch of SqlAuthenticationProviderManager — from EITHER:
  • SqlAuthenticationProvider.GetProvider/SetProvider (via the reflective bridge), OR
  • SqlConnection.Open() → … → SqlConnectionInternal.GetFedAuthToken()
      └─ static constructor
          ├─ read app.config section (ConfigurationManager)
          ├─ new manager: clientId, useWamBroker, run initializer (Instance still null!), config providers
          ├─ Instance = manager
          └─ Azure discovery → SetProvider(...) for each AD method (refused where config claimed)
then:
  registry.GetProvider(method) → provider.AcquireTokenAsync(...)   ← MSAL work happens here
```

"Fed-auth" (federated authentication) is the `GetFedAuthToken` path — reached only by
Entra-authenticated connections, never by SQL-auth or Windows-auth ones.

### Why the static constructor matters

In .NET a static constructor is guaranteed to run before the first access to its type, so *any*
touch of the registry runs discovery first, automatically and in the right order. A design that
defers the bootstrap to the fed-auth path loses that property and produces the two
[ordering hazards](design.md#ordering-hazards-any-implementation-must-avoid).
