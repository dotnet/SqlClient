// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens
{
    /// <summary>
    /// TDS Protocol versions.
    /// </summary>
    internal enum TdsVersion : uint
    {
        /// <summary>
        /// Version 7.1
        /// </summary>
        V7_1 = 0x71000001,

        /// <summary>
        /// Version 7.2
        /// </summary>
        V7_2 = 0x72090002,

        /// <summary>
        /// Version 7.3.A
        /// </summary>
        V7_3_A = 0x730A0003,

        /// <summary>
        /// Version 7.3.B
        /// </summary>
        V7_3_B = 0x730B0003,

        /// <summary>
        /// Version 7.4
        /// </summary>
        V7_4 = 0x74000004
    }
}
