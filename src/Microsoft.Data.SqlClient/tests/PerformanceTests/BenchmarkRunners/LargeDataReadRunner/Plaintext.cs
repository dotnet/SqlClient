// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.LargeDataReadRunner;

/// <summary>Compares binary/text materialization, chunked getters, and true streaming access.</summary>
public class Plaintext : LargeDataReadRunnerBase
{
    private byte[] _smallBytes;
    private byte[] _largeBytes;
    private char[] _smallChars;
    private char[] _largeChars;

    protected override CommandRunnerJob Configuration => s_config.Benchmarks.LargeDataReadRunnerConfig;

    public override IEnumerable<DataKind> ExecutedKinds => [DataKind.Binary, DataKind.Text];

    public override IEnumerable<CommandBehavior> ExecutedCommandBehaviors =>
        [CommandBehavior.Default, CommandBehavior.SequentialAccess];

    /// <summary>
    /// Size of the client-side read buffer used to drain the VARBINARY(MAX) column.
    /// Kept small (8 KB) and large (1 MB) to observe whether buffer size relative to
    /// the payload materially changes throughput.
    /// </summary>
    public IEnumerable<int> ReadBufferBytes => [8_192, 1_048_576];

    protected override Tests.Common.Fixtures.DatabaseObjects.Table CreateTable() =>
        new(Connection, nameof(Plaintext), $"(Id INT IDENTITY PRIMARY KEY, Data {(Kind == DataKind.Binary ? "VARBINARY(MAX)" : "NVARCHAR(MAX)")})");

    /// <summary>Generates and stores plaintext on the server, avoiding client upload allocations.</summary>
    protected override void PopulatePayload()
    {
        string expression = Kind == DataKind.Binary
            ? "CONVERT(varbinary(max), REPLICATE(CAST('x' AS varchar(max)), @size))"
            : "REPLICATE(CAST(NCHAR(350) AS nvarchar(max)), @size / 2)";
        SqlCommand insert = CreateCommand($"INSERT INTO {_table.Name} (Data) VALUES ({expression})");
        insert.Parameters.Add("@size", SqlDbType.Int).Value = DataSizeBytes;
        insert.ExecuteNonQuery();
    }

    /// <summary>Checks the exact server-side byte count without transferring the payload.</summary>
    protected override void ValidateFixture()
    {
        if ((long)CreateCommand($"SELECT DATALENGTH(Data) FROM {_table.Name}").ExecuteScalar() != DataSizeBytes)
        {
            throw new System.InvalidOperationException("Server-side payload does not match the requested byte count.");
        }
    }

    /// <summary>Allocates both argument-sized read buffers outside timing.</summary>
    protected override void SetupBuffers()
    {
        _smallBytes = new byte[8_192];
        _largeBytes = new byte[1_048_576];
        _smallChars = new char[8_192 / sizeof(char)];
        _largeChars = new char[1_048_576 / sizeof(char)];
    }

    /// <summary>Consumes every byte/character through synchronous chunked getters.</summary>
    [Benchmark]
    [ArgumentsSource(nameof(ReadBufferBytes))]
    public long ReadLargeDataSync_GetBytes(int readBufferBytes)
    {
        using SqlDataReader reader = ReadCommand.ExecuteReader(CommandBehavior);
        byte[] bytes = readBufferBytes == 8_192 ? _smallBytes : _largeBytes;
        char[] chars = readBufferBytes == 8_192 ? _smallChars : _largeChars;
        long rows = 0;
        long totalBytes = 0;
        while (reader.Read())
        {
            long offset = 0;
            long count;
            do
            {
                count = Kind == DataKind.Binary
                    ? reader.GetBytes(0, offset, bytes, 0, bytes.Length)
                    : reader.GetChars(0, offset, chars, 0, chars.Length);
                offset += count;
            } while (count > 0);
            totalBytes += Kind == DataKind.Binary ? offset : offset * sizeof(char);
            rows++;
        }
        return RecordConsumption(rows, totalBytes);
    }

    /// <summary>Drains the binary stream or text reader synchronously with a reusable buffer.</summary>
    [Benchmark]
    [ArgumentsSource(nameof(ReadBufferBytes))]
    public long ReadLargeDataSync_GetStream(int readBufferBytes)
    {
        using SqlDataReader reader = ReadCommand.ExecuteReader(CommandBehavior);
        byte[] bytes = readBufferBytes == 8_192 ? _smallBytes : _largeBytes;
        char[] chars = readBufferBytes == 8_192 ? _smallChars : _largeChars;
        long rows = 0;
        long totalBytes = 0;
        while (reader.Read())
        {
            int count;
            if (Kind == DataKind.Binary)
            {
                using Stream stream = reader.GetStream(0);
                while ((count = stream.Read(bytes, 0, bytes.Length)) > 0)
                {
                    totalBytes += count;
                }
            }
            else
            {
                using TextReader text = reader.GetTextReader(0);
                while ((count = text.Read(chars, 0, chars.Length)) > 0)
                {
                    totalBytes += (long)count * sizeof(char);
                }
            }
            rows++;
        }
        return RecordConsumption(rows, totalBytes);
    }

    /// <summary>Drains through actual asynchronous binary/text stream reads, including the final partial chunk.</summary>
    [Benchmark]
    [ArgumentsSource(nameof(ReadBufferBytes))]
    public async Task<long> ReadLargeDataAsync_GetStream(int readBufferBytes)
    {
        using SqlDataReader reader = await ReadCommand.ExecuteReaderAsync(CommandBehavior);
        byte[] bytes = readBufferBytes == 8_192 ? _smallBytes : _largeBytes;
        char[] chars = readBufferBytes == 8_192 ? _smallChars : _largeChars;
        long rows = 0;
        long totalBytes = 0;
        while (await reader.ReadAsync())
        {
            int count;
            if (Kind == DataKind.Binary)
            {
                using Stream stream = reader.GetStream(0);
                while ((count = await stream.ReadAsync(bytes, 0, bytes.Length)) > 0)
                {
                    totalBytes += count;
                }
            }
            else
            {
                using TextReader text = reader.GetTextReader(0);
                while ((count = await text.ReadAsync(chars, 0, chars.Length)) > 0)
                {
                    totalBytes += (long)count * sizeof(char);
                }
            }
            rows++;
        }
        return RecordConsumption(rows, totalBytes);
    }
}
