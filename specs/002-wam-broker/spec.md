# Feature Specification: WAM Broker Support for Entra ID Authentication

**Feature Branch**: `dev/automation/wam-broker-support`  
**Created**: 2026-05-20  
**Status**: Draft  
**References**:

- PR [#2884](https://github.com/dotnet/SqlClient/pull/2884) (original POC, closed)
- PR [#3874](https://github.com/dotnet/SqlClient/pull/3874) (updated POC, closed)
- ICM 781210079 (Authentication failure on persistent AVD with Conditional Access)

## Problem Statement

Microsoft.Data.SqlClient's `ActiveDirectoryIntegrated` and other Public Client Application (PCA) authentication flows do not pass device information when acquiring tokens. This causes failures on persistent Azure Virtual Desktop (AVD) devices when Conditional Access Policies require device compliance or MFA based on device state.

### Root Cause

MSAL's `AcquireTokenByIntegratedWindowsAuth` does not pass device claims to the identity provider. The Windows Web Account Manager (WAM) broker passes device information (PRT, device compliance state) to Entra ID, satisfying Conditional Access policies.

### MSAL PCA Compliance

Microsoft identity platform requires first-party applications using Public Client Applications to use WAM broker on Windows for compliance. This ensures:

- Device-based Conditional Access policies work correctly
- Primary Refresh Token (PRT) is leveraged for SSO
- Device compliance state is included in token requests

## Design

### Target Location

The `ActiveDirectoryAuthenticationProvider` is in `src/Microsoft.Data.SqlClient.Extensions/Azure/src/`. This package targets `net462;netstandard2.0`.

### Platform Support Matrix

| Platform | WAM Broker | Fallback |
| ---------- | ----------- | ---------- |
| Windows (.NET Framework 4.6.2+) | ✅ Supported | IWA (legacy) |
| Windows (.NET 8.0+ via netstandard2.0) | ✅ Supported | System browser |
| Linux/macOS (.NET via netstandard2.0) | ❌ Not available | System browser / IWA |

### Authentication Modes Covered

| Mode | WAM Broker Behavior |
| ------ | ------------------- |
| `ActiveDirectoryInteractive` | Uses WAM for interactive token acquisition on Windows |
| `ActiveDirectoryIntegrated` | Uses WAM broker to pass device claims (solves CAP issues) |
| `ActiveDirectoryDeviceCodeFlow` | Uses WAM for device code flow on Windows |
| `ActiveDirectoryPassword` | Uses WAM for username/password flow on Windows |
| `ActiveDirectoryDefault` | No change (uses Azure.Identity DefaultAzureCredential) |
| `ActiveDirectoryManagedIdentity` | No change (server-side, no WAM needed) |
| `ActiveDirectoryServicePrincipal` | No change (confidential client, no WAM needed) |
| `ActiveDirectoryWorkloadIdentity` | No change (workload identity, no WAM needed) |

### Architecture Changes

1. **Make class `partial`**: Split `ActiveDirectoryAuthenticationProvider` into platform-specific files
2. **Add WAM broker**: Configure `BrokerOptions` on `PublicClientApplicationBuilder` on Windows
3. **Parent window handle**: Provide window handle for WAM dialog (required by WAM on Windows)
4. **Cross-platform `SetParentActivityOrWindow`**: Add a cross-platform `Func<object>` API for parenting broker UI (in addition to the existing .NET Framework-only `SetIWin32WindowFunc`)
### New Public APIs

```csharp
public sealed partial class ActiveDirectoryAuthenticationProvider : SqlAuthenticationProvider
{
    // Cross-platform API to set the parent window/activity for WAM dialog
    // On Windows: accepts an IntPtr window handle (and on .NET Framework also accepts IWin32Window)
    // On Unix: no-op (WAM not available)
    public void SetParentActivityOrWindow(Func<object> parentActivityOrWindowFunc);
}
```

### Dependencies

- **New**: `Microsoft.Identity.Client.Broker` (same version as `Microsoft.Identity.Client`: 4.83.0)
- Conditional on Windows platform at runtime (the package includes platform-specific native binaries)

### File Changes

| File | Change |
| ------ | -------- |
| `Directory.Packages.props` | Add `Microsoft.Identity.Client.Broker` version |
| `Azure.csproj` | Add package reference |
| `ActiveDirectoryAuthenticationProvider.cs` | Make partial, add broker logic |
| `ActiveDirectoryAuthenticationProvider.Windows.cs` (NEW) | Windows-specific: parent window detection |
| `Interop/Interop.GetConsoleWindow.cs` (NEW) | P/Invoke for kernel32 GetConsoleWindow |
| `Interop/Interop.GetAncestor.cs` (NEW) | P/Invoke for user32 GetAncestor |

### Conditional Compilation Strategy

Since the Extensions/Azure project targets `net462;netstandard2.0`, we cannot use `#if _WINDOWS` (that's for the main SqlClient project). Instead:

- Use **runtime OS detection** (`RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`) for broker activation
- The `Microsoft.Identity.Client.Broker` package is always referenced but only invoked on Windows
- Platform-specific partial class files use `#if NETFRAMEWORK` for .NET Framework-only code paths

### Implementation Flow

```flowchart
AcquireTokenAsync
├── Non-PCA methods (Default, MSI, ServicePrincipal, Workload) → unchanged
└── PCA methods (Interactive, Integrated, Password, DeviceCodeFlow)
    ├── Build PublicClientApplication with BrokerOptions (Windows only)
    ├── Set ParentActivityOrWindow for WAM dialog
    ├── Try silent token acquisition
    └── If silent fails:
        ├── Windows + Broker: WAM handles interactive/integrated flow
        └── Non-Windows: Fallback to existing behavior (system browser, IWA)
```

## Testing

### Unit Tests

- Verify `SetParentActivityOrWindow` stores the function correctly
- Verify `SetParentActivityOrWindow` throws `ArgumentNullException` for null argument
- Verify `IsSupported` returns true for all expected auth methods

### Manual/Integration Tests (require SQL Server)

- `ActiveDirectoryInteractive` with WAM on Windows
- `ActiveDirectoryIntegrated` with WAM on Windows (validates device claims pass)
- Verify Unix/macOS falls back to non-broker behavior
- Verify CAP-protected Azure SQL MI access works from AVD

## Rollout

- WAM broker is **always enabled** on Windows when using PCA flows
- No opt-in connection string keyword needed (aligns with MSAL PCA compliance requirements)
- Existing `SetIWin32WindowFunc` remains as a backward-compatible API on .NET Framework, delegating to `SetParentActivityOrWindow`
