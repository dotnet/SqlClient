// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.Handlers;

namespace Microsoft.Data.SqlClient.Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// Handler to populate data source information required for transport handler
    /// </summary>
    internal class DataSourceParser : IHandler<ConnectionHandlerContext>
    {
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        public async ValueTask Handle(ConnectionHandlerContext request, bool isAsync, CancellationToken ct)
        {
            SqlConnectionString result = new SqlConnectionString(request.connectionString);
            ServerInfo serverInfo = new ServerInfo(result);
            string fullServerName = serverInfo.ExtendedServerName;
            string localDBDataSource = GetLocalDBDataSource(fullServerName, out bool errorWithLocalDBProcessing);

            if (errorWithLocalDBProcessing)
            {
                request.error = new Exception(localDBDataSource);
                return;
            }
            
            // If a localDB Data source is available, we need to use it.
            fullServerName = localDBDataSource ?? fullServerName;
            DataSource details = DataSource.ParseServerName(fullServerName);
            if (details == null)
            {
                request.error = new Exception(localDBDataSource);
                return;
            }
            
            request.dataSource = details;
            
            if (NextHandler is not null)
            {
                await NextHandler.Handle(request, isAsync, ct).ConfigureAwait(false);
            }
        }

        private static string GetLocalDBDataSource(string fullServerName, out bool error)
        {
            string localDBConnectionString = null;
            string localDBInstance = DataSource.GetLocalDBInstance(fullServerName, out bool isBadLocalDBDataSource);

            if (isBadLocalDBDataSource)
            {
                error = true;
                return null;
            }

            else if (!string.IsNullOrEmpty(localDBInstance))
            {
                // We have successfully received a localDBInstance which is valid.
                localDBConnectionString = LocalDB.GetLocalDBConnectionString(localDBInstance);

                if (fullServerName == null || string.IsNullOrEmpty(localDBConnectionString))
                {
                    // The Last error is set in LocalDB.GetLocalDBConnectionString. We don't need to set Last here.
                    error = true;
                    return null;
                }
            }
            
            error = false;
            return localDBConnectionString;
        }
    }
}
