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
        Auto = 0,
        AlwaysBlock = 1,
        NeverBlock = 2,
    }
}