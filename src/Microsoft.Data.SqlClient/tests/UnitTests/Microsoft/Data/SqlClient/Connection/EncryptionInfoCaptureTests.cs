// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Authentication;
using Microsoft.Data.SqlClient.Connection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Unit tests for the per-attempt <see cref="EncryptionInfoCapture"/>, focused on the negotiated
    /// cipher suite plumbing.
    /// </summary>
    public class EncryptionInfoCaptureTests
    {
        // IANA identifier for TLS_AES_256_GCM_SHA384.
        private const int SampleCipherSuite = 0x1302;

        private const string ServerVersion = "16.00.1000";

        [Fact]
        public void NotRecorded_CipherSuiteIsNull()
        {
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);

            Assert.Null(capture.CipherSuite);
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Null(snapshot.NegotiatedCipherSuite);
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
        }

        [Fact]
        public void NothingRecorded_SnapshotIsEmpty()
        {
            EncryptionInfoCapture capture = new();

            Assert.False(capture.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, capture.ToSnapshot());
        }

        [Fact]
        public void Recorded_SurfacesInSnapshot()
        {
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);
            capture.RecordCipherSuite(SampleCipherSuite);

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();

            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuite);
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, snapshot.NegotiatedEncryptionProtocol);
            Assert.Null(snapshot.PresentedServerCertificateHash256);
            Assert.Null(snapshot.ValidationErrors);
            Assert.Null(snapshot.ChainStatus);
        }

        [Fact]
        public void RecordedAlone_HasValuesAndIsNotEmpty()
        {
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(SampleCipherSuite);

            Assert.True(capture.HasValues);
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.NotSame(SqlConnectionEncryptionInfo.Empty, snapshot);
            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuite);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(0xFFFF)]
        public void Recorded_BoundaryValues_Kept(int cipherSuite)
        {
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(cipherSuite);

            Assert.Equal(cipherSuite, capture.ToSnapshot().NegotiatedCipherSuite);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0x10000)]
        public void Recorded_OutOfRange_IgnoredWithoutThrowing(int cipherSuite)
        {
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(SampleCipherSuite);
            capture.RecordCipherSuite(cipherSuite);

            Assert.Equal(SampleCipherSuite, capture.CipherSuite);
            Assert.Equal(SampleCipherSuite, capture.ToSnapshot().NegotiatedCipherSuite);
        }

        [Fact]
        public void Reset_ClearsCipherSuite()
        {
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);
            capture.RecordCipherSuite(SampleCipherSuite);

            capture.Reset();

            Assert.Null(capture.CipherSuite);
            Assert.False(capture.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, capture.ToSnapshot());
        }

        [Fact]
        public void Failure_WithObservedProtocol_KeepsCipherSuite()
        {
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);
            capture.RecordCipherSuite(SampleCipherSuite);

            capture.DiscardUnobservedNegotiation();

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuite);
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, snapshot.NegotiatedEncryptionProtocol);
        }

        [Fact]
        public void Failure_WithoutObservedProtocol_KeepsObservedCipherSuiteAndDropsMode()
        {
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.None);
            capture.RecordCipherSuite(SampleCipherSuite);

            capture.DiscardUnobservedNegotiation();

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuite);
            Assert.Null(snapshot.NegotiatedEncryptionMode);
            Assert.Null(snapshot.NegotiatedEncryptionProtocol);
        }

        [Fact]
        public void Failure_NotObserved_DoesNotInventCipherSuite()
        {
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);

            capture.DiscardUnobservedNegotiation();

            Assert.Null(capture.CipherSuite);
            Assert.Null(capture.ToSnapshot().NegotiatedCipherSuite);
        }

        [Fact]
        public void Failure_RecordModeIfProtocolObserved_DoesNotTouchCipherSuite()
        {
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(SampleCipherSuite);

            capture.RecordModeIfProtocolObserved(SqlConnectionEncryptionMode.Strict);

            Assert.Null(capture.Mode);
            Assert.Equal(SampleCipherSuite, capture.CipherSuite);
        }

        /// <summary>
        /// Mirrors the failed-open attach path: the terminal attempt's snapshot, including the
        /// cipher suite, is what the <see cref="SqlException"/> reports.
        /// </summary>
        [Fact]
        public void Snapshot_AttachedToSqlException_CarriesCipherSuite()
        {
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);
            capture.RecordCipherSuite(SampleCipherSuite);
            SqlException exception = SqlException.CreateException(
                new SqlErrorCollection
                {
                    new SqlError(
                        infoNumber: 50000,
                        errorState: 1,
                        errorClass: 16,
                        server: "server",
                        errorMessage: "Test error message.",
                        procedure: "proc",
                        lineNumber: 1,
                        exception: null,
                        batchIndex: -1)
                },
                ServerVersion);
            string messageBefore = exception.Message;

            exception.EncryptionInfo = capture.ToSnapshot();
            capture.Reset();

            Assert.Equal(SampleCipherSuite, exception.EncryptionInfo.NegotiatedCipherSuite);
            Assert.Equal(messageBefore, exception.Message);
        }

        [Fact]
        public void Snapshot_IsIndependentOfLaterAttempts()
        {
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(SampleCipherSuite);
            SqlConnectionEncryptionInfo first = capture.ToSnapshot();

            capture.Reset();
            capture.RecordCipherSuite(0x1301);

            Assert.Equal(SampleCipherSuite, first.NegotiatedCipherSuite);
            Assert.Equal(0x1301, capture.ToSnapshot().NegotiatedCipherSuite);
        }
    }
}
