// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Tds.State
{
    /// <summary>
    /// TDS Connection State information
    /// </summary>
    internal class TdsConnectionState
    {
        public string Server {  get; set; }

        public string ServerVersion { get; set; }

        public Guid ClientConnectionId { get; set; }

        public Guid OriginalClientConnectionId { get; set; }

        public string RoutingDestination { get; set; }

        public RoutingInfo RoutingInfo { get; set; }
    }
}
