// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens
{
    /// <summary>
    /// Tds data stream token.
    /// </summary>
    internal abstract class Token
    {
        /// <summary>
        /// Type of the token.
        /// </summary>
        public abstract TokenType Type { get; }
    }
}
