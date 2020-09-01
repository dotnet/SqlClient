// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
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

    // Contains methods to convert cryptography keys between different formats.
    internal sealed class KeyConverter
    {
        // The RSA public key blob is structured as follows:
        //     BCRYPT_RSAKEY_BLOB   header
        //     byte[ExponentSize]   publicExponent      
        //     byte[ModulusSize]    modulus             
        private readonly struct RSAPublicKeyBlob
        {
            // Size of an RSA public key blob
            internal static readonly int Size = 539;
            // Size of the BCRYPT_RSAKEY_BLOB header
            internal static readonly int HeaderSize = 27;
            // Size of the exponent (final 3 bytes of the header)
            internal static readonly int ExponentSize = 3;
            // Size of the modulus (remaining bytes after the header)
            internal static readonly int ModulusSize = Size - HeaderSize;
            internal static readonly int ExponentOffset = HeaderSize - ExponentSize;
            internal static readonly int ModulusOffset = HeaderSize;
        }

        // Extracts the public key's modulus and exponent from an RSA public key blob
        // and returns an RSAParameters object
        internal static RSAParameters RSAPublicKeyBlobToParams(byte[] keyBlob)
        {
            Debug.Assert(keyBlob.Length == RSAPublicKeyBlob.Size,
                $"RSA public key blob was not the expected length. Actual: {keyBlob.Length}. Expected: {RSAPublicKeyBlob.Size}");
            return new RSAParameters()
            {
                Exponent = keyBlob.Skip(RSAPublicKeyBlob.ExponentOffset).Take(RSAPublicKeyBlob.ExponentSize).ToArray(),
                Modulus = keyBlob.Skip(RSAPublicKeyBlob.ModulusOffset).Take(RSAPublicKeyBlob.ModulusSize).ToArray()
            };
        }

        // The ECC public key blob is structured as follows:
        //     BCRYPT_ECCKEY_BLOB   header
        //     byte[KeySize]        X     
        //     byte[KeySize]        Y         
        private readonly struct ECCPublicKeyBlob
        {
            // Size of an ECC public key blob
            internal static readonly int Size = 104;
            // Size of the BCRYPT_ECCKEY_BLOB header
            internal static readonly int HeaderSize = 8;
            // Size of each coordinate
            internal static readonly int KeySize = (Size - HeaderSize) / 2;
        }

        // Magic numbers identifying blob types
        // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-wcce/cba27df5-4880-4f95-a879-783f8657e53b
        private readonly struct KeyBlobMagicNumber
        {
            internal static readonly byte[] ECDHPublicP384 = new byte[] { 0x45, 0x43, 0x4b, 0x33 };
        }

        // Extracts the public key's X and Y coordinates from an ECC public key blob
        // and returns an ECParameters object
        internal static ECParameters ECCPublicKeyBlobToParams(byte[] keyBlob)
        {
            Debug.Assert(keyBlob.Length == ECCPublicKeyBlob.Size,
                $"ECC public key blob was not the expected length. Actual: {keyBlob.Length}. Expected: {ECCPublicKeyBlob.Size}");
            return new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP384,
                Q = new ECPoint
                {
                    X = keyBlob.Skip(ECCPublicKeyBlob.HeaderSize).Take(ECCPublicKeyBlob.KeySize).ToArray(),
                    Y = keyBlob.Skip(ECCPublicKeyBlob.HeaderSize + ECCPublicKeyBlob.KeySize).Take(ECCPublicKeyBlob.KeySize).ToArray()
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
            byte[] keyBlob = new byte[ECCPublicKeyBlob.Size];

            // Set magic number
            Array.Copy(KeyBlobMagicNumber.ECDHPublicP384, 0, keyBlob, 0, 4);
            // Set key size
            keyBlob[4] = (byte)ECCPublicKeyBlob.KeySize;

            ECPoint ecPoint = publicKey.ExportParameters().Q;
            Debug.Assert(ecPoint.X.Length == ECCPublicKeyBlob.KeySize && ecPoint.Y.Length == ECCPublicKeyBlob.KeySize,
               $"ECDH public key was not the expected length. Actual (X): {ecPoint.X.Length}. Actual (Y): {ecPoint.Y.Length} Expected: {ECCPublicKeyBlob.Size}");
            // Copy x and y coordinates to key blob
            Array.Copy(ecPoint.X, 0, keyBlob, ECCPublicKeyBlob.HeaderSize, ECCPublicKeyBlob.KeySize);
            Array.Copy(ecPoint.Y, 0, keyBlob, ECCPublicKeyBlob.HeaderSize + ECCPublicKeyBlob.KeySize, ECCPublicKeyBlob.KeySize);
            return keyBlob;
        }
    }
}
