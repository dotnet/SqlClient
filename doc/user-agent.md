# UserAgent telemetry

This servicing branch sends the version 2 UserAgent format at physical login:

```text
2|MS-MDS|{Driver Version}|{Arch}|{OS Type}|{OS Info}|{Runtime Info}|0|{Driver Properties}
```

Application Id is always `0`; this adaptation does not add application identity
APIs. Driver Properties is an unpadded uppercase hexadecimal 64-bit mask.
| Mask | Meaning when set |
|---|---|
| `0x1` | `Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2` is enabled. |
| `0x2` | `Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour` is configured true. |
| `0x4` | `Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni` is configured true. |

The compatibility bits report the two configured values independently, using
the existing default of true when either switch is unset. They do not apply the
parser's override: when ProcessSni compatibility is true, the parser uses async
compatibility even if its configured value is false. With pool V2 off, raw async
false and ProcessSni true produce `4`, not `6`. With both compatibility switches
unset and pool V2 off, the value is `6`.

Configure switches before first using SqlClient. Values and the payload are
cached for the process lifetime. Reusing a pooled connection does not send
another login payload.

Only interpret the compatibility bits for package versions that contain this
instrumentation. Telemetry consumers must track the first instrumented version
for each release line before interpreting clear bits as configured false.

## Verifying server telemetry

Use a fresh process for each configuration and an approved SQL Server or Azure
SQL telemetry environment. Verify sync and async login, especially raw async
false with ProcessSni true. A client LOGIN7 capture proves what was sent, not
that server-side ingestion and downstream decoding succeeded.
