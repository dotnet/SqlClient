// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Class encapsulating necessary information about the byte package that needs to be sent to the enclave
    /// </summary>
    internal class EnclavePackage
    {

        internal SqlEnclaveSession EnclaveSession { get; }
        internal byte[] EnclavePackageBytes { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="enclavePackageBytes">byte package to be sent to enclave</param>
        /// <param name="enclaveSession"> enclave session to be used</param>
        internal EnclavePackage(byte[] enclavePackageBytes, SqlEnclaveSession enclaveSession)
        {
            EnclavePackageBytes = enclavePackageBytes;
            EnclaveSession = enclaveSession;
        }
    }
}
