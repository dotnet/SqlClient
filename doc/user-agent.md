# UserAgent telemetry

SqlClient sends a UserAgent feature extension when establishing a physical
connection. Payload version 2 has nine pipe-delimited fields:

```text
2|MS-MDS|{Driver Version}|{Arch}|{OS Type}|{OS Info}|{Runtime Info}|{App Id}|{Driver Properties}
```

Driver Properties is an unpadded, uppercase hexadecimal 64-bit mask:

| Mask | Meaning when set |
|---|---|
| `0x1` | `Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2` is enabled. |
| `0x2` | `Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour` is configured true. |
| `0x4` | `Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni` is configured true. |

The compatibility bits report the two configured values independently, using
the existing default of true when either switch is unset. They do not apply the
parser's override: when ProcessSni compatibility is true, the parser uses async
compatibility even if its configured value is false. For example, with pool V2
off, raw async false and ProcessSni true produce `4`, not `6`. With neither
compatibility switch set, and pool V2 off, the value is `6`.

Configure switches before the first use of SqlClient. Switch values and the
UserAgent payload are cached for the process lifetime. Pool reuse does not send
a new login payload, and these bits do not describe individual commands.

Only interpret the new compatibility bits for package versions that contain
this instrumentation. Earlier v2 packages report only the pool V2 bit; their
clear compatibility bits do not mean those switches were configured false.
Telemetry consumers must track the first instrumented version for each release
line before enabling version-aware decoding.

## Verifying server telemetry

Use a fresh process for each switch configuration. Inspect the received
UserAgent through an approved SQL Server or Azure SQL telemetry environment;
server telemetry visibility depends on the environment and its owners. Verify
both synchronous and asynchronous connection opens, especially raw async false
with ProcessSni true. Client LOGIN7 capture proves what was sent, but does not
by itself prove server-side ingestion or downstream decoding.
