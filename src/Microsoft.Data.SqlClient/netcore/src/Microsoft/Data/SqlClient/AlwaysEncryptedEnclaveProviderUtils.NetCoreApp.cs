// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Security.Cryptography;

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

    // Methods for converting keys between different formats
    internal class KeyConverter
    {
        // Extracts the public key's modulus and exponent from an RSA public key blob
        // and returns an RSAParameters object
        internal static RSAParameters RSAPublicKeyBlobToParams(byte[] keyBlob)
        {
            // The RSA public key blob is structured as follows:
            //     BCRYPT_RSAKEY_BLOB   header
            //     byte[cbPublicExp]    publicExponent      
            //     byte[cbModulus]      modulus             

            // The exponent is the final 3 bytes in the header
            // The modulus is the final 512 bytes in the key blob
            const int modulusSize = 512;
            const int exponentSize = 3;
            int headerSize = keyBlob.Length - modulusSize;
            int exponentOffset = headerSize - exponentSize;
            int modulusOffset = exponentOffset + exponentSize;

            return new RSAParameters()
            {
                Exponent = keyBlob.Skip(exponentOffset).Take(exponentSize).ToArray(),
                Modulus = keyBlob.Skip(modulusOffset).Take(modulusSize).ToArray()
            };
        }

        // Extracts the public key's X and Y coordinates from an ECC public key blob
        // and returns an ECParameters object
        internal static ECParameters ECCPublicKeyBlobToParams(byte[] keyBlob)
        {
            // The ECC public key blob is structured as follows:
            //     BCRYPT_ECCKEY_BLOB   header
            //     byte[cbKey]          X     
            //     byte[cbKey]          Y     

            // The size of each coordinate is found after the first 4 byes (magic number)
            const int keySizeOffset = 4;
            int keySize = BitConverter.ToInt32(keyBlob, keySizeOffset);
            int keyOffset = keySizeOffset + sizeof(int);

            return new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP384,
                Q = new ECPoint
                {
                    X = keyBlob.Skip(keyOffset).Take(keySize).ToArray(),
                    Y = keyBlob.Skip(keyOffset + keySize).Take(keySize).ToArray()
                },
            };
        }

        // Serializes an ECDiffieHellmanPublicKey to an ECC public key blob 
        // "ECDiffieHellmanPublicKey.ToByteArray() doesn't have a (standards-)defined export 
        // format. The version used by ECDiffieHellmanPublicKeyCng is Windows-specific"
        // from https://github.com/dotnet/runtime/issues/27276
        // => ECDiffieHellmanPublicKey.ToByteArray() is not supported in Unix
        internal static byte[] ECDHPublicKeyToECCKeyBlob(ECDiffieHellmanPublicKey publicKey)
        {
            // Size of an ECC key blob = 104 bytes
            // BCRYPT_ECCKEY_BLOB header = 8 bytes
            // key = 96 bytes (48 bytes each coordinate)
            const int keyBlobSize = 104;
            const int headerSize = 8;
            const int KeySize = 48;
            byte[] keyBlob = new byte[keyBlobSize];

            // magic number (BCRYPT_ECDH_PUBLIC_P384_MAGIC)
            keyBlob[0] = 0x45;
            keyBlob[1] = 0x43;
            keyBlob[2] = 0x4b;
            keyBlob[3] = 0x33;
            // key size
            keyBlob[4] = KeySize;
            keyBlob[5] = 0x00;
            keyBlob[6] = 0x00;
            keyBlob[7] = 0x00;

            ECParameters ecParams = publicKey.ExportParameters();
            // copy x and y coordinates to key blob
            Array.Copy(ecParams.Q.X, 0, keyBlob, headerSize, KeySize);
            Array.Copy(ecParams.Q.Y, 0, keyBlob, headerSize + KeySize, KeySize);
            return keyBlob;
        }
    }
}
