// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.SqlServer.TDS
{
    /// <summary>
    /// TDS version routines
    /// </summary>
    public static class TDSVersion
    {
        /// <summary>
        /// 7.0 (Sphinx) TDS version
        /// </summary>
        public static Version SqlServer7_0 = new Version(7, 0, 0, 0);

        /// <summary>
        /// 2000 (Shiloh) TDS version
        /// </summary>
        public static Version SqlServer2000 = new Version(7, 1, 0, 1);

        /// <summary>
        /// 2005 (Yukon) TDS version
        /// </summary>
        public static Version SqlServer2005 = new Version(7, 2, 9, 2);

        /// <summary>
        /// 2008 (Katmai) TDS version
        /// </summary>
        public static Version SqlServer2008 = new Version(7, 3, 11, 3);

        /// <summary>
        /// 2012 (Denali) TDS version
        /// </summary>
        public static Version SqlServer2012 = new Version(7, 4, 0, 4);

        /// <summary>
        /// Map SQL Server build version to TDS version
        /// </summary>
        /// <param name="buildVersion">Build version to analyze</param>
        /// <returns>TDS version that corresponding build version supports</returns>
        public static Version GetTDSVersion(Version buildVersion)
        {
            // Check build version Major part
            if (buildVersion.Major == 11)
            {
                return SqlServer2012;
            }
            else if (buildVersion.Major == 10)
            {
                return SqlServer2008;
            }
            else if (buildVersion.Major == 9)
            {
                return SqlServer2005;
            }
            else if (buildVersion.Major == 8)
            {
                return SqlServer2000;
            }
            else if (buildVersion.Major == 7)
            {
                return SqlServer7_0;
            }
            else
            {
                // Not supported TDS version
                throw new NotSupportedException("Specified build version is not supported");
            }
        }

        /// <summary>
        /// Resolve conflicts between client and server TDS version
        /// </summary>
        /// <param name="tdsServer">Version of the server</param>
        /// <param name="tdsClient">Version of the client</param>
        /// <returns>Resulting version that both parties can talk</returns>
        public static Version Resolve(Version tdsServer, Version tdsClient)
        {
            // Pick the lowest TDS version between client and server
            if (tdsServer > tdsClient)
            {
                // Client doesn't talk our TDS version - downgrade it to client's
                return tdsClient;
            }
            else
            {
                // Client supports our TDS version
                return tdsServer;
            }
        }

        /// <summary>
        /// Check whether TDS version is supported by server
        /// </summary>
        public static bool IsSupported(Version tdsVersion)
        {
            return tdsVersion >= SqlServer7_0 && tdsVersion <= SqlServer2012;
        }
    }
}
