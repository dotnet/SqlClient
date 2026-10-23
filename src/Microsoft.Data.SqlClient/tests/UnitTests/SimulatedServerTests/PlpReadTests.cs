// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.ColMetadata;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Exercises PLP materialization and streaming across chunk and packet boundaries.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class PlpReadTests
{
    /// <summary>
    /// Covers all shared PLP readers, both async implementations, and different chunk layouts.
    /// </summary>
    public static IEnumerable<object[]> ReadCases()
    {
        foreach (string type in new[] { "binary", "unicode", "ansi", "xml" })
        foreach (bool compatibility in new[] { false, true })
        foreach (bool async in new[] { false, true })
        foreach (bool sequential in new[] { false, true })
        foreach (int chunkSize in new[] { 3, 257, 65537 })
        foreach (bool unknownLength in new[] { false, true })
        {
            yield return new object[] { type, compatibility, async, sequential, chunkSize, unknownLength, 8203 };
        }

        // Cross enough packets to wrap the wire packet ID while retaining exact value contents.
        foreach (string type in new[] { "binary", "unicode", "ansi", "xml" })
        foreach (bool compatibility in new[] { false, true })
        foreach (bool async in new[] { false, true })
        foreach (bool sequential in new[] { false, true })
        {
            yield return new object[] { type, compatibility, async, sequential, 65537, true, 131073 };
        }
    }

    /// <summary>
    /// Reads exact values with many chunks per packet, split UTF-16 characters, and split headers.
    /// Sequential reads use bounded caller-owned buffers; buffered reads materialize complete values.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadCases))]
    public async Task Read_MultiPacketValue_PreservesPayload(
        string type, bool compatibility, bool async, bool sequential, int chunkSize, bool unknownLength, int textLength)
    {
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseCompatibilityAsyncBehaviour = compatibility;
        switches.UseCompatibilityProcessSni = compatibility;
        using TdsServer server = new(new TdsServerArguments { PacketSize = 512 });
        server.Start();
        char[] pattern = new char[textLength];
        for (int i = 0; i < pattern.Length; i++)
        {
            pattern[i] = (char)('a' + i % 26);
        }
        string text = new string(pattern) + "\u4f60\u597d\ud83d\ude03";
        if (type == "ansi")
        {
            text = text.Substring(0, textLength) + "xyz";
        }
        else if (type == "xml")
        {
            text = "<value>" + text + "</value>";
        }
        byte[] payload = type == "unicode" || type == "xml"
            ? Encoding.Unicode.GetBytes(text)
            : Encoding.ASCII.GetBytes(text);
        SetResponse(server, type, payload, chunkSize,
            unknownLength ? TdsEnums.SQL_PLP_UNKNOWNLEN : (ulong)payload.Length);

        using SqlConnection connection = new(ConnectionString(server));
        if (async)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }

#if DEBUG
        bool previousForcePends = TdsParserStateObject.s_forceAllPends;
        TdsParserStateObject.s_forceAllPends = async;
        try
        {
#endif
            using SqlCommand command = new("SELECT 1", connection);
            CommandBehavior behavior = sequential ? CommandBehavior.SequentialAccess : CommandBehavior.Default;
            using SqlDataReader reader = async
                ? await command.ExecuteReaderAsync(behavior)
                : command.ExecuteReader(behavior);
            Assert.True(async ? await reader.ReadAsync() : reader.Read());
            if (sequential)
            {
                if (type == "binary")
                {
                    using Stream source = reader.GetStream(0);
                    using MemoryStream destination = new();
                    if (async)
                    {
                        await source.CopyToAsync(destination, 113);
                    }
                    else
                    {
                        source.CopyTo(destination, 113);
                    }
                    Assert.Equal(payload, destination.ToArray());
                }
                else if (type == "xml")
                {
                    using XmlReader source = reader.GetXmlReader(0);
                    source.MoveToContent();
                    Assert.Equal(text, source.ReadOuterXml());
                }
                else
                {
                    using TextReader source = reader.GetTextReader(0);
                    StringBuilder destination = new();
                    char[] buffer = new char[113];
                    int read;
                    while ((read = async
                        ? await source.ReadAsync(buffer, 0, buffer.Length)
                        : source.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        destination.Append(buffer, 0, read);
                    }
                    Assert.Equal(text, destination.ToString());
                }
            }
            else if (type == "binary")
            {
                Assert.Equal(payload, async
                    ? await reader.GetFieldValueAsync<byte[]>(0)
                    : reader.GetFieldValue<byte[]>(0));
            }
            else if (type == "xml")
            {
                Assert.Equal(text, reader.GetSqlXml(0).Value);
            }
            else
            {
                Assert.Equal(text, async ? await reader.GetFieldValueAsync<string>(0) : reader.GetString(0));
            }
            Assert.False(async ? await reader.ReadAsync() : reader.Read());
#if DEBUG
        }
        finally
        {
            TdsParserStateObject.s_forceAllPends = previousForcePends;
        }
#endif
    }

    /// <summary>
    /// DataAdapter exposes actual values even when a total-length hint is inaccurate.
    /// </summary>
    [Theory]
    [InlineData("binary", 1UL)]
    [InlineData("binary", 1048576UL)]
    [InlineData("unicode", 1UL)]
    [InlineData("unicode", 1048576UL)]
    [InlineData("ansi", 1UL)]
    [InlineData("ansi", 1048576UL)]
    public void Fill_InaccurateLengthHint_ReturnsActualValue(string type, ulong hint)
    {
        using TdsServer server = new(new TdsServerArguments { PacketSize = 512 });
        server.Start();
        const string text = "small value";
        byte[] payload = type == "unicode" ? Encoding.Unicode.GetBytes(text) : Encoding.ASCII.GetBytes(text);
        SetResponse(server, type, payload, 3, hint);
        using SqlConnection connection = new(ConnectionString(server));
        using SqlDataAdapter adapter = new("SELECT 1", connection);
        using DataTable table = new();

        Assert.Equal(1, adapter.Fill(table));

        if (type == "binary")
        {
            Assert.Equal(payload, Assert.IsType<byte[]>(table.Rows[0][0]));
        }
        else
        {
            Assert.Equal(text, table.Rows[0][0]);
        }
    }

    /// <summary>
    /// Configures a single-column result while leaving login and transport handling unchanged.
    /// </summary>
    private static void SetResponse(TdsServer server, string type, byte[] payload, int chunkSize, ulong hint)
    {
        server.OnSQLBatchCompleted = message =>
        {
            message.Clear();
            TDSColumnData column = new()
            {
                DataType = type == "binary" ? TDSDataType.BigVarBinary :
                    type == "unicode" ? TDSDataType.NVarChar : TDSDataType.BigVarChar,
                Name = "value",
                DataTypeSpecific = type == "binary"
                    ? (object)ushort.MaxValue
                    : new TDSShilohVarCharColumnSpecific(ushort.MaxValue, new TDSColumnDataCollation(13632521, 52))
            };
            column.Flags.IsNullable = true;
            TDSColMetadataToken metadata = new();
            metadata.Columns.Add(column);
            message.Add(type == "xml" ? new XmlMetadataToken() : metadata);
            message.Add(new PlpRowToken(payload, chunkSize, hint));
            message.Add(new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count,
                TDSDoneTokenCommandType.Select, 1));
        };
    }

    /// <summary>
    /// Forces small transport packets so a value spans many independent network reads.
    /// </summary>
    private static string ConnectionString(TdsServer server) => new SqlConnectionStringBuilder
    {
        DataSource = $"localhost,{server.EndPoint.Port}",
        Encrypt = SqlConnectionEncryptOption.Optional,
        Pooling = false,
        PacketSize = 512
    }.ConnectionString;

    /// <summary>
    /// Supplies untyped XML metadata, which the test server's column writer does not support.
    /// </summary>
    private sealed class XmlMetadataToken : TDSPacketToken
    {
        /// <summary>
        /// This token is only used to write test responses.
        /// </summary>
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        /// <summary>
        /// Writes one nullable XML column without an associated schema.
        /// </summary>
        public override void Deflate(Stream destination)
        {
            byte[] metadata = { 0x81, 1, 0, 0, 0, 0, 0, 1, 0, 0xF1, 0, 1, (byte)'v', 0 };
            destination.Write(metadata, 0, metadata.Length);
        }
    }

    /// <summary>
    /// Writes PLP chunks independently of transport packet boundaries.
    /// </summary>
    private sealed class PlpRowToken(byte[] payload, int chunkSize, ulong hint) : TDSPacketToken
    {
        /// <summary>
        /// This token is only used to write test responses.
        /// </summary>
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        /// <summary>
        /// Writes a complete value with a caller-selected hint and chunk size.
        /// </summary>
        public override void Deflate(Stream destination)
        {
            // BinaryWriter.Dispose flushes TDSStream, which would end the message before DONE.
            destination.WriteByte(0xD1);
            TDSUtilities.WriteUInt(destination, (uint)hint);
            TDSUtilities.WriteUInt(destination, (uint)(hint >> 32));
            for (int offset = 0; offset < payload.Length;)
            {
                int length = Math.Min(chunkSize, payload.Length - offset);
                TDSUtilities.WriteUInt(destination, (uint)length);
                destination.Write(payload, offset, length);
                offset += length;
            }
            TDSUtilities.WriteUInt(destination, 0);
        }
    }
}
