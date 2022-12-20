// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;
#if NET6_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
#if NET6_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    [PlatformSpecific(TestPlatforms.Windows)]
    class CertificateUtilityWin
    {

        public class CertificateOption
        {
            public string Subject { get; set; }
            public string KeyExportPolicy { get; set; } = "Exportable";
            public string CertificateStoreLocation { get; set; }
            public List<Tuple<string, string, string>> TextExtensions { get; set; } = new List<Tuple<string, string, string>> { new Tuple<string, string, string>("2.5.29.3", "1.3.6.1.5.5.8.2.2", "1.3.6.1.4.1.311.10.3.11") };
            public string KeySpec { get; set; } = "KeyExchange";
            public string Provider { get; set; } = "Microsoft Enhanced RSA and AES Cryptographic Provider";
            public string Type { get; set; } = "24";
            public string KeyAlgorithm { get; set; } = "rsa";
            public string KeyLength { get; set; } = "2048";
            public string HashAlgorithm { get; set; } = "sha256";
            public string KeyUsage { get; set; } = "None";
            public string TestRoot { get; set; } = "-TestRoot";
        }

        private CertificateUtilityWin()
        {
        }

        /// <summary>
        /// Create a self-signed certificate through PowerShell.
        /// </summary>
        internal static void CreateCertificate(CertificateOption option)
        {
            Assert.False(string.IsNullOrWhiteSpace(option.Subject), "FAILED: certificateName should not be null or empty.");
            Assert.False(string.IsNullOrWhiteSpace(option.CertificateStoreLocation), "FAILED: certificateLocation should not be null or empty.");

            string powerShellPath = string.IsNullOrEmpty(DataTestUtility.PowerShellPath) ? "powershell.exe" : DataTestUtility.PowerShellPath;
            ProcessStartInfo processStartInfo = new ProcessStartInfo(powerShellPath)
            {
                Arguments = $"New-SelfSignedCertificate -Subject " +
                $"'CN={option.Subject}' " +
                $"-KeyExportPolicy {option.KeyExportPolicy} " +
                $"-CertStoreLocation '{option.CertificateStoreLocation}\\My' " +
                $"-TextExtension @('{option.TextExtensions[0].Item1}={{text}}{option.TextExtensions[0].Item2},{option.TextExtensions[0].Item3}') " +
                $"-KeySpec {option.KeySpec} " +
                $"-Provider '{option.Provider}' " +
                $"-Type {option.Type}  " +
                $"-KeyAlgorithm {option.KeyAlgorithm} " +
                $"-KeyLength {option.KeyLength}  " +
                $"-HashAlgorithm {option.HashAlgorithm} " +
                $"-KeyUsage {option.KeyUsage} " +
                $"{option.TestRoot}",
                Verb="runas"
            };
            Process process = new()
            {
                StartInfo = processStartInfo
            };
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit();
        }

        /// <summary>
        /// Creates an RSA 2048 key inside the specified CSP.
        /// </summary>
        /// <param name="providerName">CSP name</param>
        /// <param name="containerName">Container name</param>
        /// <returns></returns>
        internal static bool RSAPersistKeyInCsp(string providerName, string containerName)
        {
            try
            {
                const int KEYSIZE = 2048;
                int providerType = GetProviderKey(providerName);

                // Create a new instance of CspParameters.
                CspParameters cspParams = new CspParameters(providerType, providerName, containerName);

                //Create a new instance of RSACryptoServiceProvider to generate 
                //a new key pair.  Pass the CspParameters class to persist the  
                //key in the container.
                RSACryptoServiceProvider rsaAlg = new RSACryptoServiceProvider(KEYSIZE, cspParams);
                rsaAlg.PersistKeyInCsp = true;
            }
            catch (CryptographicException e)
            {
                Console.WriteLine("\tFAILURE: The RSA key was not persisted in the container, \"{0}\".", containerName);
                Console.WriteLine(@"    {0}", e.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Deletes the specified RSA key
        /// </summary>
        /// <param name="providerName">CSP name</param>
        /// <param name="containerName">Container name to be deleted</param>
        /// <returns></returns>
        internal static bool RSADeleteKeyInCsp(string providerName, string containerName)
        {
            try
            {
                int providerType = GetProviderKey(providerName);

                // Create a new instance of CspParameters.
                CspParameters cspParams = new CspParameters(providerType, providerName, containerName);

                //Create a new instance of RSACryptoServiceProvider.  
                //Pass the CspParameters class to use the  
                //key in the container.
                RSACryptoServiceProvider rsaAlg = new RSACryptoServiceProvider(cspParams);

                //Delete the key entry in the container.
                rsaAlg.PersistKeyInCsp = false;

                //Call Clear to release resources and delete the key from the container.
                rsaAlg.Clear();
            }
            catch (CryptographicException e)
            {
                Console.WriteLine("\tFAILURE: The RSA key was not deleted from the container, \"{0}\".", containerName);
                Console.WriteLine("\t{0}", e.Message);
                return false;
            }

            return true;
        }

        internal static int GetProviderKey(string providerName)
        {
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography\Defaults\Provider");
            Microsoft.Win32.RegistryKey providerKey = key.OpenSubKey(providerName);
            return (int)providerKey.GetValue(@"Type");
        }

        /// <summary>
        /// Checks if a cert exists or not
        /// </summary>
        /// <param name="certificateName"></param>
        /// <param name="certificateStoreLocation"></param>
        /// <returns></returns>
        internal static bool CertificateExists(string certificateName, StoreLocation certificateStoreLocation)
        {
            Assert.False(string.IsNullOrWhiteSpace(certificateName), "FAILED: certificateName should not be null or empty.");

            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, certificateStoreLocation);
                certStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindBySubjectName, certificateName, validOnly: false);

                if (certCollection != null && certCollection.Count > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }
        }

        /// <summary>
        /// Gets the certificate.
        /// </summary>
        /// <param name="certificateName"></param>
        /// <param name="certificateStoreLocation"></param>
        /// <returns></returns>
        internal static X509Certificate2 GetCertificate(string certificateName, StoreLocation certificateStoreLocation)
        {
            Assert.False(string.IsNullOrWhiteSpace(certificateName), "FAILED: certificateName should not be null or empty.");
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, certificateStoreLocation);
                certStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindBySubjectName, certificateName, validOnly: false);
                Debug.Assert(certCollection != null && certCollection.Count > 0);
                return certCollection[0];
            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }
        }

        /// <summary>
        /// Gets Csp path from a given certificate.
        /// </summary>
        internal static string GetCspPathFromCertificate(X509Certificate2 certificate)
        {
            if (certificate.GetRSAPrivateKey() is RSACryptoServiceProvider csp)
            {
                return string.Concat(csp.CspKeyContainerInfo.ProviderName, @"/", csp.CspKeyContainerInfo.KeyContainerName);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Removes a certificate from the store. Cleanup purposes.
        /// </summary>
        /// <param name="certificateName"></param>
        /// <param name="certificateStoreLocation"></param>
        /// <returns></returns>
        internal static void RemoveCertificate(string certificateName, StoreLocation certificateStoreLocation)
        {
            Assert.False(string.IsNullOrWhiteSpace(certificateName), "FAILED: certificateName should not be null or empty.");

            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, certificateStoreLocation);
                certStore.Open(OpenFlags.ReadWrite);
                X509Certificate2Collection certificateCollection = certStore.Certificates.Find(X509FindType.FindBySubjectName, certificateName, validOnly: false);

                if (certificateCollection != null && certificateCollection.Count > 0)
                {
                    certStore.RemoveRange(certificateCollection);
                }
            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }
        }
    }
}
