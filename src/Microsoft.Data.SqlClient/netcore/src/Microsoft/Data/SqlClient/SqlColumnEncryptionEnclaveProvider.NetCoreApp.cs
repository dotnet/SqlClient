// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// The base class that defines the interface for enclave providers for Always Encrypted. An enclave is a protected region of memory inside SQL Server, used for computations on encrypted columns. An enclave provider encapsulates the client-side implementation details of the enclave attestation protocol as well as the logic for creating and caching enclave sessions.
    /// </summary>
    internal abstract partial class SqlColumnEncryptionEnclaveProvider
    {
        /// Performs enclave attestation, generates a symmetric key for the session, creates a an enclave session and stores the session information in the cache.
        /// <param name="enclaveAttestationInfo">The information the provider uses to attest the enclave and generate a symmetric key for the session. The format of this information is specific to the enclave attestation protocol.</param>
        /// <param name="clientDiffieHellmanKey">A Diffie-Hellman algorithm object encapsulating a client-side key pair.</param>
        /// <param name="enclaveSessionParameters">The set of parameters required for enclave session.</param>
        /// <param name="customData">The set of extra data needed for attestating the enclave.</param>
        /// <param name="customDataLength">The length of the extra data needed for attestating the enclave.</param>
        /// <param name="sqlEnclaveSession">The requested enclave session or null if the provider does not implement session caching.</param>
        /// <param name="counter">A counter that the enclave provider is expected to increment each time SqlClient retrieves the session from the cache. The purpose of this field is to prevent replay attacks.</param>
        internal abstract void CreateEnclaveSession(byte[] enclaveAttestationInfo, ECDiffieHellman clientDiffieHellmanKey, EnclaveSessionParameters enclaveSessionParameters, byte[] customData, int customDataLength,
            out SqlEnclaveSession sqlEnclaveSession, out long counter);
    }
}
