// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.Data.Sql.UnitTests;

/// <summary>
/// Test cases used to verify the successful processing of valid SSRP responses and the silent
/// discarding of invalid SSRP responses.
/// </summary>
internal static class SsrpPacketTestData
{
    /// <summary>
    /// One empty packet buffer, which should be successfully processed and contain zero responses.
    /// </summary>
    /// <see cref="DacResponseProcessorTest.Process_EmptyBuffer_ReturnsFalse"/>
    /// <see cref="SqlDataSourceResponseProcessorTest.Process_EmptyBuffer_ReturnsFalse"/>
    public static TheoryData<ReadOnlySequence<byte>> EmptyPacketBuffer =>
        new(GeneratePacketBuffers([]));

    /// <summary>
    /// Various combinations of packet buffers containing normal SVR_RESP responses, all of which
    /// should be successfully processed.
    /// </summary>
    /// <see cref="SqlDataSourceResponseProcessorTest.Process_ValidSqlDataSourceResponse_ReturnsData"/>
    public static TheoryData<ReadOnlySequence<byte>, string, int, string?> ValidSVR_RESPPacketBuffer
    {
        get
        {
            byte[] complexValidPacket = FormatSVR_RESPMessage(0x05,
                CreateRESP_DATA("svr1", "MSSQLSERVER", true, "14.0.0.0",
                    CreateProtocolParameters(tcpInfo: "tcp;1433", npInfo: @"np;\\svr1\pipe\SampléPipeName",
                        viaInfo: "via;svr1 1:1433", rpcInfo: "rpc;svr1", spxInfo: "spx;MSSQLSERVER",
                        adspInfo: "adsp;SQL2000", bvInfo: "bv;item;group;item;group;org", otherParameters: null)));
            byte[] validPacket1 = FormatSVR_RESPMessage(0x05,
                CreateRESP_DATA("svr1", "MSSQLSERVER", true, "14.0.0.0",
                    CreateProtocolParameters(tcpInfo: "tcp;1433", null, null, null, null, null, null, null)));
            byte[] validPacket2 = FormatSVR_RESPMessage(0x05,
                CreateRESP_DATA("svr1", "MSSQLSERVER", true, "14.0.0.0",
                    CreateProtocolParameters(tcpInfo: "tcp;1434", null, null, null, null, null, null, null)));
            byte[] validPacket3 = FormatSVR_RESPMessage(0x05,
                CreateRESP_DATA("svr1", "MSSQLSERVER", true, "14.0.0.0",
                    CreateProtocolParameters(tcpInfo: "tcp;1435", null, null, null, null, null, null, null)));
            byte[] validPacket4 = FormatSVR_RESPMessage(0x05,
                CreateRESP_DATA("svr1", "MSSQLSERVER", true, "14.0.0.0",
                    CreateProtocolParameters(tcpInfo: "tcp;1436", null, null, null, null, null, null, null)));
            byte[] invalidPacket1 = FormatSVR_RESPMessage(0x05,
                CreateRESP_DATA("svr1", "MSSQLSERVER", true, "v14",
                    CreateProtocolParameters(tcpInfo: "tcp;1433", null, null, null, null, null, null, null)));

            return new()
            {
                // One buffer, one response
                { GeneratePacketBuffers(
                    complexValidPacket
                ), "14.0.0.0", 1433, @"\\svr1\pipe\SampléPipeName" },
                // One response, split into four buffers in the middle of a string
                { GeneratePacketBuffers(
                    complexValidPacket.AsSpan(0, 14).ToArray(),
                    complexValidPacket.AsSpan(14, 22).ToArray(),
                    complexValidPacket.AsSpan(36, 71).ToArray(),
                    // Position 107 is the second byte of the é character when encoded to UTF8.
                    complexValidPacket.AsSpan(107).ToArray()
                ), "14.0.0.0", 1433, @"\\svr1\pipe\SampléPipeName" },
                // Four responses, each with different methods
                { GeneratePacketBuffers(
                    validPacket1, validPacket2, validPacket3, validPacket4
                ), "14.0.0.0", 1436, null },
                // Five responses, with response three invalid
                { GeneratePacketBuffers(
                    complexValidPacket, validPacket2, invalidPacket1, validPacket3, validPacket4
                ), "14.0.0.0", 1436, null }
            };
        }
    }

