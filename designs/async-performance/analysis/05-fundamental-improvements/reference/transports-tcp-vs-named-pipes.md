# Reference — Transports: TCP vs Named Pipes (and the others)

Where each transport is available and when SqlClient uses it. Source references are relative to
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/`.

---

## The transport set

| Transport | Data-source prefix | Native SNI | Managed SNI | Typical use |
| --- | --- | --- | --- | --- |
| TCP/IP | `tcp:` | Yes | **Yes** | Remote connections, the default everywhere |
| Named Pipes | `np:` | Yes | **Yes** (`SniNpHandle`) | Local IPC on Windows, some remote Windows |
| Shared Memory | `lpc:` | Yes | **No** (returns `ProtocolNotSupportedError`) | Same-machine Windows, native only |
| Admin / DAC | `admin:` | Yes | Yes (routed as TCP) | Dedicated Admin Connection |

`SniProxy` only knows `Protocol { TCP, NP, None, Admin }` (`SniProxy.netcore.cs:343`); there is no
Shared Memory path on managed SNI.

---

## Availability by platform

| Platform | TCP | Named Pipes | Shared Memory |
| --- | --- | --- | --- |
| Windows (managed SNI) | Yes | Yes (`\\.\pipe\sql\query`, `\\server\pipe\...`) | No (managed) — native SNI only |
| Windows (native SNI) | Yes | Yes | Yes |
| Linux / macOS (managed SNI) | Yes | Not usable against SQL Server (no Windows pipe server) | No |

In practice on **Unix the only transport is TCP.** Named Pipes uses `NamedPipeClientStream`
(`SniNpHandle.netcore.cs:34,59`) which targets Windows pipe semantics; SQL Server on Linux does not
expose Windows named pipes, so NP is effectively Windows-only.

---

## How the transport is chosen

1. **Explicit prefix** in the data source wins: `tcp:host,1433`, `np:\\host\pipe\sql\query`,
   `lpc:host` (managed: unsupported), `admin:host` (DAC).
2. **No prefix** → managed SNI defaults to **TCP**.
3. **`server\instance` with no port** → SqlClient may infer a Named Pipe path, or resolve the
   instance's dynamic TCP port via SSRP (see [ssrp](ssrp.md)). `SniProxy.InferNamedPipesInformation`
   (`SniProxy.netcore.cs:617`) builds the pipe path for the `server\instance` form.
4. **Constraints:** `MultiSubnetFailover=true` requires TCP — a Named Pipe (non-TCP) data source
   raises `MultiSubnetFailoverWithNonTcpProtocol` (`SniProxy.netcore.cs:275`).

---

## Ports

- **Default instance** → TCP **1433** directly (no SSRP needed).
- **Named instance** → dynamic port, resolved via **SSRP/SQL Browser on UDP 1434** (see
  [ssrp](ssrp.md)).
- **DAC (admin:)** → its own port, also discoverable via SSRP.

---

## Why this matters for redesign

On Unix the transport story is **just TCP**. The `SniHandle` virtual dispatch and protocol-selection
machinery exist mainly to keep Windows Named Pipes alongside TCP. A managed fast path that assumes
TCP (with Named Pipes as a separate, rarely-hit branch) would shed indirection on the platform where
async performance matters most. See [../01-managed-sni-and-read-path.md](../01-managed-sni-and-read-path.md).
