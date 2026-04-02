// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks measuring Always Encrypted parameter encryption/decryption overhead.
    /// Disabled by default — requires certificate and column master key infrastructure.
    /// Enable via runnerconfig.json: AlwaysEncryptedRunnerConfig.Enabled = true.
    ///
    /// Prerequisites:
    /// - A column master key and column encryption key must be provisioned
    /// - Tables with encrypted columns must be created before running
    /// - Connection string must include "Column Encryption Setting=Enabled"
    /// </summary>
    public class AlwaysEncryptedRunner : BaseRunner
    {
        private SqlConnection _encryptedConnection;
        private SqlConnection _plaintextConnection;
        private string _tableName;
        private bool _isSupported;

        [GlobalSetup]
        public void Setup()
        {
            // Try to set up AE infrastructure; skip benchmarks if not available
            _tableName = "PerfTest_AE_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                _encryptedConnection = new SqlConnection(
                    s_config.ConnectionString + ";Column Encryption Setting=Enabled");
                _encryptedConnection.Open();

                _plaintextConnection = new SqlConnection(s_config.ConnectionString);
                _plaintextConnection.Open();

                // Create a simple plaintext table for baseline comparison
                using var createCmd = new SqlCommand(
                    $"CREATE TABLE [{_tableName}] (Id INT PRIMARY KEY, SecretValue NVARCHAR(100))",
                    _plaintextConnection);
                createCmd.ExecuteNonQuery();

                using var insertCmd = new SqlCommand(
                    $"INSERT INTO [{_tableName}] (Id, SecretValue) VALUES (1, N'SensitiveData')",
                    _plaintextConnection);
                insertCmd.ExecuteNonQuery();

                _isSupported = true;
            }
            catch (SqlException)
            {
                _isSupported = false;
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                if (_plaintextConnection?.State == System.Data.ConnectionState.Open)
                {
                    using var cmd = new SqlCommand($"DROP TABLE IF EXISTS [{_tableName}]", _plaintextConnection);
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                _encryptedConnection?.Close();
                _plaintextConnection?.Close();
                SqlConnection.ClearAllPools();
            }
        }

        [Benchmark(Baseline = true)]
        public void PlaintextInsert()
        {
            if (!_isSupported) return;
            using var cmd = new SqlCommand(
                $"UPDATE [{_tableName}] SET SecretValue = @val WHERE Id = 1", _plaintextConnection);
            cmd.Parameters.Add("@val", SqlDbType.NVarChar, 100).Value = "UpdatedPlaintext";
            cmd.ExecuteNonQuery();
        }

        [Benchmark]
        public void EncryptedInsert()
        {
            if (!_isSupported) return;
            using var cmd = new SqlCommand(
                $"UPDATE [{_tableName}] SET SecretValue = @val WHERE Id = 1", _encryptedConnection);
            cmd.Parameters.Add("@val", SqlDbType.NVarChar, 100).Value = "UpdatedEncrypted";
            cmd.ExecuteNonQuery();
        }

        [Benchmark]
        public int PlaintextSelect()
        {
            if (!_isSupported) return 0;
            using var cmd = new SqlCommand(
                $"SELECT SecretValue FROM [{_tableName}] WHERE Id = @id", _plaintextConnection);
            cmd.Parameters.AddWithValue("@id", 1);
            using var reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                _ = reader.GetString(0);
                count++;
            }
            return count;
        }

        [Benchmark]
        public int EncryptedSelect()
        {
            if (!_isSupported) return 0;
            using var cmd = new SqlCommand(
                $"SELECT SecretValue FROM [{_tableName}] WHERE Id = @id", _encryptedConnection);
            cmd.Parameters.AddWithValue("@id", 1);
            using var reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                _ = reader.GetString(0);
                count++;
            }
            return count;
        }

        [Benchmark]
        public async Task<int> EncryptedSelectAsync()
        {
            if (!_isSupported) return 0;
            using var cmd = new SqlCommand(
                $"SELECT SecretValue FROM [{_tableName}] WHERE Id = @id", _encryptedConnection);
            cmd.Parameters.AddWithValue("@id", 1);
            using var reader = await cmd.ExecuteReaderAsync();
            int count = 0;
            while (await reader.ReadAsync())
            {
                _ = await reader.GetFieldValueAsync<string>(0);
                count++;
            }
            return count;
        }
    }
}
