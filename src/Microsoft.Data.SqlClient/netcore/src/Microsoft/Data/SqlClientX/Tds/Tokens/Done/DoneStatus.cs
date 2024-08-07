// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.Done
{
    /// <summary>
    /// Done status for <see cref="Done"/>, <see cref="DoneProc"/>, <see cref="DoneInProc"/>.
    /// </summary>
    [Flags]
    internal enum DoneStatus : ushort
    {
        /// <summary>
        /// Final.
        /// </summary>
        Final = 0x0000,

        /// <summary>
        /// More.
        /// </summary>
        More = 0x0001,

        /// <summary>
        /// Error.
        /// </summary>
        Error = 0x0002,

        /// <summary>
        /// In Transaction
        /// </summary>
        InXAct = 0x0004,

        /// <summary>
        /// Count.
        /// </summary>
        Count = 0x0010,

        /// <summary>
        /// Attention.
        /// </summary>
        Attn = 0x0020,

        /// <summary>
        /// Server Error.
        /// </summary>
        ServerError = 0x0100
    }
}
