// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
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
