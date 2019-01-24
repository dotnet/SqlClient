//------------------------------------------------------------------------------
// <copyright file="PoolBlockingPeriod.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">kkurni</owner>
//------------------------------------------------------------------------------

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