    /// <summary>
    /// Various combinations of packet buffers containing SVR_RESP (DAC) responses, all of which
    /// should be successfully processed.
    /// </summary>
    /// <see cref="SqlDataSourceResponseProcessorTest.Process_ValidDacResponse_ReturnsData"/>
    public static TheoryData<ReadOnlySequence<byte>, int> ValidSVR_RESP_DACPacketBuffer
    {
        get
        {
            byte[] validPacket1 = FormatSVR_RESPMessage(0x05, 0x06, CreateRESP_DATA(0x01, 1434));
            byte[] validPacket2 = FormatSVR_RESPMessage(0x05, 0x06, CreateRESP_DATA(0x01, 1435));
            byte[] validPacket3 = FormatSVR_RESPMessage(0x05, 0x06, CreateRESP_DATA(0x01, 1436));
            byte[] validPacket4 = FormatSVR_RESPMessage(0x05, 0x06, CreateRESP_DATA(0x01, 1437));
            byte[] invalidPacket1 = FormatSVR_RESPMessage(0x05, 0x03, CreateRESP_DATA(0x01, 1434));

            return new()
            {
                // One buffer, one response
                { GeneratePacketBuffers(validPacket1), 1434 },

                // One response, split into three buffers
                { GeneratePacketBuffers(validPacket1.AsSpan(0, 2).ToArray(),
                    validPacket1.AsSpan(2, 2).ToArray(),
                    validPacket1.AsSpan(4).ToArray()), 1434 },

                // Two responses with trailing data
                { GeneratePacketBuffers(validPacket1.AsSpan(0, 2).ToArray(),
                    validPacket1.AsSpan(2, 2).ToArray(),
                    [..validPacket1.AsSpan(4).ToArray(), 0x05],
                    validPacket2.AsSpan(0, 2).ToArray(),
                    validPacket2.AsSpan(2).ToArray(),
                    [0x05]), 1435 },

                // Four responses, each with different DAC ports
                { GeneratePacketBuffers(validPacket1, validPacket2, validPacket3, validPacket4), 1437 },

                // Five responses, with response three invalid
                { GeneratePacketBuffers(validPacket1, validPacket2, invalidPacket1, validPacket3, validPacket4), 1437 },

                // Four responses, with three extraneous 0x05 bytes between responses 2 and 3
                { GeneratePacketBuffers(validPacket1, [..validPacket2, 0x05], [0x05], [0x05, ..validPacket3], validPacket4), 1437 }
            };
        }
    }

    /// <summary>
    /// Packet buffers containing nothing but invalid SVR_RESP (DAC) responses.
    /// </summary>
    /// <see cref="DacResponseProcessorTest.Process_InvalidDacResponse_ReturnsFalse"/>
    public static TheoryData<ReadOnlySequence<byte>> InvalidSVR_RESP_DACPackets
    {
        get
        {
            return new()
            {
                // Invalid header byte
                { GeneratePacketBuffers(FormatSVR_RESPMessage(0x00, 0x06, CreateRESP_DATA(0x01, 1434))) },

                // Invalid size
                { GeneratePacketBuffers(FormatSVR_RESPMessage(0x05, 0x09, CreateRESP_DATA(0x01, 1434))) },

                // Invalid protocol version
                { GeneratePacketBuffers(FormatSVR_RESPMessage(0x05, 0x06, CreateRESP_DATA(0x02, 1434))) },
                
                // Invalid port
                { GeneratePacketBuffers(FormatSVR_RESPMessage(0x05, 0x06, CreateRESP_DATA(0x01, 0))) },
            };
        }
    }

    /// <summary>
    /// Packets containing an SVR_RESP response which is a valid response to a CLNT_[B|U]CAST_EX message
    /// but not to a CLNT_UCAST_INST message.
    /// </summary>
    /// <see cref="SqlDataSourceResponseProcessorTest.Process_InvalidSqlDataSourceResponseToCLNT_UCAST_INST_ReturnsFalse"/>
    public static TheoryData<ReadOnlySequence<byte>> Invalid_CLNT_UCAST_INST_SVR_RESPPackets
    {
        get
        {
            // The RESP_DATA section of the response to a CLNT_UCAST_INST message must be shorter than 1024 bytes.
            byte[] longPacket = FormatSVR_RESPMessage(0x05,
                CreateRESP_DATA("svr1", "MSSQLSERVER", true, "14.0.0.0",
                    CreateProtocolParameters(tcpInfo: "tcp;1433", npInfo: @"np;" + new string('a', 1025),
                        null, null, null, null, null, null)));

            return new()
            {
                GeneratePacketBuffers(longPacket)
            };
        }
    }

