// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Interop.Common.Sni
{
    // @TODO: These errors should only be included when native SNI can be used (ie, Windows only),
    //     but because they are deep within the TDS parser and SqlUtil, they cannot be safely
    //     pulled out to a windows-only file at this time.  
    internal static class SniErrors
    {
        // Generic errors
        internal const int ConnTerminated = 2;
        internal const int InvalidParameter = 5;
        internal const int ProtocolNotSupported = 8;
        internal const int ConnTimeout = 11;
        internal const int ConnNotUsable = 19;
        internal const int InvalidConnString = 25;
        internal const int HandshakeFailure = 31;
        internal const int InternalException = 35;
        internal const int ConnOpenFailed = 40;
        internal const int SpnLookup = 44;
        
        // Multi-subnet-failover specific error codes
        internal const uint MultiSubnetFailoverWithMoreThan64IPs = 47;
        internal const uint MultiSubnetFailoverWithInstanceSpecified = 48;
        internal const uint MultiSubnetFailoverWithNonTcpProtocol = 49;
        
        // Local DB error codes
        internal const uint LocalDBErrorCode = 50;
        internal const int LocalDBNoInstanceName = 51;
        internal const int LocalDBNoInstallation = 52;
        internal const int LocalDBInvalidConfig = 53;
        internal const int LocalDBNoSqlUserInstanceDllPath = 54;
        internal const int LocalDBInvalidSqlUserInstanceDllPath = 55;
        internal const int LocalDBFailedToLoadDll = 56;
        internal const int LocalDBBadRuntime = 57;

        // Max error code value
        internal const uint MaxErrorValue = 50157;
    }
}
