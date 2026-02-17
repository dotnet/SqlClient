// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiAuthenticationParameters.xml' path='docs/members[@name="SspiAuthenticationParameters"]/SspiAuthenticationParameters/*'/>
    public sealed class SspiAuthenticationParameters
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiAuthenticationParameters.xml' path='docs/members[@name="SspiAuthenticationParameters"]/SspiAuthenticationParameters/ctor'/>
        public SspiAuthenticationParameters(
            string serverName, 
            string resource,
            string? userId = null,
            string? databaseName = null,
            string? password = null)
        {
            ServerName = serverName;
            Resource = resource;
            UserId = userId;
            DatabaseName = databaseName;
            Password = password;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiAuthenticationParameters.xml' path='docs/members[@name="SspiAuthenticationParameters"]/SspiAuthenticationParameters/Resource'/>
        public string Resource { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiAuthenticationParameters.xml' path='docs/members[@name="SspiAuthenticationParameters"]/SspiAuthenticationParameters/ServerName'/>
        public string ServerName { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiAuthenticationParameters.xml' path='docs/members[@name="SspiAuthenticationParameters"]/SspiAuthenticationParameters/UserId'/>
        public string? UserId { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiAuthenticationParameters.xml' path='docs/members[@name="SspiAuthenticationParameters"]/SspiAuthenticationParameters/DatabaseName'/>
        public string? DatabaseName { get; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiAuthenticationParameters.xml' path='docs/members[@name="SspiAuthenticationParameters"]/SspiAuthenticationParameters/Password'/>
        public string? Password { get; }
    }
}