    /// <summary>
    /// Packet buffers containing an SSRP message which is failing due to invalid data
    /// in the top-level SVR_RESP message fields.
    /// </summary>
    /// <see cref="SqlDataSourceResponseProcessorTest.Process_InvalidSqlDataSourceResponse_ReturnsFalse"/>
    public static TheoryData<ReadOnlySequence<byte>> InvalidSVR_RESPPackets
    {
        get
        {
            return new(
                // Invalid SVR_RESP header field value
                GeneratePacketBuffers(
                    FormatSVR_RESPMessage(0x04, 0x06, CreateRESP_DATA(0x01, 1434))
                ),

                // RESP_SIZE too small (DAC response)
                GeneratePacketBuffers(
                    FormatSVR_RESPMessage(0x05, 0x05, CreateRESP_DATA(0x01, 1434))
                ),

                // RESP_SIZE too large (DAC response)
                GeneratePacketBuffers(
                    FormatSVR_RESPMessage(0x05, 0x07, CreateRESP_DATA(0x01, 1434))
                ),

                // RESP_SIZE larger than the buffer (normal response)
                GeneratePacketBuffers(
                    FormatSVR_RESPMessage(0x05, 72,
                        CreateRESP_DATA("svr1", "MSSQLSERVER", true, "14.0.0"))
                )
            );
        }
    }

