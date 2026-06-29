# Call-Site Confirmation — Resolving the Cross-Cutting Caveat

The [04-quick-wins README](../04-quick-wins/README.md#cross-cutting-caveat) flagged that the
graphify tree-sitter pass dropped `TryReadNetworkPacket`, `ReadSniSyncOverAsync`, and
`TryProcessDone` (verified to exist in source), so its call graph under-represented the
sync-over-async hotspots several quick-wins target. This pass enumerates the exact call sites with a
symbol-aware Roslyn parse, honoring `#if` and platform file suffixes.

## The three dropped methods — confirmed

All call sites below resolved under **`all`** configurations (`net8.0`/`net9.0` unix+windows and
`net462`), i.e. they are on the shared, always-compiled read path — not platform-conditional.

| Method | Call site | Containing member |
| --- | --- | --- |
| `TryReadNetworkPacket` | `TdsParser.cs:1023` | `TdsParser.ConsumePreLoginHandshake` |
| `TryReadNetworkPacket` | `TdsParserStateObject.cs:1330` | `TdsParserStateObject.TryProcessHeader` |
| `TryReadNetworkPacket` | `TdsParserStateObject.cs:1455` | `TdsParserStateObject.TryPrepareBuffer` |
| `TryReadNetworkPacket` | `TdsParserStateObject.cs:1464` | `TdsParserStateObject.TryPrepareBuffer` |
| `TryReadNetworkPacket` | `TdsParserStateObject.cs:1482` | `TdsParserStateObject.TryPrepareBuffer` |
| `ReadSniSyncOverAsync` | `TdsParserStateObject.cs:3480` | `TdsParserStateObject.TryReadNetworkPacket` |
| `ReadSniSyncOverAsync` | `TdsParserStateObject.cs:3488` | `TdsParserStateObject.TryReadNetworkPacket` |
| `TryProcessDone` | `TdsParser.cs:2627` | `TdsParser.TryRun` |

This is the chain the quick-wins care about, now anchored:

`ConsumePreLoginHandshake` → `TryReadNetworkPacket` → `ReadSniSyncOverAsync` (the blocking
sync-over-async read), and `TryRun` → `TryProcessDone`. The graph's silence on these three was a
tree-sitter artifact, not evidence they are unimportant — they sit directly on the pre-login and
packet-read paths that CE-4 and CMD-1 modify.

## Other anchors referenced by the quick-wins

The same pass confirmed the remaining method anchors named in the connection-establishment items.
These are managed-SNI only, so they resolve under the four `.NET` configurations (the `.netcore.cs`
suffix excludes `net462`):

| Method | Call site | Containing member | Quick-win |
| --- | --- | --- | --- |
| `TryConnectParallel` | `SniTcpHandle.netcore.cs:167` | `SniTcpHandle..ctor` | CE-1 |
| `TryConnectParallel` | `SniTcpHandle.netcore.cs:203` | `SniTcpHandle..ctor` | CE-1 |
| `TryConnectParallel` | `SniTcpHandle.netcore.cs:222` | `SniTcpHandle..ctor` | CE-1 |
| `GetHostAddresses` | `SniTcpHandle.netcore.cs:330` | `SniTcpHandle.GetHostAddressesSortedByPreference` | CE-3 |
| `GetHostAddresses` | `SniCommon.netcore.cs:181` | `SniCommon.GetDnsIpAddresses` | CE-3 |
| `AuthenticateAsClient` | `SniTcpHandle.netcore.cs:735` | `SniTcpHandle.EnableSsl` | CE-2 |
| `AuthenticateAsClient` | `SniTcpHandle.netcore.cs:739` | `SniTcpHandle.EnableSsl` | CE-2 |
| `AuthenticateAsClient` | `SniNpHandle.netcore.cs:338` | `SniNpHandle.EnableSsl` | CE-2 |
| `AuthenticateAsClient` | `SniNpHandle.netcore.cs:343` | `SniNpHandle.EnableSsl` | CE-2 |
| `ConsumePreLoginHandshake` | `TdsParser.cs:580` | `TdsParser.Connect` | CE-4 |
| `ConsumePreLoginHandshake` | `TdsParser.cs:634` | `TdsParser.Connect` | CE-4 |

## What this changes for implementation

- **CE-1 (async TCP connect)** — `TryConnectParallel` is invoked three times from the
  `SniTcpHandle` constructor, confirming the "drive `ConnectAsync` through the ctor" framing; all
  three call sites must move to the async path together.
- **CE-2 (async TLS)** — `AuthenticateAsClient` (synchronous) is called from both `SniTcpHandle`
  and `SniNpHandle` `EnableSsl`; the named-pipe handle shares the pattern and must not be missed.
- **CE-3 (async DNS)** — `GetHostAddresses` (blocking) has two distinct call sites; the
  `SniCommon.GetDnsIpAddresses` helper is the shared one.
- **CE-4 (async pre-login read)** — the blocking read is `TryReadNetworkPacket` at
  `TdsParser.cs:1023` inside `ConsumePreLoginHandshake`, which bottoms out in `ReadSniSyncOverAsync`
  at `TdsParserStateObject.cs:3480`/`3488` — the exact "pre-login handshake starvation" path.

The line anchors in the 04-quick-wins items were sourced from the 01-initial manual analysis; this
pass independently re-derives them with conditional-compilation correctness.
