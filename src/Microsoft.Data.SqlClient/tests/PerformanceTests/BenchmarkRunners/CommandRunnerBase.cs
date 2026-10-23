// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Owns untimed connection/command setup and teardown for execution benchmarks.</summary>
    public abstract class CommandRunnerBase : BaseRunner
    {
        private readonly List<SqlCommand> _commands = new();
        protected SqlConnection Connection { get; private set; }

        // Every benchmark runs both settings; no connection-string or config toggle is needed.
        [Params(false, true)]
        public bool Mars { get; set; }

        protected abstract CommandRunnerJob Settings { get; }

        /// <summary>Opens a dedicated session and prepares reusable workload resources.</summary>
        [GlobalSetup]
        public void Setup()
        {
            Settings.Validate();
            _setupComplete = false;
            try
            {
                // A dedicated physical session owns the local temporary tables. Disposing it
                // also removes fixtures after partial setup failures, without clearing other pools.
                SqlConnectionStringBuilder builder = CreateConnectionStringBuilder();
                builder.MultipleActiveResultSets = Mars;
                builder.Pooling = false;
                Connection = new SqlConnection(builder.ConnectionString);
                Connection.Open();
                SetupCommands();
                _setupComplete = true;
            }
            finally
            {
                if (!_setupComplete)
                {
                    Cleanup();
                }
            }
        }

        private bool _setupComplete;

        protected virtual SqlConnectionStringBuilder CreateConnectionStringBuilder() =>
            new(s_config.ConnectionString);

        protected abstract void SetupCommands();

        protected virtual void CleanupFixtures() { }

        /// <summary>Creates a reusable command owned by this runner, including its SQL timeout.</summary>
        /// <param name="sql">SQL text or procedure name.</param>
        /// <returns>The tracked command.</returns>
        protected SqlCommand CreateCommand(string sql)
        {
            SqlCommand command = new(sql, Connection)
            {
                CommandTimeout = Settings.CommandTimeoutSeconds
            };
            _commands.Add(command);
            return command;
        }

        /// <summary>Disposes commands and fixtures before closing the owning session.</summary>
        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                foreach (SqlCommand command in _commands)
                {
                    command.Dispose();
                }
                _commands.Clear();
                CleanupFixtures();
            }
            finally
            {
                Connection?.Dispose();
                Connection = null;
            }
        }
    }
}
