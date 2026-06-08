// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.ColMetadata;
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
// Serializes execution with other SimulatedServerTests classes.  Required here because
// DebugAssertSuppressor mutates the global Trace.Listeners collection, which is not
// safe to do concurrently with other tests that may trigger Debug.Assert.
[Collection("SimulatedServerTests")]
public class TdsTokenBoundsTests : IDisposable
{
    private readonly TdsServerFixture _fixture;
    private readonly TdsServer _server;
    private readonly string _connectionString;

    public TdsTokenBoundsTests()
    {
        _fixture = new TdsServerFixture();
        _server = _fixture.TdsServer;
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
    }

    public void Dispose() => _fixture.Dispose();

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
        Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
        Assert.Contains($"Length: {TdsEnums.MaxTokenDataLength + 1}", ex.Message);
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
        Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
        Assert.Contains($"Length: {TdsEnums.MaxTokenDataLength + 1}", ex.Message);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 2b: SessionState token with negative inner option length
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryProcessSessionState</c> rejects an individual session state
    /// option whose inner data length (encoded via the 0xFF + DWORD path) is negative,
    /// which would be interpreted as a huge unsigned value if not bounds-checked.
    /// </summary>
    [Fact]
    public void SessionState_NegativeInnerOptionLength_ThrowsParsingError()
    {
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            // Inject a SessionState token with an inner state option claiming
            // a negative data length (-1 = 0xFFFFFFFF as uint32)
            responseMessage.Insert(doneIndex, new MaliciousSessionStateInnerLenToken(
                innerClaimedLength: -1));
        };

        using SqlConnection connection = new(_connectionString);
        Exception ex = Assert.ThrowsAny<InvalidOperationException>(connection.Open);
        Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
        Assert.Contains("Length: -1", ex.Message);
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
        Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
        Assert.Contains("Length: 999", ex.Message);
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
        Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
        Assert.Contains($"Length: {TdsEnums.MaxTokenDataLength + 1}", ex.Message);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 5: ENV_PROMOTETRANSACTION with oversized newLength
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryProcessEnvChange</c> rejects a PromoteTransaction
    /// environment change token (type 15) whose inner <c>newLength</c> field exceeds
    /// <see cref="TdsEnums.MaxPromoteTransactionLength"/> (64 KB). A malicious server
    /// can set the outer uint16 token length to a small value while writing an
    /// int32 inner length claiming gigabytes, causing unbounded allocation.
    /// </summary>
    [Fact]
    public void EnvChange_PromoteTransaction_OversizedLength_ThrowsParsingError()
    {
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            // Inject a PromoteTransaction env change with a fraudulently large newLength
            responseMessage.Insert(doneIndex, new MaliciousPromoteTransactionEnvChangeToken(
                claimedNewLength: TdsEnums.MaxPromoteTransactionLength + 1));
        };

        using SqlConnection connection = new(_connectionString);
        Exception ex = Assert.ThrowsAny<InvalidOperationException>(connection.Open);
        Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
        Assert.Contains($"Length: {TdsEnums.MaxPromoteTransactionLength + 1}", ex.Message);
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

    /// <summary>
    /// Writes an EnvChange token (0xE3) with type PromoteTransaction (15) whose
    /// inner int32 newLength exceeds <see cref="TdsEnums.MaxPromoteTransactionLength"/>.
    /// Wire format: [0xE3][uint16 tokenLen][byte type=15][int32 newLen][data...][byte oldLen=0]
    /// The outer uint16 tokenLen is set to accommodate the header but the int32
    /// newLength claims far more data than actually follows, triggering the bounds check.
    /// </summary>
    private sealed class MaliciousPromoteTransactionEnvChangeToken : TDSPacketToken
    {
        private readonly int _claimedNewLength;

