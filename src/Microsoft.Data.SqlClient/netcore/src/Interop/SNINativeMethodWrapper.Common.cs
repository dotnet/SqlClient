// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClient
{
    internal static partial class SNINativeMethodWrapper
    {
        internal enum SniSpecialErrors : uint
        {
            LocalDBErrorCode = SNICommon.LocalDBErrorCode,

            // multi-subnet-failover specific error codes
            MultiSubnetFailoverWithMoreThan64IPs = SNICommon.MultiSubnetFailoverWithMoreThan64IPs,
            MultiSubnetFailoverWithInstanceSpecified = SNICommon.MultiSubnetFailoverWithInstanceSpecified,
            MultiSubnetFailoverWithNonTcpProtocol = SNICommon.MultiSubnetFailoverWithNonTcpProtocol,

            // max error code value
            MaxErrorValue = SNICommon.MaxErrorValue
        }
    }
}
