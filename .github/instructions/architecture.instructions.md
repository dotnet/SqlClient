---
applyTo: "**"
---
# Microsoft.Data.SqlClient Architecture

## Project Structure

This repository contains the official Microsoft ADO.NET data provider for SQL Server. The driver is built from a **single unified project** that multi-targets all supported frameworks.

```
src/
├── Microsoft.Data.SqlClient/
│   ├── add-ons/                    # Azure Key Vault provider
│   ├── netcore/                    # ⚠️ LEGACY - being phased out
│   │   └── ref/                    # Reference assemblies for .NET Core/.NET
│   ├── netfx/                      # ⚠️ LEGACY - being phased out
│   │   └── ref/                    # Reference assemblies for .NET Framework
│   ├── ref/                        # Shared reference assembly files
│   ├── src/                        # ✅ PRIMARY - Unified source for all platforms
│   │   ├── Microsoft.Data.SqlClient.csproj  # Multi-target project file
│   │   ├── Interop/               # P/Invoke and native interop
│   │   ├── Microsoft/Data/SqlClient/        # Main driver source
│   │   ├── Resources/             # Embedded resources and strings
│   │   ├── System/                # System-level helpers
│   │   └── TypeForwards.netcore.cs          # Type forwarding for .NET Core
│   └── tests/                      # All test projects
│       ├── FunctionalTests/        # Tests without SQL Server dependency
│       ├── ManualTests/            # Integration tests requiring SQL Server
│       └── UnitTests/              # Unit tests
├── Microsoft.Data.SqlClient.Extensions/  # Extension libraries
└── Microsoft.SqlServer.Server/           # SQL Server CLR types
```

## Unified Project Model

### Architecture Goal
The driver is transitioning away from separate `netfx/` and `netcore/` project files toward a **single unified project** at `src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj`. This project multi-targets all supported frameworks from one codebase:

```xml
<TargetFrameworks>net462;net8.0;net9.0</TargetFrameworks>
```

**All new code MUST go into `src/Microsoft.Data.SqlClient/src/`**. Do NOT add files to the legacy `netcore/src/` or `netfx/src/` directories.

### Legacy Folders
The `netcore/` and `netfx/` directories are legacy artifacts from the old dual-project model:
- `netcore/src/` and `netfx/src/` — **DEPRECATED**. These contain legacy project files that are being phased out. Do not add new code here.
- `netcore/ref/` and `netfx/ref/` — **STILL ACTIVE**. Reference assemblies remain in these directories and define the public API surface for each target framework.

### OS Targeting with `TargetOs`
The unified project uses a `TargetOs` MSBuild property to handle OS-specific compilation:

```xml
<!-- Automatic OS detection -->
<TargetOs Condition="... '.NETFramework'">Windows_NT</TargetOs>  <!-- .NET Framework always targets Windows -->
<TargetOs Condition="'$(TargetOs)' == ''">$(OS)</TargetOs>        <!-- .NET uses host OS by default -->
```

This defines preprocessor constants:
- `_WINDOWS` — Defined when `TargetOs` is `Windows_NT`
- `_UNIX` — Defined when `TargetOs` is `Unix`

> **NOTE**: These constants are prefixed with `_` (underscore) to avoid conflict with .NET 5+ built-in OS-specific target framework preprocessor flags.

### Platform-Specific Files
The driver supports both .NET Framework and .NET Core/.NET 8+. Platform-specific code uses file suffixes:
- `.netfx.cs` — .NET Framework only (compiled when targeting `net462`)
- `.netcore.cs` — .NET Core/.NET only (compiled when targeting `net8.0`/`net9.0`)
- `.windows.cs` — Windows only (compiled when `_WINDOWS` is defined)
- `.unix.cs` — Unix/Linux/macOS only (compiled when `_UNIX` is defined)

All platform-specific files live in the unified `src/` directory alongside shared code.

### Conditional Compilation
When writing code that differs by platform, use these preprocessor directives:

| Directive | When to Use |
|-----------|------------|
| `#if NETFRAMEWORK` | Code for .NET Framework (`net462`) only |
| `#if NET` | Code for .NET Core/.NET 8+ only |
| `#if _WINDOWS` | Code for Windows OS (any framework) |
| `#if _UNIX` | Code for Unix/Linux/macOS OS (any framework) |