        public MaliciousPromoteTransactionEnvChangeToken(int claimedNewLength)
        {
            _claimedNewLength = claimedNewLength;
        }

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // Token type: ENVCHANGE
            destination.WriteByte((byte)TDSTokenType.EnvironmentChange); // 0xE3

            // Outer token length (uint16): type(1) + newLength(4) + 1 byte data + oldLength(1)
            // We write just enough to contain the header fields the parser reads before
            // hitting the bounds check. The parser reads: type(1) + int32 newLen(4) = 5 bytes min.
            ushort outerLength = 1 + 4 + 1 + 1; // type + newLen + 1 fake byte + oldLen
            byte[] outerLenBytes = BitConverter.GetBytes(outerLength);
            destination.Write(outerLenBytes, 0, 2);

            // Env change type: PromoteTransaction = 15
            destination.WriteByte(15);

            // newLength (int32) — fraudulently large
            byte[] newLenBytes = BitConverter.GetBytes(_claimedNewLength);
            destination.Write(newLenBytes, 0, 4);

            // Write 1 byte of fake data (the bounds check fires before attempting to read this much)
            destination.WriteByte(0x00);

            // Old value length (byte) = 0
            destination.WriteByte(0x00);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 6: Post-login batch response injection — PromoteTransaction
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that bounds checks fire during command execution (post-login)
    /// by injecting a malicious PromoteTransaction env change token into the
    /// SQL batch response. This exercises the <c>OnSQLBatchCompleted</c> hook
    /// on the simulated server and proves the same bounds check fires regardless
    /// of whether the token arrives during login or command execution.
    /// </summary>
    [Fact]
    public void BatchResponse_PromoteTransaction_OversizedLength_ThrowsParsingError()
    {
        _server.OnSQLBatchCompleted = responseMessage =>
        {
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            responseMessage.Insert(doneIndex, new MaliciousPromoteTransactionEnvChangeToken(
                claimedNewLength: TdsEnums.MaxPromoteTransactionLength + 1));
        };

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1";

        Exception ex = Assert.ThrowsAny<InvalidOperationException>(
            () => command.ExecuteNonQuery());
        Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
        Assert.Contains($"Length: {TdsEnums.MaxPromoteTransactionLength + 1}", ex.Message);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 7: Post-login batch response — DateTime oversized length
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryReadSqlDateTime</c> rejects a TIME column value whose
    /// data length byte exceeds the maximum datetime wire size (10 bytes). A
    /// malicious server could set this length to a large value, causing the
    /// parser to attempt an unbounded heap allocation.
    /// </summary>
    [Fact]
    public void BatchResponse_DateTime_OversizedLength_ThrowsParsingError()
    {
        _server.OnSQLBatchCompleted = responseMessage =>
        {
            // Replace the entire response with a crafted result set containing
            // a TIME column whose ROW data length exceeds the maximum.
            responseMessage.Clear();

            // Use proper library tokens for COLMETADATA so framing is correct
            var metadata = new TDSColMetadataToken();
            var col = new TDSColumnData();
            col.DataType = TDSDataType.TimeN;
            col.DataTypeSpecific = (byte)7; // scale = 7
            col.Flags.IsNullable = true;
            col.Name = string.Empty;
            metadata.Columns.Add(col);
            responseMessage.Add(metadata);

            // Add a malicious ROW token with oversized data length
            responseMessage.Add(new MaliciousTimeRowToken());

            // Add DONE token
            responseMessage.Add(new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1));
        };

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "MALICIOUS_QUERY_NOT_RECOGNIZED";

        SqlDataReader reader = command.ExecuteReader();
        try
        {
            // Read() returns true (data is ready) but doesn't actually parse the
            // column values — that happens on GetValue/GetTimeSpan. Force the read.
            Assert.True(reader.Read());
            Exception ex = Assert.ThrowsAny<InvalidOperationException>(
                () => reader.GetValue(0));
            Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
            Assert.Contains("Length: 11", ex.Message);
        }
        finally
        {
            // Disposing the reader after a corrupted stream causes the driver to
            // attempt further TDS parsing during teardown, which can trip unrelated
            // Debug.Assert calls in TdsParser.
            using (new DebugAssertSuppressor())
            {
                try { reader.Dispose(); } catch { }
            }
        }
    }

