// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.FedAuthInfo
{
    /// <summary>
    /// Federate authentication information token.
    /// </summary>
    internal sealed class FedAuthInfoToken : Token
    {
        /// <summary>
        /// Token type.
        /// </summary>
        public override TokenType Type => TokenType.FedAuthInfo;

        /// <summary>
        /// Service principal name. 
        /// Can be null.
        /// </summary>
        public string SPN { get; }

        /// <summary>
        /// Token endpoint url. 
        /// Can be null.
        /// </summary>
        public string STSUrl { get; }

        /// <summary>
        /// Creates a new instance of the token.
        /// </summary>
        /// <param name="spn">Service principal name.</param>
        /// <param name="sTSUrl">Token endpoint url.</param>
        public FedAuthInfoToken(string spn, string sTSUrl)
        {
            SPN = spn;
            STSUrl = sTSUrl;
        }

        /// <summary>
        /// Gets a human readable string representation of this token.
        /// </summary>
        /// <returns>Human readable string representation.</returns>
        public override string ToString()
        {
            return $"FedAuthInfo[SPN={SPN}, STSUrl={STSUrl}]";
        }

    }
}
