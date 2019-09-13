// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// Encapsulates the information SqlClient sends to SQL Server to initiate the process of attesting and creating a secure session with the enclave, SQL Server uses for computations on columns protected using Always Encrypted.
    /// </summary>
    public class SqlEnclaveAttestationParameters
    {

        private static readonly string _clientDiffieHellmanKeyName = "ClientDiffieHellmanKey";
        private static readonly string _inputName = "input";
        private static readonly string _className = "EnclaveAttestationParameters";

        private readonly byte[] _input;

        /// <summary>
        /// Identifies an enclave attestation protocol.  
        /// </summary>
        public int Protocol { get; }


        /// <summary>
        /// A Diffie-Hellman algorithm encapsulating a key pair, SqlClient uses to establish a secure session with the enclave. 
        /// </summary>
        public ECDiffieHellmanCng ClientDiffieHellmanKey { get; }

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

        /// <summary>
        /// Initializes a new instance of SqlEnclaveAttestationParameters.
        /// </summary>
        /// <param name="protocol">Identifies an enclave attestation protocol.</param>
        /// <param name="input">The input of the enclave attestation protocol.</param>
        /// <param name="clientDiffieHellmanKey">A Diffie-Hellman algorithm encapsulating a client-side key pair.</param>
        public SqlEnclaveAttestationParameters(int protocol, byte[] input, ECDiffieHellmanCng clientDiffieHellmanKey)
        {
            if (null == clientDiffieHellmanKey)
            { throw SQL.NullArgumentInConstructorInternal(_clientDiffieHellmanKeyName, _className); }
            if (null == input)
            { throw SQL.NullArgumentInConstructorInternal(_inputName, _className); }

            _input = input;
            Protocol = protocol;
            ClientDiffieHellmanKey = clientDiffieHellmanKey;
        }
    }
}