    /// <summary>
    /// Writes a ROW token (0xD1) with a single TimeN column whose data length
    /// byte is set to 11 (exceeding the maximum datetime wire size of 10 bytes).
    /// </summary>
    private sealed class MaliciousTimeRowToken : TDSPacketToken
    {
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // ROW token type
            destination.WriteByte(0xD1);

            // TimeN data: length prefix (1 byte) = 11 (INVALID — max for time is 5, max datetime overall is 10)
            destination.WriteByte(11);

            // Write 11 bytes of dummy data
            destination.Write(new byte[11], 0, 11);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 8: Post-login batch response — ReturnValue with oversized data length
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryProcessReturnValue</c> rejects a RETURNVALUE token (0xAC)
    /// whose inner data length (for a non-PLP IMAGE type) exceeds int.MaxValue.
    /// A malicious server can craft a TEXT/IMAGE return value with a spoofed int32
    /// data length that becomes a huge value when cast to ulong, triggering unbounded
    /// allocation.
    /// </summary>
    [Fact]
    public void BatchResponse_ReturnValue_OversizedLength_ThrowsParsingError()
    {
        _server.OnSQLBatchCompleted = responseMessage =>
        {
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            responseMessage.Insert(doneIndex, new MaliciousReturnValueToken());
        };

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1";

        // The malicious RETURNVALUE token corrupts parser state before the
        // exception propagates. Suppress Debug.Assert calls that fire in
        // TdsParser during error handling and connection teardown.
        using (new DebugAssertSuppressor())
        {
            Exception ex = Assert.ThrowsAny<InvalidOperationException>(
                () => command.ExecuteNonQuery());
            Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
            Assert.Contains("Length: -1", ex.Message);
        }
    }

    /// <summary>
    /// Writes a RETURNVALUE token (0xAC) with an IMAGE (0x22) type whose data
    /// length field is set to -1 (0xFFFFFFFF). When cast to ulong this exceeds
    /// int.MaxValue, triggering the bounds check in TryProcessReturnValue.
    /// Wire layout:
    ///   [0xAC] token
    ///   [uint16] ordinal
    ///   [byte] param name length (0)
    ///   [byte] status
    ///   [uint32] user type
    ///   [byte] flags1
    ///   [byte] flags2
    ///   [byte] tds type = 0x22 (IMAGE)
    ///   [int32] max length
    ///   [byte] textPtrLen = 16
    ///   [16 bytes] textPtr
    ///   [8 bytes] timestamp
    ///   [int32] data length = -1 (INVALID)
    /// </summary>
    private sealed class MaliciousReturnValueToken : TDSPacketToken
    {
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // RETURNVALUE token
            destination.WriteByte(0xAC);

            // Ordinal (uint16 LE)
            destination.WriteByte(0x00);
            destination.WriteByte(0x00);

            // Param name length (byte) = 0
            destination.WriteByte(0x00);

            // Status (byte) = 0x01 (output parameter)
            destination.WriteByte(0x01);

            // UserType (uint32 LE) = 0
            destination.Write(new byte[4], 0, 4);

            // Flags byte 1 (ignored)
            destination.WriteByte(0x00);

            // Flags byte 2
            destination.WriteByte(0x00);

            // TDS type = SQLIMAGE (0x22)
            destination.WriteByte(0x22);

            // MaxLen (int32 LE) — for IMAGE this is read via TryGetTokenLength
            // which for 0x22 reads int32. Value doesn't matter much, just needs
            // to be valid for MetaType lookup.
            destination.Write(new byte[] { 0x10, 0x00, 0x00, 0x00 }, 0, 4); // 16

