// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.TestUtilities
{
    public static class Utils
    {
        private static readonly string[] s_azureSqlServerEndpoints = {".database.windows.net",
                                                                     ".database.cloudapi.de",
                                                                     ".database.usgovcloudapi.net",
                                                                     ".database.chinacloudapi.cn"};

        // This method assumes dataSource parameter is in TCP connection string format.
        public static bool IsAzureSqlServer(string dataSource)
        {
            int i = dataSource.LastIndexOf(',');
            if (i >= 0)
            {
                dataSource = dataSource.Substring(0, i);
            }

            i = dataSource.LastIndexOf('\\');
            if (i >= 0)
            {
                dataSource = dataSource.Substring(0, i);
            }

            // trim redundant whitespace
            dataSource = dataSource.Trim();

            // check if server name end with any azure endpoints
            for (i = 0; i < s_azureSqlServerEndpoints.Length; i++)
            {
                if (dataSource.EndsWith(s_azureSqlServerEndpoints[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
