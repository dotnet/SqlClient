// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class KeyConverter
    {
        internal static RSA CreateRSAFromPublicKeyBlob(byte[] keyBlob)
        {
            CngKey key = CngKey.Import(keyBlob, CngKeyBlobFormat.GenericPublicBlob);
            return new RSACng(key);
        }

        internal static ECDiffieHellman CreateECDiffieHellmanFromPublicKeyBlob(byte[] keyBlob)
        {
            CngKey key = CngKey.Import(keyBlob, CngKeyBlobFormat.GenericPublicBlob);
            return new ECDiffieHellmanCng(key);
        }

        internal static ECDiffieHellman CreateECDiffieHellman(int keySize)
        {
            // Cng sets the key size and hash algorithm at creation time and these
            // parameters are then used later when DeriveKeyMaterial is called
            ECDiffieHellmanCng clientDHKey = new ECDiffieHellmanCng(keySize);
            clientDHKey.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            clientDHKey.HashAlgorithm = CngAlgorithm.Sha256;
            return clientDHKey;
        }

        public static byte[] GetECDiffieHellmanPublicKeyBlob(ECDiffieHellman ecDiffieHellman)
        {
            if (ecDiffieHellman is ECDiffieHellmanCng cng)
            {
                return cng.Key.Export(CngKeyBlobFormat.EccPublicBlob);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        internal static byte[] DeriveKey(ECDiffieHellman ecDiffieHellman, ECDiffieHellmanPublicKey publicKey)
        {
            if (ecDiffieHellman is ECDiffieHellmanCng cng)
            {
                return cng.DeriveKeyMaterial(publicKey);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        internal static RSA GetRSAFromCertificate(X509Certificate2 certificate)
        {
            RSAParameters parameters;
            using (RSA rsaCsp = certificate.GetRSAPublicKey())
            {
                parameters = rsaCsp.ExportParameters(includePrivateParameters: false);
            }
            RSACng rsaCng = new RSACng();
            rsaCng.ImportParameters(parameters);
            return rsaCng;
        }
    }
}