    /// <summary>
    /// Packet buffers containing an SSRP message with valid top-level SVR_RESP message
    /// fields but invalid components of the child RESP_DATA structure.
    /// </summary>
    /// <see cref="SqlDataSourceResponseProcessorTest.Process_InvalidSqlDataSourceResponse_RESP_DATA_ReturnsFalse"/>
    public static TheoryData<ReadOnlySequence<byte>> InvalidRESP_DATAPackets
    {
        get
        {
            string validTcpInfo = CreateProtocolParameters("tcp;1433", null, null, null, null, null, null, null);

            return new(
                // Does not start with "ServerName" string
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0", validTcpInfo, omitServerName: true))),

                // Server name longer than 255 bytes
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA(new string('a', 256), "MSSQLSERVER", true, "14.0.0.0", validTcpInfo))),

                // Missing semicolons between keys and values
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0", validTcpInfo, omitKeyValueSeparators: true))),

                // Missing terminating pair of semicolons
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0", validTcpInfo, omitTrailingSemicolons: true))),

                // Missing "InstanceName"
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0", validTcpInfo, omitInstanceName: true))),

                // Instance name longer than 255 bytes
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", new string('a', 256), true, "14.0.0.0", validTcpInfo))),

                // Missing "IsClustered"
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0", validTcpInfo, omitIsClustered: true))),

                // Invalid IsClustered value
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0", "IsClustered;INVALID;" + validTcpInfo, omitIsClustered: true))),

                // Missing "Version"
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0", validTcpInfo, omitVersion: true))),

                // Empty version string
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "", validTcpInfo, omitVersion: true))),

                // Version string longer than 16 bytes
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "65535.65535.65.53", validTcpInfo))),

                // Version string not in the correct format: 1*[0-9"."]
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "v14", validTcpInfo))),

                // Protocol components listed twice
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0",
                        CreateProtocolParameters("tcp;1434", null, null, null, null, null, null, "tcp;1434")))),

                // Invalid protocol components appear
                GeneratePacketBuffers(FormatSVR_RESPMessage(0x05,
                    CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0",
                        CreateProtocolParameters("tcp;1434", null, null, null, null, null, null, "invalid_protocol;value")))),

                // Invalid PROTOCOLVERSION field value
                GeneratePacketBuffers(
                    FormatSVR_RESPMessage(0x05, 0x06, CreateRESP_DATA(0x02, 1434))
                )
            );
        }
    }

    /// <summary>
    /// Packet buffers containing an SSRP message with valid top-level SVR_RESP message
    /// fields, a valid RESP_DATA child structure but an invalid TCP_INFO structure.
    /// </summary>
    /// <see cref="SqlDataSourceResponseProcessorTest.Process_InvalidSqlDataSourceResponse_TCP_INFO_ReturnsFalse"/>
    public static TheoryData<ReadOnlySequence<byte>> InvalidTCP_INFOPackets
    {
        get
        {
            return new(
                // Port is absent
                CreateSVR_RESPMessage("tcp"),

                // Port is non-numeric
                CreateSVR_RESPMessage("tcp;one"),

                // Port is > ushort.MaxValue
                CreateSVR_RESPMessage("tcp;65536"),

                // Port is < 0
                CreateSVR_RESPMessage("tcp;-1")
            );

            static ReadOnlySequence<byte> CreateSVR_RESPMessage(string tcpInfo) =>
                GeneratePacketBuffers(
                    FormatSVR_RESPMessage(0x05,
                        CreateRESP_DATA("srv1", "MSSQLSERVER", true, "14.0.0.0",
                            CreateProtocolParameters(tcpInfo, null, null, null, null, null, null, null)
                        )
                    )
                );
        }
    }

    private static ReadOnlySequence<byte> GeneratePacketBuffers(params byte[][] packetBuffers)
    {
        if (packetBuffers.Length == 0)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        PacketBuffer first = new(packetBuffers[0], null);
        PacketBuffer curr = first;
        PacketBuffer last;

        for (int i = 1; i < packetBuffers.Length; i++)
        {
            curr = new(packetBuffers[i], curr);
        }
        last = curr;

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    /// <summary>
    /// Generates an SVR_RESP message with a valid length.
    /// </summary>
    /// <param name="header">The SVR_RESP header value. Expected to be 0x05.</param>
    /// <param name="respData">The serialized RESP_DATA section.</param>
    /// <seealso cref="FormatSVR_RESPMessage(byte, ushort, ReadOnlySpan{byte}, int?)"/>
    /// <returns>A byte representation of one SVR_RESP message.</returns>
    private static byte[] FormatSVR_RESPMessage(byte header, ReadOnlySpan<byte> respData) =>
        FormatSVR_RESPMessage(header, (ushort)respData.Length, respData);

    /// <summary>
    /// Generates an SVR_RESP message with specific characteristics.
    /// </summary>
    /// <param name="header">The SVR_RESP header value. Expected to be 0x05.</param>
    /// <param name="serializedResponseSize">The RESP_SIZE field to be serialized to the header.</param>
    /// <param name="respData">The serialized RESP_DATA section.</param>
    /// <param name="realResponseSize">If specified, the number of bytes to actually write.</param>
    /// <returns>A byte representation of one SVR_RESP message.</returns>
    /// <seealso href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/2e1560c9-5097-4023-9f5e-72b9ff1ec3b1"/>
    /// <seealso href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/45b52721-7a48-45cf-9c84-e6db905ad6df"/>
    private static byte[] FormatSVR_RESPMessage(byte header, ushort serializedResponseSize, ReadOnlySpan<byte> respData,
        int? realResponseSize = null)
    {
        byte[] realRespData = realResponseSize is null
            ? new byte[sizeof(byte) + sizeof(ushort) + respData.Length]
            : new byte[realResponseSize.Value];

        // Pad any free space after RESP_DATA with 0x05
        if (realRespData.Length > sizeof(byte) + sizeof(ushort) + respData.Length)
        {
            realRespData.AsSpan(sizeof(byte) + sizeof(ushort) + respData.Length).Fill(0x05);
        }

        // Write RESP_DATA
        if (realRespData.Length > sizeof(byte) + sizeof(ushort))
        {
            int bytesToCopy = Math.Min(respData.Length, realRespData.Length - sizeof(byte) - sizeof(ushort));

            respData.Slice(0, bytesToCopy).CopyTo(realRespData.AsSpan(sizeof(byte) + sizeof(ushort)));
        }

        // Write RESP_SIZE
        if (realRespData.Length > sizeof(byte))
        {
            Span<byte> responseSizeBytes = stackalloc byte[sizeof(ushort)];
            int bytesToCopy = Math.Min(responseSizeBytes.Length, realRespData.Length - sizeof(byte));

            BinaryPrimitives.WriteUInt16LittleEndian(responseSizeBytes, serializedResponseSize);
            responseSizeBytes.Slice(0, bytesToCopy).CopyTo(realRespData.AsSpan(sizeof(byte)));
        }

        // Write SVR_RESP
        if (realRespData.Length > 0)
        {
            realRespData[0] = header;
        }

        return realRespData;
    }

    /// <summary>
    /// Creates a new RESP_DATA section of an SVR_RESP message for the DAC request with a specified protocol version and TCP port number.
    /// </summary>
    /// <param name="protocolVersion">Protocol version. Expected to be 0x01.</param>
    /// <param name="dacPort">TCP port number of the DAC.</param>
    /// <returns>A byte representation of a RESP_DATA section.</returns>
    /// <seealso href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/45b52721-7a48-45cf-9c84-e6db905ad6df"/>
    private static byte[] CreateRESP_DATA(byte protocolVersion, ushort dacPort)
    {
        byte[] data = new byte[sizeof(byte) + sizeof(ushort)];

        data[0] = protocolVersion;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(1), dacPort);
        return data;
    }

    /// <summary>
    /// Creates a RESP_DATA section of an SVR_RESP message with specific characteristics.
    /// </summary>
    /// <param name="serverName">ServerName parameter value.</param>
    /// <param name="instanceName">InstanceName parameter value.</param>
    /// <param name="isClustered">IsClustered parameter value.</param>
    /// <param name="version">Version parameter value.</param>
    /// <param name="protocolParameters">If specified, the protocol parameters. Generated by <see cref="CreateProtocolParameters"/>.</param>
    /// <param name="lowercaseKey">If true, the ServerName, InstanceName, IsClustered and Version keys will be written in lowercase.</param>
    /// <param name="omitTrailingSemicolons">If true, the return value will not include the trailing <c>;;</c>.</param>
    /// <param name="omitKeyValueSeparators">If true, no separators between the keys and values will be written.</param>
    /// <param name="omitServerName">If true, the mandatory ServerName parameter value will not be written.</param>
    /// <param name="omitInstanceName">If true, the mandatory InstanceName parameter value will not be written.</param>
    /// <param name="omitIsClustered">If true, the mandatory IsClustered parameter value will not be written.</param>
    /// <param name="omitVersion">If true, the mandatory Version parameter value will not be written.</param>
    /// <param name="shuffleKeys">If true, the key/value pairs will be written in a non-sequential order.</param>
    /// <returns>A byte representation of a RESP_DATA section.</returns>
    /// <seealso href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/2e1560c9-5097-4023-9f5e-72b9ff1ec3b1"/>
    private static byte[] CreateRESP_DATA(string serverName, string instanceName, bool isClustered, string version,
        string? protocolParameters = null,
        bool lowercaseKey = false, bool omitTrailingSemicolons = false, bool omitKeyValueSeparators = false,
        bool omitServerName = false, bool omitInstanceName = false, bool omitIsClustered = false, bool omitVersion = false,
        bool shuffleKeys = false)
    {
        string serverNameKey = GenerateKeyValuePair("ServerName", serverName, omitServerName);
        string instanceNameKey = GenerateKeyValuePair("InstanceName", instanceName, omitInstanceName);
        string isClusteredKey = GenerateKeyValuePair("IsClustered", isClustered ? "Yes" : "No", omitIsClustered);
        string versionKey = GenerateKeyValuePair("Version", version, omitVersion);
        string[] components =
            shuffleKeys
            ? [protocolParameters ?? string.Empty, versionKey, isClusteredKey, instanceNameKey, serverNameKey]
            : [serverNameKey, instanceNameKey, isClusteredKey, versionKey, protocolParameters ?? string.Empty];
        string outputString = string.Join(";", components)
            + (omitTrailingSemicolons ? string.Empty : ";;");

        return Encoding.UTF8.GetBytes(outputString);

        string GenerateKeyValuePair(string key, string value, bool omitKey)
        {
            if (omitKey)
            {
                return string.Empty;
            }

            if (lowercaseKey)
            {
                key = key.ToLower();
            }

            return key + (omitKeyValueSeparators ? string.Empty : ";") + value;
        }
    }

    /// <summary>
    /// Creates the protocol parameters for a RESP_DATA section.
    /// </summary>
    /// <param name="tcpInfo">If non-null, the TCP_INFO data.</param>
    /// <param name="npInfo">If non-null, the NP_INFO data.</param>
    /// <param name="viaInfo">If non-null, the VIA_INFO data.</param>
    /// <param name="rpcInfo">If non-null, the RPC_INFO data</param>
    /// <param name="spxInfo">If non-null, the SPX_INFO data.</param>
    /// <param name="adspInfo">If non-null, the ADSP_INFO data.</param>
    /// <param name="bvInfo">If non-null, the BV_INFO data.</param>
    /// <param name="otherParameters">Any additional protocol parameters to include.</param>
    /// <returns>The collated protocol parameters for a RESP_DATA section.</returns>
    private static string CreateProtocolParameters(string? tcpInfo, string? npInfo,
        string? viaInfo, string? rpcInfo, string? spxInfo, string? adspInfo, string? bvInfo,
        string? otherParameters)
    {
        List<string> protocolParameters = [];
        ReadOnlySpan<string?> allParams = [tcpInfo, npInfo, viaInfo, rpcInfo, spxInfo, adspInfo, bvInfo, otherParameters];

        foreach (string? param in allParams)
        {
            if (param is not null)
            {
                protocolParameters.Add(param);
            }
        }

        return string.Join(";", protocolParameters);
    }
}
