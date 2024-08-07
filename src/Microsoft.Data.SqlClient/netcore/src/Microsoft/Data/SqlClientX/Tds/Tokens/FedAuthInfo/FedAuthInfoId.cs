// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.FedAuthInfo
{
    /// <summary>
    /// Federate authentication information identifier.
    /// </summary>
    internal enum FedAuthInfoId : byte
    {
        /// <summary>
        /// Identifier for SPN.
        /// </summary>
        SPN = 0x02,

        /// <summary>
        /// Identifier for STSUrl.
        /// </summary>
        STSUrl = 0x01
    }
}
