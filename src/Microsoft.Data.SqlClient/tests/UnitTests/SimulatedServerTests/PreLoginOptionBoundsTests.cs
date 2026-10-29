// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Tests that the client validates every option header in a PRELOGIN response and reports a
/// parsing error, rather than reading outside the response buffer or across option
/// boundaries, when an option header or the option data it describes lies beyond the end of
/// the response, or when an option the client reads declares an unexpected data length.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class PreLoginOptionBoundsTests : IDisposable
{
    private const byte Version = 0x00;
    private const byte Encrypt = 0x01;
    private const byte Instance = 0x02;
    private const byte ThreadId = 0x03;
    private const byte Mars = 0x04;
    private const byte TraceId = 0x05;
    private const byte FedAuthRequired = 0x06;
    private const byte Terminator = 0xFF;

    private readonly PreLoginOverrideTdsServer _server;
    private readonly string _connectionString;

    public PreLoginOptionBoundsTests()
    {
        _server = new PreLoginOverrideTdsServer();
        _server.Start();
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
    }

    public void Dispose() => _server.Dispose();

    /// <summary>
    /// Control: a well-formed PRELOGIN response containing every option the client reads
    /// is accepted and the connection opens.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WellFormedResponse_Connects(bool useAsync)
    {
        _server.BuildResponse = encryption => BuildValidResponse(encryption);

        using SqlConnection connection = new(_connectionString);
        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    /// <summary>
    /// An option whose data offset points far beyond the end of the response is rejected
    /// with a parsing error.
    /// </summary>
    [Theory]
    [InlineData(Version, false)]
    [InlineData(Version, true)]
    [InlineData(Encrypt, false)]
    [InlineData(Encrypt, true)]
    [InlineData(Instance, false)]
    [InlineData(Instance, true)]
    [InlineData(Mars, false)]
    [InlineData(Mars, true)]
    [InlineData(FedAuthRequired, false)]
    [InlineData(FedAuthRequired, true)]
    public async Task OptionOffsetBeyondResponse_ThrowsParsingError(byte option, bool useAsync)
    {
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption);
            SetOptionOffset(response, option, 0xFFFF);
            return response;
        };

        Exception ex = await OpenExpectingFailure(useAsync);

        AssertCorruptedStream(ex, expectedOffset: 0xFFFF);
    }

    /// <summary>
    /// An option whose data starts inside the response but does not fit before the end of
    /// the response is rejected. This checks the boundary: VERSION declares 6 bytes, so an
    /// offset 3 bytes from the end is too short; single-byte options at an offset equal to
    /// the response length are just past the end.
    /// </summary>
    [Theory]
    [InlineData(Version, 3, false)]
    [InlineData(Version, 3, true)]
    [InlineData(Encrypt, 0, false)]
    [InlineData(Encrypt, 0, true)]
    [InlineData(Instance, 0, false)]
    [InlineData(Instance, 0, true)]
    [InlineData(Mars, 0, false)]
    [InlineData(Mars, 0, true)]
    [InlineData(FedAuthRequired, 0, false)]
    [InlineData(FedAuthRequired, 0, true)]
    public async Task OptionDataTruncatedAtEndOfResponse_ThrowsParsingError(byte option, int bytesAvailable, bool useAsync)
    {
        int expectedOffset = 0;
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption);
            expectedOffset = response.Length - bytesAvailable;
            SetOptionOffset(response, option, (ushort)expectedOffset);
            return response;
        };

        Exception ex = await OpenExpectingFailure(useAsync);

        AssertCorruptedStream(ex, expectedOffset);
    }

    /// <summary>
    /// A response that ends in the middle of an option header is rejected.
    /// </summary>
    [Theory]
    [InlineData(Version, false)]
    [InlineData(Version, true)]
    [InlineData(Encrypt, false)]
    [InlineData(Encrypt, true)]
    [InlineData(Instance, false)]
    [InlineData(Instance, true)]
    [InlineData(ThreadId, false)]
    [InlineData(ThreadId, true)]
    [InlineData(Mars, false)]
    [InlineData(Mars, true)]
    [InlineData(TraceId, false)]
    [InlineData(TraceId, true)]
    [InlineData(FedAuthRequired, false)]
    [InlineData(FedAuthRequired, true)]
    public async Task OptionHeaderTruncated_ThrowsParsingError(byte option, bool useAsync)
    {
        // Option token followed by only 2 of the 4 header bytes.
        _server.BuildResponse = _ => new byte[] { option, 0x00, 0x05 };

        Exception ex = await OpenExpectingFailure(useAsync);

        // The header begins immediately after the option token, at offset 1.
        AssertCorruptedStream(ex, expectedOffset: 1);
    }

    /// <summary>
    /// An option the client reads whose header declares a data length other than the length
    /// defined for that option is rejected, even when the declared data lies within the
    /// response.
    /// </summary>
    [Theory]
    [InlineData(Version, 0, false)]
    [InlineData(Version, 4, false)]
    [InlineData(Version, 4, true)]
    [InlineData(Version, 7, false)]
    [InlineData(Encrypt, 0, false)]
    [InlineData(Encrypt, 0, true)]
    [InlineData(Encrypt, 2, false)]
    [InlineData(Instance, 0, false)]
    [InlineData(Instance, 0, true)]
    [InlineData(Instance, 2, false)]
    [InlineData(Mars, 0, false)]
    [InlineData(Mars, 0, true)]
    [InlineData(Mars, 2, false)]
    [InlineData(FedAuthRequired, 0, false)]
    [InlineData(FedAuthRequired, 0, true)]
    [InlineData(FedAuthRequired, 2, false)]
    public async Task OptionLengthMismatch_ThrowsParsingError(
        byte option,
        ushort declaredLength,
        bool useAsync)
    {
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption);
            SetOptionLength(response, option, declaredLength);
            return response;
        };

        Exception ex = await OpenExpectingFailure(useAsync);

        AssertCorruptedStream(ex);
        Assert.Contains($"Length: {declaredLength}", ex.Message);
    }

    /// <summary>
    /// A VERSION option that declares only 2 bytes of data, placed so that the 6 bytes the
    /// client expects would run into the data of the following options, is rejected rather
    /// than being read together with the other options' bytes.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionDataShorterThanExpectedOverlapsNextOption_ThrowsParsingError(
        bool useAsync)
    {
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption);
            ushort encryptOffset = GetOptionOffset(response, Encrypt);
            SetOptionOffset(response, Version, (ushort)(encryptOffset - 2));
            SetOptionLength(response, Version, 2);
            return response;
        };

        Exception ex = await OpenExpectingFailure(useAsync);

        AssertCorruptedStream(ex);
        Assert.Contains("Length: 2", ex.Message);
    }

    /// <summary>
    /// An option the client does not read (THREADID, TRACEID) is still validated: data that
    /// starts beyond the end of the response is rejected.
    /// </summary>
    [Theory]
    [InlineData(ThreadId, false)]
    [InlineData(ThreadId, true)]
    [InlineData(TraceId, false)]
    [InlineData(TraceId, true)]
    public async Task UnreadOptionOffsetBeyondResponse_ThrowsParsingError(
        byte option,
        bool useAsync)
    {
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption, includeTraceId: true);
            SetOptionOffset(response, option, 0xFFFF);
            return response;
        };

        Exception ex = await OpenExpectingFailure(useAsync);

        AssertCorruptedStream(ex, expectedOffset: 0xFFFF);
    }

    /// <summary>
    /// An option the client does not read (THREADID, TRACEID) is still validated: a declared
    /// length that runs past the end of the response is rejected.
    /// </summary>
    [Theory]
    [InlineData(ThreadId, false)]
    [InlineData(ThreadId, true)]
    [InlineData(TraceId, false)]
    [InlineData(TraceId, true)]
    public async Task UnreadOptionLengthBeyondResponse_ThrowsParsingError(
        byte option,
        bool useAsync)
    {
        int expectedOffset = 0;
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption, includeTraceId: true);
            expectedOffset = GetOptionOffset(response, option);
            SetOptionLength(response, option, (ushort)(response.Length - expectedOffset + 1));
            return response;
        };

        Exception ex = await OpenExpectingFailure(useAsync);

        AssertCorruptedStream(ex, expectedOffset);
    }

    /// <summary>
    /// Control: options the client does not read may declare any length that fits within
    /// the response, and the connection still opens.
    /// </summary>
    [Theory]
    [InlineData(ThreadId, false)]
    [InlineData(ThreadId, true)]
    [InlineData(TraceId, false)]
    [InlineData(TraceId, true)]
    public async Task UnreadOptionAnyLengthWithinResponse_Connects(byte option, bool useAsync)
    {
        _server.BuildResponse = encryption =>
        {
            byte[] response = BuildValidResponse(encryption, includeTraceId: true);
            SetOptionLength(response, option, 0);
            return response;
        };

        using SqlConnection connection = new(_connectionString);
        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    private async Task<Exception> OpenExpectingFailure(bool useAsync)
    {
        using SqlConnection connection = new(_connectionString);
        if (useAsync)
        {
            return await Assert.ThrowsAnyAsync<InvalidOperationException>(connection.OpenAsync);
        }

        return Assert.ThrowsAny<InvalidOperationException>(connection.Open);
    }

    private static void AssertCorruptedStream(Exception ex)
    {
        Assert.IsNotType<IndexOutOfRangeException>(ex);
        Assert.Contains("Error state: 18", ex.Message); // ParsingErrorState.CorruptedTdsStream
    }

    private static void AssertCorruptedStream(Exception ex, int expectedOffset)
    {
        AssertCorruptedStream(ex);
        Assert.Contains($"Offset: {expectedOffset}", ex.Message);
    }

    /// <summary>
    /// Builds a well-formed PRELOGIN response containing VERSION, ENCRYPT, INSTANCE,
    /// THREADID, MARS and FEDAUTHREQUIRED options, and optionally TRACEID.
    /// </summary>
    private static byte[] BuildValidResponse(
        TDSPreLoginTokenEncryptionType encryption,
        bool includeTraceId = false)
    {
        // The test server omits ENCRYPT when it has no encryption preference; NOT_SUP is the
        // equivalent wire value and keeps the client in agreement with the server session.
        byte encryptValue = encryption == TDSPreLoginTokenEncryptionType.None
            ? (byte)TDSPreLoginTokenEncryptionType.NotSupported
            : (byte)encryption;

        List<(byte Option, byte[] Data)> options = new()
        {
            (Version, new byte[] { 0x10, 0x00, 0x00, 0x00, 0x00, 0x00 }),
            (Encrypt, new byte[] { encryptValue }),
            (Instance, new byte[] { 0x00 }),
            (ThreadId, new byte[] { 0x00, 0x00, 0x00, 0x00 }),
            (Mars, new byte[] { 0x00 }),
            (FedAuthRequired, new byte[] { 0x00 }),
        };

        if (includeTraceId)
        {
            options.Add((TraceId, new byte[36]));
        }

        int headerLength = options.Count * 5 + 1;
        byte[] response = new byte[headerLength + options.Sum(o => o.Data.Length)];

        int headerPosition = 0;
        int dataPosition = headerLength;
        foreach ((byte option, byte[] data) in options)
        {
            response[headerPosition++] = option;
            response[headerPosition++] = (byte)(dataPosition >> 8);
            response[headerPosition++] = (byte)dataPosition;
            response[headerPosition++] = (byte)(data.Length >> 8);
            response[headerPosition++] = (byte)data.Length;

            Buffer.BlockCopy(data, 0, response, dataPosition, data.Length);
            dataPosition += data.Length;
        }

        response[headerPosition] = Terminator;
        return response;
    }

    private static int FindOptionHeader(byte[] response, byte option)
    {
        for (int position = 0; response[position] != Terminator; position += 5)
        {
            if (response[position] == option)
            {
                return position;
            }
        }

        throw new ArgumentException(
            $"Option {option} not found in PRELOGIN response.", nameof(option));
    }

    private static ushort GetOptionOffset(byte[] response, byte option)
    {
        int position = FindOptionHeader(response, option);
        return (ushort)(response[position + 1] << 8 | response[position + 2]);
    }

    private static void SetOptionOffset(byte[] response, byte option, ushort offset)
    {
        int position = FindOptionHeader(response, option);
        response[position + 1] = (byte)(offset >> 8);
        response[position + 2] = (byte)offset;
    }

    private static void SetOptionLength(byte[] response, byte option, ushort length)
    {
        int position = FindOptionHeader(response, option);
        response[position + 3] = (byte)(length >> 8);
        response[position + 4] = (byte)length;
    }

    /// <summary>
    /// A TDS server that replaces its PRELOGIN response with caller-supplied raw bytes.
    /// </summary>
    private sealed class PreLoginOverrideTdsServer : TdsServer
    {
        public Func<TDSPreLoginTokenEncryptionType, byte[]>? BuildResponse { get; set; }

        public override TDSMessageCollection OnPreLoginRequest(ITDSServerSession session, TDSMessage request)
        {
            // Let the base server negotiate so its session state matches a normal handshake.
            TDSMessageCollection response = base.OnPreLoginRequest(session, request);

            if (BuildResponse is null)
            {
                return response;
            }

            TDSPreLoginToken token = response[0].OfType<TDSPreLoginToken>().First();
            return new TDSMessageCollection(
                new TDSMessage(TDSMessageType.Response, new RawToken(BuildResponse(token.Encryption))));
        }
    }

    private sealed class RawToken : TDSPacketToken
    {
        private readonly byte[] _bytes;

        public RawToken(byte[] bytes) => _bytes = bytes;

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination) => destination.Write(_bytes, 0, _bytes.Length);
    }
}
