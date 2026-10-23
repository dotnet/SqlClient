# UserAgent telemetry

This servicing branch sends the version 2 UserAgent format at physical login:

```text
2|MS-MDS|{Driver Version}|{Arch}|{OS Type}|{OS Info}|{Runtime Info}|0|{Driver Properties}
```

Application Id is always `0`; this adaptation does not add application identity
APIs. Driver Properties is an unpadded uppercase hexadecimal 64-bit mask.
Mask `0x1` reports whether connection pool V2 is enabled.

Configure switches before first using SqlClient. Values and the payload are
cached for the process lifetime. Reusing a pooled connection does not send
another login payload.
