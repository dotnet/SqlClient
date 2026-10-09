# Connection pooling and Windows impersonation

On Windows, integrated-security connections are pooled by Windows identity and
logon session, in addition to the connection-string pool group. This applies to
both the wait-handle pool and the channel pool, with native or managed networking.

Network-only logons (`LOGON32_LOGON_NEW_CREDENTIALS`, also used by `runas /netonly`)
can retain the process user's local SID while supplying different credentials for
outbound authentication. The token's `AuthenticationId` identifies its logon
session and prevents these connections from sharing a pool with the process
account or another network-only logon session.

Duplicated tokens in the same logon session can reuse the same pool. Separate
logon sessions, even for the same account, use separate pools. Applications that
create a fresh network-only logon for every operation can therefore create more
pools and should consider reusing a suitably scoped token.

Background replenishment creates connections only when its effective identity
matches the pool's identity, including the logon-session ID. Returning an expired
connection outside impersonation can cause replenishment to skip that pool.
`Min Pool Size` is therefore not guaranteed across impersonation boundaries;
subsequent opens under the owning identity can create connections on demand.

Open connections inside the intended impersonation scope, including awaiting
asynchronous opens within that scope (see the
[complete sample](samples/ConnectionPool_Impersonation.cs)):

```csharp
// token is a SafeAccessTokenHandle obtained from LogonUser.
await WindowsIdentity.RunImpersonated(token, async () =>
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    using var command = new SqlCommand("SELECT ORIGINAL_LOGIN()", connection);
    Console.WriteLine(await command.ExecuteScalarAsync());
});
```

## Regression tests

`DbConnectionPoolIdentityTest` uses synthetic network-only tokens and stub
connections; it requires Windows but no SQL Server or valid outbound credentials.

`PoolIdentityImpersonationTest` exercises real SQL Server connections using both
pools and synchronous/asynchronous APIs.

The `InvalidNetOnlyCredentials_*` tests need only an integrated-security
`TCPConnectionString` (local or remote; on a local instance a named-pipe or
shared-memory data source also works). They use a network-only token with
placeholder credentials, which cannot open a new physical connection, so any
successful open under that token proves it received a process-account connection.
They also verify that the failed open does not put the process account's pool into
its error blocking period.

The remaining tests require an alternate account, supplied through the
`SQLCLIENT_NETONLY_USER`, `SQLCLIENT_NETONLY_DOMAIN`, and
`SQLCLIENT_NETONLY_PASSWORD` environment variables. Do not store credentials in
source or test configuration. The login-reversion test requires both the process
account and the alternate account to authenticate successfully as different SQL
logins; the replenishment test requires only the alternate account to succeed. Use
a TCP connection: loopback TCP authentication uses network-only credentials, but
local named-pipe and shared-memory connections cannot be opened under a
network-only impersonation token. Managed networking is selected through the
existing `Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows` setting.
