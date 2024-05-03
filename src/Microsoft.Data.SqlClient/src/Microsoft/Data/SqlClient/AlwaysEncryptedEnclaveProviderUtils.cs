// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    internal class EnclavePublicKey
    {
        public byte[] PublicKey { get; set; }

        public EnclavePublicKey(byte[] payload)
        {
            PublicKey = payload;
        }
    }

    internal class EnclaveDiffieHellmanInfo
    {
        public int Size => sizeof(int) + sizeof(int) + PublicKey?.Length ?? 0 + PublicKeySignature?.Length ?? 0;

        public byte[] PublicKey { get; private set; }

        public byte[] PublicKeySignature { get; private set; }

        public EnclaveDiffieHellmanInfo(byte[] payload, int offset)
        {
            int publicKeySize = BitConverter.ToInt32(payload, offset + 0);
            int publicKeySignatureSize = BitConverter.ToInt32(payload, offset + 4);

            PublicKey = new byte[publicKeySize];
            PublicKeySignature = new byte[publicKeySignatureSize];
            Buffer.BlockCopy(payload, offset + 8, PublicKey, 0, publicKeySize);
            Buffer.BlockCopy(payload, offset + 8 + publicKeySize, PublicKeySignature, 0, publicKeySignatureSize);
        }
    }

    internal enum EnclaveType
    {
        None = 0,
        /// <summary>
        /// Virtualization Based Security
        /// </summary>
        Vbs = 1,
        /// <summary>
        /// Intel SGX based security
        /// </summary>
        Sgx = 2
    }
}
