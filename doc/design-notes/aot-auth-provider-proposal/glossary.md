# Glossary

> Part of the [AOT-safe authentication provider registration](README.md) proposal.
> **Audience:** anyone meeting an unfamiliar term. Concepts are explained at length in the
> [primer](primer.md).

## Trimming and AOT

| Term | Plain meaning |
|---|---|
| **JIT** (Just-In-Time) | Normal .NET. The app ships as IL (bytecode) and the runtime compiles it to machine code as it runs. Everything is present at runtime, so reflection can find anything. |
| **AOT** (Ahead-Of-Time) / **NativeAOT** | The app is compiled to a single native executable at *publish* time. There is no IL left and no compiler at runtime. Anything the compiler could not see is gone. See [primer](primer.md#trimming-and-nativeaot-explained). |
| **Trimming** | Deleting code that the publish-time analysis proves is never called, to shrink the output. Enabled with `PublishTrimmed`. NativeAOT always trims. |
| **ILLink** | The trimmer used for `PublishTrimmed`. Reads your IL and removes unreachable code. Its version follows the **application's** target framework. |
| **ILC** | The NativeAOT compiler. Does the same reachability analysis as ILLink, then emits native code. Also versioned with the application's target framework. |
| **Rooted** / **reachable** | Code the analysis can prove is used, by following real method calls and type references from the app's entry point. Rooted code is kept; everything else is deleted. |
| **Reflection metadata** | Under NativeAOT, a compiled method is not automatically discoverable by `GetMethod`; ILC must decide to emit metadata for it. Main's bridge fails on exactly this. |
| **Statically traceable** | Written so the analysis *can* follow it — a direct `new Foo()` rather than a type name in a string. |
| **Trim-safe / AOT-safe** | Code that behaves identically before and after trimming. |
| **Silent failure** | The failure mode in [#4193](https://github.com/dotnet/SqlClient/issues/4193): the code was trimmed away, the only build output was a generic per-assembly warning, and the app failed at runtime with a confusing error. |
| **`TrimmerSingleWarn`** | MSBuild property, default `true` for libraries not marked trimmable: collapses all of an assembly's trim warnings into one IL2104 (or IL3053 for AOT). Set `false` to see individual warnings. |
| **`TrimmerRootAssembly`** | MSBuild item that tells the trimmer to keep an entire assembly regardless of references. |

## The gating mechanism

| Term | Plain meaning |
|---|---|
| **Feature switch** | A named on/off flag. The app can set it at publish time and the trimmer will then delete the code behind it. **Opt-in** — nothing happens unless the app sets it. See [primer](primer.md#feature-switches-and-feature-guards). |
| **Feature guard** | A property marked `[FeatureGuard(...)]` that tells the trimmer "the code behind this needs capabilities trimming removes." The .NET 9+ trimmer and ILC treat it as `false` **automatically** whenever trimming is on. The .NET 8 tools ignore it (relevant only to the backports). |
| **Substitution** | What the trimmer does to a switch or guard: it replaces the property body with a hard-coded `return false`, making the `if` a dead branch it can then delete. |
| **`RuntimeHostConfigurationOption`** | The MSBuild item an app uses to set a feature switch. With `Trim="true"` the value is also visible to the trimmer, not just at runtime. |
| **`ILLink.Substitutions.xml`** | An XML file embedded in a library that declares substitutions keyed on feature values. Works on every toolchain version. |
| **`[RequiresUnreferencedCode]`** (RUC) | "This method uses reflection in a way trimming can break." Warns callers. |
| **`[RequiresDynamicCode]`** (RDC) | "This method needs to generate code at runtime." Warns callers under AOT. |
| **`RuntimeFeature.IsDynamicCodeSupported`** | Runtime property: `true` under JIT, `false` under NativeAOT. ILC also exposes it as a feature value that substitutions can key on. |

## Warning codes

| Code | Meaning | Raised where |
|---|---|---|
| **IL2026** | You are calling a method marked `[RequiresUnreferencedCode]`. | At the call site |
| **IL2057 / IL2067 / IL2070 / IL2072 / IL2075** | Reflection whose target the trimmer cannot see (string type names, un-annotated `Type` values). | At the reflective call |
| **IL2104** | "Assembly X produced trim warnings" — the collapsed form under `TrimmerSingleWarn`. | Once per assembly |
| **IL3050** | You are calling a method marked `[RequiresDynamicCode]`. | At the call site |
| **IL3053** | "Assembly X produced AOT analysis warnings" — collapsed AOT form. | Once per assembly |
| **IL4000** | A `[FeatureGuard]` property's body isn't a shape the analyzer recognises. | On the guard property |
| **IL1013** | ILC failed while processing linker XML. Seen once in the backport measurements, as an intermittent ILC 9.0.20 crash. | ILC |
| **NETSDK1210** | Trim/AOT analyzers requested for a TFM that cannot run them (e.g. `netstandard2.0`). | At build |
| **CS0122** | Inaccessible type — here, `typeof` of an internal type in another assembly. | At compile |
| **NU1605** | NuGet package downgrade. Mentioned only to note it does **not** fire for our mixed-version case. | At restore |

## Assemblies and packages

| Term | Plain meaning |
|---|---|
| **Assembly** | One compiled `.dll`. A NuGet **package** is a shipping container that usually holds one. |
| **BCL** | Base Class Library — the types built into .NET itself (`System.*`). |
| **TFM** (Target Framework Moniker) | Which .NET a project builds for: `net462`, `net8.0`, `net9.0`, `net10.0`, `netstandard2.0`. SqlClient 8.0 targets `net462` and `net10.0`. |
| **`netstandard2.0`** | A common denominator both .NET Framework and modern .NET can consume. Abstractions and Logging target only this. |
| **Multi-targeting** | Building one project for several TFMs at once. |
| **Polyfill** | Declaring a type yourself with the same full name as one from a newer .NET, so older targets can compile. Tooling matches **by name**, so it works. See [primer](primer.md#assemblies-visibility-and-type-forwarding). |
| **`InternalsVisibleTo`** (IVT) | An attribute letting one named assembly see another's `internal` members. |
| **Type forwarding** | A marker saying "this type moved to another assembly" so old compiled code still finds it. Only works **downward**, and only for types the forwarder can see. |
| **`[ModuleInitializer]`** | A method that runs automatically the first time its assembly is loaded. |
| **Ref assembly** | A compile-time-only `.dll` describing the public API surface. Changes to public API must be mirrored there. |
| **Strong naming / public key token** | A cryptographic identity stamped on an assembly, used here to check the Azure extension isn't an impostor. |
| **Source generator** | A compiler plug-in that adds C# source to the app's own compilation at build time. See [primer](primer.md#nuget-packaging-and-versioning). |
| **Configuration section handler** | The `ConfigurationSection` subclass that `<configSections>` in `app.config` names by assembly-qualified type name; `System.Configuration` instantiates it reflectively. |

## Domain terms

| Term | Plain meaning |
|---|---|
| **Provider** | An object that obtains an auth token — concretely `SqlAuthenticationProvider`. |
| **Registry** | The dictionary mapping each auth method to its provider. Backs the public `GetProvider`/`SetProvider`. On `main` it lives inside `SqlAuthenticationProviderManager`; this proposal moves it to Abstractions. |
| **Bridge** | `SqlAuthenticationProvider.Internal` in Abstractions: the reflective path from the public API to the registry in SqlClient. |
| **Bootstrap** | The one-time startup code that populates the registry from `app.config` and from the Azure package. On `main`: the manager's static and instance constructors. |
| **Initializer** | A user `SqlAuthenticationInitializer` named in `app.config` (`initializerType`), run once during bootstrap. |
| **Discovery** | The bootstrap finding the Azure package at runtime by name, without a compile-time reference. |
| **Default-provider factory** | A public registration point (`SetDefaultProviderFactory`/`TrySetDefaultProviderFactory`) for the function that builds the default provider, so discovery isn't the only way to obtain one. Apps, libraries and generated code all use it. |
| **Fed-auth path** | The code that runs when a connection actually authenticates with Entra ID. |
| **Tier** | Whether a registration came from config, the user, or the Azure default — which decides who wins. |
| **Entra ID** | Microsoft's cloud identity service (formerly Azure Active Directory). |
| **MSAL** | Microsoft Authentication Library — what the Azure provider uses to get tokens. |
| **WAM broker** | A Windows component that can perform interactive sign-in on the app's behalf. |
| **Lockstep** | The policy that all SqlClient-family packages ship and upgrade at the same version. |
