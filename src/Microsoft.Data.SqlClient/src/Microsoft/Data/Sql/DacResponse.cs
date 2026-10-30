// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System;
using System.Buffers;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Data.Sql;

/// <summary>
/// A single parsed SSRP response.
/// </summary>
/// <seealso href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/45b52721-7a48-45cf-9c84-e6db905ad6df"/>
/// <remarks>
/// <para>This corresponds to an SVR_RESP (DAC) structure within the MC-SQLR specification.</para>
/// <para>An SVR_RESP (DAC) structure is a byte array with the following layout:</para>
/// <list type="number">
/// <item>SVR_RESP: 1 byte, always 0x05.</item>
/// <item>RESP_SIZE: 2 bytes, always 0x06. Written in little-endian byte order.</item>
/// <item>PROTOCOLVERSION: 1 byte, always 0x01.</item>
/// <item>TCP_DAC_PORT: 2 bytes, the TCP port number that is used for the DAC. Written in little-endian byte order.</item>
/// </list>
/// </remarks>
internal readonly ref struct DacResponse
{
    private const int ResponseHeaderOffset = 0;
    private const int ResponseSizeOffset = ResponseHeaderOffset + sizeof(byte);
    private const int ProtocolVersionOffset = ResponseSizeOffset + sizeof(ushort);
    private const int DacPortOffset = ProtocolVersionOffset + sizeof(byte);
    private const int TotalResponseSize = DacPortOffset + sizeof(ushort);

    private const byte ResponseHeaderValue = 0x05;
    private const ushort ResponseSizeValue = 0x06;
    private const byte ProtocolVersionValue = 0x01;

    public ushort DacPort { get; }

    private DacResponse(ushort tcpDacPort)
    {
        DacPort = tcpDacPort;
    }

    /// <summary>
    /// Attempts to parse a single SSRP response from the start of the provided source sequence.
    /// If an SSRP response cannot be found, supplies the maximum number of bytes to advance the
    /// sequence by before attempting to parse another response.
    /// </summary>
    /// <param name="sourceSequence">The source buffer to read from.</param>
    /// <param name="response">The populated SSRP response (or default, if one cannot be found.)</param>
    /// <param name="bytesRead">The number of bytes to advance <paramref name="sourceSequence"/> by.</param>
    /// <returns><c>true</c> if the response was processed, <c>false</c> if not.</returns>
    /// <remarks>
    /// If the sequence does not start with an SSRP response, <paramref name="bytesRead"/> will
    /// contain the position of the next possible <c>SVR_RESP</c> header byte (<c>0x05</c>), or
    /// the length of <paramref name="sourceSequence"/> if this header byte is not present in the
    /// sequence.
    /// </remarks>
    public static bool TryParse(ReadOnlySequence<byte> sourceSequence, out DacResponse response, out long bytesRead)
    {
        // Make sure we have enough data to read the header.
        if (sourceSequence.Length < TotalResponseSize)
        {
            bytesRead = sourceSequence.Length;
            response = default;
            return false;
        }

        ReadOnlySequence<byte> currSequence = sourceSequence;
        ReadOnlySpan<byte> currSpan = currSequence.First.Span;
        long currOffset = 0;

        // Read and validate the response header.
        if (currSequence.ReadByte(ref currSpan, ref currOffset, out byte responseHeader)
            // RESP_SVR must be 0x05.
            && responseHeader == ResponseHeaderValue
            && currSequence.ReadLittleEndian(ref currSpan, ref currOffset, out ushort responseSize)
            // RESP_SIZE must be 0x0006.
            && responseSize == ResponseSizeValue
            && currSequence.ReadByte(ref currSpan, ref currOffset, out byte protocolVersion)
            // PROTOCOLVERSION must be 0x01.
            && protocolVersion == ProtocolVersionValue
            && currSequence.ReadLittleEndian(ref currSpan, ref currOffset, out ushort tcpDacPort)
            // TCP_DAC_PORT must be non-zero.
            && tcpDacPort != 0)
        {
            Debug.Assert(currOffset == TotalResponseSize);

            bytesRead = currOffset;
            response = new DacResponse(tcpDacPort);
            return true;
        }
        else
        {
            // Find the next possible response start across the sequence. Always advance by one byte (to skip the current
            // header byte.)
            long idx = sourceSequence.Slice(1).IndexOf(ResponseHeaderValue);

            bytesRead = idx == -1
                ? sourceSequence.Length
                : idx + 1;
            response = default;
            return false;
        }
    }
}
