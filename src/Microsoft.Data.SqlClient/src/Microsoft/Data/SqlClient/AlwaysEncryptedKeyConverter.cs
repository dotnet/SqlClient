// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient
{
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
            internal const int Size = 539;
            // Size of the BCRYPT_RSAKEY_BLOB header
            internal const int HeaderSize = 27;
            // Size of the exponent (final 3 bytes of the header)
            internal const int ExponentSize = 3;
            // Size of the modulus (remaining bytes after the header)
            internal const int ModulusSize = Size - HeaderSize;
            internal const int ExponentOffset = HeaderSize - ExponentSize;
            internal const int ModulusOffset = HeaderSize;
        }

        // Extracts the public key's modulus and exponent from an RSA public key blob
        // and returns an RSAParameters object
        internal static RSAParameters RSAPublicKeyBlobToParams(byte[] keyBlob)
        {
            Debug.Assert(keyBlob.Length == RSAPublicKeyBlob.Size,
                $"RSA public key blob was not the expected length. Actual: {keyBlob.Length}. Expected: {RSAPublicKeyBlob.Size}");

            byte[] exponent = new byte[RSAPublicKeyBlob.ExponentSize];
            byte[] modulus = new byte[RSAPublicKeyBlob.ModulusSize];
            Buffer.BlockCopy(keyBlob, RSAPublicKeyBlob.ExponentOffset, exponent, 0, RSAPublicKeyBlob.ExponentSize);
            Buffer.BlockCopy(keyBlob, RSAPublicKeyBlob.ModulusOffset, modulus, 0, RSAPublicKeyBlob.ModulusSize);

            return new RSAParameters()
            {
                Exponent = exponent,
                Modulus = modulus
            };
        }

        // The ECC public key blob is structured as follows:
        //     BCRYPT_ECCKEY_BLOB   header
        //     byte[KeySize]        X     
        //     byte[KeySize]        Y         
        private readonly struct ECCPublicKeyBlob
        {
            // Size of an ECC public key blob
            internal const int Size = 104;
            // Size of the BCRYPT_ECCKEY_BLOB header
            internal const int HeaderSize = 8;
            // Size of each coordinate
            internal const int KeySize = (Size - HeaderSize) / 2;
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

            byte[] x = new byte[ECCPublicKeyBlob.KeySize];
            byte[] y = new byte[ECCPublicKeyBlob.KeySize];
            Buffer.BlockCopy(keyBlob, ECCPublicKeyBlob.HeaderSize, x, 0, ECCPublicKeyBlob.KeySize);
            Buffer.BlockCopy(keyBlob, ECCPublicKeyBlob.HeaderSize + ECCPublicKeyBlob.KeySize, y, 0, ECCPublicKeyBlob.KeySize);

            return new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP384,
                Q = new ECPoint
                {
                    X = x,
                    Y = y
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
            Buffer.BlockCopy(KeyBlobMagicNumber.ECDHPublicP384, 0, keyBlob, 0, 4);
            // Set key size
            keyBlob[4] = (byte)ECCPublicKeyBlob.KeySize;

            ECPoint ecPoint = publicKey.ExportParameters().Q;
            Debug.Assert(ecPoint.X.Length == ECCPublicKeyBlob.KeySize && ecPoint.Y.Length == ECCPublicKeyBlob.KeySize,
               $"ECDH public key was not the expected length. Actual (X): {ecPoint.X.Length}. Actual (Y): {ecPoint.Y.Length} Expected: {ECCPublicKeyBlob.Size}");
            // Copy x and y coordinates to key blob
            Buffer.BlockCopy(ecPoint.X, 0, keyBlob, ECCPublicKeyBlob.HeaderSize, ECCPublicKeyBlob.KeySize);
            Buffer.BlockCopy(ecPoint.Y, 0, keyBlob, ECCPublicKeyBlob.HeaderSize + ECCPublicKeyBlob.KeySize, ECCPublicKeyBlob.KeySize);
            return keyBlob;
        }
    }
}
