// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.EnvChange
{
    /// <summary>
    /// Database mirroring partner environment change token.
    /// </summary>
    internal sealed class DatabaseMirroringPartnerEnvChangeToken : EnvChangeToken<string>
    {

        /// <summary>
        /// EnvChange token sub type.
        /// </summary>
        public override EnvChangeTokenSubType SubType => EnvChangeTokenSubType.DatabaseMirroringPartner;

        /// <summary>
        /// Create a new instance of this token.
        /// </summary>
        /// <param name="oldValue">Old value./</param>
        /// <param name="newValue">New value.</param>
        public DatabaseMirroringPartnerEnvChangeToken(string oldValue, string newValue) : base(oldValue, newValue)
        {
        }

    }
}
