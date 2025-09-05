// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.SqlServer.TDS.Servers
{
    public class TdsServer : GenericTdsServer<TdsServerArguments>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public TdsServer() : this(new TdsServerArguments())
        {
        }

        /// <summary>
        /// Constructor with arguments
        /// </summary>
        public TdsServer(TdsServerArguments arguments) : base(arguments)
        {
        }

        /// <summary>
        /// Constructor with arguments and query engine
        /// </summary>
        /// <param name="queryEngine">Query engine</param>
        /// <param name="arguments">Server arguments</param>
        public TdsServer(QueryEngine queryEngine, TdsServerArguments arguments) : base(arguments, queryEngine)
        {
        }
    }
}
