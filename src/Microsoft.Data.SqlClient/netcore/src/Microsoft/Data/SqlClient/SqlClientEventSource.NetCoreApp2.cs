// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// not implemented cause of low performance for .Net core 2.1 and .Net standard 2.0
    /// </summary>
    internal partial class SqlClientEventSource : SqlClientEventSourceBase
    {
        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void HardConnectRequest()
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void HardDisconnectRequest()
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void SoftConnectRequest()
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void SoftDisconnectRequest()
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void NonPooledConnectionRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void PooledConnectionRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void ActiveConnectionPoolGroupRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void InactiveConnectionPoolGroupRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void ActiveConnectionPoolRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void InactiveConnectionPoolRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void ActiveConnectionRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void FreeConnectionRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void StasisConnectionRequest(bool increment = true)
        {
            //no-op
        }

        /// <summary>
        /// not implemented for .Net core 2.1, .Net standard 2.0 and lower
        /// </summary>
        internal void ReclaimedConnectionRequest()
        {
            //no-op
        }
    }
}
