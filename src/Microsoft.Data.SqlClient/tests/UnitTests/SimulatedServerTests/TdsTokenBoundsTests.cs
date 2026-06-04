// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.FeatureExtAck;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SessionState;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Tests that the TDS parser rejects various token types with data lengths
/// exceeding protocol-reasonable bounds, preventing unbounded memory allocation
/// from a malicious server.
/// </summary>
[Collection("SimulatedServerTests")]
public class TdsTokenBoundsTests : IClassFixture<TdsServerFixture>
{
    private readonly TdsServer _server;
    private readonly string _connectionString;

    public TdsTokenBoundsTests(TdsServerFixture fixture)
    {
        _server = fixture.TdsServer;
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 1: SessionState token with oversized total length
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryProcessSessionState</c> rejects a SessionState token (0xE4)
    /// whose total length field exceeds <see cref="TdsEnums.MaxTokenDataLength"/> (1 MB),
    /// preventing unbounded memory allocation from a spoofed token length.
    /// </summary>
    [Fact]
    public void SessionState_OversizedTotalLength_ThrowsParsingError()
    {
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            // Inject a SessionState token claiming a total length exceeding MaxTokenDataLength
            responseMessage.Insert(doneIndex, new MaliciousSessionStateToken(
                claimedTotalLength: (uint)(TdsEnums.MaxTokenDataLength + 1)));
        };

        using SqlConnection connection = new(_connectionString);
        Exception ex = Assert.ThrowsAny<InvalidOperationException>(connection.Open);
        Assert.Contains("18", ex.Message); // CorruptedTdsStream
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 2: SessionState token with oversized inner option length
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryProcessSessionState</c> rejects an individual session state
    /// option whose inner data length (encoded via the 0xFF + DWORD path) exceeds
    /// <see cref="TdsEnums.MaxTokenDataLength"/>, even when the outer token length is valid.
    /// </summary>
    [Fact]
    public void SessionState_OversizedInnerOptionLength_ThrowsParsingError()
    {
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            // Inject a SessionState token with a valid outer length but an inner
            // state option claiming a huge data length
            responseMessage.Insert(doneIndex, new MaliciousSessionStateInnerLenToken(
                innerClaimedLength: TdsEnums.MaxTokenDataLength + 1));
        };

        using SqlConnection connection = new(_connectionString);
        Exception ex = Assert.ThrowsAny<InvalidOperationException>(connection.Open);
        Assert.Contains("18", ex.Message); // CorruptedTdsStream
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 3: SRECOVERY feature ack with malformed inner state data
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the secondary parse of FEATUREEXT_SRECOVERY data in
    /// <c>SqlConnectionInternal.OnFeatureExtAck</c> rejects inner state options
    /// whose claimed length exceeds the remaining buffer, preventing an
    /// out-of-bounds read or over-allocation.
    /// </summary>
    [Fact]
    public void SRecovery_MalformedInnerStateLength_ThrowsParsingError()
    {
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            // Remove existing FeatureExtAck if present
            var existing = responseMessage.OfType<TDSFeatureExtAckToken>().FirstOrDefault();
            if (existing != null)
            {
                responseMessage.Remove(existing);
            }

            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            // Inject a FeatureExtAck with SessionRecovery feature containing
            // inner state data where a state option claims a length exceeding the buffer
            responseMessage.Insert(doneIndex, new MaliciousSRecoveryFeatureExtAckToken());
        };

        using SqlConnection connection = new(_connectionString);
        Exception ex = Assert.ThrowsAny<InvalidOperationException>(connection.Open);
        Assert.Contains("18", ex.Message); // CorruptedTdsStream
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 4: FedAuthInfo token with oversized total length
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryProcessFedAuthInfo</c> rejects a FedAuthInfo token (0xEE)
    /// whose total length exceeds <see cref="TdsEnums.MaxTokenDataLength"/> (1 MB).
    /// The token type is dispatched unconditionally by <c>TryRun</c>, so this check
    /// fires regardless of whether federated authentication was negotiated.
    /// </summary>
    [Fact]
    public void FedAuthInfo_OversizedTokenLength_ThrowsParsingError()
    {
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            // Inject a FedAuthInfo token with a total length exceeding MaxTokenDataLength.
            // The parser dispatches on token type regardless of whether FedAuth was negotiated.
            responseMessage.Insert(doneIndex, new MaliciousFedAuthInfoToken(
                claimedLength: TdsEnums.MaxTokenDataLength + 1));
        };

        using SqlConnection connection = new(_connectionString);
        Exception ex = Assert.ThrowsAny<InvalidOperationException>(connection.Open);
        Assert.Contains("18", ex.Message); // CorruptedTdsStream
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Malicious token helpers
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Writes a SessionState token (0xE4) with a fraudulently large total length.
    /// Wire format: [0xE4][uint32 totalLen][uint32 seqNum][byte status][...]
    /// The bounds check fires on the totalLen value before any data is read.
    /// </summary>
    private sealed class MaliciousSessionStateToken : TDSPacketToken
    {
        private readonly uint _claimedTotalLength;

        public MaliciousSessionStateToken(uint claimedTotalLength)
        {
            _claimedTotalLength = claimedTotalLength;
        }

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // Token type
            destination.WriteByte((byte)TDSTokenType.SessionState); // 0xE4

            // Total token length (uint32) — this is the fraudulent value
            byte[] lenBytes = BitConverter.GetBytes(_claimedTotalLength);
            destination.Write(lenBytes, 0, 4);

            // Write minimal valid-looking data: seqNum (4 bytes) + status (1 byte)
            // The bounds check should fire before trying to process option data.
            destination.Write(new byte[] { 0x01, 0x00, 0x00, 0x00 }, 0, 4); // seqNum = 1
            destination.WriteByte(0x01); // status = recoverable
        }
    }

