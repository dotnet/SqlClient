# ICM 792529661 — Native Memory OOM with `Encrypt=Strict`

## Executive Summary

When using **`Encrypt=Strict`** (TDS 8.0) with **SQL Authentication** on **Windows native SNI**, each new `SqlConnection.Open()` leaks **~50–100 KB of native memory** that is never reclaimed, even after connection close/dispose. Under high-throughput workloads, this leads to **out-of-memory crashes**.

**Root Cause:** Windows SChannel's **TLS 1.3 session ticket cache** stores resumption tickets in a process-global, per-credential cache. Each new TLS 1.3 connection receives session tickets from the server, and SChannel caches them indefinitely. There is **no public API** to evict, limit, or disable this cache from user mode.

**Key observation — the leak does NOT occur with:**
- Managed SNI (uses .NET `SslStream`, which doesn't use SChannel's session cache)
- TLS 1.2 connections (no session tickets)
- Non-Strict encryption modes (TLS handshake is handled differently)

---

## Detailed Technical Explanation

### 1. The Connection Flow with `Encrypt=Strict` (TDS 8.0)

In TDS 8.0 ("Strict" encryption), the TLS handshake happens **before any TDS traffic**. The flow is:

```
Client                          SQL Server
  |                                  |
  |--- TCP Connect ----------------->|
  |--- TLS ClientHello ------------>|   ← TLS wraps the entire connection
  |<-- TLS ServerHello + Cert ------|
  |--- TLS Finished --------------->|
  |<-- TLS Finished ----------------|
  |                                  |
  |=== TDS traffic inside TLS ======|
  |--- TDS Login7 (SQL Auth) ------>|
  |<-- TDS Login Response ----------|
```

This differs from `Encrypt=Mandatory` where TDS pre-login happens first, then TLS wraps only the login, then optionally continues encrypted.

### 2. TLS 1.3 Session Tickets

TLS 1.3 introduced **post-handshake session tickets** (RFC 8446 §4.6.1). After the handshake completes, the server sends `NewSessionTicket` messages:

```
Client                          SQL Server
  |                                  |
  |=== Handshake complete ===========|
  |<-- NewSessionTicket (ticket 1) --|   ← Server pushes tickets
  |<-- NewSessionTicket (ticket 2) --|   ← Often 2+ tickets
  |                                  |
```

These tickets allow the client to perform **0-RTT or 1-RTT resumption** on future connections — skipping the expensive key exchange. SQL Server typically sends **2 session tickets** per connection.

### 3. SChannel's Session Ticket Cache

On Windows, the TLS implementation is **SChannel** (Secure Channel), a system DLL (`schannel.dll`). When SChannel receives `NewSessionTicket` messages, it:

1. Deserializes the ticket (contains encrypted session state, PSK identity, expiry)
2. Stores it in a **process-global hash table** keyed by server name + credential handle
3. Each ticket is ~20–50 KB (includes the PSK, ticket nonce, server certificate chain hash, etc.)

**The critical problem:** SChannel has **no public API** to:
- Limit the number of cached tickets
- Evict specific tickets
- Disable ticket acceptance per-context
- Set a maximum cache size

The cache grows unbounded as new connections produce new tickets.

### 4. Why Only `Encrypt=Strict`?

With `Encrypt=Mandatory` or `Encrypt=Optional`:
- The TLS session is often **reused** across the connection pool because the pool keeps TCP connections alive
- New TLS handshakes happen infrequently (only on pool misses or reconnects)
- The ticket cache grows slowly

With `Encrypt=Strict`:
- In high-throughput scenarios or when connections are frequently created/destroyed, many new TLS sessions occur
- Each new TLS 1.3 handshake → server sends 2 new tickets → SChannel caches them
- **~50–100 KB per connection** leaked permanently

### 5. Memory Growth Mechanics

```
Connection 1: TLS handshake → 2 tickets cached → +50 KB
Connection 2: TLS handshake → 2 tickets cached → +50 KB  (old tickets NOT evicted)
Connection 3: TLS handshake → 2 tickets cached → +50 KB
...
Connection N: TLS handshake → 2 tickets cached → +50 KB

Total leaked: N × ~50 KB (never freed)
```

Even though connections are closed and disposed, the tickets remain in SChannel's process-global cache. The `DeleteSecurityContext` and `FreeCredentialsHandle` calls do NOT purge associated tickets.

### 6. Why Managed SNI Doesn't Leak

Managed SNI uses .NET's `SslStream` class, which:
- Uses its own managed TLS implementation
- .NET's `SslStream` disposes cleanly and the managed GC reclaims all associated buffers
- The session cache in the managed path is bounded and properly evicted

### 7. The Native SNI Code Path

In `ssl.cpp`, the relevant flow is:

