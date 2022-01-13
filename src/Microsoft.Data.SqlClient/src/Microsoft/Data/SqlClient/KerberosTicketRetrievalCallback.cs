// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


namespace Microsoft.Data.SqlClient
{
        /// <summary>
        /// A callback to provide kerberos ticket for given service principal name (SPN) on demand.
        /// </summary>
        /// <returns></returns>
        public delegate byte[] KerberosTicketRetrievalCallback(string serverPrincipalName);
}
