// Copyright (c) Microsoft Corporation. All rights reserved.

using System.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    internal class ConnectionRequest : HandlerRequest
    {
        public Stream ConnectionStream { get; set; }

        public TransportCreationParams TransportCreationParams { get; set; }
    }
}
