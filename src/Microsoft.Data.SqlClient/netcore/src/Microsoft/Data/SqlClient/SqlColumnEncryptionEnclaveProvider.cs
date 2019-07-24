// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// The base class that defines the interface for enclave providers for Always Encrypted. An enclave is a protected region of memory inside SQL Server, used for computations on encrypted columns. An enclave provider encapsulates the client-side implementation details of the enclave attestation protocol as well as the logic for creating and caching enclave sessions.
    /// </summary>
    public abstract partial class SqlColumnEncryptionEnclaveProvider
    {
        /// <summary>
        /// Looks up an existing enclave session information in the enclave session cache. If the enclave provider does not implement enclave session caching, this method is expected to return null.
        /// </summary>
        /// <param name="serverName">The name of the SQL Server instance containing the enclave.</param>
        /// <param name="attestationUrl">The endpoint of an attestation service, SqlClient contacts to attest the enclave.</param>
        /// <param name="sqlEnclaveSession">The requested enclave session or null if the provider does not implement session caching.</param>
        /// <param name="counter">A counter that the enclave provider is expected to increment each time SqlClient retrieves the session from the cache. The purpose of this field is to prevent replay attacks.</param>
        public abstract void GetEnclaveSession(string serverName, string attestationUrl, out SqlEnclaveSession sqlEnclaveSession, out long counter);

        /// <summary>
        /// Returns the information SqlClient subsequently uses to initiate the process of attesting the enclave and to establish a secure session with the enclave. 
        /// </summary>
        /// <returns>Enclave attestation parameters.</returns>
        public abstract SqlEnclaveAttestationParameters GetAttestationParameters();

        /// <summary>
        /// Looks up and evicts an enclave session from the enclave session cache, if the provider implements session caching.
        /// </summary>
        /// <param name="serverName">The name of the SQL Server instance containing the enclave.</param>
        /// <param name="enclaveAttestationUrl">The endpoint of an attestation service, SqlClient contacts to attest the enclave.</param>
        /// <param name="enclaveSession">The session to be invalidated.</param>
        public abstract void InvalidateEnclaveSession(string serverName, string enclaveAttestationUrl, SqlEnclaveSession enclaveSession);
    }
}
