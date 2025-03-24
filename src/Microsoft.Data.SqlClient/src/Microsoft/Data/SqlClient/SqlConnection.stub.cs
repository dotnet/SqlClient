// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: This is only a stub class for removing clearing errors while merging other files.

using System;

namespace Microsoft.Data.SqlClient
{
    public class SqlConnection
    {
        internal Guid ClientConnectionId { get; set; }

        #if NETFRAMEWORK
        internal static System.Security.CodeAccessPermission ExecutePermission { get; set; }
        #endif

        internal SqlStatistics Statistics { get; set; }

        internal void Abort(Exception e) { }
    }
}
