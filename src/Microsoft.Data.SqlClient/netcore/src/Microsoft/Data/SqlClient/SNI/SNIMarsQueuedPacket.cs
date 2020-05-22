// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// Mars queued packet
    /// </summary>
    internal sealed class SNIMarsQueuedPacket
    {
        private readonly SNIPacket _packet;
        private readonly SNIAsyncCallback _callback;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="packet">SNI packet</param>
        /// <param name="callback">Completion callback</param>
        public SNIMarsQueuedPacket(SNIPacket packet, SNIAsyncCallback callback)
        {
            _packet = packet;
            _callback = callback;
        }

        public SNIPacket Packet => _packet;

        public SNIAsyncCallback Callback => _callback;
    }
}
