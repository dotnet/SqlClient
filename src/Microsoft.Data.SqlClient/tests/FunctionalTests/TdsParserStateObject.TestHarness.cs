// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.SqlClient.Tests;

namespace Microsoft.Data.SqlClient
{
#if NETFRAMEWORK
    using PacketHandle = IntPtr; 
#elif NETCOREAPP
    internal struct PacketHandle
    {
    }
#endif
    internal partial class TdsParserStateObject
    {
        internal int ObjectID = 1;

        internal class SQL
        {
            internal static Exception InvalidInternalPacketSize(string v) => throw new Exception(v ?? nameof(InvalidInternalPacketSize));

            internal static Exception ParsingError(ParsingErrorState state) => throw new Exception(state.ToString());
        }

        internal static class SqlClientEventSource
        {
            internal static class Log
            {
                internal static void TryAdvancedTraceBinEvent(string message, params object[] values)
                {
                }
            }
        }

        private enum SnapshotStatus
        {
            NotActive,
            ReplayStarting,
            ReplayRunning
        }

        internal enum TdsParserState
        {
            Closed,
            OpenNotLoggedIn,
            OpenLoggedIn,
            Broken,
        }

        private uint GetSniPacket(PacketHandle packet, ref uint dataSize)
        {
            return SNIPacketGetData(packet, _inBuff, ref dataSize);
        }

        private class StringsHelper
        {
            internal static string GetString(string sqlMisc_InvalidArraySizeMessage) => Strings.SqlMisc_InvalidArraySizeMessage;
        }

        internal class Strings
        {
            internal static string SqlMisc_InvalidArraySizeMessage = nameof(SqlMisc_InvalidArraySizeMessage);

        }

        public class Parser
        {
            internal object ProcessSNIError(TdsParserStateObject tdsParserStateObject) => "ProcessSNIError";
            public TdsParserState State = TdsParserState.OpenLoggedIn;
        }

        sealed internal class LastIOTimer
        {
            internal long _value;
        }

        internal sealed class Snapshot
        {
            public List<PacketData> List;

            public Snapshot() => List = new List<PacketData>();
            [DebuggerStepThrough]
            internal void AppendPacketData(byte[] buffer, int read) => List.Add(new PacketData(buffer, 0, read));
            [DebuggerStepThrough]
            internal void MoveNext()
            {

            }
        }

        public List<PacketData> Input;
        public PacketData Current;
        public bool IsAsync { get => _snapshot != null; }

        public int _packetSize;

        internal Snapshot _snapshot;
        public int _inBytesRead;
        public int _inBytesUsed;
        public byte[] _inBuff;
        [DebuggerStepThrough]
        public TdsParserStateObject(List<PacketData> input, int packetSize, bool isAsync)
        {
            _packetSize = packetSize;
            _inBuff = new byte[_packetSize];
            Input = input;
            if (isAsync)
            {
                _snapshot = new Snapshot();
            }
        }
        [DebuggerStepThrough]
        private uint SNIPacketGetData(PacketHandle packet, byte[] inBuff, ref uint dataSize)
        {
            Span<byte> target = inBuff.AsSpan(0, _packetSize);
            Span<byte> source = Current.Array.AsSpan(Current.Start, Current.Length);
            source.CopyTo(target);
            dataSize = (uint)Current.Length;
            return TdsEnums.SNI_SUCCESS;
        }

        [DebuggerStepThrough]
        void SetBuffer(byte[] buffer, int inBytesUsed, int inBytesRead)
        {
            _inBuff = buffer;
            _inBytesUsed = inBytesUsed;
            _inBytesRead = inBytesRead;
        }

        //  stubs
        private LastIOTimer _lastSuccessfulIOTimer = new LastIOTimer();
        private Parser _parser = new Parser();
        private SnapshotStatus _snapshotStatus = SnapshotStatus.NotActive;

        [DebuggerStepThrough]
        private void SniReadStatisticsAndTracing() { }
        [DebuggerStepThrough]
        private void AssertValidState() { }
        [DebuggerStepThrough]
        private void AddError(object value) => throw new Exception(value as string ?? "AddError");
    }

    internal static class TdsEnums
    {
        public const uint SNI_SUCCESS = 0;        // The operation completed successfully.
        // header constants
        public const int HEADER_LEN = 8;
        public const int HEADER_LEN_FIELD_OFFSET = 2;
        public const int SPID_OFFSET = 4;
    }

    internal enum ParsingErrorState
    {
        CorruptedTdsStream = 18,
        ProcessSniPacketFailed = 19,
    }

    internal sealed class Packet
    {
        public const int UnknownDataLength = -1;

        private bool _disposed;
        private int _dataLength;
        private int _totalLength;
        private byte[] _buffer;

        public Packet()
        {
            _disposed = false;
            _dataLength = UnknownDataLength;
        }

        public int DataLength
        {
            get
            {
                CheckDisposed();
                return _dataLength;
            }
            set
            {
                CheckDisposed();
                _dataLength = value;
            }
        }
        public byte[] Buffer
        {
            get
            {
                CheckDisposed();
                return _buffer;
            }
            set
            {
                CheckDisposed();
                _buffer = value;
            }
        }
        public int CurrentLength
        {
            get
            {
                CheckDisposed();
                return _totalLength;
            }
            set
            {
                CheckDisposed();
                _totalLength = value;
            }
        }

        public int RequiredLength
        {
            get
            {
                CheckDisposed();
                if (!HasDataLength)
                {
                    throw new InvalidOperationException($"cannot get {nameof(RequiredLength)} when {nameof(HasDataLength)} is false");
                }
                return TdsEnums.HEADER_LEN + _dataLength;
            }
        }

        public bool HasHeader => _totalLength >= TdsEnums.HEADER_LEN;

        public bool HasDataLength => _dataLength >= 0;

        public bool IsComplete => _dataLength != UnknownDataLength && (TdsEnums.HEADER_LEN + _dataLength) == _totalLength;

        public ReadOnlySpan<byte> GetHeaderSpan() => _buffer.AsSpan(0, TdsEnums.HEADER_LEN);

        public void Dispose()
        {
            _disposed = true;
        }

        public void CheckDisposed()
        {
            if (_disposed)
            {
                ThrowDisposed();
            }
        }

        public static void ThrowDisposed()
        {
            throw new ObjectDisposedException(nameof(Packet));
        }

        internal static byte GetStatusFromHeader(ReadOnlySpan<byte> header) => header[1];

        internal static int GetDataLengthFromHeader(ReadOnlySpan<byte> header)
        {
            return (header[TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8 | header[TdsEnums.HEADER_LEN_FIELD_OFFSET + 1]) - TdsEnums.HEADER_LEN;
        }
        internal static int GetSpidFromHeader(ReadOnlySpan<byte> header)
        {
            return (header[TdsEnums.SPID_OFFSET] << 8 | header[TdsEnums.SPID_OFFSET + 1]);
        }
        internal static int GetIDFromHeader(ReadOnlySpan<byte> header)
        {
            return header[TdsEnums.HEADER_LEN_FIELD_OFFSET + 4];
        }

        internal static int GetDataLengthFromHeader(Packet packet) => GetDataLengthFromHeader(packet.GetHeaderSpan());

        internal static bool GetIsEOMFromHeader(ReadOnlySpan<byte> header) => GetStatusFromHeader(header) == 1;
    }
}
