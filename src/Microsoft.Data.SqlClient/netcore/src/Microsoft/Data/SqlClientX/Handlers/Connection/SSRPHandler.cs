using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Handler to resolve SSRP related ports,instance name etc.
    /// </summary>
    internal class SSRPHandler : ContextHandler<ConnectionHandlerContext>
    {
        private const int DefaultPort = 1434;
        public override async ValueTask Handle(ConnectionHandlerContext request, bool isAsync, CancellationToken ct) 
        {
            bool isAdmin = request.DataSource.ResolvedProtocol == DataSource.Protocol.Admin;
            bool instanceNameProvided = !string.IsNullOrWhiteSpace(request.DataSource.InstanceName);
            bool isPortProvided = request.DataSource.Port == -1;

            if (isAdmin)
            {
                if (instanceNameProvided)
                {
                    //TODO: resolve DAC port via SSRP
                    throw new NotImplementedException();
                }
                else if (isPortProvided)
                {
                    request.DataSource.ResolvedPort = request.DataSource.Port;
                }
                else
                {
                    request.DataSource.ResolvedPort = DefaultPort;
                }
            }

            if (NextHandler is not null)
            {
                await NextHandler.Handle(request, isAsync, ct).ConfigureAwait(false);
            }
        }
    }
}
