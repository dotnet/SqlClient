# Priority 4: Async SNI Connection Opening

**Addresses:** #979, #601
**Impact:** Medium-High — removes last major blocking point in OpenAsync
**Effort:** High (requires native SNI changes or managed SNI improvements)

## Current State

### Managed SNI (Cross-Platform)

- **File:**
  `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs`

- The `SniTcpHandle` constructor calls `Connect()` or `TryConnectParallel()` synchronously
- Internally uses `Socket.ConnectAsync()` — the async capability exists at the socket level but the
  constructor boundary forces synchronous completion

- SSL handshake uses `SslStream.AuthenticateAsClientAsync()` but is awaited synchronously

### Native SNI (Windows)

- **File:** `src/Microsoft.Data.SqlClient/src/Interop/Windows/Sni/SniNativeWrapper.cs`
- Only exposes `SniOpenSyncEx()` (line 164) — P/Invoke to native C++ `SNIOpenSyncExWrapper()`
- **No async equivalent exists** in the native SNI library
- This is the primary blocking point for Windows `OpenAsync()`

### TDS Parser Connection Flow

```text
SqlConnection.OpenAsync()
  → SqlInternalConnectionTds (LoginNoFailover / AttemptOneLogin)
    → TdsParser.Connect()
      → TdsParserStateObject.CreatePhysicalSNIHandle()
        → [BLOCKS here — either SNI ctor or P/Invoke]
      → SendPreLoginHandshake()  — sync
      → ConsumePreLoginHandshake()  — sync
    → TdsParser.TdsLogin()  — sync
```

## Incremental Fixes

| # | Fix | Complexity | Impact |
| --- | ----- | ----------- | -------- |
| 1 | [Async managed SNI TCP connect](01-async-managed-connect.md) | Medium | High |
| 2 | [Async SSL handshake](02-async-ssl.md) | Medium | Medium |
| 3 | [Async prelogin handshake](03-async-prelogin.md) | Medium | Medium |
| 4 | [Async TDS login](04-async-login.md) | High | Medium |
| 5 | [End-to-end async open pipeline](05-async-open-pipeline.md) | High | High |

## Dependencies

- Fix 1 is the highest-impact standalone change
- Fixes 1-4 can be done independently but all feed into Fix 5
- Fix 5 requires all of 1-4 to be complete for a fully non-blocking `OpenAsync()`
- Native SNI is out of scope here — it requires changes to the separate
  `Microsoft.Data.SqlClient.SNI` native library
