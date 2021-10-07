﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.ProviderBase
{
    sealed internal partial class DbConnectionPool
    {
        partial void CheckPoolBlockingPeriod(Exception e)
        {
            if (!IsBlockingPeriodEnabled())
            {
                throw e;
            }
        }

        private bool IsBlockingPeriodEnabled()
        {
            var poolGroupConnectionOptions = _connectionPoolGroup.ConnectionOptions as SqlConnectionString;
            if (poolGroupConnectionOptions == null)
            {
                return true;
            }

            switch (poolGroupConnectionOptions.PoolBlockingPeriod)
            {
                case PoolBlockingPeriod.Auto:
                    return !ADP.IsAzureSqlServerEndpoint(poolGroupConnectionOptions.DataSource);
                case PoolBlockingPeriod.AlwaysBlock:
                    return true;
                case PoolBlockingPeriod.NeverBlock:
                    return false;
                default:
                    Debug.Fail("Unknown PoolBlockingPeriod. Please specify explicit results in above switch case statement.");
                    return true;

            }
        }
    }
}
