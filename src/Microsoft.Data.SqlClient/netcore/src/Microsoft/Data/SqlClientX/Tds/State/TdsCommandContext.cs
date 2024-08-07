// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.State
{
    internal class TdsCommandContext
    {
        // TDS stream processing variables
        /// <summary>
        /// PLP data length indicator
        /// </summary>
        public ulong PlpLength;

        /// <summary>
        /// Length of data left to read (64 bit lengths)
        /// </summary>
        public ulong PlpLengthLeft;
    }
}
