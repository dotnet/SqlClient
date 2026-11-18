# Migrating connection networking defaults

`MultiSubnetFailover` now defaults to `true` on .NET Framework and modern .NET.
On .NET Framework, the obsolete `TransparentNetworkIPResolution` (TNIR) option
now defaults to `false`. Explicit connection-string values are unchanged.

MSF attempts TCP connections to DNS-resolved IP addresses in parallel and uses
the first successful connection. The addresses do not need to be in different
subnets. With MSF enabled, `IPAddressPreference` and TNIR have no effect.

## Where the default applies

The default applies only when SqlClient connects over TCP. When
`MultiSubnetFailover` is not set in the connection string, it is turned off for:

- A configured `Failover Partner`, or a failover partner sent by the server
  (database mirroring).
- Named Pipes and Shared Memory, from an `np:` or `lpc:` prefix, a pipe path,
  `Network Library` (.NET Framework), or a client alias.
- LocalDB, and `admin:` or `via:` connections.
- On Windows with native SNI, a local server without a prefix (for example `.`,
  `(local)`, `localhost`, or the machine name), which tries Shared Memory first.

For a remote server without a prefix, native SNI on Windows uses TCP only with
MSF, so it no longer falls back to Named Pipes. Add `np:` or set
`MultiSubnetFailover=false` to keep Named Pipes.

`SqlConnectionStringBuilder.MultiSubnetFailover` returns the default (`true`), not
the value used for the connection.

An explicit `MultiSubnetFailover=true` is always used as-is. It still fails with
a failover partner and with non-TCP protocols. MSF also fails when a hostname
resolves to more than 64 IP addresses.

## Restoring the previous defaults

The existing AppContext switches provide compatibility opt-outs; no new switches
are required. Set them before constructing connection-string builders or using
SqlClient, because switch values and connection options are cached.

```csharp
AppContext.SetSwitch(
    "Switch.Microsoft.Data.SqlClient.EnableMultiSubnetFailoverByDefault", false);

// .NET Framework only: restore the previous TNIR default as well.
AppContext.SetSwitch(
    "Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString", false);
```

An explicit connection-string value overrides its corresponding switch.
Restoring TNIR does not override MSF: disable MSF as well to use TNIR.
Existing automatic TNIR suppression for Azure SQL endpoints, Microsoft Entra ID
authentication, and caller-supplied access tokens is unchanged.
On modern .NET, TNIR remains unsupported.

See [SqlClient high availability and disaster recovery](https://learn.microsoft.com/sql/connect/ado-net/sql/sqlclient-support-high-availability-disaster-recovery)
for the supported connection strategies and their limitations.

## Pool-wait timeout budget

`Switch.Microsoft.Data.SqlClient.UseOverallConnectTimeoutForPoolWait` now defaults
to `true` on .NET Framework and modern .NET. `SqlConnection.Open` and `OpenAsync`
carry the caller's remaining `Connect Timeout` budget through pool acquisition and
login, for both pool implementations. Time spent waiting for the pool is deducted
from the budget available for physical connection creation instead of granting
login another full timeout. Applications that previously relied on those separate
budgets may now time out sooner under pool contention. `Connect Timeout=0` remains
infinite.

To restore the previous behavior, set the existing switch to `false` before using
SqlClient, because its value is cached on first use:

```csharp
AppContext.SetSwitch(
    "Switch.Microsoft.Data.SqlClient.UseOverallConnectTimeoutForPoolWait", false);
```

With this compatibility opt-out, login starts a fresh `Connect Timeout` after pool
acquisition. The legacy wait-handle pool also uses its configured pool-wait timeout
rather than the caller's remaining budget; the channel pool continues to use the
caller's remaining budget for its internal waits.
