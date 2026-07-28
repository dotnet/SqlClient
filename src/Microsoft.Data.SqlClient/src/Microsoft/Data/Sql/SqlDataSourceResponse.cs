// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

#nullable enable

namespace Microsoft.Data.Sql;

/// <summary>
/// A single parsed SSRP response.
/// </summary>
/// <seealso href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/mc-sqlr/2e1560c9-5097-4023-9f5e-72b9ff1ec3b1"/>
/// <remarks>
/// <para>This corresponds to an SVR_RESP structure within the MC-SQLR specification.</para>
/// <para>An SVR_RESP (DAC) structure is a byte array with the following layout:</para>
/// <list type="number">
/// <item>SVR_RESP: 1 byte, always 0x05.</item>
/// <item>RESP_SIZE: 2 bytes, length of the RESP_DATA data. Written in little-endian byte order.</item>
/// <item>RESP_DATA: variable length, up to 1024 bytes if responding to CLNT_UCAST_INST or 65535 bytes if responding to CLNT_UCAST_EX.</item>
/// </list>
/// </remarks>
internal readonly ref struct SqlDataSourceResponse
{
    // Constants for offsets in the fixed-length fields in the SSRP header.
    private const int ResponseHeaderOffset = 0;
    private const int ResponseSizeOffset = ResponseHeaderOffset + sizeof(byte);
    private const int RespDataOffset = ResponseSizeOffset + sizeof(ushort);
    private const int ConstantHeaderSize = RespDataOffset;

    private const byte ResponseHeaderValue = 0x05;

    private static readonly Encoding s_mbcsEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Transport struct used to process a specific component of the RESP_DATA response.
    /// Users of this struct must use the TryGetX methods, or check that <see cref="Valid"/>
    /// is <c>true</c> before interpreting the <see cref="Value"/> property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <em>component</em> is a combination of a <em>key</em> and a <em>value</em>. A
    /// <em>value</em> may consist of multiple <em>tokens</em>. The key and the value are separated
    /// by a <c>;</c> character. Each token in the value is also separated by a <c>;</c> character.
    /// </para>
    /// <para>
    /// <see cref="Valid"/> relates strictly to <em>structural</em> validity. It indicates solely
    /// that this component is the correctly-parsed product of a RESP_DATA string. It does not
    /// indicate that the value is valid for the key (or safe for user consumption.)
    /// </para>
    /// </remarks>
    private readonly ref struct RespDataComponent
    {
        // Maximum lengths of the values of the RESP_DATA components of the response.
        // Measured in bytes.
        public const long MaxServerNameLength = 255;
        public const long MaxInstanceNameLength = 255;
        public const long MaxIsClusteredLength = 255;
        public const long MaxVersionLength = 16;

        public ReadOnlySpan<char> Key { get; }

        public ReadOnlySpan<char> Value { get; }

        public bool Valid { get; }

        private RespDataComponent(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
        {
            Key = key;
            Value = value;
            Valid = true;
        }

        /// <summary>
        /// Parses a RESP_DATA component containing a key and one value, starting from position
        /// <paramref name="startPos"/> and incrementing this position following the parsing of the
        /// component.
        /// </summary>
        /// <param name="respData">The entire RESP_DATA string.</param>
        /// <param name="startPos">Position to start parsing within this string (in characters.)</param>
        /// <param name="maxValueLength">Maximum length of the component's value, or <c>-1</c> if no limit exists.</param>
        /// <param name="token">The parsed component.</param>
        /// <returns><c>true</c> if the RESP_DATA component was parsed, <c>false</c> if not.</returns>
        /// <remarks>
        /// Note that a parsed component is a key/value pair which is structurally correct. It does
        /// not imply that the value is actually valid. To enforce this, access the value via the
        /// TryGetX methods or check that <see cref="Valid"/> is <c>true</c> before accessing the
        /// <see cref="Value"/> property.
        /// </remarks>
        public static bool TryParse(string respData, scoped ref int startPos, long maxValueLength, out RespDataComponent token)
            => TryParse(respData, ref startPos, maxValueLength, expectedTokensInValue: 1, out token);

        /// <summary>
        /// Parses a RESP_DATA component which contains a key and exactly <paramref name="expectedTokensInValue"/>
        /// values, starting from position <paramref name="startPos"/> and incrementing this position
        /// following the parsing of the component.
        /// </summary>
        /// <seealso cref="TryParse(string, ref int, long, out RespDataComponent)"/>
        /// <param name="respData">The entire RESP_DATA string.</param>
        /// <param name="startPos">Position to start parsing within this string (in characters.)</param>
        /// <param name="maxValueLength">Maximum length (in bytes) of the component's value, or <c>-1</c> if no limit exists.</param>
        /// <param name="expectedTokensInValue">The number of <c>;</c>-delimited tokens in the value.</param>
        /// <param name="token">The parsed component.</param>
        /// <returns><c>true</c> if the RESP_DATA component was parsed, <c>false</c> if not.</returns>
        /// <remarks>
        /// MC-SQLR specifies that certain RESP_DATA component values have a maximum length. As a
        /// result, <paramref name="startPos"/> will specify the start position of the component in
        /// characters, but <paramref name="maxValueLength"/> will specify the maximum length of the
        /// component in bytes.
        /// </remarks>
        public static bool TryParse(string respData, scoped ref int startPos, long maxValueLength, int expectedTokensInValue, out RespDataComponent token)
        {
            const char Terminator = ';';

            ReadOnlySpan<char> respDataSpan = respData.AsSpan(startPos);
            // The buffer must start with the key, and the key must be followed by a terminator.
            // Following the terminator comes a value, which must also be followed by a terminator.
            int terminatorPos = respDataSpan.IndexOf(Terminator);

            if (terminatorPos == -1)
            {
                token = default;
                return false;
            }

            ReadOnlySpan<char> keyCandidate = respDataSpan.Slice(0, terminatorPos);
            ReadOnlySpan<char> valueCandidate = respDataSpan.Slice(terminatorPos + 1);

            // There could potentially be a number of terminators within the RESP_DATA token's value. The Banyan VINES parameters contain five.
            // BV_PARAMETERS = "bv;<ITEM NAME>;<GROUP NAME>;<ITEM NAME>;<GROUP NAME>;<ORG NAME>;"
            // Accounting for this here means that we don't run the risk of <GROUP NAME> being interpreted as a key in its own right.
            int valueTerminatorPosition = 0;
            ReadOnlySpan<char> trailingValue = valueCandidate;
            int tokenCount = 0;

            for (int i = 0; i < expectedTokensInValue; i++)
            {
                int nextTerminatorPos = trailingValue.IndexOf(Terminator);

                if (nextTerminatorPos == -1)
                {
                    break;
                }

                tokenCount++;
                valueTerminatorPosition += nextTerminatorPos + 1;
                trailingValue = trailingValue.Slice(nextTerminatorPos + 1);
            }

            if (tokenCount != expectedTokensInValue)
            {
                token = default;
                return false;
            }

            terminatorPos = valueTerminatorPosition - 1;
            valueCandidate = valueCandidate.Slice(0, terminatorPos);

            // If a maximum value length is specified, this is in bytes. Calculate the number of
            // bytes in the string, and compare.
            if (maxValueLength != -1)
            {
                int valueByteCount = s_mbcsEncoding.GetByteCount(valueCandidate);

                if (valueByteCount > maxValueLength)
                {
                    token = default;
                    return false;
                }
            }

            startPos += keyCandidate.Length + 1 + valueCandidate.Length + 1;

            token = new RespDataComponent(keyCandidate, valueCandidate);
            return true;
        }

        public bool TryGetBoolean(out bool value)
        {
            value = false;

            if (Valid)
            {
                if (Value.Equals("Yes".AsSpan(), StringComparison.Ordinal))
                {
                    value = true;
                    return true;
                }
                else if (Value.Equals("No".AsSpan(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetVersion([NotNullWhen(true)] out Version? value)
        {
            value = null;
            #if NET
            return Valid && Version.TryParse(Value, out value);
            #else
            return Valid && Version.TryParse(Value.ToString(), out value);
            #endif
        }

        public bool TryGetUInt16(out ushort value)
        {
            value = 0;

            #if NET
            return Valid && ushort.TryParse(Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
            #else
            return Valid && ushort.TryParse(Value.ToString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
            #endif
        }

        public bool TryGetServerName(out ReadOnlySpan<char> value)
        {
            bool valid = Valid && Value.Length > 0;

            if (valid)
            {
                foreach (char ch in Value)
                {
                    if (ch != '.'
                        && ch != '-'
                        && ch != '_'
                        && (!(ch >= '0' && ch <= '9'))
                        && (!(ch >= 'A' && ch <= 'Z'))
                        && (!(ch >= 'a' && ch <= 'z')))
                    {
                        valid = false;
                        break;
                    }
                }
            }
            value = valid ? Value : [];
            return valid;
        }

        public bool TryGetInstanceName(out ReadOnlySpan<char> value)
        {
            bool valid = Valid && Value.Length > 0;

            if (valid)
            {
                foreach (char ch in Value)
                {
                    if (char.IsControl(ch))
                    {
                        valid = false;
                        break;
                    }
                }
            }
            value = valid ? Value : [];
            return valid;
        }
    }

    /// <summary>
    /// Transport struct used to collate all library-relevant protocol metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Users must verify that <see cref="TcpEnabled"/> is <c>true</c> before reading <see cref="TcpPort"/>
    /// or that <see cref="NamedPipeEnabled"/> is <c>true</c> before reading <see cref="NamedPipe"/>.
    /// If <see cref="Valid"/> is <c>false</c>, no protocol metadata is present and the protocol
    /// details are invalid - this SSRP response is unusable, because we can't connect in any
    /// relevant way.
    /// </para>
    /// <para>
    /// A successfully parsed instance does not guarantee secure data. Callers must ensure that
    /// <see cref="NamedPipe"/> points to the same server identified by the SSRP response's server
    /// name before they connect to it.
    /// </para>
    /// </remarks>
    private readonly ref struct ExposedProtocols
    {
        public bool TcpEnabled { get; }

        public ushort TcpPort { get; }

        public bool NamedPipeEnabled { get; }

        public ReadOnlySpan<char> NamedPipe { get; }

        public bool Valid => TcpEnabled || NamedPipeEnabled;

        public ExposedProtocols(RespDataComponent tcpToken, RespDataComponent npToken)
        {
            ushort tcpPort = 0;

            TcpEnabled = tcpToken.Valid && tcpToken.TryGetUInt16(out tcpPort) && tcpPort != 0;
            if (TcpEnabled)
            {
                TcpPort = tcpPort;
            }

            NamedPipeEnabled = npToken.Valid && !npToken.Value.IsEmpty;
            if (NamedPipeEnabled)
            {
                NamedPipe = npToken.Value;
            }
        }
    }

    public ReadOnlySpan<char> ServerName { get; }

    public ReadOnlySpan<char> InstanceName { get; }

    public bool IsClustered { get; }

    public Version Version { get; }

    public bool TcpEnabled { get; }

    public ushort TcpPort { get; }

    public bool NamedPipeEnabled { get; }

    public ReadOnlySpan<char> NamedPipe { get; }

    private SqlDataSourceResponse(ReadOnlySpan<char> serverName, ReadOnlySpan<char> instanceName,
        bool isClustered, Version version,
        ExposedProtocols protocols)
    {
        ServerName = serverName;
        InstanceName = instanceName;
        IsClustered = isClustered;
        Version = version;

        TcpEnabled = protocols.TcpEnabled;
        TcpPort = protocols.TcpPort;
        NamedPipeEnabled = protocols.NamedPipeEnabled;
        NamedPipe = protocols.NamedPipe;
    }

    /// <summary>
    /// Attempts to parse a single SSRP response from the start of the provided source sequence.
    /// If an SSRP response cannot be found, supplies the maximum number of bytes to advance the
    /// sequence by before attempting to parse another response.
    /// </summary>
    /// <param name="sourceSequence">The source buffer to read from.</param>
    /// <param name="maxDynamicDataSize">The maximum allowed size for the RESP_DATA section of the response.</param>
    /// <param name="response">The populated SSRP response (or default, if one cannot be found.)</param>
    /// <param name="bytesRead">The number of bytes to advance <paramref name="sourceSequence"/> by.</param>
    /// <returns><c>true</c> if the response was processed, <c>false</c> if not.</returns>
    /// <remarks>
    /// If the sequence does not start with an SSRP response, <paramref name="bytesRead"/> will
    /// contain the position of the next possible <c>SVR_RESP</c> header byte (<c>0x05</c>), or
    /// the length of <paramref name="sourceSequence"/> if this header byte is not present in the
    /// sequence.
    /// </remarks>
    public static bool TryParse(ReadOnlySequence<byte> sourceSequence, ushort maxDynamicDataSize, out SqlDataSourceResponse response, out long bytesRead)
    {
        // Make sure we have enough data to read the constant size of the header.
        if (sourceSequence.Length < ConstantHeaderSize)
        {
            bytesRead = sourceSequence.Length;
            response = default;
            return false;
        }

        ReadOnlySequence<byte> currSequence = sourceSequence;
        ReadOnlySpan<byte> currSpan = sourceSequence.First.Span;
        long currOffset = 0;

        // Read and validate the constant part of the response header.
        if (currSequence.ReadByte(ref currSpan, ref currOffset, out byte responseHeader)
            // RESP_SVR must be 0x05.
            && responseHeader == ResponseHeaderValue
            && currSequence.ReadLittleEndian(ref currSpan, ref currOffset, out ushort responseSize)
            // RESP_SIZE must be greater than 0 and fit within the source sequence.
            // It must also not exceed the maximum allowed dynamic data size.
            && responseSize > 0
            && (currOffset + responseSize) <= sourceSequence.Length
            && responseSize <= maxDynamicDataSize
            // RESP_DATA must successfully parse.
            && TryParseRespData(ref currSequence, ref currOffset, responseSize, out response))
        {
            bytesRead = currOffset;
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

    private static bool TryParseRespData(scoped ref ReadOnlySequence<byte> sequence, scoped ref long currOffset, ushort responseSize, out SqlDataSourceResponse response)
    {
        const string ServerNameKey = "ServerName";
        const string InstanceNameKey = "InstanceName";
        const string IsClusteredKey = "IsClustered";
        const string VersionKey = "Version";

        // Make sure we have enough data to read the constant mandatory header components.
        ushort minLength = (ushort)(ServerNameKey.Length + 1 + InstanceNameKey.Length + 1
            + IsClusteredKey.Length + 1 + VersionKey.Length + 1);

        if (responseSize < minLength)
        {
            response = default;
            return false;
        }

        int currRespDataOffset = 0;
        string decodedRespData;

        try
        {
            #if NET
            decodedRespData = s_mbcsEncoding.GetString(sequence.Slice(0, responseSize));
            #else
            decodedRespData = s_mbcsEncoding.GetString(sequence.Slice(0, responseSize).ToArray());
            #endif
        }
        catch (DecoderFallbackException)
        {
            response = default;
            return false;
        }

        // Parse and validate the header components
        if (!RespDataComponent.TryParse(decodedRespData, ref currRespDataOffset, RespDataComponent.MaxServerNameLength, out RespDataComponent serverNameToken)
            // The first token must be ServerName.
            || !serverNameToken.Key.Equals(ServerNameKey.AsSpan(), StringComparison.Ordinal)
            // The ServerName token must be a valid FQDN.
            // Note: This validation is stricter than called for in MC-SQLR. It is designed to
            // ensure that server names only contain valid characters (".", "-", "_", a-z, A-Z, 0-9).
            || !serverNameToken.TryGetServerName(out ReadOnlySpan<char> serverName)
            || !RespDataComponent.TryParse(decodedRespData, ref currRespDataOffset, RespDataComponent.MaxInstanceNameLength, out RespDataComponent instanceNameToken)
            // The second token must be InstanceName.
            || !instanceNameToken.Key.Equals(InstanceNameKey.AsSpan(), StringComparison.Ordinal)
            // The InstanceName token must be valid.
            // Note: This validation is stricter than called for in MC-SQLR, but more lenient
            // than a server name. Its sole goal is to block control characters
            || !instanceNameToken.TryGetInstanceName(out ReadOnlySpan<char> instanceName)
            || !RespDataComponent.TryParse(decodedRespData, ref currRespDataOffset, RespDataComponent.MaxIsClusteredLength, out RespDataComponent isClusteredToken)
            // The third token must be IsClustered.
            || !isClusteredToken.Key.Equals(IsClusteredKey.AsSpan(), StringComparison.Ordinal)
            // The IsClustered token must be Yes or No.
            || !isClusteredToken.TryGetBoolean(out bool isClustered)
            || !RespDataComponent.TryParse(decodedRespData, ref currRespDataOffset, RespDataComponent.MaxVersionLength, out RespDataComponent versionToken)
            // The fourth token must be Version.
            || !versionToken.Key.Equals(VersionKey.AsSpan(), StringComparison.Ordinal)
            // The Version token must be a valid version.
            || !versionToken.TryGetVersion(out Version? version))
        {
            response = default;
            return false;
        }

        // Now, move to the dynamic data. Iterate over each of the following keys, performing basic checks to ensure that each key only appears once and
        // looking for our key protocols: TCP and Named Pipes.
        if (!TryParseRespProtocolData(decodedRespData, ref currRespDataOffset,
            out int tcpTokenOffset, out int npTokenOffset))
        {
            response = default;
            return false;
        }

        // Try to parse the TCP and the named pipes tokens, if they exist.
        RespDataComponent tcpToken = default;
        RespDataComponent npToken = default;

        if (tcpTokenOffset != -1
            && !RespDataComponent.TryParse(decodedRespData, ref tcpTokenOffset, maxValueLength: -1, out tcpToken))
        {
            response = default;
            return false;
        }

        if (npTokenOffset != -1
            && !RespDataComponent.TryParse(decodedRespData, ref npTokenOffset, maxValueLength: -1, out npToken))
        {
            response = default;
            return false;
        }

        // With both relevant tokens tested for existence, ensure that the combination of them is valid.
        // We must ensure that at least one of them is available, but that both of them are valid, if present.
        ExposedProtocols protocols = new(tcpToken, npToken);
        if (!protocols.Valid)
        {
            response = default;
            return false;
        }

        // All RESP_DATA has been successfully parsed. Ensure that nothing remains in the string except for the final trailing ";".
        // We've already validated that the final value ends with a ";", so we're actually verifying that the string as a whole ends with ";;".
        if (currRespDataOffset != decodedRespData.Length - 1
            || decodedRespData[currRespDataOffset] != ';')
        {
            response = default;
            return false;
        }

        currOffset += responseSize;
        sequence = sequence.Slice(responseSize);
        response = new SqlDataSourceResponse(serverName, instanceName, isClustered, version, protocols);

        return true;
    }

    private static bool TryParseRespProtocolData(string respData, ref int currPos, out int tcpTokenOffset, out int npTokenOffset)
    {
        const string NamedPipesInfoKey = "np";
        const string TcpInfoKey = "tcp";
        const string ViaInfoKey = "via";
        const string RpcInfoKey = "rpc";
        const string SpxInfoKey = "spx";
        const string AdspInfoKey = "adsp";
        const string BanyanVinesInfoKey = "bv";

        int tempCurrPos = currPos;

        bool npKeyFound = false;
        bool tcpKeyFound = false;
        bool viaKeyFound = false;
        bool rpcKeyFound = false;
        bool spxKeyFound = false;
        bool adspKeyFound = false;
        bool bvKeyFound = false;

        tcpTokenOffset = -1;
        npTokenOffset = -1;

        while (RespDataComponent.TryParse(respData, ref tempCurrPos, maxValueLength: -1, out RespDataComponent currentToken))
        {
            // If we encounter a BV_INFO key, then we need to set the position back to currPos and
            // re-parse allowing for extra separators. This is because a BV_INFO key has a value in
            // the format "<item name>;<group name>;<item name>;<group name>;<org name>" and failing
            // to reparse would mean that we would see the group name parsed as the next key.
            // Technically, the extra separators would permit text which looks like another protocol
            // key to be smuggled into BV_INFO. This parser ignores such text. The example below will
            // be treated as a BV_INFO key, not as a duplicate TCP_INFO key:
            //     "bv;ITEM1;tcp;ITEM1;tcp;80;"
            // <item name> is ITEM1, <group name> is tcp, <org name> is 80.
            if (currentToken.Key.Equals(BanyanVinesInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                tempCurrPos = currPos;

                if (!RespDataComponent.TryParse(respData, ref tempCurrPos, maxValueLength: -1, expectedTokensInValue: 5, out currentToken))
                {
                    break;
                }
            }

            // Each protocol may only appear once, as per MC-SQLR, section 2.2.5, Note 1.
            bool protocolSpecifiedTwice = !TrySetFlag(currentToken.Key, ref npKeyFound,
                ref tcpKeyFound, ref viaKeyFound, ref rpcKeyFound,
                ref spxKeyFound, ref adspKeyFound, ref bvKeyFound);

            if (protocolSpecifiedTwice)
            {
                return false;
            }

            // Processing TCP_INFO. The TCP_PORT parameter is mandatory.
            // It must also be a number in the 1-65535 range.
            if (currentToken.Key.Equals(TcpInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                tcpTokenOffset = currPos;
            }
            // Processing NP_INFO. The PIPENAME parameter is mandatory.
            else if (currentToken.Key.Equals(NamedPipesInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                npTokenOffset = currPos;
            }
            // Other protocols also appear, and have their own validation logic. This typically
            // involves maximum lengths (in either bytes or characters) or expected formats. This
            // parser explicitly does not verify these - downstream clients can only connect via
            // TCP or named pipes.

            currPos = tempCurrPos;
        }

        return true;

        static bool TrySetFlag(ReadOnlySpan<char> key,
            ref bool npKeyFound, ref bool tcpKeyFound,
            ref bool viaKeyFound, ref bool rpcKeyFound,
            ref bool spxKeyFound, ref bool adspKeyFound,
            ref bool bvKeyFound)
        {
            bool dummyFlag = true;
            ref bool relevantFlag = ref dummyFlag;

            // relevantFlag is a managed reference to the appropriate parameter. This parameter is
            // selected based upon the key - a protocol name. A failure to match a known key will
            // allow the default value (a managed reference to a boolean true) value to stand, forcing
            // this method to return false.
            if (key.Equals(NamedPipesInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                relevantFlag = ref npKeyFound;
            }
            else if (key.Equals(TcpInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                relevantFlag = ref tcpKeyFound;
            }
            else if (key.Equals(ViaInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                relevantFlag = ref viaKeyFound;
            }
            else if (key.Equals(RpcInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                relevantFlag = ref rpcKeyFound;
            }
            else if (key.Equals(SpxInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                relevantFlag = ref spxKeyFound;
            }
            else if (key.Equals(AdspInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                relevantFlag = ref adspKeyFound;
            }
            else if (key.Equals(BanyanVinesInfoKey.AsSpan(), StringComparison.Ordinal))
            {
                relevantFlag = ref bvKeyFound;
            }

            // If key is not as we expect, or if the key's flag is already set, return false.
            // Otherwise, set the flag and return true.
            if (relevantFlag)
            {
                return false;
            }
            else
            {
                relevantFlag = true;
                return true;
            }
        }
    }
}
