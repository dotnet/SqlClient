// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

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
        public int Size { get; private set; }

        public byte[] PublicKey { get; private set; }

        public byte[] PublicKeySignature { get; private set; }

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

    internal enum EnclaveType
    {
        None = 0,

        Vbs = 1,

        Sgx = 2
    }
}
