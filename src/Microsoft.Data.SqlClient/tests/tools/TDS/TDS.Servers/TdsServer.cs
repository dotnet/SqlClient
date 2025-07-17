// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.SqlServer.TDS.Servers
{
    public class TdsServer : GenericTDSServer<TDSServerArguments>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public TdsServer() : this(new TDSServerArguments())
        {
        }
        /// <summary>
        /// Constructor with arguments
        /// </summary>
        public TdsServer(TDSServerArguments arguments) : base(arguments)
        {
        }

        /// <summary>
        /// Constructor with arguments and query engine
        /// </summary>
        /// <param name="queryEngine">Query engine</param>
        /// <param name="arguments">Server arguments</param>
        public TdsServer(QueryEngine queryEngine, TDSServerArguments arguments) : base(arguments, queryEngine)
        {
        }
    }
}