            // -- TryProcessColumnHeaderNoNBC: IsLong && !IsPlp path --
            // TextPtr length (byte) = 16
            destination.WriteByte(0x10);

            // TextPtr data (16 bytes)
            destination.Write(new byte[16], 0, 16);

            // Timestamp (8 bytes)
            destination.Write(new byte[8], 0, 8);

            // Data length (int32 LE) = -1 (0xFFFFFFFF)
            // (ulong)(-1) = 0xFFFFFFFFFFFFFFFF > int.MaxValue → triggers bounds check
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 9: Post-login batch response — PLP ReturnValue (regression guard)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryProcessReturnValue</c> correctly handles PLP
    /// (Partially Length-Prefixed) return values without triggering the bounds
    /// check. PLP types use the unknown-length sentinel (0xFFFFFFFFFFFFFFFE)
    /// which must NOT be rejected by the non-PLP bounds check.
    /// </summary>
    [Fact]
    public void BatchResponse_ReturnValue_PlpUnknownLength_Succeeds()
    {
        _server.OnSQLBatchCompleted = responseMessage =>
        {
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            responseMessage.Insert(doneIndex, new ValidPlpReturnValueToken());
        };

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1";

        // Should NOT throw — PLP unknown-length sentinel is valid
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Writes a valid RETURNVALUE token (0xAC) with a PLP VARBINARY(MAX) type
    /// using the unknown-length sentinel (0xFFFFFFFFFFFFFFFE) followed by an
    /// immediate PLP terminator (chunk length = 0). This exercises the IsPlp
    /// branch in TryProcessReturnValue and must NOT trigger the bounds check.
    /// Wire layout:
    ///   [0xAC] token
    ///   [uint16] ordinal
    ///   [byte] param name length (0)
    ///   [byte] status
    ///   [uint32] user type
    ///   [byte] flags1
    ///   [byte] flags2
    ///   [byte] tds type = 0xA5 (BIGVARBINARY)
    ///   [uint16] max length = 0xFFFF (PLP marker)
    ///   [uint64] PLP length = 0xFFFFFFFFFFFFFFFE (unknown)
    ///   [uint32] chunk length = 0 (terminator)
    /// </summary>
    private sealed class ValidPlpReturnValueToken : TDSPacketToken
    {
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // RETURNVALUE token
            destination.WriteByte(0xAC);

            // Ordinal (uint16 LE)
            destination.WriteByte(0x00);
            destination.WriteByte(0x00);

            // Param name length (byte) = 0
            destination.WriteByte(0x00);

            // Status (byte) = 0x01 (output parameter)
            destination.WriteByte(0x01);

            // UserType (uint32 LE) = 0
            destination.Write(new byte[4], 0, 4);

            // Flags byte 1 (ignored)
            destination.WriteByte(0x00);

            // Flags byte 2
            destination.WriteByte(0x00);

            // TDS type = SQLBIGVARBINARY (0xA5)
            destination.WriteByte(0xA5);

            // MaxLen (uint16 LE) = 0xFFFF — PLP marker
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);

            // -- TryProcessColumnHeaderNoNBC: non-IsLong path --
            // TryGetDataLength → TryReadPlpLength:
            //   reads uint64 = 0xFFFFFFFFFFFFFFFE (unknown length sentinel)
            destination.WriteByte(0xFE);
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);
            destination.WriteByte(0xFF);

