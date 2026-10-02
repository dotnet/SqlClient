// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Text;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Builds deterministic session-local mixed rows, including NULLs, outside timing.</summary>
    internal static class MixedRowFixture
    {
        /// <summary>Creates one 16- or 64-column fixture and its temporary row-number source.</summary>
        /// <param name="createCommand">Factory that owns commands and applies the setup timeout.</param>
        /// <param name="rows">Number of rows to populate.</param>
        /// <param name="columnGroups">Number of repeated 16-type column groups.</param>
        /// <returns>The session-local table name.</returns>
        internal static string Create(Func<string, SqlCommand> createCommand, long rows, int columnGroups)
        {
            if (rows <= 0 || rows > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(rows));
            }
            if (columnGroups != 1 && columnGroups != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(columnGroups));
            }

            // Generate deterministic data once. Every fourth row has NULLs in all value columns.
            // Create outside sp_executesql so the table belongs to the session, not the
            // parameterized setup batch's scope.
            createCommand("CREATE TABLE #Numbers (Id int NOT NULL);").ExecuteNonQuery();
            SqlCommand numbers = createCommand(@"
                INSERT INTO #Numbers VALUES (1);
                DECLARE @count int = 1;
                WHILE @count < @rows
                BEGIN
                    INSERT INTO #Numbers SELECT TOP (@rows - @count) Id + @count FROM #Numbers;
                    SET @count = (SELECT COUNT(*) FROM #Numbers);
                END;");
            numbers.Parameters.Add("@rows", SqlDbType.Int).Value = checked((int)rows);
            numbers.ExecuteNonQuery();

            string[] expressions =
            {
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 1 END AS bit)",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 7 END AS tinyint)",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 123 END AS smallint)",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE n.Id END AS int)",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 1234567890123 END AS bigint)",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 1234.5678 END AS decimal(18,4))",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 1.25 END AS real)",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 2.5 END AS float)",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE '2024-01-02T03:04:05' END AS datetime2(7))",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE '2024-01-02T03:04:05+02:00' END AS datetimeoffset(7))",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE '03:04:05' END AS time(7))",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE '12345678-1234-1234-1234-123456789012' END AS uniqueidentifier)",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 'small ascii value' END AS varchar(32))",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE N'small unicode value' END AS nvarchar(32))",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 0x01020304 END AS binary(16))",
                "CAST(CASE WHEN n.Id % 4 = 0 THEN NULL ELSE 0x0102030405060708 END AS varbinary(32))"
            };
            StringBuilder query = new("SELECT ");
            for (int group = 0; group < columnGroups; group++)
            {
                for (int column = 0; column < expressions.Length; column++)
                {
                    if (group != 0 || column != 0)
                    {
                        query.Append(", ");
                    }
                    query.Append(expressions[column]).Append(" AS c")
                        .Append(group).Append('_').Append(column);
                }
            }
            query.Append(" INTO #SmallRows FROM #Numbers AS n;");
            createCommand(query.ToString()).ExecuteNonQuery();
            return "#SmallRows";
        }
    }
}
