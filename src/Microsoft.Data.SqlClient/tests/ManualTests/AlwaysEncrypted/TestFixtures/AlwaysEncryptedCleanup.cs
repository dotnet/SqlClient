// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// Helpers for reliably removing Always Encrypted (AE) test artifacts.
    ///
    /// Column Master Keys (CMKs) and Column Encryption Keys (CEKs) are assigned
    /// identifiers from a bounded range. When AE test setup/teardown leaks keys
    /// (a create fails part way, a drop throws, or the test host is killed),
    /// those identifiers are never reclaimed. On a long-lived shared server the
    /// range eventually fills up and every subsequent CREATE COLUMN ENCRYPTION
    /// KEY fails with "Create failed because all available identifiers have been
    /// exhausted." These helpers make cleanup best-effort so a single failure
    /// never leaves keys behind.
    /// </summary>
    internal static class AlwaysEncryptedCleanup
    {
        // Ensures the orphan sweep runs at most once per test process.
        private static int s_orphanSweepStarted;

        /// <summary>
        /// Number of orphaned AE artifacts removed by a sweep.
        /// </summary>
        internal readonly struct AeSweepResult
        {
            public AeSweepResult(int tables, int ceks, int cmks)
            {
                Tables = tables;
                Ceks = ceks;
                Cmks = cmks;
            }

            public int Tables { get; }
            public int Ceks { get; }
            public int Cmks { get; }
            public int Total => Tables + Ceks + Cmks;
        }

        /// <summary>
        /// Drops every supplied object, continuing past any individual failure.
        /// Intended for teardown and for rolling back a partially-completed
        /// setup. Objects should already be ordered so that dependents (tables)
        /// come before the keys they reference.
        /// </summary>
        internal static void DropSafely(SqlConnection sqlConnection, IEnumerable<DbObject> databaseObjects)
        {
            foreach (DbObject databaseObject in databaseObjects)
            {
                try
                {
                    databaseObject.Drop(sqlConnection);
                }
                catch (Exception ex)
                {
                    // Best-effort: log and keep going so the remaining objects
                    // (especially the CMK/CEK) still get dropped.
                    Console.WriteLine($"AlwaysEncryptedCleanup: failed to drop '{databaseObject.Name}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Runs the orphan sweep once per test process, before the AE fixtures
        /// create any keys. Recovers identifiers leaked by earlier runs that
        /// were cancelled or hard-killed before their teardown could run. The
        /// ManualTests assembly runs serially (see XUnitAssemblyAttributes), so
        /// this cannot race with keys created by the current run.
        /// </summary>
        internal static void SweepOrphansOnce(IEnumerable<string> setupConnectionStrings)
        {
            if (Interlocked.Exchange(ref s_orphanSweepStarted, 1) != 0)
            {
                return;
            }

            foreach (string connectionString in setupConnectionStrings)
            {
                try
                {
                    using SqlConnection sqlConnection = new SqlConnection(connectionString);
                    sqlConnection.Open();
                    AeSweepResult result = DropOrphanedAeArtifacts(sqlConnection);
                    if (result.Total > 0)
                    {
                        Console.WriteLine(
                            $"AlwaysEncryptedCleanup: swept {result.Cmks} orphaned CMK(s), " +
                            $"{result.Ceks} CEK(s), {result.Tables} table(s) from " +
                            $"'{sqlConnection.DataSource}'.");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"AlwaysEncryptedCleanup: no orphaned AE artifacts found on " +
                            $"'{sqlConnection.DataSource}'.");
                    }
                }
                catch (Exception ex)
                {
                    // Best-effort recovery; never fail a test run because the
                    // sweep could not complete.
                    Console.WriteLine($"AlwaysEncryptedCleanup: orphan sweep failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Drops any orphaned AE test artifacts left on the server by earlier
        /// runs (e.g. runs that were cancelled or hard-killed before teardown).
        /// Only objects created by the AE test fixtures are removed: those are
        /// named with the well-known "AE_" / "AE-" prefixes produced by
        /// <see cref="DatabaseHelper.GenerateUniqueName(string)"/> and
        /// <c>SQLSetupStrategy.GenerateUniqueName</c>. Drops in dependency
        /// order (tables, then CEKs, then CMKs) and is idempotent. Returns the
        /// number of objects removed.
        /// </summary>
        internal static AeSweepResult DropOrphanedAeArtifacts(SqlConnection sqlConnection)
        {
            const string sql = @"
SET NOCOUNT ON;
DECLARE @name sysname, @sql nvarchar(max);
DECLARE @tables int = 0, @ceks int = 0, @cmks int = 0;

-- 1) Tables whose encrypted columns depend on an AE-prefixed CEK, plus any
--    AE-prefixed tables directly.
DECLARE tbl CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
    FROM sys.columns AS c
    JOIN sys.tables AS t ON t.object_id = c.object_id
    JOIN sys.column_encryption_keys AS cek
         ON cek.column_encryption_key_id = c.column_encryption_key_id
    WHERE c.column_encryption_key_id <> 0
      AND (cek.name LIKE 'AE[_]%' OR cek.name LIKE 'AE-%')
    UNION
    SELECT QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
    FROM sys.tables AS t
    WHERE t.name LIKE 'AE[_]%' OR t.name LIKE 'AE-%';
OPEN tbl;
FETCH NEXT FROM tbl INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DROP TABLE ' + @name + N';';
    BEGIN TRY EXEC sp_executesql @sql; SET @tables += 1; END TRY BEGIN CATCH END CATCH;
    FETCH NEXT FROM tbl INTO @name;
END
CLOSE tbl; DEALLOCATE tbl;

-- 2) AE-prefixed Column Encryption Keys.
DECLARE cek CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.column_encryption_keys
    WHERE name LIKE 'AE[_]%' OR name LIKE 'AE-%';
OPEN cek;
FETCH NEXT FROM cek INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DROP COLUMN ENCRYPTION KEY ' + QUOTENAME(@name) + N';';
    BEGIN TRY EXEC sp_executesql @sql; SET @ceks += 1; END TRY BEGIN CATCH END CATCH;
    FETCH NEXT FROM cek INTO @name;
END
CLOSE cek; DEALLOCATE cek;

-- 3) AE-prefixed Column Master Keys.
DECLARE cmk CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.column_master_keys
    WHERE name LIKE 'AE[_]%' OR name LIKE 'AE-%';
OPEN cmk;
FETCH NEXT FROM cmk INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DROP COLUMN MASTER KEY ' + QUOTENAME(@name) + N';';
    BEGIN TRY EXEC sp_executesql @sql; SET @cmks += 1; END TRY BEGIN CATCH END CATCH;
    FETCH NEXT FROM cmk INTO @name;
END
CLOSE cmk; DEALLOCATE cmk;

SELECT @tables AS DroppedTables, @ceks AS DroppedCeks, @cmks AS DroppedCmks;";

            using SqlCommand command = sqlConnection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new AeSweepResult(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2));
            }

            return default;
        }
    }
}
