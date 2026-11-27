// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Serializes opt-in BAG tests because they change a process-wide compatibility switch.
    /// </summary>
    [CollectionDefinition(nameof(BasicAvailabilityGroupFailoverTest), DisableParallelization = true)]
    public class BasicAvailabilityGroupFailoverCollection
    {
    }

    /// <summary>
    /// Exercises #5029 against a real, listenerless Basic AG after operator-controlled role changes.
    /// </summary>
    [Collection(nameof(BasicAvailabilityGroupFailoverTest))]
    [Trait("category", "interactive")]
    public class BasicAvailabilityGroupFailoverTest
    {
        private readonly ITestOutputHelper _output;

        public BasicAvailabilityGroupFailoverTest(ITestOutputHelper output)
        {
            _output = output;
        }

        public static bool IsLabEnabled =>
            Environment.GetEnvironmentVariable("SQLCLIENT_BAG_TESTS") == "1";

        /// <summary>
        /// Verifies direct-primary access and diagnoses secondary-to-partner login behavior,
        /// with pooling and async parity. A legacy-path failure is surfaced, not treated as success.
        /// </summary>
        [ConditionalTheory(typeof(BasicAvailabilityGroupFailoverTest), nameof(IsLabEnabled))]
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
        public async Task SecondaryLogin_WithFailoverPartner_RespectsLegacySwitch(
            bool? useLegacySwitch, bool pooling, bool useAsync)
        {
            string password = Environment.GetEnvironmentVariable("SQLCLIENT_BAG_PASSWORD");
            Assert.False(string.IsNullOrEmpty(password), "Set SQLCLIENT_BAG_PASSWORD before running lab tests.");

            SqlConnectionStringBuilder first = CreateOptions(
                Environment.GetEnvironmentVariable("SQLCLIENT_BAG_SERVER1") ?? "127.0.0.1,15191", password);
            SqlConnectionStringBuilder second = CreateOptions(
                Environment.GetEnvironmentVariable("SQLCLIENT_BAG_SERVER2") ?? "127.0.0.1,15192", password);
            bool firstIsPrimary = await InspectReplica(first);
            bool secondIsPrimary = await InspectReplica(second);
            Assert.NotEqual(firstIsPrimary, secondIsPrimary);

            SqlConnectionStringBuilder primary = firstIsPrimary ? first : second;
            SqlConnectionStringBuilder secondary = firstIsPrimary ? second : first;
            primary.InitialCatalog = "SqlClientBagDb";
            secondary.InitialCatalog = "SqlClientBagDb";
            secondary.FailoverPartner = primary.DataSource;
            secondary.Pooling = pooling;
            // ClearPool does not reset pool-group partner selection learned by an earlier case.
            secondary.ApplicationName = $"SqlClientBag-{Guid.NewGuid():N}";

            using SqlConnection directConnection = new(primary.ConnectionString);
            if (useAsync)
            {
                await directConnection.OpenAsync();
            }
            else
            {
                directConnection.Open();
            }

            using SqlCommand directQuery = new("SELECT @@SERVERNAME", directConnection);
            string primaryName = (string)(useAsync
                ? await directQuery.ExecuteScalarAsync()
                : directQuery.ExecuteScalar());
            _output.WriteLine($"Direct primary: {primaryName}; legacy={useLegacySwitch}; pooling={pooling}; async={useAsync}");

            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyFailoverAlternationOnLoginSqlErrors = useLegacySwitch;
            using SqlConnection connection = new(secondary.ConnectionString);
            try
            {
                if (useLegacySwitch == false)
                {
                    SqlException error = useAsync
                        ? await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync())
                        : Assert.Throws<SqlException>(() => connection.Open());
                    WriteErrors(error);
                    Assert.Contains(error.Errors.Cast<SqlError>(), item => item.Number == 976 && item.Class == 14);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
                else
                {
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
                    }
                    catch (SqlException error)
                    {
                        WriteErrors(error);
                        throw;
                    }
                    _output.WriteLine($"Partner connection DataSource: {connection.DataSource}");
                    Assert.Equal(primary.DataSource, connection.DataSource);
                    using SqlCommand query = new("SELECT @@SERVERNAME", connection);
                    Assert.Equal(primaryName, (string)(useAsync
                        ? await query.ExecuteScalarAsync()
                        : query.ExecuteScalar()));
                }
            }
            finally
            {
                if (pooling)
                {
                    SqlConnection.ClearPool(connection);
                }
            }
        }

        /// <summary>
        /// Builds lab-only options with credentials supplied by the operator and bounded timeouts.
        /// </summary>
        private static SqlConnectionStringBuilder CreateOptions(string endpoint, string password) => new()
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

        /// <summary>
        /// Checks that the endpoint hosts a synchronized Basic AG with no listener or readable secondary.
        /// </summary>
        private async Task<bool> InspectReplica(SqlConnectionStringBuilder options)
        {
            using SqlConnection connection = new(options.ConnectionString);
            await connection.OpenAsync();
            using SqlCommand command = new(@"
                SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')),
                    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')),
                    CONVERT(nvarchar(128), SERVERPROPERTY('Edition')),
                    ag.basic_features, ag.cluster_type_desc, ars.role_desc,
                    ar.secondary_role_allow_connections_desc, drs.synchronization_state_desc,
                    (SELECT COUNT(*) FROM sys.availability_group_listeners l WHERE l.group_id = ag.group_id)
                FROM sys.availability_groups ag
                JOIN sys.availability_replicas ar ON ar.group_id = ag.group_id
                JOIN sys.dm_hadr_availability_replica_states ars ON ars.replica_id = ar.replica_id
                JOIN sys.dm_hadr_database_replica_states drs ON drs.replica_id = ar.replica_id
                WHERE ag.name = N'SqlClientBag' AND ars.is_local = 1 AND drs.is_local = 1
                    AND drs.database_id = DB_ID(N'SqlClientBagDb')", connection);
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Missing lab AG/database metadata.");
            _output.WriteLine($"Replica: {reader.GetString(0)}; SQL {reader.GetString(1)}; {reader.GetString(2)}; role={reader.GetString(5)}");
            Assert.True(reader.GetBoolean(3), "A regular AG is not an exact Basic AG reproduction.");
            Assert.Equal("NONE", reader.GetString(4), ignoreCase: true);
            string role = reader.GetString(5);
            Assert.Contains(role, new[] { "PRIMARY", "SECONDARY" });
            Assert.Equal("NO", reader.GetString(6));
            Assert.Equal("SYNCHRONIZED", reader.GetString(7));
            Assert.Equal(0, reader.GetInt32(8));
            Assert.False(await reader.ReadAsync(), "Expected exactly one local lab database.");
            return role == "PRIMARY";
        }

        /// <summary>
        /// Records individual server error numbers and severities without connection strings or passwords.
        /// </summary>
        private void WriteErrors(SqlException exception)
        {
            foreach (SqlError error in exception.Errors)
            {
                _output.WriteLine($"SQL error {error.Number}, severity {error.Class}, state {error.State}: {error.Message}");
            }
        }
    }
}
