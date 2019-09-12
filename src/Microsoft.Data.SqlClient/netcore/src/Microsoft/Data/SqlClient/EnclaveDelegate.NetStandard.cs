// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// A delegate for communicating with secure enclave
    /// </summary>
    internal partial class EnclaveDelegate
    {
        internal byte[] GetSerializedAttestationParameters(
            SqlEnclaveAttestationParameters sqlEnclaveAttestationParameters, string enclaveType)
        {
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// Create a new enclave session
        /// </summary>
        /// <param name="enclaveType">enclave type</param>
        /// <param name="serverName">servername</param>
        /// <param name="attestationUrl">attestation url for attestation service endpoint</param>
        /// <param name="attestationInfo">attestation info from SQL Server</param>
        /// <param name="attestationParameters">attestation parameters</param>
        internal void CreateEnclaveSession(string enclaveType, string serverName, string attestationUrl,
            byte[] attestationInfo, SqlEnclaveAttestationParameters attestationParameters)
        {
            throw new PlatformNotSupportedException();
        }
    }
}
