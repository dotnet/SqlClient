# Migrating connection networking defaults

`MultiSubnetFailover` now defaults to `true` on .NET Framework and modern .NET.
On .NET Framework, the obsolete `TransparentNetworkIPResolution` (TNIR) option
now defaults to `false`. Explicit connection-string values are unchanged.

MSF attempts TCP connections to DNS-resolved IP addresses in parallel and uses
the first successful connection. The addresses do not need to be in different
subnets. With MSF enabled, `IPAddressPreference` and TNIR have no effect.

## Incompatible connection options

Set `MultiSubnetFailover=false` for database mirroring, including when the server
supplies the failover partner, and for non-TCP connections such as Named Pipes,
Shared Memory, and LocalDB. MSF also fails when a hostname resolves to more than 64
IP addresses. Use an explicit TCP hostname and port where appropriate.

For example, a mirroring connection string must include the opt-out:

```text
Server=<principal>;Database=<database>;Failover Partner=<partner>;Integrated Security=true;MultiSubnetFailover=false
```

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
