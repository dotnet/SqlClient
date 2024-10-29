// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Contains a buffer for a partial or full packet and methods to get information about the status of
    /// the packet that the buffer represents.<br /> 
    /// This class is used to contain partial packet data and helps ensure that the packet data is completely
    /// received before a full packet is made available to the rest of the library
    /// </summary>
    internal sealed partial class Packet
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

        /// <summary>
        /// If the packet data has enough bytes available to determine the length amount of data that should be present
        /// in the packet then this property will be set to the count of data bytes in the packet, <br />
        /// otherwise this will be -1
        /// </summary>
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

        /// <summary>
        /// A byte array containing <see cref="CurrentLength"/> bytes of data or 
        /// </summary>
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

        /// <summary>
        /// The total count of bytes currently in the <see cref="Buffer"/> array including the tds header bytes
        /// </summary>
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

        /// <summary>
        /// If the packet data has enough bytes available to determine the length amount of data that should be present
        /// in the packet then this property will return the count of data bytes that are expected to be in the packet.<br />
        /// If there are not enough bytes to determine the data byte count then this property will throw an exception.<br />
        /// <br />
        /// Call <see cref="HasDataLength"/> to check if there will be a value before using this property.
        /// </summary>
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

        /// <summary>
        /// returns a boolean value indicating if there are enough total bytes availble in the <see cref="Buffer"/> to read the tds header
        /// </summary>
        public bool HasHeader => _totalLength >= TdsEnums.HEADER_LEN;

        /// <summary>
        /// returns a boolean value indicating if the <see cref="DataLength"/> value has been set.
        /// </summary>
        public bool HasDataLength => _dataLength >= 0;

        /// <summary>
        /// returns a boolean value indicating whether the <see cref="Buffer"/> contains enough
        /// data for a valid tds header, has a <see cref="DataLength"/> set and that the 
        /// <see cref="CurrentLength"/> is greater than or equal to the <see cref="DataLength"/> + tds header length.<br /> 
        /// </summary>
        public bool ContainsCompletePacket => _dataLength != UnknownDataLength && (TdsEnums.HEADER_LEN + _dataLength) <= _totalLength;

        /// <summary>
        /// returns a <seealso href="ReadOnlySpan&lt;byte&gt;"/> containing the first 8 bytes of the <see cref="Buffer"/> array which will
        /// contain the TDS header bytes. This can be passed to static functions on <see cref="Packet"/> to extract information from the
        /// tds packet header.<br />
        /// Call <see cref="HasHeader "/> before using this function.
        /// </summary>
        /// <returns></returns>
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

        internal void SetCreatedBy(int creator) => SetCreatedByImpl(creator);

        partial void SetCreatedByImpl(int creator);

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

#if DEBUG
    internal sealed partial class Packet
    {
        private int _createdBy;

        public int CreatedBy => _createdBy;

        partial void SetCreatedByImpl(int creator) => _createdBy = creator;
    }
#endif 
}