    /// <summary>
    /// Writes a SessionState token (0xE4) with a valid outer length but an inner
    /// state option that claims a huge data length (using the 0xFF + DWORD encoding).
    /// Wire format: [0xE4][uint32 totalLen][uint32 seqNum][byte status]
    ///              [byte stateId][0xFF][int32 innerLen][...data...]
    /// The inner bounds check fires on the innerLen value.
    /// </summary>
    private sealed class MaliciousSessionStateInnerLenToken : TDSPacketToken
    {
        private readonly int _innerClaimedLength;

        public MaliciousSessionStateInnerLenToken(int innerClaimedLength)
        {
            _innerClaimedLength = innerClaimedLength;
        }

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // Token type
            destination.WriteByte((byte)TDSTokenType.SessionState); // 0xE4

            // Calculate total token length:
            // seqNum(4) + status(1) + stateId(1) + lenMarker(1) + innerLen(4) + minimal data(1)
            uint totalLength = 4 + 1 + 1 + 1 + 4 + 1;

            byte[] lenBytes = BitConverter.GetBytes(totalLength);
            destination.Write(lenBytes, 0, 4);

            // Sequence number
            destination.Write(new byte[] { 0x01, 0x00, 0x00, 0x00 }, 0, 4);

            // Status (recoverable)
            destination.WriteByte(0x01);

            // State option: stateId
            destination.WriteByte(0x00); // UserOptions state ID

            // Length marker: 0xFF means next 4 bytes are the DWORD length
            destination.WriteByte(0xFF);

            // Inner claimed length — fraudulently large
            byte[] innerLenBytes = BitConverter.GetBytes(_innerClaimedLength);
            destination.Write(innerLenBytes, 0, 4);

            // Write only 1 byte of actual data
            destination.WriteByte(0x42);
        }
    }

    /// <summary>
    /// Writes a FeatureExtAck token (0xAE) with a SessionRecovery feature (ID=1)
    /// that carries inner state data where a state option claims a length exceeding
    /// the remaining buffer. This exercises the bounds check in
    /// SqlConnectionInternal.OnFeatureExtAck for FEATUREEXT_SRECOVERY.
    /// </summary>
    private sealed class MaliciousSRecoveryFeatureExtAckToken : TDSPacketToken
    {
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // Token type: FEATUREEXTACK
            destination.WriteByte((byte)TDSTokenType.FeatureExtAck); // 0xAE

            // Feature ID: SessionRecovery = 0x01
            destination.WriteByte(0x01);

            // The feature data for SRECOVERY is parsed by SqlConnectionInternal.OnFeatureExtAck:
            // It reads pairs of [stateId(1)][lenByte(1)][data(len)] or [stateId(1)][0xFF][int32 len][data(len)]
            // We'll craft inner data where one option claims length > remaining buffer.

            // Inner data layout:
            // stateId(1) + 0xFF marker(1) + int32 len(4) + 1 byte actual data
            byte[] innerData;
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x00); // stateId = 0

                // Use 0xFF marker to indicate DWORD length
                ms.WriteByte(0xFF);

                // Claim a length that exceeds the remaining buffer
                // The remaining buffer after reading stateId + 0xFF + 4-byte-len will be 1 byte,
                // but we claim 999 bytes
                byte[] claimedLen = BitConverter.GetBytes(999);
                ms.Write(claimedLen, 0, 4);

                // Only provide 1 byte of actual data
                ms.WriteByte(0x42);

                innerData = ms.ToArray();
            }

            // Feature data length (uint32)
            byte[] featureDataLen = BitConverter.GetBytes((uint)innerData.Length);
            destination.Write(featureDataLen, 0, 4);

            // Feature data
            destination.Write(innerData, 0, innerData.Length);

            // Terminator
            destination.WriteByte((byte)TDSFeatureID.Terminator); // 0xFF
        }
    }

    /// <summary>
    /// Writes a FedAuthInfo token (0xEE) with a fraudulently large total length.
    /// Wire format: [0xEE][int32 tokenLen][...data...]
    /// The bounds check fires on tokenLen before any data is read.
    /// </summary>
    private sealed class MaliciousFedAuthInfoToken : TDSPacketToken
    {
        private readonly int _claimedLength;

        public MaliciousFedAuthInfoToken(int claimedLength)
        {
            _claimedLength = claimedLength;
        }

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // Token type
            destination.WriteByte(0xEE); // SQLFEDAUTHINFO

            // Token length (int32) — fraudulently large
            byte[] lenBytes = BitConverter.GetBytes(_claimedLength);
            destination.Write(lenBytes, 0, 4);

            // Write minimal data (at least sizeof(uint) to pass the lower bound check,
            // but the upper bound check should fire first)
            destination.Write(new byte[] { 0x01, 0x00, 0x00, 0x00 }, 0, 4); // optionsCount = 1
        }
    }
}
