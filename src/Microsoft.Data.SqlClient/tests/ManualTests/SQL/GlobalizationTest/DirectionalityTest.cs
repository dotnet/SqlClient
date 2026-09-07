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

namespace Microsoft.Data.SqlClient.ManualTesting.Tests;

/// <summary>
/// Verifies that SqlClient preserves the logical UTF-16 representation of bidirectional text.
/// Visual direction, shaping, and mirroring are responsibilities of the consuming UI.
/// </summary>
[Trait("Set", "3")]
public sealed class DirectionalityTest
{
    private static readonly string[] s_bidiText =
    {
        "\u0645\u0631\u062D\u0628\u0627 Microsoft 01 - \u0639\u0627\u0644\u0645 \U0001F310",
        "\u05E9\u05DC\u05D5\u05DD Microsoft 01 - \u05E2\u05D5\u05DC\u05DD \U0001F310"
    };

    /// <summary>
    /// Ensures mixed Arabic/Hebrew, Latin, numeric, punctuation, and supplementary characters
    /// round-trip unchanged through parameters, readers, sequential streaming, and bulk copy.
    /// </summary>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BidiText_RoundTripsWithoutTransformation(bool async)
    {
        using SqlConnection setupConnection = new(DataTestUtility.TCPConnectionString);
        await OpenConnection(setupConnection, async);

        using Table sourceTable = new(
            setupConnection,
            "DirectionalitySource",
            "(Id int NOT NULL, Value nvarchar(max) NOT NULL)");
        using Table destinationTable = new(
            setupConnection,
            "DirectionalityDestination",
            "(Id int NOT NULL, Value nvarchar(max) NOT NULL)");

        await InsertValues(setupConnection, sourceTable.Name, async);
        await VerifyOrdinaryReader(setupConnection, sourceTable.Name, async);
        await VerifyGetChars(setupConnection, sourceTable.Name, async);
        await VerifyTextReader(setupConnection, sourceTable.Name, async);
        await CopyValues(sourceTable.Name, destinationTable.Name, async);
        await VerifyOrdinaryReader(setupConnection, destinationTable.Name, async);
    }

    /// <summary>
    /// Opens a connection through the requested synchronous or asynchronous API.
    /// </summary>
    private static async Task OpenConnection(SqlConnection connection, bool async)
    {
        if (async)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }
    }

    /// <summary>
    /// Inserts the bidi samples through explicitly typed Unicode parameters.
    /// </summary>
    private static async Task InsertValues(SqlConnection connection, string tableName, bool async)
    {
        using SqlCommand command = new($"INSERT INTO {tableName} (Id, Value) VALUES (@id, @value)", connection);
        SqlParameter idParameter = command.Parameters.Add("@id", SqlDbType.Int);
        SqlParameter valueParameter = command.Parameters.Add("@value", SqlDbType.NVarChar, -1);

        for (int index = 0; index < s_bidiText.Length; index++)
        {
            idParameter.Value = index;
            valueParameter.Value = s_bidiText[index];

            if (async)
            {
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                command.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Reads complete strings through ordinary reader accessors and compares their UTF-16 content.
    /// </summary>
    private static async Task VerifyOrdinaryReader(SqlConnection connection, string tableName, bool async)
    {
        using SqlCommand command = new($"SELECT Id, Value FROM {tableName} ORDER BY Id", connection);
        using SqlDataReader reader = async
            ? await command.ExecuteReaderAsync()
            : command.ExecuteReader();

        for (int index = 0; index < s_bidiText.Length; index++)
        {
            bool hasRow = async ? await reader.ReadAsync() : reader.Read();
            Assert.True(hasRow);
            Assert.Equal(index, reader.GetInt32(0));
            AssertOrdinalEqual(s_bidiText[index], reader.GetString(1));
            AssertOrdinalEqual(s_bidiText[index], reader.GetFieldValue<string>(1));
        }

        Assert.False(async ? await reader.ReadAsync() : reader.Read());
    }

    /// <summary>
    /// Reads one UTF-16 code unit at a time to cover direction and surrogate boundaries in GetChars.
    /// </summary>
    private static async Task VerifyGetChars(SqlConnection connection, string tableName, bool async)
    {
        using SqlCommand command = new($"SELECT Value FROM {tableName} ORDER BY Id", connection);
        using SqlDataReader reader = async
            ? await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess)
            : command.ExecuteReader(CommandBehavior.SequentialAccess);

        for (int index = 0; index < s_bidiText.Length; index++)
        {
            bool hasRow = async ? await reader.ReadAsync() : reader.Read();
            Assert.True(hasRow);

            StringBuilder result = new();
            char[] buffer = new char[1];
            long dataIndex = 0;
            long charsRead;
            do
            {
                charsRead = reader.GetChars(0, dataIndex, buffer, 0, buffer.Length);
                result.Append(buffer, 0, (int)charsRead);
                dataIndex += charsRead;
            }
            while (charsRead != 0);

            AssertOrdinalEqual(s_bidiText[index], result.ToString());
        }
    }

    /// <summary>
    /// Reads bidi text through the sequential TextReader using small sync or async buffer operations.
    /// </summary>
    private static async Task VerifyTextReader(SqlConnection connection, string tableName, bool async)
    {
        using SqlCommand command = new($"SELECT Value FROM {tableName} ORDER BY Id", connection);
        using SqlDataReader reader = async
            ? await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess)
            : command.ExecuteReader(CommandBehavior.SequentialAccess);

        for (int index = 0; index < s_bidiText.Length; index++)
        {
            bool hasRow = async ? await reader.ReadAsync() : reader.Read();
            Assert.True(hasRow);

            using TextReader textReader = reader.GetTextReader(0);
            StringBuilder result = new();
            char[] buffer = new char[2];
            int charsRead;
            do
            {
                charsRead = async
                    ? await textReader.ReadAsync(buffer, 0, buffer.Length)
                    : textReader.Read(buffer, 0, buffer.Length);
                result.Append(buffer, 0, charsRead);
            }
            while (charsRead != 0);

            AssertOrdinalEqual(s_bidiText[index], result.ToString());
        }
    }

    /// <summary>
    /// Copies the Unicode rows through streaming SqlBulkCopy using the requested execution mode.
    /// </summary>
    private static async Task CopyValues(string sourceTableName, string destinationTableName, bool async)
    {
        using SqlConnection sourceConnection = new(DataTestUtility.TCPConnectionString);
        using SqlConnection destinationConnection = new(DataTestUtility.TCPConnectionString);
        await OpenConnection(sourceConnection, async);
        await OpenConnection(destinationConnection, async);

        using SqlCommand command = new($"SELECT Id, Value FROM {sourceTableName} ORDER BY Id", sourceConnection);
        using SqlDataReader reader = async
            ? await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess)
            : command.ExecuteReader(CommandBehavior.SequentialAccess);
        using SqlBulkCopy bulkCopy = new(destinationConnection)
        {
            DestinationTableName = destinationTableName,
            EnableStreaming = true
        };
        bulkCopy.ColumnMappings.Add(0, 0);
        bulkCopy.ColumnMappings.Add(1, 1);

        if (async)
        {
            await bulkCopy.WriteToServerAsync(reader);
        }
        else
        {
            bulkCopy.WriteToServer(reader);
        }
    }

    /// <summary>
    /// Compares strings ordinally so the assertion checks logical storage rather than visual rendering.
    /// </summary>
    private static void AssertOrdinalEqual(string expected, string actual)
    {
        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            $"Expected and actual UTF-16 values differ. Expected length: {expected.Length}; actual length: {actual?.Length}.");
    }
}
