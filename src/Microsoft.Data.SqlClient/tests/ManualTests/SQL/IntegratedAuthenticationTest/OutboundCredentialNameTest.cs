// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Win32.SafeHandles;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Compares targetless outbound SSPI names with actual unpooled SQL authentication.
    /// This experiment does not implement or change connection pooling.
    /// </summary>
    [Collection("PoolIdentityImpersonation")]
    [Trait("Set", "3")]
    public class OutboundCredentialNameTest
    {
        private readonly ITestOutputHelper _output;

        /// <summary>Captures non-secret observations in published test results.</summary>
        public OutboundCredentialNameTest(ITestOutputHelper output) => _output = output;

        public static bool RunExperiment => PoolIdentityImpersonationTest.RequiresNetOnlyTests
            || (PoolIdentityImpersonationTest.IsConfigured
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_PEER_USER")));

        /// <summary>
        /// Tests two real accounts, independent same-account logons, invalid same-name credentials,
        /// and restoration with both Open APIs and the CI-selected networking implementation.
        /// </summary>
        [ConditionalTheory(nameof(RunExperiment))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OutboundName_UnpooledSqlAuthentication(bool async)
        {
            AssertConfiguration();
            await DiagnosePrequery(async);
            string connectionString = ConnectionString();
            string processName = CredentialName();
            TokenStatistics processStatistics = Statistics();
            Login process = await ReadLogin(connectionString, async);
            using SafeAccessTokenHandle first = CreateToken(false);
            using SafeAccessTokenHandle repeated = CreateToken(false);
            using SafeAccessTokenHandle peer = CreateToken(true);
            string firstId = WindowsIdentity.RunImpersonated(first, () => Statistics().AuthenticationId.ToString());
            string repeatedId = WindowsIdentity.RunImpersonated(repeated, () => Statistics().AuthenticationId.ToString());
            Assert.NotEqual(firstId, repeatedId);
            Login firstLogin = await Probe(first, false, connectionString, async, "A/session1");
            Login repeatedLogin = await Probe(repeated, false, connectionString, async, "A/session2");
            Login peerLogin = await Probe(peer, true, connectionString, async, "B");
            Assert.Equal(firstLogin.Sid, repeatedLogin.Sid);
            Assert.NotEqual(firstLogin.Sid, peerLogin.Sid);
            Assert.NotEqual(process.Sid, firstLogin.Sid);

            using SafeAccessTokenHandle invalid = WindowsImpersonationHelper.LogonNetOnly(
                User(false), Domain(false), "<invalid-pwd>");
            await WindowsIdentity.RunImpersonated(invalid, async () =>
            {
                AssertCredentialName(false, CredentialName());
                SqlException failure = await Assert.ThrowsAsync<SqlException>(() => ReadLogin(connectionString, async));
                Assert.NotEqual(-2, failure.Number);
                _output.WriteLine($"A/invalid: same SSPI name, SQL rejected credentials; error={failure.Number}.");
            });
            await Probe(first, false, connectionString, async, "A/after-invalid");
            Assert.Equal(processName, CredentialName());
            Assert.Equal(processStatistics.AuthenticationId.ToString(), Statistics().AuthenticationId.ToString());
            Login restored = await ReadLogin(connectionString, async);
            Assert.Equal(process.Original, restored.Original);
            Assert.Equal(process.Sid, restored.Sid);
            _output.WriteLine($"Restoration verified; async={async}; managedSNI={DataTestUtility.UseManagedSNIOnWindows}.");
            _output.WriteLine("Kerberos/domain aliases are uncovered: these are local Windows accounts.");
        }

        /// <summary>
        /// Probes a SQL-SPN credential override only in a fresh disposable logon session,
        /// observing whether a targetless name/cache predicts the actual SQL login.
        /// </summary>
        [ConditionalTheory(nameof(RunExperiment))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OutboundName_SessionCredentialOverride(bool async)
        {
            AssertConfiguration();
            string connectionString = ConnectionString();
            var builder = new SqlConnectionStringBuilder(connectionString);
            Assert.StartsWith("tcp:localhost,", builder.DataSource, StringComparison.OrdinalIgnoreCase);
            string target = "MSSQLSvc/localhost:" + builder.DataSource.Substring("tcp:localhost,".Length);
            TokenStatistics process = Statistics();
            Login processLogin = await ReadLogin(connectionString, async);
            using SafeAccessTokenHandle token = CreateToken(false);
            await WindowsIdentity.RunImpersonated(token, async () =>
            {
                TokenStatistics before = Statistics();
                Assert.NotEqual(process.AuthenticationId.ToString(), before.AuthenticationId.ToString());
                string cachedName = CredentialName();
                AssertCredentialName(false, cachedName);
                await ProbeCurrent(false, connectionString, async, "override/before");
                if (!WriteSessionCredential(target))
                {
                    return;
                }
                try
                {
                    TokenStatistics after = Statistics();
                    string freshName = CredentialName();
                    _output.WriteLine($"Override written with CRED_PERSIST_SESSION; target={target}; "
                        + $"sameAuthenticationId={before.AuthenticationId.ToString() == after.AuthenticationId.ToString()}; "
                        + $"sameModifiedId={before.ModifiedId.ToString() == after.ModifiedId.ToString()}; "
                        + $"sameTargetlessName={string.Equals(cachedName, freshName, StringComparison.OrdinalIgnoreCase)}.");
                    Login actual = null;
                    try
                    {
                        actual = await ReadLogin(connectionString, async);
                    }
                    catch (SqlException ex)
                    {
                        _output.WriteLine($"Override SQL authentication failed; error={ex.Number}. Override selection is unverified.");
                    }
                    if (actual != null)
                    {
                        bool selectedPeer = string.Equals(actual.Original, Expected(true), StringComparison.OrdinalIgnoreCase);
                        Assert.True(selectedPeer || string.Equals(actual.Original, Expected(false), StringComparison.OrdinalIgnoreCase),
                            "SQL authenticated neither of the disposable accounts.");
                        AssertLogin(actual, selectedPeer);
                        Log("override/after", freshName, actual);
                        _output.WriteLine(selectedPeer
                            ? $"Override selected B; cachedNameMatchesSqlAccount={CredentialNameMatchesAccount(Domain(true), User(true), cachedName)}."
                            : "UNCOVERED: SQL retained A; this target override was not selected by this environment.");
                    }
                }
                finally
                {
                    if (!CredDelete(target, CredentialTypeDomainPassword, 0))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Session credential cleanup failed.");
                    }
                }
                await ProbeCurrent(false, connectionString, async, "override/removed");
            });
            Assert.Equal(process.AuthenticationId.ToString(), Statistics().AuthenticationId.ToString());
            Assert.Equal(processLogin.Sid, (await ReadLogin(connectionString, async)).Sid);
        }

        /// <summary>Fails designated CI coverage explicitly if either disposable account is absent.</summary>
        private static void AssertConfiguration()
        {
            Assert.True(PoolIdentityImpersonationTest.IsConfigured, "Account A and the TCP SQL endpoint must be configured.");
            Assert.False(string.IsNullOrWhiteSpace(User(true)), "Account B must be configured.");
            Assert.False(string.IsNullOrWhiteSpace(Domain(true)), "Account B's domain must be configured.");
            Assert.False(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_PEER_PASSWORD")),
                "Account B's secret must be configured.");
            Assert.False(string.Equals(Expected(false), Expected(true), StringComparison.OrdinalIgnoreCase),
                "The experiment requires two distinct disposable accounts.");
        }

        /// <summary>Builds a TCP/master connection that cannot reuse any pooled authentication.</summary>
        private static string ConnectionString() => new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_CONNECTION_STRING"))
        {
            Pooling = false,
            MinPoolSize = 0,
            ConnectRetryCount = 0,
            ConnectTimeout = 15
        }.ConnectionString;

        /// <summary>Returns the configured username for one disposable account.</summary>
        private static string User(bool peer) => Environment.GetEnvironmentVariable(
            peer ? "SQLCLIENT_NETONLY_PEER_USER" : "SQLCLIENT_NETONLY_USER");

        /// <summary>Returns the configured local domain for one disposable account.</summary>
        private static string Domain(bool peer) => Environment.GetEnvironmentVariable(
            peer ? "SQLCLIENT_NETONLY_PEER_DOMAIN" : "SQLCLIENT_NETONLY_DOMAIN");

        /// <summary>Returns the expected SQL Windows login for one disposable account.</summary>
        private static string Expected(bool peer) => Domain(peer) + "\\" + User(peer);

        /// <summary>Creates a fresh outbound logon without validating or logging its secret.</summary>
        private static SafeAccessTokenHandle CreateToken(bool peer) => WindowsImpersonationHelper.LogonNetOnly(
            User(peer), Domain(peer), Environment.GetEnvironmentVariable(
                peer ? "SQLCLIENT_NETONLY_PEER_PASSWORD" : "SQLCLIENT_NETONLY_PASSWORD"));

        /// <summary>Queries SSPI and SQL inside the same impersonation scope, awaiting all SQL work.</summary>
        private Task<Login> Probe(SafeAccessTokenHandle token, bool peer, string connectionString, bool async, string label) =>
            WindowsIdentity.RunImpersonated(token, () => ProbeCurrent(peer, connectionString, async, label));

        /// <summary>Isolates name-query effects using the passing regression preflight's unpooled open path.</summary>
        private async Task DiagnosePrequery(bool async)
        {
            string connectionString = new SqlConnectionStringBuilder(PoolIdentityImpersonationTest.BuildConnectionString())
            {
                Pooling = false,
                MinPoolSize = 0
            }.ConnectionString;
            Log("Control/cacheable-process", "(no SSPI query)", await ReadLogin(connectionString, async, false));
            using SafeAccessTokenHandle control = CreateToken(false);
            using SafeAccessTokenHandle queried = CreateToken(false);
            var controlResults = await WindowsIdentity.RunImpersonated(control, async () =>
            {
                string beforeOpen = Statistics().AuthenticationId.ToString();
                string login = await PoolIdentityImpersonationTest.GetLogin(connectionString, async);
                _output.WriteLine($"Control/no-SSPI: beforeOpenAuthenticationId={beforeOpen}; "
                    + $"afterOpenAuthenticationId={Statistics().AuthenticationId}; SQL={login}; async={async}.");
                Login cached = await ReadLogin(connectionString, async, false);
                Login recompiled = await ReadLogin(connectionString, async);
                Log("Control/cacheable-A", "(no SSPI query)", cached);
                Log("Control/recompiled-A", "(no SSPI query)", recompiled);
                return (Original: login, Cached: cached, Recompiled: recompiled);
            });
            var queriedResults = await WindowsIdentity.RunImpersonated(queried, async () =>
            {
                string lookupId = Statistics().AuthenticationId.ToString();
                string name = CredentialName();
                string beforeOpen = Statistics().AuthenticationId.ToString();
                string login = await PoolIdentityImpersonationTest.GetLogin(connectionString, async);
                _output.WriteLine($"Control/acquire-query-free: SSPI={name}; lookupAuthenticationId={lookupId}; "
                    + $"beforeOpenAuthenticationId={beforeOpen}; afterOpenAuthenticationId={Statistics().AuthenticationId}; "
                    + $"SQL={login}; async={async}.");
                Assert.Equal(lookupId, beforeOpen);
                AssertCredentialName(false, name);
                Login cached = await ReadLogin(connectionString, async, false);
                Login recompiled = await ReadLogin(connectionString, async);
                Log("Control/queried-cacheable-A", name, cached);
                Log("Control/queried-recompiled-A", name, recompiled);
                return (Original: login, Cached: cached, Recompiled: recompiled);
            });
            AssertName(Expected(false), controlResults.Original);
            AssertName(Expected(false), queriedResults.Original);
            AssertName(Expected(false), controlResults.Cached.Original);
            AssertName(Expected(false), queriedResults.Cached.Original);
            AssertLogin(controlResults.Recompiled, false);
            AssertLogin(queriedResults.Recompiled, false);
        }

        /// <summary>Checks the preconnection name and the authenticated SQL name and SID.</summary>
        private async Task<Login> ProbeCurrent(bool peer, string connectionString, bool async, string label)
        {
            string lookupId = Statistics().AuthenticationId.ToString();
            string name = CredentialName();
            AssertCredentialName(peer, name);
            string beforeOpen = Statistics().AuthenticationId.ToString();
            Login login = await ReadLogin(connectionString, async);
            Log(label, name, login);
            _output.WriteLine($"{label}: lookupAuthenticationId={lookupId}; beforeOpenAuthenticationId={beforeOpen}; "
                + $"afterOpenAuthenticationId={Statistics().AuthenticationId}; async={async}; "
                + $"managedSNI={DataTestUtility.UseManagedSNIOnWindows}.");
            Assert.Equal(lookupId, beforeOpen);
            AssertLogin(login, peer);
            return login;
        }

        /// <summary>Checks the two observed SSPI formats against the exact configured local account.</summary>
        private static void AssertCredentialName(bool peer, string actual) =>
            Assert.True(CredentialNameMatchesAccount(Domain(peer), User(peer), actual),
                $"Expected SSPI name for '{Expected(peer)}', received '{actual}'.");

        /// <summary>Recognizes local SAM and user-at-machine forms without resolving domain aliases.</summary>
        private static bool CredentialNameMatchesAccount(string domain, string user, string actual) =>
            string.Equals(domain + "\\" + user, actual, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user + "@" + domain, actual, StringComparison.OrdinalIgnoreCase);

        /// <summary>Preserves exact account matching across the two SSPI formats observed in CI.</summary>
        [Theory]
        [InlineData(@"machine\account", true)]
        [InlineData("account@machine", true)]
        [InlineData(@"MACHINE\ACCOUNT", true)]
        [InlineData("ACCOUNT@MACHINE", true)]
        [InlineData(@"machine\other", false)]
        [InlineData("other@machine", false)]
        [InlineData(@"other\account", false)]
        [InlineData("account@other", false)]
        public void OutboundName_LocalAccountFormats(string actual, bool expected) =>
            Assert.Equal(expected, CredentialNameMatchesAccount("machine", "account", actual));

        /// <summary>Checks SQL SAM-form names without treating case differences as distinct accounts.</summary>
        private static void AssertName(string expected, string actual) =>
            Assert.True(string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase),
                $"Expected '{expected}', received '{actual}'.");

        /// <summary>Verifies both SQL name functions, Windows SID, NTLM authentication and TCP.</summary>
        private static void AssertLogin(Login login, bool peer)
        {
            AssertName(Expected(peer), login.Original);
            AssertName(Expected(peer), login.Current);
            Assert.Equal(((SecurityIdentifier)new NTAccount(Domain(peer), User(peer))
                .Translate(typeof(SecurityIdentifier))).Value, login.Sid);
            Assert.Equal("NTLM", login.Authentication);
            Assert.Equal("TCP", login.Transport);
        }

        /// <summary>Publishes only identities and protocol observations, never credentials.</summary>
        private void Log(string label, string name, Login login) => _output.WriteLine(
            $"{label}: SSPI={name}; ORIGINAL_LOGIN={login.Original}; SUSER_SNAME={login.Current}; "
            + $"SUSER_SID={login.Sid}; auth_scheme={login.Authentication}; transport={login.Transport}.");

        /// <summary>Reads current-session properties, normally recompiling context-sensitive scalar expressions.</summary>
        private async Task<Login> ReadLogin(string connectionString, bool async, bool recompile = true)
        {
            using var connection = new SqlConnection(connectionString);
            if (async) await connection.OpenAsync();
            else connection.Open();
            using var command = new SqlCommand(
                "SELECT ORIGINAL_LOGIN(), SUSER_SNAME(), SUSER_SID(), "
                + "CONVERT(varchar(30), CONNECTIONPROPERTY('auth_scheme')), "
                + "CONVERT(varchar(30), CONNECTIONPROPERTY('net_transport'))"
                + (recompile ? " OPTION (RECOMPILE)" : ""), connection);
            Login login;
            using (SqlDataReader reader = async ? await command.ExecuteReaderAsync() : command.ExecuteReader())
            {
                Assert.True(async ? await reader.ReadAsync() : reader.Read());
                login = new Login
                {
                    Original = reader.GetString(0),
                    Current = reader.GetString(1),
                    Sid = new SecurityIdentifier((byte[])reader.GetValue(2), 0).Value,
                    Authentication = reader.GetString(3),
                    Transport = reader.GetString(4)
                };
            }
            if (!string.Equals(login.Original, login.Current, StringComparison.OrdinalIgnoreCase))
            {
                await LogSqlSecurityContext(connection, async);
            }
            return login;
        }

        /// <summary>Diagnoses mismatched identities using only the current session and its login token.</summary>
        private async Task LogSqlSecurityContext(SqlConnection connection, bool async)
        {
            using var command = new SqlCommand(
                "SELECT login_name, original_login_name, CONVERT(varchar(172), security_id, 1), "
                + "CONVERT(varchar(172), original_security_id, 1), CONVERT(varchar(10), IS_SRVROLEMEMBER('sysadmin')) "
                + "FROM sys.dm_exec_sessions WHERE session_id = @@SPID; "
                + "SELECT name, type, usage, CONVERT(varchar(172), sid, 1) FROM sys.login_token", connection);
            using SqlDataReader reader = async ? await command.ExecuteReaderAsync() : command.ExecuteReader();
            Assert.True(async ? await reader.ReadAsync() : reader.Read());
            _output.WriteLine($"SQL/session: login_name={reader.GetString(0)}; original_login_name={reader.GetString(1)}; "
                + $"security_id={reader.GetString(2)}; original_security_id={reader.GetString(3)}; "
                + $"sysadmin={(reader.IsDBNull(4) ? "NULL" : reader.GetString(4))}.");
            Assert.True(async ? await reader.NextResultAsync() : reader.NextResult());
            while (async ? await reader.ReadAsync() : reader.Read())
            {
                _output.WriteLine($"SQL/login-token: name={reader.GetString(0)}; type={reader.GetString(1)}; "
                    + $"usage={reader.GetString(2)}; sid={reader.GetString(3)}.");
            }
        }

        /// <summary>Acquires a fresh targetless Negotiate credential under the effective identity.</summary>
        private static string CredentialName()
        {
            CheckStatus(AcquireCredentialsHandle(null, "Negotiate", 2, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, IntPtr.Zero, out CredentialHandle handle, out _));
            try
            {
                CheckStatus(QueryCredentialsAttributes(ref handle, 1, out IntPtr name));
                try { return Marshal.PtrToStringUni(name); }
                finally { CheckStatus(FreeContextBuffer(name)); }
            }
            finally { CheckStatus(FreeCredentialsHandle(ref handle)); }
        }

        /// <summary>Reports native SSPI errors without credential data.</summary>
        private static void CheckStatus(int status)
        {
            if (status != 0) throw new Win32Exception(status, $"SSPI status 0x{status:X8}.");
        }

        /// <summary>Snapshots the effective token's authentication and modification IDs.</summary>
        private static TokenStatistics Statistics()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            if (!GetTokenInformation(identity.AccessToken, 10, out TokenStatistics statistics,
                Marshal.SizeOf<TokenStatistics>(), out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return statistics;
        }

        /// <summary>
        /// Writes B's credential with session-only persistence; callers must already be in a
        /// fresh NEW_CREDENTIALS session and delete the exact target in finally.
        /// </summary>
        private bool WriteSessionCredential(string target)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_PEER_PASSWORD"));
            IntPtr blob = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, blob, bytes.Length);
                var credential = new Credential
                {
                    Type = CredentialTypeDomainPassword,
                    TargetName = target,
                    BlobSize = (uint)bytes.Length,
                    Blob = blob,
                    Persist = 1, // CRED_PERSIST_SESSION: never write the agent's ordinary credential store.
                    UserName = Expected(true)
                };
                if (CredWrite(ref credential, 0)) return true;
                int error = Marshal.GetLastWin32Error();
                if (error != 5 && error != 50 && error != 1312)
                    throw new Win32Exception(error, "Session credential write failed.");
                _output.WriteLine($"UNCOVERED: session Credential Manager override unavailable; Win32={error}.");
                return false;
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, blob, bytes.Length);
                Marshal.FreeHGlobal(blob);
            }
        }

        private sealed class Login
        {
            internal string Original, Current, Sid, Authentication, Transport;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            internal uint Low;
            internal int High;
            public override string ToString() => $"{High:X8}{Low:X8}";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenStatistics
        {
            internal Luid TokenId, AuthenticationId;
            internal long Expiration;
            internal int Type, Level;
            internal uint Charged, Available, Groups, Privileges;
            internal Luid ModifiedId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CredentialHandle { internal IntPtr Lower, Upper; }

        private const uint CredentialTypeDomainPassword = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            internal uint Flags, Type;
            internal string TargetName, Comment;
            internal long LastWritten;
            internal uint BlobSize;
            internal IntPtr Blob;
            internal uint Persist, AttributeCount;
            internal IntPtr Attributes;
            internal string TargetAlias, UserName;
        }

        [DllImport("secur32.dll", EntryPoint = "AcquireCredentialsHandleW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int AcquireCredentialsHandle(string principal, string package, uint use,
            IntPtr logonId, IntPtr authData, IntPtr getKey, IntPtr getKeyArgument,
            out CredentialHandle handle, out long expiry);

        [DllImport("secur32.dll", EntryPoint = "QueryCredentialsAttributesW", ExactSpelling = true)]
        private static extern int QueryCredentialsAttributes(ref CredentialHandle handle, uint attribute, out IntPtr name);

        [DllImport("secur32.dll", ExactSpelling = true)]
        private static extern int FreeCredentialsHandle(ref CredentialHandle handle);

        [DllImport("secur32.dll", ExactSpelling = true)]
        private static extern int FreeContextBuffer(IntPtr buffer);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int kind,
            out TokenStatistics statistics, int size, out int needed);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredWrite(ref Credential credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredDelete(string target, uint type, uint flags);
    }
}
