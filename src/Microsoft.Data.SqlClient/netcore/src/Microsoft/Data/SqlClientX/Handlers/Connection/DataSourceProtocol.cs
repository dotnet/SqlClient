// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Represents the different protocols that can be specified in a SQL connection string data
    /// source.
    /// </summary>
    public enum DataSourceProtocol
    {
        /// <summary>
        /// Indicates the protocol was not specified in the data source and could not be determined
        /// based on other parameters in the data source.
        /// </summary>
        NotSpecified = 0,
        
        /// <summary>
        /// Indicates the protocol to connect with is the admin/DAC protocol.
        /// </summary>
        Admin,
        
        /// <summary>
        /// Indicates the protocol to connect with is named pipes.
        /// </summary>
        NamedPipe,
        
        /// <summary>
        /// Indicates the protocol to connect with is shared memory.
        /// </summary>
        SharedMemory,
        
        /// <summary>
        /// Indicates the protocol to connect with is TCP.
        /// </summary>
        Tcp,
    }
}
