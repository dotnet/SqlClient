// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/SqlAuthenticationToken/*'/>
    public sealed class SqlAuthenticationToken : SqlAuthenticationTokenBase
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlAuthenticationToken.xml' path='docs/members[@name="SqlAuthenticationToken"]/ctor/*'/>
        public SqlAuthenticationToken(string accessToken, DateTimeOffset expiresOn)
        : base(accessToken, expiresOn)
        {
            if (string.IsNullOrEmpty(AccessToken))
                throw SQL.ParameterCannotBeEmpty("AccessToken");
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        internal SqlAuthenticationToken(byte[] accessToken, DateTimeOffset expiresOn)
            : this(AccessTokenStringFromBytes(accessToken), expiresOn) { }

        /// <summary>
        /// Convert token bytes to string.
        /// </summary>
        internal static string AccessTokenStringFromBytes(byte[] bytes)
        {
            return Encoding.Unicode.GetString(bytes);
        }
    }
}
