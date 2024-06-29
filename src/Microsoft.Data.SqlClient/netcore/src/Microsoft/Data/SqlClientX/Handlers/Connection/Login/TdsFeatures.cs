// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.Login
{
    internal class TdsFeatures
    {
        public bool SessionRecoveryRequested { get; internal set; }
        public bool FederatedAuthenticationInfoRequested { get; internal set; }
        public FederatedAuthenticationFeatureExtensionData FedAuthFeatureExtensionData { get; internal set; }
        public bool FederatedAuthenticationRequested { get; internal set; }
        public TdsEnums.FeatureExtension RequestedFeatures { get; internal set; }
    }
}
