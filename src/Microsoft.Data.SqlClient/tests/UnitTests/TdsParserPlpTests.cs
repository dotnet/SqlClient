// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies that PLP buffers grow with received data rather than server length hints.
/// </summary>
public sealed class TdsParserPlpTests : IDisposable
{
    private readonly TdsParser _parser = new(false, false);

    /// <summary>
    /// Releases the parser state used by each isolated in-memory read.
    /// </summary>
    public void Dispose() => _parser._physicalStateObj.Dispose();

    /// <summary>
    /// A partial binary read must not reserve the declared value or chunk length.
    /// </summary>
    [Theory]
    [InlineData(1048576UL, 1048576U)]
    [InlineData(TdsEnums.SQL_PLP_UNKNOWNLEN, 1048576U)]
    [InlineData(4294967296UL, uint.MaxValue)]
    [InlineData(TdsEnums.SQL_PLP_UNKNOWNLEN, uint.MaxValue)]
    [InlineData(ulong.MaxValue - 2, uint.MaxValue)]
    public void TryReadPlpBytes_PartialChunk_AllocatesOnlyReceivedData(ulong totalLength, uint chunkLength)
    {
        byte[] payload = { 1, 2, 3, 4 };
        TdsParserStateObject state = SetInput(payload, totalLength, chunkLength);
        byte[]? buffer = null;

        Assert.Equal(TdsOperationStatus.Done,
            state.TryReadPlpBytes(ref buffer, 0, payload.Length, out int read));

        Assert.Equal(payload.Length, read);
        Assert.NotNull(buffer);
        Assert.InRange(buffer.Length, payload.Length, payload.Length * 2);
        Assert.Equal(payload, buffer.AsSpan(0, read).ToArray());
        Assert.Equal(chunkLength - (ulong)payload.Length, state._longlenleft);
    }

    /// <summary>
    /// Materialized binary results expose their actual length, not spare buffer capacity.
    /// </summary>
    [Theory]
    [InlineData(1048576UL)]
    [InlineData(TdsEnums.SQL_PLP_UNKNOWNLEN)]
    [InlineData(4294967296UL)]
    [InlineData(ulong.MaxValue - 2)]
    [InlineData(1UL)]
    public void TryReadByteArrayWithContinue_ShortValue_ReturnsExactBytes(ulong totalLength)
    {
        byte[] payload = { 1, 2, 3, 4 };
        TdsParserStateObject state = SetInput(WithTerminator(payload), totalLength, (uint)payload.Length);

        Assert.Equal(TdsOperationStatus.Done,
            state.TryReadByteArrayWithContinue(int.MaxValue, isPlp: true, out byte[] value));

        Assert.Equal(payload, value);
        Assert.Equal(0UL, state._longlen);
        Assert.Equal(0UL, state._longlenleft);
    }

    /// <summary>
    /// A Unicode read must not allocate from an oversized total-length hint.
    /// </summary>
    [Theory]
    [InlineData(1048576UL)]
    [InlineData(TdsEnums.SQL_PLP_UNKNOWNLEN)]
    [InlineData(4294967296UL)]
    [InlineData(ulong.MaxValue - 2)]
    [InlineData(1UL)]
    public void TryReadPlpUnicodeChars_ShortValue_AllocatesOnlyReceivedData(ulong totalLength)
    {
        const string expected = "abcd";
        byte[] payload = Encoding.Unicode.GetBytes(expected);
        TdsParserStateObject state = SetInput(WithTerminator(payload), totalLength, (uint)payload.Length);
        char[]? buffer = null;
        bool rented = false;

        Assert.Equal(TdsOperationStatus.Done,
            _parser.TryReadPlpUnicodeChars(ref buffer, 0, int.MaxValue / 2, state,
                out int read, supportRentedBuff: false, ref rented));

        Assert.Equal(expected.Length, read);
        Assert.NotNull(buffer);
        Assert.InRange(buffer.Length, expected.Length, expected.Length * 2);
        Assert.Equal(expected, new string(buffer, 0, read));
    }

    /// <summary>
    /// Missing chunk data must fail without first reserving the entire chunk.
    /// The parser has no connection, making attempts to fetch missing data fail deterministically.
    /// </summary>
    [Theory]
    [InlineData(false, 1048576U)]
    [InlineData(true, 1048576U)]
    [InlineData(false, uint.MaxValue)]
    [InlineData(true, uint.MaxValue)]
    public void PlpRead_TruncatedChunk_DoesNotAllocateMissingData(bool unicode, uint chunkLength)
    {
        byte[] payload = Encoding.Unicode.GetBytes("ab");
        TdsParserStateObject state = SetInput(payload, TdsEnums.SQL_PLP_UNKNOWNLEN, chunkLength);

        if (unicode)
        {
            char[]? buffer = null;
            bool rented = false;
            Assert.Throws<InvalidOperationException>(() =>
                _parser.TryReadPlpUnicodeChars(ref buffer, 0, int.MaxValue / 2, state,
                    out _, supportRentedBuff: false, ref rented));
            Assert.NotNull(buffer);
            Assert.InRange(buffer.Length, 2, 4);
        }
        else
        {
            byte[]? buffer = null;
            Assert.Throws<InvalidOperationException>(() =>
                state.TryReadPlpBytes(ref buffer, 0, int.MaxValue, out _));
            Assert.NotNull(buffer);
            Assert.InRange(buffer.Length, payload.Length, payload.Length * 2);
        }
    }

