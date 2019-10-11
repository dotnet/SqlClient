using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Class to hold the enclave's RSA public key
    /// </summary>
    internal class EnclavePublicKey
    {
        /// <summary>
        /// 
        /// </summary>
        public byte[] PublicKey { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="payload"></param>
        public EnclavePublicKey(byte[] payload)
        {
            PublicKey = payload;
        }
    }

    /// <summary>
    /// Class to hold the Enclave's Diffie-Hellman public key and signature
    /// </summary>
    internal class EnclaveDiffieHellmanInfo
    {
        /// <summary>
        /// 
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public byte[] PublicKey { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public byte[] PublicKeySignature { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="payload"></param>
        public EnclaveDiffieHellmanInfo(byte[] payload)
        {
            Size = payload.Length;

            int offset = 0;
            int publicKeySize = BitConverter.ToInt32(payload, offset);
            offset += sizeof(int);

            int publicKeySignatureSize = BitConverter.ToInt32(payload, offset);
            offset += sizeof(int);

            PublicKey = payload.Skip(offset).Take(publicKeySize).ToArray();
            offset += publicKeySize;

            PublicKeySignature = payload.Skip(offset).Take(publicKeySignatureSize).ToArray();
            offset += publicKeySignatureSize;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    internal enum EnclaveType
    {
        /// <summary>
        /// 
        /// </summary>
        None = 0,

        /// <summary>
        /// 
        /// </summary>
        Vbs = 1,

        /// <summary>
        /// 
        /// </summary>
        Sgx = 2
    }
}