Guidelines:
1. All code must compile for all target frameworks (`net462`, `net8.0`, `net9.0`)
2. Use `#if NETFRAMEWORK` or `#if NET` for framework-specific code paths
3. Use `#if _WINDOWS` or `#if _UNIX` for OS-specific code paths
4. Avoid APIs that don't exist on a target platform without conditional compilation
5. Prefer `#if NET` over `#if NETCOREAPP` for .NET (net8.0/net9.0) code paths to keep conditions consistent

### Framework-Specific Dependencies
The unified project uses conditional `ItemGroup` elements for dependencies:

- **net462**: References `System.Configuration`, `System.EnterpriseServices`, `System.Transactions`, plus `Microsoft.Data.SqlClient.SNI` native package
- **net8.0/net9.0**: References `Microsoft.Data.SqlClient.SNI.runtime`, `System.Configuration.ConfigurationManager`, `Microsoft.SqlServer.Server`
- **Shared**: `Azure.Core`, `Azure.Identity`, `Microsoft.Bcl.Cryptography`, `Microsoft.Extensions.Caching.Memory`, `Microsoft.IdentityModel.*`, `System.Security.Cryptography.Pkcs`

### Reference Assemblies
The `ref/` directories define the public API surface:
- `netcore/ref/` — Public APIs for .NET Core/.NET (includes `Microsoft.Data.SqlClient.cs`, `Microsoft.Data.SqlClient.Manual.cs`)
- `netfx/ref/` — Public APIs for .NET Framework (includes `Microsoft.Data.SqlClient.cs`)
- `ref/` — Shared reference assembly files (e.g., `Microsoft.Data.SqlClient.Batch.cs`, `Microsoft.Data.SqlClient.Batch.NetCoreApp.cs`)

**IMPORTANT**: Any public API changes MUST update the corresponding reference assembly in the appropriate `ref/` directory.

### Build Output
Build artifacts are organized by framework and OS:
```
artifacts/Microsoft.Data.SqlClient/{Configuration}/{TargetOs}/{TargetFramework}/
```

## SNI (SQL Server Network Interface) Layer

Two implementations exist:

### Managed SNI
- Cross-platform managed code implementation
- Located in `src/Microsoft/Data/SqlClient/ManagedSni/`
- Used by default on Unix platforms
- Can be opted into on Windows via `UseManagedSNIOnWindows` connection string option

### Native SNI
- Windows-only native library (C++)
- Shipped as separate NuGet packages:
  - `Microsoft.Data.SqlClient.SNI` — For .NET Framework (`net462`)
  - `Microsoft.Data.SqlClient.SNI.runtime` — For .NET Core/.NET on Windows
- Provides optimal performance on Windows

## Key Components

### SqlConnection
Entry point for database connectivity. Manages:
- Connection string parsing (via `SqlConnectionStringBuilder`)
- Connection pooling integration
- Transaction enlistment (local and distributed)
- Authentication (SQL, Windows, Azure AD)

### SqlCommand
Executes SQL statements and stored procedures:
- Batch execution support (`SqlBatch`)
- Async methods (`ExecuteReaderAsync`, `ExecuteNonQueryAsync`, etc.)
- Always Encrypted parameter encryption
- Query metadata caching

### TdsParser
Core TDS protocol implementation:
- Packet encoding/decoding
- Protocol state machine
- Feature negotiation
- Token parsing (see `TdsEnums.cs` for token definitions)

### Connection Pooling
Located in `ConnectionPool/`:
- `ChannelDbConnectionPool` — Modern async-friendly pool using System.Threading.Channels
- `WaitHandleDbConnectionPool` — Traditional pool using wait handles
- Pool groups manage pools for different connection strings

### Authentication Providers
Located in `SSPI/` and authentication-related files:
- Windows Authentication (SSPI/Kerberos)
- SQL Server Authentication
- Azure Active Directory authentication modes

### Always Encrypted
Column-level encryption implementation:
- Certificate-based key providers
- Azure Key Vault integration (add-ons package)
- Secure enclave support (VBS, SGX)

## Package Products

| Package | Description |
|---------|-------------|
| Microsoft.Data.SqlClient | Main driver package |
| Microsoft.Data.SqlClient.SNI | Native SNI for .NET Framework |
| Microsoft.Data.SqlClient.SNI.runtime | Native SNI runtime for .NET |
| Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider | AKV integration |
| Microsoft.SqlServer.Server | SQL CLR types |

## Dependencies and Framework Support

- .NET Framework 4.6.2+
- .NET 8.0+
- See `Directory.Packages.props` for centralized package version management
