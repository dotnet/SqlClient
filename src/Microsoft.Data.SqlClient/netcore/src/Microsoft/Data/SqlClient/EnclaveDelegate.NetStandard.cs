// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

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
        /// <param name="attestationProtocol">attestation protocol</param>
        /// <param name="enclaveType">enclave type</param>
        /// <param name="serverName">servername</param>
        /// <param name="attestationUrl">attestation url for attestation service endpoint</param>
        /// <param name="attestationInfo">attestation info from SQL Server</param>
        /// <param name="attestationParameters">attestation parameters</param>
        internal void CreateEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string serverName, string attestationUrl,
            byte[] attestationInfo, SqlEnclaveAttestationParameters attestationParameters)
        {
            throw new PlatformNotSupportedException();
        }

        internal void GetEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string serverName, string enclaveAttestationUrl, out SqlEnclaveSession sqlEnclaveSession)
        {
            throw new PlatformNotSupportedException();
        }

        internal EnclavePackage GenerateEnclavePackage(SqlConnectionAttestationProtocol attestationProtocol, Dictionary<int, SqlTceCipherInfoEntry> keysTobeSentToEnclave, string queryText, string enclaveType, string serverName, string enclaveAttestationUrl)
        {
            throw new PlatformNotSupportedException();
        }

        internal void InvalidateEnclaveSession(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType, string serverName, string EnclaveAttestationUrl, SqlEnclaveSession enclaveSession)
        {
            throw new PlatformNotSupportedException();
        }

        internal SqlEnclaveAttestationParameters GetAttestationParameters(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType)
        {
            throw new PlatformNotSupportedException();
        }
    }
}
