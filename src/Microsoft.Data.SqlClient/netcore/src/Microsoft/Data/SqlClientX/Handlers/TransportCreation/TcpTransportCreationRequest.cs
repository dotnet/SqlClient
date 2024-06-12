// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers
{
    internal class TcpTransportCreationParams : TransportCreationParams
    {
        public string Hostname { get; set; }

        public int Port { get; set; }

        public SqlConnectionIPAddressPreference IpAddressPreference;
    }
}
