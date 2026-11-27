// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Data.Sql;

/// <summary>
/// Utilities used to extract zero or more unparsed SSRP responses from a single source buffer.
/// </summary>
internal static class SqlDataSourceResponseReader
{
    /// <summary>
    /// Returns the first SSRP response from the provided source sequence, if one exists.
    /// </summary>
    /// <param name="sourceSequence">The source sequence of buffers from the network.</param>
    /// <param name="firstResponse">The first SSRP response (if available.)</param>
    /// <returns><c>true</c> if an SSRP response can be located in the source sequence, <c>false</c> otherwise.</returns>
    /// <remarks>
    /// This method parses SVR_RESP messages sent as the result of issuing a CLNT_UCAST_INST message.
    /// </remarks>
    public static bool TryReadFirst(ReadOnlySequence<byte> sourceSequence, out SqlDataSourceResponse firstResponse)
    {
        const ushort MaxDynamicDataSize = 1024;

        ReadOnlySequence<byte> remainingSequence = sourceSequence;

        while (!remainingSequence.IsEmpty)
        {
            bool parsedCurrentRequest = SqlDataSourceResponse.TryParse(remainingSequence, MaxDynamicDataSize, out SqlDataSourceResponse current, out long bytesRead);

            if (parsedCurrentRequest)
            {
                firstResponse = current;
                return true;
            }

            // If the current request cannot be parsed, advance by bytesRead.
            // bytesRead will always be greater than zero - it'll be the next possibly-viable
            // position of an SSRP response, or the end of the sequence if no viable SSRP responses
            // can be found.
            Debug.Assert(bytesRead > 0 && bytesRead <= remainingSequence.Length);
            remainingSequence = remainingSequence.Slice(bytesRead);
        }

        firstResponse = default;
        return false;
    }

    /// <summary>
    /// Returns the final SSRP response from the provided source sequence, if one exists.
    /// </summary>
    /// <param name="sourceSequence">The source sequence of buffers from the network.</param>
    /// <param name="lastResponse">The final SSRP response (if available.)</param>
    /// <returns><c>true</c> if at least one SSRP response is present in the source sequence, <c>false</c> otherwise.</returns>
    /// <remarks>
    /// This method parses SVR_RESP messages sent as the result of issuing a CLNT_BCAST_EX or a CLNT_UCAST_EX
    /// message.
    /// </remarks>
    public static bool TryReadLast(ReadOnlySequence<byte> sourceSequence, out SqlDataSourceResponse lastResponse)
    {
        const ushort MaxDynamicDataSize = 65535;

        ReadOnlySequence<byte> remainingSequence = sourceSequence;
        SqlDataSourceResponse lastGoodResponse = default;
        bool responseExists = false;
        bool parsedCurrentRequest;

        while (!remainingSequence.IsEmpty)
        {
            parsedCurrentRequest = SqlDataSourceResponse.TryParse(remainingSequence, MaxDynamicDataSize, out SqlDataSourceResponse current, out long bytesRead);

            // If the current request cannot be parsed, advance by bytesRead.
            // bytesRead will always be greater than zero - it'll be the next possibly-viable
            // position of an SSRP response, or the end of the sequence if no viable SSRP responses
            // can be found.
            Debug.Assert(bytesRead > 0 && bytesRead <= remainingSequence.Length);
            remainingSequence = remainingSequence.Slice(bytesRead);
            if (parsedCurrentRequest)
            {
                responseExists = true;
                lastGoodResponse = current;
            }
        }

        lastResponse = responseExists ? lastGoodResponse : default;
        return responseExists;
    }
}
