// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// represents the Pool Blocking Period behaviour for connections in connection pool
    /// </summary>
    [Serializable]
    public enum PoolBlockingPeriod
    {
        /// <summary>
        /// Specifies a value for the <see cref="Microsoft.Data.SqlClient.SqlConnectionStringBuilder.PoolBlockingPeriod" /> property. 
        /// </summary>
        /// <Value>Auto is set to 0.</Value>
        Auto = 0,

        /// <summary>
        /// Blocking period ON for all SQL servers including Azure SQL servers.
        /// </summary>
        /// <value>AlwaysBlock is set to 1.</value>
        AlwaysBlock = 1,

        /// <summary>
        /// Blocking period OFF for all SQL servers including Azure SQL servers.
        /// </summary>
        /// <value>NeverBlock is set to 2.</value>
        NeverBlock = 2,
    }
}