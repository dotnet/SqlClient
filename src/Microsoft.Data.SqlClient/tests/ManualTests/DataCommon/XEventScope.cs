using System;
using System.Data;
using System.Threading;
using System.Xml;

#nullable enable

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public readonly ref struct XEventScope : IDisposable
    {
        #region Private Fields

        /// <summary>
        /// Maximum dispatch latency for XEvents, in seconds.
        /// </summary>
        private const int MaxDispatchLatencySeconds = 5;

        /// <summary>
        /// The connection to use for all operations.
        /// </summary>
        private readonly SqlConnection _connection;

        /// <summary>
        /// Duration for the XEvent session, in minutes.
        /// </summary>
        private readonly ushort _durationInMinutes;

        /// <summary>
        /// <c>true</c> if connected to an Azure SQL instance.
        /// </summary>
        private readonly bool _isAzureSql;

        /// <summary>
        /// <c>true</c> if connected to a non-Azure SQL Server 2025 (version 17) or higher.
        /// </summary>
        private readonly bool _isVersion17OrHigher;

        #endregion

        #region Construction

        /// <summary>
        /// Construct with the specified parameters.
        ///
        /// This will use the connection to query the server properties and
        /// setup and start the XEvent session.
        /// </summary>
        /// <param name="sessionName">The base name of the session.</param>
        /// <param name="connection">The SQL connection to use. (Must already be open.)</param>
        /// <param name="eventSpecification">The event specification T-SQL string.</param>
        /// <param name="targetSpecification">The target specification T-SQL string.</param>
        /// <param name="durationInMinutes">The duration of the session in minutes.</param>
        public XEventScope(
            string sessionName,
            SqlConnection connection,
            string eventSpecification,
            string targetSpecification,
            ushort durationInMinutes = 5)
        {
            SessionName = DataTestUtility.GenerateRandomCharacters(sessionName);

            _connection = connection;
            if (connection.State is not ConnectionState.Open)
            {
                throw new InvalidOperationException("Connection is not open");
            }

            _durationInMinutes = durationInMinutes;

            // EngineEdition 5 indicates Azure SQL.
            _isAzureSql = DataTestUtility.GetSqlServerProperty(connection, DataTestUtility.ServerProperty.EngineEdition) == "5";
            if (!_isAzureSql)
            {
                // Determine if we're connected to a SQL Server instance version 17 or higher.
                string majorVersionString = DataTestUtility.GetSqlServerProperty(
                        connection,
                        DataTestUtility.ServerProperty.ProductMajorVersion);
                int majorVersionInt = int.Parse(majorVersionString);

                _isVersion17OrHigher = majorVersionInt >= 17;
            }

            // Setup and start the XEvent session.
            string sessionLocation = _isAzureSql ? "DATABASE" : "SERVER";

            // Both Azure SQL and SQL Server 2025+ support setting a maximum duration for the
            // XEvent session.
            string duration = _isAzureSql || _isVersion17OrHigher
                ? $"MAX_DURATION={_durationInMinutes} MINUTES,"
                : string.Empty;

            string xEventCreateAndStartCommandText =
                $"CREATE EVENT SESSION [{SessionName}] ON {sessionLocation}" +
                $"  {eventSpecification} " +
                $"  {targetSpecification} " +
                $"WITH (" +
                $"  {duration} " +
                $"  MAX_MEMORY=16 MB," +
                $"  EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS, " +
                $"  MAX_DISPATCH_LATENCY={MaxDispatchLatencySeconds} SECONDS, " +
                $"  MAX_EVENT_SIZE=0 KB, " +
                $"  MEMORY_PARTITION_MODE=NONE, " +
                $"  TRACK_CAUSALITY=ON, " +
                $"  STARTUP_STATE=OFF " +
                $"); " +
                $"ALTER EVENT SESSION [{SessionName}] ON {sessionLocation} STATE = START ";

            using SqlCommand createXEventSession = new(xEventCreateAndStartCommandText, _connection);
            createXEventSession.ExecuteNonQuery();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The name of the XEvent session, derived from the session name provided at construction
        /// time, with a unique suffix appended.
        /// </summary>
        public string SessionName { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Disposal stops and drops the XEvent session.
        /// </summary>
        /// <remarks>
        /// Disposal isn't perfect - tests can abort without cleaning up the events they have
        /// created. For Azure SQL targets that outlive the test pipelines, it is beneficial to
        /// periodically log into the database and drop old XEvent sessions using T-SQL similar to
        /// this:
        ///
        /// <code lang="SQL">
        /// DECLARE @sql NVARCHAR(MAX) = N'';
        ///
        /// -- Identify inactive (stopped) event sessions and generate DROP commands
        /// SELECT @sql += N'DROP EVENT SESSION [' + name + N'] ON SERVER;' + CHAR(13) + CHAR(10)
        /// FROM sys.server_event_sessions
        /// WHERE running = 0; -- Filter for sessions that are not running (inactive)
        ///
        /// -- Print the generated commands for review (optional, but recommended)
        /// PRINT @sql;
        ///
        /// -- Execute the generated commands
        /// EXEC sys.sp_executesql @sql;
        /// </code>
        /// </remarks>
        public void Dispose()
        {
            // We choose the sys.(database|server)_event_sessions views here to ensure we find
            // sessions that may not be running.
            string dropXEventSessionCommand = _isAzureSql
                ? $"IF EXISTS (SELECT * FROM sys.database_event_sessions WHERE name='{SessionName}')" +
                  $"  DROP EVENT SESSION [{SessionName}] ON DATABASE"
                : $"IF EXISTS (SELECT * FROM sys.server_event_sessions WHERE name='{SessionName}')" +
                  $"  DROP EVENT SESSION [{SessionName}] ON SERVER";

            using SqlCommand command = new SqlCommand(dropXEventSessionCommand, _connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Query the XEvent session for its collected events, returning them as an XML document.
        /// </summary>
        /// <remarks>
        /// This always blocks the thread for MaxDispatchLatencySeconds to ensure that all events
        /// have been flushed into the ring buffer.
        /// </remarks>
        /// <exception cref="Exception">Thrown if the query did not return a string result.</exception>
        public XmlDocument GetEvents()
        {
            string xEventQuery = _isAzureSql
                ? $"SELECT xet.target_data " +
                  $"FROM sys.dm_xe_database_session_targets AS xet " +
                  $"  INNER JOIN sys.dm_xe_database_sessions AS xe" +
                  $"    ON (xe.address = xet.event_session_address) " +
                  $"WHERE xe.name = '{SessionName}'"
                : $"SELECT xet.target_data " +
                  $"FROM sys.dm_xe_session_targets AS xet " +
                  $"  INNER JOIN sys.dm_xe_sessions AS xe " +
                  $"    ON (xe.address = xet.event_session_address) " +
                  $"WHERE xe.name = '{SessionName}'";

            using SqlCommand command = new SqlCommand(xEventQuery, _connection);

            // Wait for maximum dispatch latency to ensure all events have been flushed to the
            // ring buffer.
            Thread.Sleep(MaxDispatchLatencySeconds * 1000);

            string targetData = command.ExecuteScalar() as string
                                ?? throw new Exception("Command did not return a string result");

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(targetData);

            return xmlDocument;
        }

        #endregion
    }
}
