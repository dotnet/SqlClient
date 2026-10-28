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
logon sessions, even for the same account, use separate pools. This applies to
all Windows logon sessions, not just network-only logons. Applications that call
`LogonUser` for every operation can therefore create more pools and should
consider reusing a suitably scoped token. Repeated impersonation with the same
token does not create new logon sessions. Separate processes already have
separate pools.

This is conceptually similar to `SqlConnection.AccessToken` pooling: a different
access-token value creates a separate pool even when it represents the same
account. Here the discriminator is the Windows logon session, not the token
object, so duplicated tokens from the same session can still share a pool.

The session ID is not a token-object identifier or an outbound-account name.
It does not detect changes to credentials within an existing logon session.

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
shared-memory data source also works). They first verify that an unpooled
connection rejects the placeholder network-only credentials. Only then does a
successful pooled open under that token indicate process-account connection
reuse. If the unpooled probe succeeds, as can happen with some loopback
configurations, these tests skip.
They also verify that the failed open does not put the process account's pool into
its error blocking period.

The eight successful-login cases require an alternate account, supplied through the
`SQLCLIENT_NETONLY_USER`, `SQLCLIENT_NETONLY_DOMAIN`, and
`SQLCLIENT_NETONLY_PASSWORD` environment variables. Do not store credentials in
source or test configuration. The login-reversion test requires both the process
account and the alternate account to authenticate successfully as different SQL
logins; the replenishment test requires only the alternate account to succeed. Use
a TCP connection and verify that `ORIGINAL_LOGIN()` reports the configured
account: some loopback configurations may fall back to the process identity. Local
named-pipe and shared-memory transports do not exercise outbound credentials.
Managed networking is selected through the
existing `Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows` setting.

Windows local-server Set 3 CI jobs for the default SQL instance provision a unique
non-administrator local account and a SQL Windows login with access to `master`.
Provisioning runs under PowerShell 7.4 or later and uses the pinned `SqlServer`
PowerShell module (22.4.5.1), backed by Microsoft.Data.SqlClient. The reusable module
installation step installs that version for the current user if it is missing,
registering and immediately using `NuGet.config`'s `governed` source. Account setup
and cleanup only import the preinstalled module. For local setup, first run
`.\eng\scripts\Install-SqlClientPSModule.ps1 -Name SqlServer -RequiredVersion 22.4.5.1 -SkipIfInstalled -AllowClobber`.
SQL login management connects to the configured local TCP port with Windows
authentication and encryption, explicitly trusting the local CI server certificate.
The generated password is passed only as a secret test-process environment
variable. An always-run cleanup step removes the SQL login and local account.
These jobs set `SQLCLIENT_NETONLY_REQUIRED=true` and
`SQLCLIENT_NETONLY_CONNECTION_STRING` to an explicit TCP endpoint using `master`;
missing prerequisites fail the successful-login tests rather than skipping them.
Both successful-login tests first verify that an unpooled connection reports the
configured alternate `ORIGINAL_LOGIN()`. Required CI coverage also verifies that
the process account reports a different original login.

These assertions cover pool selection, login-name reversion, and replenishment;
they do not prove remote permission isolation or password validation. In
same-machine NTLM/TCP investigations, Windows SSPI reported the alternate
context name with the process account's accepted token, independently of
SqlClient and SQL Server. SQL likewise reported the alternate `ORIGINAL_LOGIN()`
while retaining the process account's effective security context, and an
unpooled invalid-password connection succeeded. Compensating for that
same-machine Windows behavior is outside this pooling fix. Cross-machine and
Kerberos validation remain separate coverage gaps.