```cpp
// Credential acquisition
AcquireCredentialsHandle(..., &schCredentials, ..., &credHandle);

// TLS handshake
InitializeSecurityContext(&credHandle, ..., &ctxtHandle, ...);
// ↑ This is where SChannel receives and caches session tickets

// Connection close
DeleteSecurityContext(&ctxtHandle);      // Does NOT purge ticket cache
FreeCredentialHandle(&credHandle);       // Does NOT purge ticket cache
```

---

## Fix Attempts (All Failed)

| # | Approach | Implementation | Outcome |
|---|----------|---------------|---------|
| 1 | **`SCH_CRED_DISABLE_RECONNECTS`** | Set flag on `SCHANNEL_CRED` structure passed to `AcquireCredentialsHandle` | Only prevents client from *offering* tickets for resumption. Does NOT prevent server from *sending* tickets, and does NOT prevent SChannel from *caching* received tickets. **Still leaks.** |
| 2 | **Per-connection unique credentials** | Create fresh `CredHandle` for each connection instead of sharing | Ticket cache is indexed by {server name, credential config}. Fresh creds just create new cache buckets — tickets still accumulate. **Still leaks.** |
| 3 | **`dwSessionLifespan = 1`** | Set minimum session lifetime on credential | Controls how long SChannel will *reuse* a cached ticket for outbound reconnection. Does NOT control how long tickets are *stored* in memory. **Still leaks.** |
| 4 | **`ApplyControlToken` + `SSL_SESSION_DISABLE`** | Applied post-handshake to disable caching on the security context | Only applies to future operations on that context — tickets already received and cached are not affected. **Still leaks.** |
| 5 | **`SslEmptyCacheW(NULL)`** | Called periodically or per-connection to flush entire SChannel cache | Nuclear option: purges ALL cached sessions process-wide. Causes thundering-herd re-handshakes, race conditions, and performance collapse. Tickets re-accumulate immediately. **Not viable.** |

**Benchmark results after all fixes:** ~68–108 KB/connection growth (unchanged from baseline).

---

## Platform Constraints

| Platform | Managed SNI Available? | Native SNI Required? | Workaround Possible? |
|----------|----------------------|---------------------|---------------------|
| **.NET 8/9 (Windows)** | Yes (opt-in via `UseManagedSNIOnWindows`) | Default but not required | Yes — use managed SNI |
| **.NET Framework 4.6.2+** | **No** | **Yes — only option** | **No managed fallback** |

---

## Impact

- **Affected:** Any Windows application using native SNI + `Encrypt=Strict` + TLS 1.3 (default for SQL Server 2022+)
- **Severity:** Process eventually OOMs under sustained connection creation patterns
- **Rate:** ~50–100 KB per unique TLS session
- **.NET Framework:** Cannot use managed SNI — permanently affected unless native fix found
- **.NET Core:** Can work around via `UseManagedSNIOnWindows=true`

---

## Viable Paths Forward

### For .NET Core/.NET 8+ (Short-term)
- Auto-switch to managed SNI when `Encrypt=Strict` is used
- Or document `UseManagedSNIOnWindows=true` as recommended workaround

### For .NET Framework (Short-term)
- **Cap TLS to 1.2** for Strict connections on native SNI (avoids session tickets entirely, but loses TLS 1.3 benefits)
- **Accept and document** the limitation with guidance on connection pooling to minimize new TLS handshakes

### Long-term
- **File a Windows/SChannel bug** requesting a cache eviction API or per-context opt-out
- Windows team provides a proper API to control session ticket caching behavior per-credential or per-context

---

## The Fundamental Problem

This is a **design limitation in Windows SChannel**. The session ticket cache was designed for web browsers where:
- You connect to a few hundred unique servers
- Cache growth is bounded by the number of unique servers
- The browser process restarts regularly

For database drivers:
- You connect to the SAME server thousands/millions of times
- Each connection gets new tickets (SQL Server rotates tickets)
- The process runs for months/years (service lifetime)
- The cache grows unboundedly because SQL Server issues fresh tickets per-connection

**There is no user-mode fix — it requires either a Windows/SChannel update to provide a cache control API, or avoiding TLS 1.3 on the native path.**

---

## References

- **ICM:** 792529661
- **Affected component:** `Microsoft.Data.SqlClient.SNI` (native SNI, `ssl.cpp`)
- **Branch (SNI):** `dev/ad/oom-fix` in `Microsoft.Data.SqlClient.sni` repo
- **Branch (SqlClient):** `dev/ad/strict-oom` in `dotnet/SqlClient`
- **Benchmark tool:** `tools/StrictEncryptMemoryBenchmark/`
- **RFC 8446 §4.6.1:** TLS 1.3 Post-Handshake Messages — NewSessionTicket
- **MS-TDS 8.0:** Strict encryption mode specification
