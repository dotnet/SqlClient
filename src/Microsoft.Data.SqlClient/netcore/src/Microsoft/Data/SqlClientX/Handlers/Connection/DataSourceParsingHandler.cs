// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using DataSourceX = Microsoft.Data.SqlClientX.Handlers.Connection.DataSource;

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
            string localDbDataSource = GetLocalDbDataSource(fullServerName, out bool errorWithLocalDbProcessing);

            if (errorWithLocalDbProcessing)
            {
                //TODO: populate other details for the SQLException
                SqlErrorCollection collection = new SqlErrorCollection
                {
                    new SqlError(
                        0,
                        0,
                        TdsEnums.FATAL_ERROR_CLASS,
                        null,
                        StringsHelper.GetString("LocalDB_UnobtainableMessage"),
                        null,
                        0)
                };
                
                request.Error = SqlException.CreateException(collection, null);
                return;
            }
            
            // If a localDB Data source is available, we need to use it.
            fullServerName = localDbDataSource ?? fullServerName;
            DataSourceX details = DataSourceX.ParseServerName(fullServerName);
            if (details == null)
            {
                //TODO: populate other details for the SQLException
                SqlErrorCollection collection = new SqlErrorCollection
                {
                    new SqlError(
                        0, 
                        0,
                        TdsEnums.FATAL_ERROR_CLASS,
                        null,
                        StringsHelper.GetString("LocalDB_UnobtainableMessage"),
                        null,
                        0)
                };
                
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
        private static string GetLocalDbDataSource(string fullServerName, out bool error)
        {
            string localDbConnectionString = null;
            string localDbInstance = DataSourceX.GetLocalDbInstance(fullServerName, out bool isBadLocalDbDataSource);

            if (isBadLocalDbDataSource)
            {
                error = true;
                return null;
            }

            if (!string.IsNullOrEmpty(localDbInstance))
            {
                // We have successfully received a localDBInstance which is valid.
                localDbConnectionString = LocalDB.GetLocalDBConnectionString(localDbInstance);

                if (fullServerName == null || string.IsNullOrEmpty(localDbConnectionString))
                {
                    // The Last error is set in LocalDB.GetLocalDBConnectionString. We don't need to set Last here.
                    error = true;
                    return null;
                }
            }
            
            error = false;
            return localDbConnectionString;
        }
    }
}
