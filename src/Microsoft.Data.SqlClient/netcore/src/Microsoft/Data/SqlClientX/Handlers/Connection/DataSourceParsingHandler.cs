// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Handler to populate data source information required for transport handler
    /// </summary>
    internal class DataSourceParsingHandler : ContextHandler<ConnectionHandlerContext>
    {
        /// <inheritdoc />
        public override async ValueTask Handle(ConnectionHandlerContext request, bool isAsync, CancellationToken ct)
        {
            ServerInfo serverInfo = request.ServerInfo;
            string fullServerName = serverInfo.ExtendedServerName;
            string localDBDataSource = GetLocalDBDataSource(fullServerName, out bool errorWithLocalDBProcessing);

            if (errorWithLocalDBProcessing)
            {
                //TODO: populate other details for the SQLException
                SqlErrorCollection collection = new SqlErrorCollection();
                collection.Add(new SqlError(0,0,TdsEnums.FATAL_ERROR_CLASS, null, StringsHelper.GetString("LocalDB_UnobtainableMessage"), null, 0));
                request.Error = SqlException.CreateException(collection, null);
                return;
            }
            
            // If a localDB Data source is available, we need to use it.
            fullServerName = localDBDataSource ?? fullServerName;
            DataSource details = DataSource.ParseServerName(fullServerName);
            if (details == null)
            {
                //TODO: populate other details for the SQLException
                SqlErrorCollection collection = new SqlErrorCollection();
                collection.Add(new SqlError(0, 0, TdsEnums.FATAL_ERROR_CLASS, null, StringsHelper.GetString("LocalDB_UnobtainableMessage"), null, 0));
                request.Error = SqlException.CreateException(collection, null);
                return;
            }
            
            request.DataSource = details;
            
            if (NextHandler is not null)
            {
                await NextHandler.Handle(request, isAsync, ct).ConfigureAwait(false);
            }
        }

        //TODO: Refactor function for better handling of error flag and return params
        private static string GetLocalDBDataSource(string fullServerName, out bool error)
        {
            string localDBConnectionString = null;
            string localDBInstance = DataSource.GetLocalDBInstance(fullServerName, out bool isBadLocalDBDataSource);

            if (isBadLocalDBDataSource)
            {
                error = true;
                return null;
            }

            if (!string.IsNullOrEmpty(localDBInstance))
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
