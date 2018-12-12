using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Class encapsulating necessary information about the byte package that needs to be sent to the enclave
    /// </summary>
    internal class EnclavePackage
    {

        public SqlEnclaveSession EnclaveSession { get; }
        public byte[] EnclavePackageBytes { get; }

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
