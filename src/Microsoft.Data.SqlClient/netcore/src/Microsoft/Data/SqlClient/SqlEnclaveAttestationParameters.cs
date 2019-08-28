// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Encapsulates the information SqlClient sends to SQL Server to initiate the process of attesting and creating a secure session with the enclave, SQL Server uses for computations on columns protected using Always Encrypted.
    /// </summary>
    public partial class SqlEnclaveAttestationParameters
    {
        private readonly byte[] _input = null;

        /// <summary>
        /// Identifies an enclave attestation protocol.  
        /// </summary>
        public int Protocol { get; }

        /// <summary>
        /// The information used to initiate the process of attesting the enclave. The format and the content of this information is specific to the attestation protocol. 
        /// </summary>
        public byte[] GetInput()
        {
            return Clone(_input);
        }

        /// <summary>
        /// Deep copy the array into a new array
        /// </summary>
        /// <param name="arrayToClone"></param>
        /// <returns></returns>
        private byte[] Clone(byte[] arrayToClone)
        {

            if (null == arrayToClone)
            {
                return null;
            }

            byte[] returnValue = new byte[arrayToClone.Length];

            for (int i = 0; i < arrayToClone.Length; i++)
            {
                returnValue[i] = arrayToClone[i];
            }

            return returnValue;
        }
    }
}
