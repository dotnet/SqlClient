//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class VirtualizationBasedSecurityEnclaveProviderBase : EnclaveProviderBase
    {
        #region Members

        private static readonly MemoryCache rootSigningCertificateCache = new MemoryCache("RootSigningCertificateCache");

        #endregion

        #region Constants

        private const int DiffieHellmanKeySize = 384;
        private const int VsmHGSProtocolId = 3;

        /// <summary>
        /// ENCLAVE_IDENTITY related constants
        /// </summary>
        private static readonly EnclaveIdentity ExpectedPolicy = new EnclaveIdentity()
        {
            OwnerId = new byte[]
            {
                0x10, 0x20, 0x30, 0x40, 0x41, 0x31, 0x21, 0x11,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            },

            UniqueId = new byte[] { },

            // This field is calculated as follows:
            // "Fixed Microsoft GUID" = {845319A6-706C-47BC-A7E8-5137B0BC750D}
            // CN of the certificate that signed the file
            // Opus Info - Authenticated Attribute that contains a Name and a URL
            //
            // In our case the Opus Info is:
            // Description: Microsoft SQL Server Always Encrypted VBS Enclave Library,
            // Description URL: https://go.microsoft.com/fwlink/?linkid=2018716
            AuthorId = new byte[]
            {
                0x04, 0x37, 0xCA, 0xE2, 0x53, 0x7D, 0x8B, 0x9B,
                0x07, 0x76, 0xB6, 0x1B, 0x11, 0xE6, 0xCE, 0xD3,
                0xD2, 0x32, 0xE9, 0x30, 0x8F, 0x60, 0xE2, 0x1A,
                0xDA, 0xB2, 0xFD, 0x91, 0xE3, 0xDA, 0x95, 0x98
            },

            FamilyId = new byte[]
            {
                0xFE, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            },

            ImageId = new byte[]
            {
                // This value should be same as defined in sqlserver code Sql/Ntdbms/aetm/enclave/dllmain.cpp
                0x19, 0x17, 0x12, 0x00, 0x01, 0x05, 0x20, 0x13,
                0x00, 0x05, 0x14, 0x03, 0x12, 0x01, 0x22, 0x05
            },

            EnclaveSvn = 0,

            SecureKernelSvn = 0,

            PlatformSvn = 1,

            // 0: ENCLAVE_VBS_FLAG_NO_DEBUG in ds_main; Flag does not permit debug enclaves
            Flags = 0,

            SigningLevel = 0,

            Reserved = 0
        };

        #endregion

        #region Public methods

        /// <summary>
        /// When overridden in a derived class, looks up an existing enclave session information in the enclave session cache.
        /// If the enclave provider doesn't implement enclave session caching, this method is expected to return null in the sqlEnclaveSession parameter.
        /// </summary>
        /// <param name="servername">The name of the SQL Server instance containing the enclave.</param>
        /// <param name="attestationUrl">The endpoint of an attestation service, SqlClient contacts to attest the enclave.</param>
        /// <param name="sqlEnclaveSession">When this method returns, the requested enclave session or null if the provider doesn't implement session caching. This parameter is treated as uninitialized.</param>
        /// <param name="counter">A counter that the enclave provider is expected to increment each time SqlClient retrieves the session from the cache. The purpose of this field is to prevent replay attacks.</param>
        public override void GetEnclaveSession(string servername, string attestationUrl, out SqlEnclaveSession sqlEnclaveSession, out long counter)
        {
            GetEnclaveSessionHelper(servername, attestationUrl, false, out sqlEnclaveSession, out counter);
        }

        /// <summary>
        /// Gets the information that SqlClient subsequently uses to initiate the process of attesting the enclave and to establish a secure session with the enclave.
        /// </summary>
        /// <returns>The information SqlClient subsequently uses to initiate the process of attesting the enclave and to establish a secure session with the enclave.</returns>
        public override SqlEnclaveAttestationParameters GetAttestationParameters()
        {
            ECDiffieHellmanCng clientDHKey = new ECDiffieHellmanCng(DiffieHellmanKeySize);
            clientDHKey.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            clientDHKey.HashAlgorithm = CngAlgorithm.Sha256;
            return new SqlEnclaveAttestationParameters(VsmHGSProtocolId, new byte[] { }, clientDHKey);
        }

        /// <summary>
        /// When overridden in a derived class, performs enclave attestation, generates a symmetric key for the session, creates a an enclave session and stores the session information in the cache.
        /// </summary>
        /// <param name="attestationInfo">The information the provider uses to attest the enclave and generate a symmetric key for the session. The format of this information is specific to the enclave attestation protocol.</param>
        /// <param name="clientDHKey">A Diffie-Hellman algorithm object that encapsulates a client-side key pair.</param>
        /// <param name="attestationUrl">The endpoint of an attestation service for attesting the enclave.</param>
        /// <param name="servername">The name of the SQL Server instance containing the enclave.</param>
        /// <param name="sqlEnclaveSession">The requested enclave session or null if the provider doesn't implement session caching.</param>
        /// <param name="counter">A counter that the enclave provider is expected to increment each time SqlClient retrieves the session from the cache. The purpose of this field is to prevent replay attacks.</param>
        public override void CreateEnclaveSession(byte[] attestationInfo, ECDiffieHellmanCng clientDHKey, string attestationUrl, string servername, out SqlEnclaveSession sqlEnclaveSession, out long counter)
        {
            sqlEnclaveSession = null;
            counter = 0;
            try
            {
                AttestationInfoCacheItem attestationInfoCacheItem = AttestationInfoCache.Remove(Thread.CurrentThread.ManagedThreadId.ToString()) as AttestationInfoCacheItem;
                sqlEnclaveSession = GetEnclaveSessionFromCache(servername, attestationUrl, out counter);
                if (sqlEnclaveSession == null)
                {
                    if (attestationInfoCacheItem != null)
                    {
                        // Deserialize the payload
                        AttestationInfo info = new AttestationInfo(attestationInfo);

                        // Verify enclave policy matches expected policy
                        VerifyEnclavePolicy(info.EnclaveReportPackage);

                        // Perform Attestation per VSM protocol
                        VerifyAttestationInfo(attestationUrl, info.HealthReport, info.EnclaveReportPackage);

                        // Set up shared secret and validate signature
                        byte[] sharedSecret = GetSharedSecret(info.Identity, info.EnclaveDHInfo, clientDHKey);

                        // add session to cache
                        sqlEnclaveSession = AddEnclaveSessionToCache(attestationUrl, servername, sharedSecret, info.SessionId, out counter);
                    }
                    else
                    {
                        throw new AlwaysEncryptedAttestationException(SR.FailToCreateEnclaveSession);
                    }
                }
            }
            finally
            {
                UpdateEnclaveSessionLockStatus(sqlEnclaveSession);
            }
        }

        /// <summary>
        /// When overridden in a derived class, looks up and evicts an enclave session from the enclave session cache, if the provider implements session caching.
        /// </summary>
        /// <param name="serverName">The name of the SQL Server instance containing the enclave.</param>
        /// <param name="enclaveAttestationUrl">The endpoint of an attestation service, SqlClient contacts to attest the enclave.</param>
        /// <param name="enclaveSessionToInvalidate">The session to be invalidated.</param>
        public override void InvalidateEnclaveSession(string serverName, string enclaveAttestationUrl, SqlEnclaveSession enclaveSessionToInvalidate)
        {
            InvalidateEnclaveSessionHelper(serverName, enclaveAttestationUrl, enclaveSessionToInvalidate);
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Performs Attestation per the protocol used by Virtual Secure Modules.
        /// </summary>
        /// <param name="attestationUrl">Url of the attestation service</param>
        /// <param name="healthReport">The health report about the SQL Server host</param>
        /// <param name="enclaveReportPackage">The enclave report about the SQL Server host's enclave</param>
        private void VerifyAttestationInfo(string attestationUrl, HealthReport healthReport, EnclaveReportPackage enclaveReportPackage)
        {
            bool shouldRetryValidation;
            bool shouldForceUpdateSigningKeys = false;
            do
            {
                shouldRetryValidation = false;

                // Get HGS Root signing certs from HGS
                X509Certificate2Collection signingCerts = GetSigningCertificate(attestationUrl, shouldForceUpdateSigningKeys);

                // Verify SQL Health report root chain of trust is the HGS root signing cert
                X509ChainStatusFlags chainStatus = VerifyHealthReportAgainstRootCertificate(signingCerts, healthReport.Certificate);
                if (chainStatus != X509ChainStatusFlags.NoError)
                {
                    // In cases if we fail to validate the health report, it might be possible that we are using old signing keys
                    // let's re-download the signing keys again and re-validate the health report
                    if (!shouldForceUpdateSigningKeys)
                    {
                        shouldForceUpdateSigningKeys = true;
                        shouldRetryValidation = true;
                    }
                    else
                    {
                        throw new AlwaysEncryptedAttestationException(String.Format(SR.VerifyHealthCertificateChainFormat, attestationUrl, chainStatus));
                    }
                }
            } while (shouldRetryValidation);

            // Verify enclave report is signed by IDK_S from health report
            VerifyEnclaveReportSignature(enclaveReportPackage, healthReport.Certificate);
        }

        /// <summary>
        /// Makes a web request to the provided url and returns the response as a byte[]
        /// </summary>
        /// <param name="url">The url to make the request to</param>
        /// <returns>The response as a byte[]</returns>
        protected abstract byte[] MakeRequest(string url);

        /// <summary>
        /// Gets the root signing certificate for the provided attestation service.
        /// If the certificate does not exist in the cache, this will make a call to the
        /// attestation service's "/signingCertificates" endpoint. This endpoint can
        /// return multiple certificates if the attestation service consists
        /// of multiple nodes.
        /// </summary>
        /// <param name="attestationUrl">Url of attestation service</param>
        /// <param name="forceUpdate">Re-download the signing certificate irrespective of caching</param>
        /// <returns>The root signing certificate(s) for the attestation service</returns>
        private X509Certificate2Collection GetSigningCertificate(string attestationUrl, bool forceUpdate)
        {
            attestationUrl = GetAttestationUrl(attestationUrl);
            X509Certificate2Collection signingCertificates = (X509Certificate2Collection)rootSigningCertificateCache[attestationUrl];
            if (forceUpdate || signingCertificates == null || AnyCertificatesExpired(signingCertificates))
            {
                byte[] data = MakeRequest(attestationUrl);
                var certificateCollection = new X509Certificate2Collection();

                try
                {
                    certificateCollection.Import(data);
                }
                catch (CryptographicException exception)
                {
                    throw new AlwaysEncryptedAttestationException(String.Format(SR.GetAttestationSigningCertificateFailedInvalidCertificate, attestationUrl), exception);
                }

                rootSigningCertificateCache.Add(attestationUrl, certificateCollection, DateTime.Now.AddDays(1));
            }

            return (X509Certificate2Collection)rootSigningCertificateCache[attestationUrl];
        }

        /// <summary>
        /// Return the endpoint for given attestation url
        /// </summary>
        /// <param name="attestationUrl">The url to alter for corresponding provider</param>
        /// <returns>altered url</returns>
        protected abstract string GetAttestationUrl(string attestationUrl);

        /// <summary>
        /// Checks if any certificates in the collection are expired
        /// </summary>
        /// <param name="certificates">A collection of certificates</param>
        /// <returns>true if any certificates or expired, false otherwise</returns>
        private bool AnyCertificatesExpired(X509Certificate2Collection certificates)
        {
            return certificates.OfType<X509Certificate2>().Any(c => c.NotAfter < DateTime.Now);
        }

        /// <summary>
        /// Verifies that a chain of trust can be built from the health report provided
        /// by SQL Server and the attestation service's root signing certificate(s).
        /// </summary>
        /// <param name="signingCerts">The root signing certificate(s) of the attestation service</param>
        /// <param name="healthReportCert">The health report about the SQL Server host in the form of an X509Certificate2</param>
        /// <returns>An X509ChainStatusFlags indicating why the chain failed to build</returns>
        private X509ChainStatusFlags VerifyHealthReportAgainstRootCertificate(X509Certificate2Collection signingCerts, X509Certificate2 healthReportCert)
        {
            var chain = new X509Chain();

            foreach (var cert in signingCerts)
            {
                chain.ChainPolicy.ExtraStore.Add(cert);
            }

            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            if (!chain.Build(healthReportCert))
            {
                bool untrustedRoot = false;

                // iterate over the chain status to check why the build failed
                foreach (X509ChainStatus status in chain.ChainStatus)
                {
                    if (status.Status == X509ChainStatusFlags.UntrustedRoot)
                    {
                        untrustedRoot = true;
                    }
                    else
                    {
                        return status.Status;
                    }
                }

                // if the chain failed with untrusted root, this could be because the client doesn't have the root cert
                // installed. If the chain's untrusted root cert has the same thumbprint as the signing cert, then we
                // do trust it.
                if (untrustedRoot)
                {
                    // iterate through the certificate chain, starting at the root since it's likely the
                    // signing certificate is the root
                    for (int i = 0; i < chain.ChainElements.Count; i++)
                    {
                        X509ChainElement element = chain.ChainElements[chain.ChainElements.Count - 1 - i];

                        foreach (X509Certificate2 cert in signingCerts)
                        {
                            if (element.Certificate.Thumbprint == cert.Thumbprint)
                            {
                                return X509ChainStatusFlags.NoError;
                            }
                        }
                    }

                    // in the case where we didn't find matching thumbprint
                    return X509ChainStatusFlags.UntrustedRoot;
                }
            }

            return X509ChainStatusFlags.NoError;
        }

        /// <summary>
        /// Verifies the enclave report signature using the health report.
        /// </summary>
        /// <param name="enclaveReportPackage">The enclave report about the SQL Server host's enclave</param>
        /// <param name="healthReportCert">The health report about the SQL Server host in the form of an X509Certificate2</param>
        private void VerifyEnclaveReportSignature(EnclaveReportPackage enclaveReportPackage, X509Certificate2 healthReportCert)
        {
            // Check if report is formatted correctly
            UInt32 calculatedSize = Convert.ToUInt32(enclaveReportPackage.PackageHeader.GetSizeInPayload()) + enclaveReportPackage.PackageHeader.SignedStatementSize + enclaveReportPackage.PackageHeader.SignatureSize;

            if (calculatedSize != enclaveReportPackage.PackageHeader.PackageSize)
            {
                throw new ArgumentException(SR.VerifyEnclaveReportFormatFailed);
            }

            // IDK_S is contained in healthReport cert public key
            RSA rsacsp = healthReportCert.GetRSAPublicKey();
            RSAParameters rsaparams = rsacsp.ExportParameters(includePrivateParameters: false);
            RSACng rsacng = new RSACng();
            rsacng.ImportParameters(rsaparams);

            if (!rsacng.VerifyData(enclaveReportPackage.ReportAsBytes, enclaveReportPackage.SignatureBlob, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            {
                throw new ArgumentException(SR.VerifyEnclaveReportFailed);

            }
        }

        /// <summary>
        /// Verifies the enclave policy matches expected policy.
        /// </summary>
        /// <param name="enclaveReportPackage">The enclave report about the SQL Server host's enclave</param>
        private void VerifyEnclavePolicy(EnclaveReportPackage enclaveReportPackage)
        {
            EnclaveIdentity identity = enclaveReportPackage.Report.Identity;

            VerifyEnclavePolicyProperty("OwnerId", identity.OwnerId, ExpectedPolicy.OwnerId);
            VerifyEnclavePolicyProperty("AuthorId", identity.AuthorId, ExpectedPolicy.AuthorId);
            VerifyEnclavePolicyProperty("FamilyId", identity.FamilyId, ExpectedPolicy.FamilyId);
            VerifyEnclavePolicyProperty("ImageId", identity.ImageId, ExpectedPolicy.ImageId);
            VerifyEnclavePolicyProperty("EnclaveSvn", identity.EnclaveSvn, ExpectedPolicy.EnclaveSvn);
            VerifyEnclavePolicyProperty("SecureKernelSvn", identity.SecureKernelSvn, ExpectedPolicy.SecureKernelSvn);
            VerifyEnclavePolicyProperty("PlatformSvn", identity.PlatformSvn, ExpectedPolicy.PlatformSvn);


            // This is a check that the enclave is running without debug support or not.
            //
            if (identity.Flags != ExpectedPolicy.Flags)
            {
                throw new InvalidOperationException(SR.VerifyEnclaveDebuggable);
            }
        }

        /// <summary>
        /// Verifies a byte[] enclave policy property
        /// </summary>
        /// <param name="property">The enclave property name</param>
        /// <param name="actual">The actual enclave property from the enclave report</param>
        /// <param name="expected">The expected enclave property</param>
        private void VerifyEnclavePolicyProperty(string property, byte[] actual, byte[] expected)
        {
            if (!actual.SequenceEqual(expected))
            {
                string exceptionMessage = String.Format(SR.VerifyEnclavePolicyFailedFormat, property, BitConverter.ToString(actual), BitConverter.ToString(expected));
                throw new ArgumentException(exceptionMessage);
            }
        }

        /// <summary>
        /// Verifies a uint enclave policy property
        /// </summary>
        /// <param name="property">The enclave property name</param>
        /// <param name="actual">The actual enclave property from the enclave report</param>
        /// <param name="expected">The expected enclave property</param>
        private void VerifyEnclavePolicyProperty(string property, uint actual, uint expected)
        {
            if (actual < expected)
            {
                string exceptionMessage = String.Format(SR.VerifyEnclavePolicyFailedFormat, property, actual, expected);
                throw new ArgumentException(exceptionMessage);
            }
        }

        /// <summary>
        /// Derives the shared secret between the client and enclave.
        /// </summary>
        /// <param name="enclavePublicKey">The enclave's RSA public key</param>
        /// <param name="enclaveDHInfo">The enclave's DiffieHellman key and signature info</param>
        /// <param name="clientDHKey">The client's DiffieHellman key info</param>
        /// <returns>A byte buffer containing the shared secret</returns>
        private byte[] GetSharedSecret(EnclavePublicKey enclavePublicKey, EnclaveDiffieHellmanInfo enclaveDHInfo, ECDiffieHellmanCng clientDHKey)
        {
            // Perform signature verification. The enclave's DiffieHellman public key was signed by the enclave's RSA public key.
            CngKey cngkey = CngKey.Import(enclavePublicKey.PublicKey, CngKeyBlobFormat.GenericPublicBlob);
            RSACng rsacng = new RSACng(cngkey);
            if (!rsacng.VerifyData(enclaveDHInfo.PublicKey, enclaveDHInfo.PublicKeySignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                throw new ArgumentException(SR.GetSharedSecretFailed);
            }

            CngKey key = CngKey.Import(enclaveDHInfo.PublicKey, CngKeyBlobFormat.GenericPublicBlob);
            return clientDHKey.DeriveKeyMaterial(key);
        }

        #endregion
    }
}
