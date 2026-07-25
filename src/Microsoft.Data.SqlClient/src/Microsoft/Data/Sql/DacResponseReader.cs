// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;

#nullable enable

namespace Microsoft.Data.Sql;

/// <summary>
/// Utilities used to extract zero or more parsed SSRP DAC responses from a set of packet buffers.
/// </summary>
internal static class DacResponseReader
{
    /// <summary>
    /// Returns the first SSRP response from the provided source sequence, if one exists.
    /// </summary>
    /// <param name="sourceSequence">The source sequence of buffers from the network.</param>
    /// <param name="firstResponse">The first SSRP response (if available.)</param>
    /// <returns><c>true</c> if an SSRP response can be located in the source sequence, <c>false</c> otherwise.</returns>
    public static bool TryReadFirst(ReadOnlySequence<byte> sourceSequence, out DacResponse firstResponse)
    {
        ReadOnlySequence<byte> remainingSequence = sourceSequence;

        while (!remainingSequence.IsEmpty)
        {
            bool parsedCurrentRequest = DacResponse.TryParse(remainingSequence, out DacResponse current, out long bytesRead);

            if (parsedCurrentRequest)
            {
                firstResponse = current;
                return true;
            }

            remainingSequence = remainingSequence.Slice(bytesRead);
        }

        firstResponse = default;
        return false;
    }
}
