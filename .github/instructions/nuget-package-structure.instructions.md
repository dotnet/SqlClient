---
applyTo: "**/nuspec,**/build.proj,**/ref/**"
---
# NuGet Package Structure — Microsoft.Data.SqlClient

This document describes the folder layout of the generated `Microsoft.Data.SqlClient` NuGet package, what each folder holds, and how runtime resolution works.

## Package Resolution Overview

- **`ref/`** — Used at **compile time only**. Thin assemblies with `throw null` bodies defining the public API surface. NuGet selects the best-matching TFM.
- **`runtimes/{rid}/lib/`** — Used at **runtime** when the host OS RID matches. Contains full implementations with OS-specific code. These **override** the corresponding `lib/` assemblies.
- **`lib/`** — Used at **runtime** as a **fallback** when no RID-specific match exists in `runtimes/`. For `net462` this is the real implementation. For `net8.0`/`net9.0` these are AnyOS stubs. For `netstandard2.0` this is the only runtime target.

## Folder Structure

```
Microsoft.Data.SqlClient.nupkg
│
├── ref/                                # Compile-time reference assemblies (throw null bodies, no real implementation)
│   ├── net462/                         # Built from netfx/ref, Windows_NT
│   │   ├── Microsoft.Data.SqlClient.dll    # NetFx ref assembly — public API surface for .NET Framework
│   │   └── Microsoft.Data.SqlClient.xml    # XML doc comments for IntelliSense
│   ├── net8.0/                         # Built from netcore/ref, AnyOS
│   │   ├── Microsoft.Data.SqlClient.dll    # NetCore ref assembly — public API surface for .NET 8
│   │   └── Microsoft.Data.SqlClient.xml    # XML doc comments for IntelliSense
│   ├── net9.0/                         # Built from netcore/ref, AnyOS
│   │   ├── Microsoft.Data.SqlClient.dll    # NetCore ref assembly — public API surface for .NET 9
│   │   └── Microsoft.Data.SqlClient.xml    # XML doc comments for IntelliSense
│   └── netstandard2.0/                 # Built from netcore/ref, AnyOS
│       ├── Microsoft.Data.SqlClient.dll    # NetStandard ref assembly — public API surface for netstandard2.0
│       └── Microsoft.Data.SqlClient.xml    # XML doc comments for IntelliSense
│
├── lib/                                # Default runtime assemblies (used when no RID-specific match in runtimes/)
│   ├── net462/                         # Built from netfx/src, Windows_NT
│   │   ├── Microsoft.Data.SqlClient.dll        # Full .NET Framework implementation (Windows-only)
│   │   ├── Microsoft.Data.SqlClient.pdb        # Debug symbols
│   │   ├── Microsoft.Data.SqlClient.xml        # XML doc comments
│   │   └── {locale}/                           # cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, tr, zh-Hans, zh-Hant
│   │       └── Microsoft.Data.SqlClient.resources.dll  # Localized satellite resource DLLs
│   ├── net8.0/                         # Built from netcore/src, OSGroup=AnyOS
│   │   ├── Microsoft.Data.SqlClient.dll        # AnyOS stub — generated via GenAPI/NotSupported.targets
│   │   ├── Microsoft.Data.SqlClient.pdb        # Debug symbols
│   │   ├── Microsoft.Data.SqlClient.xml        # XML doc comments (from Windows build)
│   │   └── {locale}/                           # Localized satellite resource DLLs (from Windows build)
│   │       └── Microsoft.Data.SqlClient.resources.dll
│   ├── net9.0/                         # Built from netcore/src, OSGroup=AnyOS
│   │   ├── Microsoft.Data.SqlClient.dll        # AnyOS stub — same as net8.0
│   │   ├── Microsoft.Data.SqlClient.pdb        # Debug symbols
│   │   ├── Microsoft.Data.SqlClient.xml        # XML doc comments (from Windows build)
│   │   └── {locale}/                           # Localized satellite resource DLLs (from Windows build)
│   │       └── Microsoft.Data.SqlClient.resources.dll
│   └── netstandard2.0/                 # Built from netcore/ref with BuildForLib=true, AnyOS
│       ├── Microsoft.Data.SqlClient.dll        # ⚠️ Currently a ref assembly (throw null bodies) — see known issue below
│       ├── Microsoft.Data.SqlClient.pdb        # Debug symbols
│       └── Microsoft.Data.SqlClient.xml        # XML doc comments
│
└── runtimes/                           # RID-specific runtime assemblies (override lib/ when RID matches)
    ├── win/lib/                        # Windows-specific implementations (native SNI)
    │   ├── net462/
    │   │   ├── Microsoft.Data.SqlClient.dll    # Full .NET Framework impl (same as lib/net462)
    │   │   └── Microsoft.Data.SqlClient.pdb    # Debug symbols
    │   ├── net8.0/
    │   │   ├── Microsoft.Data.SqlClient.dll    # Full .NET 8 impl compiled for Windows_NT
    │   │   └── Microsoft.Data.SqlClient.pdb    # Debug symbols
    │   └── net9.0/
    │       ├── Microsoft.Data.SqlClient.dll    # Full .NET 9 impl compiled for Windows_NT
    │       └── Microsoft.Data.SqlClient.pdb    # Debug symbols
    └── unix/lib/                       # Unix/Linux/macOS implementations (managed SNI)
        ├── net8.0/
        │   ├── Microsoft.Data.SqlClient.dll    # Full .NET 8 impl compiled for Unix
        │   └── Microsoft.Data.SqlClient.pdb    # Debug symbols
        └── net9.0/
            ├── Microsoft.Data.SqlClient.dll    # Full .NET 9 impl compiled for Unix
            └── Microsoft.Data.SqlClient.pdb    # Debug symbols
```

## Known Issue: `lib/netstandard2.0/` Assembly

The `lib/netstandard2.0/` DLL is intended to be a `PlatformNotSupportedException` stub for unsupported platforms. However, it is built by the `BuildNetStandard` target, which builds the **ref project** (`netcore/ref/`) with `BuildForLib=true` and `OSGroup=AnyOS`.

The `NotSupported.targets` file is imported, but its logic is gated on `GeneratePlatformNotSupportedAssemblyMessage` being set — a property that only the **src project** (`netcore/src/`) defines. Since the ref project never sets it, the `GenerateNotSupportedSource` target is inert, and the output is just the ref assembly with `throw null` bodies instead of a proper `PlatformNotSupportedException` stub.