    /// <summary>
    /// Streaming reads must retain the caller's array and leave unrelated elements unchanged.
    /// </summary>
    [Fact]
    public void TryReadPlpBytes_CallerBuffer_PreservesBufferAndOffset()
    {
        byte[] payload = { 1, 2, 3, 4 };
        TdsParserStateObject state = SetInput(WithTerminator(payload),
            TdsEnums.SQL_PLP_UNKNOWNLEN, (uint)payload.Length);
        byte[] buffer = { 9, 9, 9, 9, 9, 9, 9, 9 };
        byte[] originalBuffer = buffer;

        Assert.Equal(TdsOperationStatus.Done,
            state.TryReadPlpBytes(ref buffer, 2, payload.Length, out int read));

        Assert.Same(originalBuffer, buffer);
        Assert.Equal(payload.Length, read);
        Assert.Equal(new byte[] { 9, 9, 1, 2, 3, 4, 9, 9 }, buffer);
    }

    /// <summary>
    /// A UTF-16 pair split by a TDS header must not consume header bytes as character data.
    /// </summary>
    [Fact]
    public void TryReadPlpUnicodeChars_CharacterAcrossPackets_SkipsHeader()
    {
        byte[] payload = Encoding.Unicode.GetBytes("ab");
        byte[] remainingPayload = WithTerminator(payload.AsSpan(1).ToArray());
        byte[] wire = new byte[1 + TdsEnums.HEADER_LEN + remainingPayload.Length];
        wire[0] = payload[0];
        wire[1] = 4;
        wire[2] = 1;
        wire[4] = (byte)(TdsEnums.HEADER_LEN + remainingPayload.Length);
        remainingPayload.CopyTo(wire, 1 + TdsEnums.HEADER_LEN);
        TdsParserStateObject state = SetInput(wire, (ulong)payload.Length, (uint)payload.Length);
        state._inBytesPacket = 1;
        char[]? buffer = null;
        bool rented = false;

        Assert.Equal(TdsOperationStatus.Done,
            _parser.TryReadPlpUnicodeChars(ref buffer, 0, int.MaxValue / 2, state,
                out int read, supportRentedBuff: false, ref rented));

        Assert.Equal("ab", new string(buffer!, 0, read));
        Assert.Equal(wire.Length, state._inBytesUsed);
    }

    /// <summary>
    /// Capacity arithmetic remains amortized, honors accurate hints, and does not overflow.
    /// </summary>
    [Theory]
    [InlineData(0, 4, ulong.MaxValue - 2, 4)]
    [InlineData(4, 5, 100UL, 8)]
    [InlineData(4, 5, 6UL, 6)]
    [InlineData(4, 5, 1UL, 8)]
    [InlineData(int.MaxValue / 2 + 1, int.MaxValue / 2 + 2, TdsEnums.SQL_PLP_UNKNOWNLEN, int.MaxValue)]
    public void GetPlpBufferSize_GrowsWithoutTrustingHint(int current, int required, ulong hint, int expected)
    {
        Assert.Equal(expected, TdsParserStateObject.GetPlpBufferSize(current, required, hint));
    }

    /// <summary>
    /// Empty and null PLP values preserve their existing representations.
    /// </summary>
    [Theory]
    [InlineData(0UL, false)]
    [InlineData(TdsEnums.SQL_PLP_UNKNOWNLEN, false)]
    [InlineData(TdsEnums.SQL_PLP_NULL, true)]
    public void TryReadPlpLength_EmptyOrNull_PreservesSemantics(ulong totalLength, bool isNull)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.Unicode, leaveOpen: true))
        {
            writer.Write(totalLength);
            if (!isNull)
            {
                writer.Write(0U);
            }
        }
        TdsParserStateObject state = SetInput(stream.ToArray(), 0, 0);

        Assert.Equal(TdsOperationStatus.Done, state.TryReadPlpLength(true, out ulong length));
        Assert.Equal(isNull ? TdsEnums.SQL_PLP_NULL : 0UL, length);
        Assert.Equal(TdsOperationStatus.Done,
            state.TryReadByteArrayWithContinue(int.MaxValue, isPlp: true, out byte[] value));
        Assert.Empty(value);
    }

    /// <summary>
    /// Supplies an already parsed PLP header and buffered payload without using the network.
    /// </summary>
    private TdsParserStateObject SetInput(byte[] payload, ulong totalLength, uint chunkLength)
    {
        TdsParserStateObject state = _parser._physicalStateObj;
        byte[] packet = new byte[Math.Max(512, payload.Length)];
        payload.CopyTo(packet, 0);
        state.SetBuffer(packet, 0, payload.Length);
        state._inBytesPacket = payload.Length;
        state._longlen = totalLength;
        state._longlenleft = chunkLength;
        return state;
    }

    /// <summary>
    /// Appends the zero-length chunk terminating a complete PLP value.
    /// </summary>
    private static byte[] WithTerminator(byte[] payload)
    {
        using MemoryStream stream = new();
        stream.Write(payload, 0, payload.Length);
        stream.Write(new byte[4], 0, 4);
        return stream.ToArray();
    }
}
