// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Provides representative worldwide text for encoding validation tests.
    /// </summary>
    internal static class GlobalizationTestData
    {
        internal const string RepresentativeText =
            "Latin: Caf\u00E9 / Cafe\u0301 | " +
            "Arabic: \u0627\u0644\u0639\u064E\u0631\u064E\u0628\u0650\u064A\u064E\u0651\u0629 | " +
            "Devanagari: \u0939\u093F\u0928\u094D\u0926\u0940 | " +
            "Thai: \u0E20\u0E32\u0E29\u0E32\u0E44\u0E17\u0E22 | " +
            "CJK: \u65E5\u672C\u8A9E \u4E2D\u6587 \uD55C\uAD6D\uC5B4 | " +
            "Supplementary: \uD83D\uDE00 \uD834\uDD1E | " +
            "Emoji sequence: \uD83D\uDC69\uD83C\uDFFD\u200D\uD83D\uDCBB";

        /// <summary>
        /// Repeats the representative text so reads span TDS packets and internal character buffers.
        /// </summary>
        internal static string CreatePacketSpanningText()
        {
            StringBuilder value = new();
            for (int i = 0; i < 16; i++)
            {
                value.Append(RepresentativeText);
            }

            return value.ToString();
        }
    }

    /// <summary>
    /// Validates exact worldwide text preservation through parameters, SQL Server storage, and reader APIs.
    /// </summary>
    [Trait("Set", "3")]
    public static class GlobalizationEncodingTests
    {
        /// <summary>
        /// Verifies normal and streamed Unicode parameters round-trip exactly through buffered and sequential
        /// readers on synchronous and asynchronous paths.
        /// </summary>
        /// <param name="useAsync">Whether command and reader operations use asynchronous APIs.</param>
        /// <param name="streamInput">Whether the input parameter is supplied through a <see cref="StringReader"/>.</param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static async Task UnicodeParameterAndReaderRoundTrip_PreservesWorldwideText(bool useAsync, bool streamInput)
        {
            string expected = GlobalizationTestData.CreatePacketSpanningText();
            SqlConnectionStringBuilder connectionString = new(DataTestUtility.TCPConnectionString)
            {
                PacketSize = 512
            };

            using SqlConnection connection = new(connectionString.ConnectionString);
            if (useAsync)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }

            using Table table = new(connection, nameof(UnicodeParameterAndReaderRoundTrip_PreservesWorldwideText), "(Value nvarchar(max) NOT NULL)");
            using StringReader inputReader = new(expected);
            using (SqlCommand insert = new($"INSERT INTO {table.Name} (Value) VALUES (@value)", connection))
            {
                insert.Parameters.Add(new SqlParameter("@value", SqlDbType.NVarChar, -1)
                {
                    Value = streamInput ? inputReader : expected
                });

                if (useAsync)
                {
                    await insert.ExecuteNonQueryAsync();
                }
                else
                {
                    insert.ExecuteNonQuery();
                }
            }

            string query = $"SELECT Value FROM {table.Name}";
            string directValue = await ReadDirectValue(connection, query, useAsync);
            string streamedValue = await ReadStreamedValue(connection, query, useAsync);

            Assert.Equal(expected, directValue);
            Assert.Equal(expected, streamedValue);
            Assert.Equal(expected.Length, directValue.Length);
            Assert.Equal(expected.Length, streamedValue.Length);
        }

        /// <summary>
        /// Verifies a UTF-8-collated varchar value returns exact worldwide text and the expected UTF-8 bytes
        /// on synchronous and asynchronous paths.
        /// </summary>
        /// <param name="useAsync">Whether command and reader operations use asynchronous APIs.</param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsUTF8Supported))]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task Utf8VarcharRoundTrip_PreservesWorldwideTextAndBytes(bool useAsync)
        {
            string expected = GlobalizationTestData.CreatePacketSpanningText();
            SqlConnectionStringBuilder connectionString = new(DataTestUtility.TCPConnectionString)
            {
                PacketSize = 512
            };
            using SqlConnection connection = new(connectionString.ConnectionString);
            if (useAsync)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }

            using Table table = new(
                connection,
                nameof(Utf8VarcharRoundTrip_PreservesWorldwideTextAndBytes),
                "(Value varchar(max) COLLATE Latin1_General_100_CS_AS_KS_WS_SC_UTF8 NOT NULL)");
            using (SqlCommand insert = new($"INSERT INTO {table.Name} (Value) VALUES (@value)", connection))
            {
                insert.Parameters.Add(new SqlParameter("@value", SqlDbType.NVarChar, -1) { Value = expected });
                if (useAsync)
                {
                    await insert.ExecuteNonQueryAsync();
                }
                else
                {
                    insert.ExecuteNonQuery();
                }
            }

            using SqlCommand select = new($"SELECT Value, CONVERT(varbinary(max), Value) FROM {table.Name}", connection);
            using SqlDataReader reader = useAsync
                ? await select.ExecuteReaderAsync(CommandBehavior.SequentialAccess)
                : select.ExecuteReader(CommandBehavior.SequentialAccess);
            bool hasRow = useAsync ? await reader.ReadAsync() : reader.Read();

            Assert.True(hasRow);
            Assert.Equal(expected, reader.GetString(0));
            Assert.Equal(Encoding.UTF8.GetBytes(expected), reader.GetFieldValue<byte[]>(1));
            Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
        }

        /// <summary>
        /// Reads a string through the standard buffered reader path.
        /// </summary>
        /// <param name="connection">The open SQL connection used to execute the query.</param>
        /// <param name="query">The query that returns one string value.</param>
        /// <param name="useAsync">Whether command and reader operations use asynchronous APIs.</param>
        /// <returns>The string returned by SQL Server.</returns>
        private static async Task<string> ReadDirectValue(SqlConnection connection, string query, bool useAsync)
        {
            using SqlCommand command = new(query, connection);
            using SqlDataReader reader = useAsync
                ? await command.ExecuteReaderAsync()
                : command.ExecuteReader();
            bool hasRow = useAsync ? await reader.ReadAsync() : reader.Read();

            Assert.True(hasRow);
            string result = reader.GetString(0);
            Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
            return result;
        }

        /// <summary>
        /// Reads a string through sequential <see cref="TextReader"/> calls with a small buffer so character
        /// sequences cross read boundaries.
        /// </summary>
        /// <param name="connection">The open SQL connection used to execute the query.</param>
        /// <param name="query">The query that returns one string value.</param>
        /// <param name="useAsync">Whether command and reader operations use asynchronous APIs.</param>
        /// <returns>The string returned by SQL Server.</returns>
        private static async Task<string> ReadStreamedValue(SqlConnection connection, string query, bool useAsync)
        {
            using SqlCommand command = new(query, connection);
            using SqlDataReader reader = useAsync
                ? await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess)
                : command.ExecuteReader(CommandBehavior.SequentialAccess);
            bool hasRow = useAsync ? await reader.ReadAsync() : reader.Read();

            Assert.True(hasRow);
            using TextReader textReader = reader.GetTextReader(0);
            char[] buffer = new char[3];
            StringBuilder result = new();
            int charsRead;
            do
            {
                charsRead = useAsync
                    ? await textReader.ReadAsync(buffer, 0, buffer.Length)
                    : textReader.Read(buffer, 0, buffer.Length);
                result.Append(buffer, 0, charsRead);
            }
            while (charsRead != 0);

            Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
            return result.ToString();
        }
    }
}
