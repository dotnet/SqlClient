// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#if NETFRAMEWORK

namespace Microsoft.Data.SqlClient
{
    // DO NOT USE THIS FILE IN ANY PROJECT!
    // This is a temporary stub to enable migrating DbConnectionInternal to the common project.
    internal abstract class SqlInternalConnectionSmi : SqlInternalConnection
    {
        protected SqlInternalConnectionSmi(SqlConnectionString connectionOptions) : base(connectionOptions)
        {
            throw new NotImplementedException();
        }
    }
}

#endif