            // PLP chunk terminator (uint32 = 0) — empty data
            destination.Write(new byte[4], 0, 4);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 10: Post-login batch response — sql_variant with oversized binary
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryReadSqlValueInternal</c> rejects a binary value inside
    /// a sql_variant column whose inner data length exceeds
    /// <see cref="TdsEnums.MAXSIZE"/> (8000 bytes). The bounds check in the
    /// sql_variant deserialization path prevents unbounded heap allocation.
    /// </summary>
    [Fact]
    public void BatchResponse_SqlVariantBinary_OversizedLength_ThrowsParsingError()
    {
        _server.OnSQLBatchCompleted = responseMessage =>
        {
            responseMessage.Clear();

            // COLMETADATA: one SSVariant column
            var metadata = new TDSColMetadataToken();
            var col = new TDSColumnData();
            col.DataType = TDSDataType.SSVariant;
            col.DataTypeSpecific = (uint)8009; // max length for SSVariant
            col.Flags.IsNullable = true;
            col.Name = string.Empty;
            metadata.Columns.Add(col);
            responseMessage.Add(metadata);

            // ROW with a sql_variant containing oversized binary data
            responseMessage.Add(new MaliciousSqlVariantBinaryRowToken());

            // DONE
            responseMessage.Add(new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1));
        };

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "MALICIOUS_QUERY_NOT_RECOGNIZED";

        SqlDataReader reader = command.ExecuteReader();
        try
        {
            Assert.True(reader.Read());
            Exception ex = Assert.ThrowsAny<InvalidOperationException>(
                () => reader.GetValue(0));
            Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
            Assert.Contains("Length: 8001", ex.Message);
        }
        finally
        {
            // Disposing the reader after a corrupted stream causes the driver to
            // attempt further TDS parsing during teardown, which can trip unrelated
            // Debug.Assert calls in TdsParser.
            using (new DebugAssertSuppressor())
            {
                try { reader.Dispose(); } catch { }
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 11: Post-login batch response — sql_variant with negative inner length
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>TryReadSqlValueInternal</c> rejects a sql_variant binary
    /// value whose declared total length is too small for the type overhead,
    /// causing the computed inner data length to be negative. The bounds check
    /// <c>if (length &lt; 0 || length &gt; TdsEnums.MAXSIZE)</c> catches this.
    /// </summary>
    [Fact]
    public void BatchResponse_SqlVariantBinary_NegativeLength_ThrowsParsingError()
    {
        _server.OnSQLBatchCompleted = responseMessage =>
        {
            responseMessage.Clear();

            // COLMETADATA: one SSVariant column
            var metadata = new TDSColMetadataToken();
            var col = new TDSColumnData();
            col.DataType = TDSDataType.SSVariant;
            col.DataTypeSpecific = (uint)8009; // max length for SSVariant
            col.Flags.IsNullable = true;
            col.Name = string.Empty;
            metadata.Columns.Add(col);
            responseMessage.Add(metadata);

            // ROW with a sql_variant claiming a total length too small for its overhead
            responseMessage.Add(new NegativeLenSqlVariantBinaryRowToken());

            // DONE
            responseMessage.Add(new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1));
        };

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "MALICIOUS_QUERY_NOT_RECOGNIZED";

        SqlDataReader reader = command.ExecuteReader();
        try
        {
            Assert.True(reader.Read());
            Exception ex = Assert.ThrowsAny<InvalidOperationException>(
                () => reader.GetValue(0));
            Assert.Contains("Error state: 18", ex.Message); // CorruptedTdsStream
            Assert.Contains("Length: -1", ex.Message);
        }
        finally
        {
            // Disposing the reader after a corrupted stream causes the driver to
            // attempt further TDS parsing during teardown, which can trip unrelated
            // Debug.Assert calls in TdsParser.
            using (new DebugAssertSuppressor())
            {
                try { reader.Dispose(); } catch { }
            }
        }
    }

    /// <summary>
    /// Writes a ROW token (0xD1) with a single SSVariant column containing a
    /// BigVarBinary variant whose inner data length exceeds MAXSIZE (8000).
    /// Wire layout for the variant:
    ///   [int32] total variant length = 8005
    ///   [byte] inner type = 0xA5 (BigVarBinary)
    ///   [byte] cbPropBytes = 2
    ///   [ushort] maxLen (property) = 8001
    ///   [8001 bytes would be data, but we only write 4 to trigger the check]
    /// lenData = 8005 - 2(SQLVARIANT_SIZE) - 2(cbProps) = 8001 > MAXSIZE → throws
    /// </summary>
    private sealed class MaliciousSqlVariantBinaryRowToken : TDSPacketToken
    {
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // ROW token type
            destination.WriteByte(0xD1);

            // SSVariant column data: total length (int32 LE)
            // lenData = totalLength - SQLVARIANT_SIZE(2) - cbPropBytes(2) = totalLength - 4
            // We want lenData = 8001, so totalLength = 8005
            int totalLength = 8005;
            byte[] lenBytes = BitConverter.GetBytes(totalLength);
            destination.Write(lenBytes, 0, 4);

            // Inner type: BigVarBinary = 0xA5
            destination.WriteByte(0xA5);

            // cbPropBytes = 2
            destination.WriteByte(0x02);

            // Properties: maxLen (ushort) = 8001
            destination.WriteByte(0x41); // 8001 & 0xFF = 0x41
            destination.WriteByte(0x1F); // 8001 >> 8 = 0x1F

            // Write 4 bytes of dummy data (bounds check fires before trying to read 8001)
            destination.Write(new byte[4], 0, 4);
        }
    }

