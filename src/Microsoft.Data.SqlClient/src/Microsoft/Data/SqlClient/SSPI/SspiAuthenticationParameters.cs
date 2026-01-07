// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal sealed class SspiAuthenticationParameters
    {
        public SspiAuthenticationParameters(string serverName, string resource)
        {
            ServerName = serverName;
            Resource = resource;
        }

        public string Resource { get; }

        public string ServerName { get; }

        public string? UserId { get; set; }

        public string? DatabaseName { get; set; }

        public string? Password { get; set; }
    }
}
