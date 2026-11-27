// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Prevents process-wide switch changes from overlapping other tests.
    /// </summary>
    [CollectionDefinition(nameof(DatabaseMirroringFailoverTest), DisableParallelization = true)]
    public class DatabaseMirroringFailoverCollection
    {
    }

    /// <summary>
    /// Verifies genuine database-mirroring partner selection after operator-controlled role changes.
    /// </summary>
    [Collection(nameof(DatabaseMirroringFailoverTest))]
    [Trait("category", "interactive")]
    public class DatabaseMirroringFailoverTest
    {
        private const string DatabaseName = "SqlClientMirrorDb";
        private readonly ITestOutputHelper _output;

        public DatabaseMirroringFailoverTest(ITestOutputHelper output) => _output = output;

        public static bool IsLabEnabled =>
            Environment.GetEnvironmentVariable("SQLCLIENT_MIRROR_TESTS") == "1";

        /// <summary>
        /// Requires default/enabled partner opens to reach the principal and explicit false
        /// to retain login rejection, covering pooling and synchronous/asynchronous opens.
        /// </summary>
        [ConditionalTheory(typeof(DatabaseMirroringFailoverTest), nameof(IsLabEnabled))]
        [InlineData(null, false, false)]
        [InlineData(null, false, true)]
        [InlineData(null, true, false)]
        [InlineData(null, true, true)]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public async Task MirrorLogin_WithFailoverPartner_ReachesPrincipal(
            bool? useLegacySwitch, bool pooling, bool useAsync)
        {
            Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Run this lab from native Windows .NET, not WSL.");
            Assert.Equal(10, Environment.Version.Major);
            using LocalAppContextSwitchesHelper switches = new();
            if (!useLegacySwitch.HasValue)
            {
                Assert.False(AppContext.TryGetSwitch(
                    "Switch.Microsoft.Data.SqlClient.UseLegacyFailoverAlternationOnLoginSqlErrors", out _),
                    "Default coverage requires an unset AppContext switch.");
            }
            switches.UseLegacyFailoverAlternationOnLoginSqlErrors = useLegacySwitch;
            #if NET
            switches.UseManagedNetworking = false;
            #endif
            Assembly driver = typeof(SqlConnection).Assembly;
            string information = driver.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? throw new InvalidOperationException("The loaded driver has no informational version.");
            _output.WriteLine($"Driver: {information}; {driver.FullName}; {driver.Location}");
            _output.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; legacy={useLegacySwitch}; pooling={pooling}; async={useAsync}; native SNI requested");

            SqlConnectionStringBuilder first = CreateOptions("SQLCLIENT_MIRROR_SERVER1");
            SqlConnectionStringBuilder second = CreateOptions("SQLCLIENT_MIRROR_SERVER2");
            (string firstName, bool firstIsPrincipal) = await InspectReplica(first);
            (string secondName, bool secondIsPrincipal) = await InspectReplica(second);
            Assert.NotEqual(firstIsPrincipal, secondIsPrincipal);
            Assert.NotEqual(firstName, secondName);
            SqlConnectionStringBuilder principal = firstIsPrincipal ? first : second;
            SqlConnectionStringBuilder mirror = firstIsPrincipal ? second : first;
            string principalName = firstIsPrincipal ? firstName : secondName;

            principal.InitialCatalog = DatabaseName;
            principal.Pooling = pooling;
            principal.ApplicationName = $"SqlClientMirror-direct-{Guid.NewGuid():N}";
            await VerifyConnection(principal, principalName, useAsync);

            mirror.InitialCatalog = DatabaseName;
            mirror.FailoverPartner = principal.DataSource;
            mirror.Pooling = pooling;
            // Pool groups retain learned partner selection even after ClearPool.
            mirror.ApplicationName = $"SqlClientMirror-partner-{Guid.NewGuid():N}";
            if (useLegacySwitch == false)
            {
                using SqlConnection connection = new(mirror.ConnectionString);
                try
                {
                    SqlException error = useAsync
                        ? await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync())
                        : Assert.Throws<SqlException>(() => connection.Open());
                    foreach (SqlError item in error.Errors)
                    {
                        _output.WriteLine($"Expected explicit-false rejection: SQL error {item.Number}, severity {item.Class}: {item.Message}");
                    }
                    Assert.Contains(error.Errors.Cast<SqlError>(), item => item.Number == 4060 && item.Class == 11);
                    Assert.Contains(error.Errors.Cast<SqlError>(), item => item.Number == 18456 && item.Class == 14);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
                finally
                {
                    if (pooling)
                    {
                        SqlConnection.ClearPool(connection);
                    }
                }
                return;
            }
            await VerifyConnection(mirror, principalName, useAsync);
        }

        /// <summary>
        /// Builds bounded lab connections from runtime credentials and explicitly supplied endpoints.
        /// </summary>
        private static SqlConnectionStringBuilder CreateOptions(string endpointVariable)
        {
            string endpoint = Environment.GetEnvironmentVariable(endpointVariable) ?? string.Empty;
            string password = Environment.GetEnvironmentVariable("SQLCLIENT_MIRROR_PASSWORD") ?? string.Empty;
            Assert.False(string.IsNullOrEmpty(endpoint), $"Set {endpointVariable} to a reachable Windows SQL endpoint.");
            Assert.False(string.IsNullOrEmpty(password), "Set SQLCLIENT_MIRROR_PASSWORD before running lab tests.");
            return new SqlConnectionStringBuilder
            {
                DataSource = endpoint,
                InitialCatalog = "master",
                UserID = "sa",
                Password = password,
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false,
                ConnectTimeout = 30,
                ConnectRetryCount = 1,
                ConnectRetryInterval = 1,
            };
        }

        /// <summary>
        /// Validates synchronous, witnessless database mirroring rather than AG endpoint metadata.
        /// </summary>
        private async Task<(string Name, bool IsPrincipal)> InspectReplica(SqlConnectionStringBuilder options)
        {
            using SqlConnection connection = new(options.ConnectionString);
            await connection.OpenAsync();
            using SqlCommand command = new(@"
                SELECT CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')),
                    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')),
                    CONVERT(nvarchar(128), SERVERPROPERTY('Edition')),
                    mirroring_role_desc, mirroring_state_desc, mirroring_safety_level_desc,
                    mirroring_witness_name, mirroring_partner_name
                FROM sys.database_mirroring WHERE database_id = DB_ID(N'SqlClientMirrorDb')", connection);
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Missing mirrored lab database.");
            string name = reader.GetString(0);
            string role = reader.GetString(3);
            _output.WriteLine($"Replica: {name}; SQL {reader.GetString(1)}; {reader.GetString(2)}; role={role}");
            Assert.Contains(role, new[] { "PRINCIPAL", "MIRROR" });
            Assert.Equal("SYNCHRONIZED", reader.GetString(4));
            Assert.Equal("FULL", reader.GetString(5));
            Assert.True(reader.IsDBNull(6) || reader.GetString(6).Length == 0, "This lab must not have a witness.");
            Assert.False(reader.IsDBNull(7));
            Assert.False(string.IsNullOrEmpty(reader.GetString(7)));
            Assert.False(await reader.ReadAsync(), "Expected exactly one mirrored lab database.");
            return (name, role == "PRINCIPAL");
        }

        /// <summary>
        /// Opens through the chosen API and checks the actual server/database, logging SQL failures.
        /// </summary>
        private async Task VerifyConnection(SqlConnectionStringBuilder options, string principalName, bool useAsync)
        {
            using SqlConnection connection = new(options.ConnectionString);
            try
            {
                if (useAsync)
                {
                    await connection.OpenAsync();
                }
                else
                {
                    connection.Open();
                }
                Assert.Equal(ConnectionState.Open, connection.State);
                using SqlCommand query = new("SELECT CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) + N'|' + DB_NAME()", connection);
                Assert.Equal($"{principalName}|{DatabaseName}", Assert.IsType<string>(useAsync
                    ? await query.ExecuteScalarAsync()
                    : query.ExecuteScalar()));
                _output.WriteLine($"Connected principal: {principalName}; DataSource={connection.DataSource}; partner={options.FailoverPartner}");
            }
            catch (SqlException exception)
            {
                foreach (SqlError error in exception.Errors)
                {
                    _output.WriteLine($"SQL error {error.Number}, severity {error.Class}, state {error.State}: {error.Message}");
                }
                throw;
            }
            finally
            {
                if (options.Pooling)
                {
                    SqlConnection.ClearPool(connection);
                }
            }
        }
    }
}