    /// <summary>
    /// Writes a ROW token (0xD1) with a single SSVariant column containing a
    /// BigVarBinary variant whose total length (3) is too small to cover the
    /// type overhead (SQLVARIANT_SIZE=2 + cbPropBytes=2 = 4), producing a
    /// negative computed inner data length: lenData = 3 - 4 = -1.
    /// </summary>
    private sealed class NegativeLenSqlVariantBinaryRowToken : TDSPacketToken
    {
        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // ROW token type
            destination.WriteByte(0xD1);

            // SSVariant column data: total length (int32 LE)
            // lenConsumed = SQLVARIANT_SIZE(2) + cbPropBytes(2) = 4
            // lenData = totalLength - lenConsumed = 3 - 4 = -1 → triggers < 0 check
            int totalLength = 3;
            byte[] lenBytes = BitConverter.GetBytes(totalLength);
            destination.Write(lenBytes, 0, 4);

            // Inner type: BigVarBinary = 0xA5
            destination.WriteByte(0xA5);

            // cbPropBytes = 2
            destination.WriteByte(0x02);

            // Properties: maxLen (ushort) = 100 (arbitrary, just need 2 bytes)
            destination.WriteByte(0x64); // 100 & 0xFF
            destination.WriteByte(0x00); // 100 >> 8

            // No data bytes — the bounds check fires before attempting to read
        }
    }

    /// <summary>
    /// Temporarily suppresses Debug.Assert failures by clearing trace listeners.  Used when
    /// disposing resources after intentionally corrupting a TDS stream.  A static lock serializes
    /// access for the lifetime of the instance because Trace.Listeners is a global collection.
    /// </summary>
    private sealed class DebugAssertSuppressor : IDisposable
    {
        private static readonly object s_listenerLock = new();
        private readonly System.Diagnostics.TraceListener[] _listeners;

        public DebugAssertSuppressor()
        {
            System.Threading.Monitor.Enter(s_listenerLock);
            try
            {
                _listeners = new System.Diagnostics.TraceListener[System.Diagnostics.Trace.Listeners.Count];
                System.Diagnostics.Trace.Listeners.CopyTo(_listeners, 0);
                System.Diagnostics.Trace.Listeners.Clear();
            }
            catch
            {
                System.Threading.Monitor.Exit(s_listenerLock);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                System.Diagnostics.Trace.Listeners.Clear();
                System.Diagnostics.Trace.Listeners.AddRange(_listeners);
            }
            finally
            {
                System.Threading.Monitor.Exit(s_listenerLock);
            }
        }
    }
}
