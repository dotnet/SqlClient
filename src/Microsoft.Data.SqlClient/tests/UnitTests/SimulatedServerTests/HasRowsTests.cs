// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Info;
using Microsoft.SqlServer.TDS.Row;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Guards against issue #3018 using INFO tokens before or after column metadata,
/// without requiring a SQL Server instance.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class HasRowsTests
{
    private readonly ITestOutputHelper _output;

    public HasRowsTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Covers the single-token boundary, multiple tokens, and empty results with
    /// both synchronous and asynchronous readers.
    /// </summary>
    /// <returns>INFO count, placement after metadata, async flag, and row presence.</returns>
    public static IEnumerable<object[]> InfoTokenCases()
    {
        foreach (int infoCount in new[] { 0, 1, 2, 3, 10 })
        {
            foreach (bool afterMetadata in new[] { false, true })
            {
                foreach (bool useAsync in new[] { false, true })
                {
                    yield return new object[] { infoCount, afterMetadata, useAsync, false };
                    yield return new object[] { infoCount, afterMetadata, useAsync, true };
                }
            }
        }
    }

    /// <summary>
    /// Verifies INFO tokens do not affect HasRows or change deferred message delivery.
    /// </summary>
    [Theory]
    [MemberData(nameof(InfoTokenCases))]
    public async Task HasRows_WithInfoTokens_ReflectsRowPresence(
        int infoCount, bool afterMetadata, bool useAsync, bool returnsRow)
    {
        TdsServerArguments arguments = new();
        InfoQueryEngine engine = new(arguments, infoCount, afterMetadata, returnsRow);
        using TdsServer server = new(engine, arguments);
        server.Start();

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectTimeout = 5
        };
        using SqlConnection connection = new(builder.ConnectionString);
        List<string> infoMessages = new();
        connection.InfoMessage += (_, args) =>
        {
            foreach (SqlError error in args.Errors)
            {
                infoMessages.Add(error.Message);
            }
        };

        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }
        infoMessages.Clear();

        using SqlCommand command = new(InfoQueryEngine.CommandText, connection)
        {
            CommandTimeout = 5
        };
        using SqlDataReader reader = useAsync
            ? await command.ExecuteReaderAsync()
            : command.ExecuteReader();

        bool hasRowsBeforeRead = reader.HasRows;
        Assert.Equal(afterMetadata ? 0 : infoCount, infoMessages.Count);
        bool read = useAsync ? await reader.ReadAsync() : reader.Read();
        Assert.Equal(returnsRow, read);
        if (read)
        {
            Assert.Equal(1, reader.GetInt32(0));
        }
        bool hasRowsAfterRead = reader.HasRows;
        Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
        Assert.Equal(infoCount, infoMessages.Count);
        for (int i = 0; i < infoCount; i++)
        {
            Assert.Equal($"Info message {i}", infoMessages[i]);
        }

        _output.WriteLine(
            $"INFO count={infoCount}, after metadata={afterMetadata}, async={useAsync}, returns row={returnsRow}: " +
            $"HasRows before Read={hasRowsBeforeRead}, Read={read}, " +
            $"HasRows after Read={hasRowsAfterRead}, INFO received={infoMessages.Count}");

        Assert.Equal(returnsRow, hasRowsBeforeRead);
        Assert.Equal(returnsRow, hasRowsAfterRead);
        Assert.Equal(returnsRow, reader.HasRows);
        Assert.False(useAsync ? await reader.NextResultAsync() : reader.NextResult());
    }

    /// <summary>
    /// Inserts INFO tokens into the existing SELECT 1 response and optionally
    /// removes its row to exercise empty result sets.
    /// </summary>
    private sealed class InfoQueryEngine : QueryEngine
    {
        internal const string CommandText = "SELECT 1";
        private readonly int _infoCount;
        private readonly bool _afterMetadata;
        private readonly bool _returnsRow;

        internal InfoQueryEngine(TdsServerArguments arguments, int infoCount, bool afterMetadata, bool returnsRow)
            : base(arguments)
        {
            _infoCount = infoCount;
            _afterMetadata = afterMetadata;
            _returnsRow = returnsRow;
        }

        /// <summary>
        /// Places messages immediately before or after SELECT 1's column metadata.
        /// </summary>
        /// <param name="session">The connected test session.</param>
        /// <param name="batchRequest">The batch to execute.</param>
        /// <returns>A response with the requested INFO tokens and row presence.</returns>
        protected override TDSMessageCollection CreateQueryResponse(
            ITDSServerSession session, TDSSQLBatchToken batchRequest)
        {
            TDSMessageCollection response = base.CreateQueryResponse(session, batchRequest);
            if (batchRequest.Text == CommandText)
            {
                if (!_returnsRow)
                {
                    response[0].RemoveAll(token => token is TDSRowToken);
                    foreach (TDSPacketToken token in response[0])
                    {
                        if (token is TDSDoneToken done)
                        {
                            done.RowCount = 0;
                        }
                    }
                }

                int insertAt = _afterMetadata ? 1 : 0;
                for (int i = 0; i < _infoCount; i++)
                {
                    response[0].Insert(insertAt + i, new TDSInfoToken(30000, 0, 0, $"Info message {i}"));
                }
            }

            return response;
        }
    }
}
