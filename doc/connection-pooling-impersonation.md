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
If the environment accepts the placeholder credentials (an unpooled probe open
succeeds, as can happen with some loopback configurations), these tests skip.
They also verify that the failed open does not put the process account's pool into
its error blocking period.

The remaining tests require an alternate account, supplied through the
`SQLCLIENT_NETONLY_USER`, `SQLCLIENT_NETONLY_DOMAIN`, and
`SQLCLIENT_NETONLY_PASSWORD` environment variables. Do not store credentials in
source or test configuration. The login-reversion test requires both the process
account and the alternate account to authenticate successfully as different SQL
logins; the replenishment test requires only the alternate account to succeed. Use
a TCP connection and verify the configured account is actually authenticated:
some loopback configurations may fall back to the process identity. Local
named-pipe and shared-memory transports do not exercise outbound credentials.
Managed networking is selected through the
existing `Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows` setting.

Windows local-server Set 3 CI jobs for the default SQL instance provision a unique
non-administrator local account and a SQL Windows login with access to `master`.
The generated password is passed only as a secret test-process environment
variable. An always-run cleanup step removes the SQL login and local account.
These jobs set `SQLCLIENT_NETONLY_REQUIRED=true` and
`SQLCLIENT_NETONLY_CONNECTION_STRING` to an explicit TCP endpoint using `master`;
missing prerequisites fail the successful-login tests rather than skipping them.
Both successful-login tests first verify that an unpooled connection authenticates
as the configured alternate account. Required CI coverage also verifies that the
process account authenticates as a different login, preventing loopback fallback
from producing a false pass.

## CI-only outbound-name experiment

The same designated jobs also provision a second disposable non-admin account
and SQL Windows login, using separate secret variables and always-run cleanup.
`OutboundCredentialNameTest` compares a fresh, targetless Negotiate SSPI
credential name acquired inside impersonation with `ORIGINAL_LOGIN()`,
`SUSER_SNAME()`, `SUSER_SID()` and connection authentication/transport properties.
All SQL opens are unpooled. It covers independent same-account sessions,
different accounts, restoration, and invalid credentials with the same name,
using sync/async APIs and the pipeline-selected native/managed networking.
For these configured local accounts, SSPI can return `user@machine` while SQL
returns `machine\user`; the experiment recognizes both exact account forms,
logs the raw names, and independently verifies SQL's SAM-form login and SID.
This is not general domain-alias normalization or a production pool-key rule.
Matched no-prequery and SSPI-prequery controls reuse the passing regression
preflight's unpooled open path and record logon-session IDs. Both arms collect
their observations before strict SQL identity assertions, so a baseline mismatch
does not prevent capturing the SSPI-prequery arm. A separate
cacheable/recompiled SQL comparison distinguishes context-sensitive scalar
results from the original authenticated login; normal identity probes recompile
the statement and retain strict configured-login and SID assertions.
The additional credentials are mapped through `SQLCLIENT_NETONLY_PEER_USER`,
`SQLCLIENT_NETONLY_PEER_DOMAIN`, and `SQLCLIENT_NETONLY_PEER_PASSWORD`.
Nothing is provisioned on the developer machine.

The same-machine CI baseline has reported the configured alternate
`ORIGINAL_LOGIN()` but the process account's current login, session security
SIDs, and administrative login token, including without an SSPI prequery and
with recompilation. A passing original-login regression alone therefore does
not establish distinct effective permissions or password validation.

A bounded, in-memory Negotiate handshake independently queries the accepted
SSPI context name and `QuerySecurityContextToken` user SID before the SQL
comparison. It uses process, fresh network-only A and B, and ordinary
authenticated A controls; unavailable interactive logon rights are reported
without changing policy. Authentication token bytes and passwords are never
logged, and all credential, context and access-token handles are released.
In CI this reproduced the split below SQL: network-only A's accepted context
name is A while its token is the process account, whereas ordinary
authenticated A yields A's token. The exact Windows local-call mechanism, and
remote or Kerberos behavior, are not established.

This same-machine Windows behavior is treated as an environment limitation
outside the driver's design responsibility. When the local diagnostic measures
the process token for a network-only account, the experiment logs an
`ENVIRONMENT LIMITATION` line and asserts SQL's current login and SID against
that measured process identity instead of the account. The driver-relevant
assertions remain strict: `ORIGINAL_LOGIN()` must be the configured account,
A and B must authenticate as distinct original logins distinct from the
process, authentication must be NTLM over TCP, and restoration is verified.
Any other measured token identity fails the experiment. When Windows supplies
the account's own token, the full account current-login and SID assertions
apply. This classification does not establish whether outbound-name pooling is
viable, and the original pool-isolation regressions are unchanged.

A separate probe writes a SQL-SPN override using `CRED_PERSIST_SESSION` only
inside a fresh disposable network-only session, then deletes that exact target.
It records whether SQL selects the override and whether the targetless name,
`AuthenticationId`, or `ModifiedId` changes. An unavailable credential store or
an override not selected by SQL is explicitly reported as uncovered, not proof
that name-based caching is safe. Local accounts exercise NTLM, not Kerberos or
domain aliases. These probes do not implement outbound-name pooling or change
the driver's identity/replenishment strategy.
