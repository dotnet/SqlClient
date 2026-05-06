// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Microsoft.Data.SqlClient.Tests.Common.Fixtures;

/// <summary>
/// Provides a base class for managing the creation, storage, and disposal of X.509 certificates used in cryptographic
/// operations. This class is designed to facilitate scenarios where certificates need to be programmatically generated,
/// added to certificate stores, and cleaned up after use.
/// </summary>
/// <remarks>
/// This class includes functionality for creating self-signed certificates with specific configurations,
/// adding certificates to system certificate stores, and ensuring proper cleanup of certificates to avoid resource
/// leaks. It is intended to be used as a base class for test fixtures or other scenarios requiring temporary
/// certificate management.
/// <para>The class implements <see cref="IDisposable"/> to ensure that any certificates added to
/// certificate stores are removed and properly disposed of when the object is no longer needed.</para>
/// </remarks>
public abstract class CertificateFixtureBase : IDisposable
{
    /// <summary>
    /// Certificates must be created using this provider. Certificates created by PowerShell
    /// using another provider aren't accessible from RSACryptoServiceProvider, which means
    /// that we could not roundtrip between SqlColumnEncryptionCertificateStoreProvider and
    /// SqlColumnEncryptionCspProvider.
    /// </summary>
    private const string CspProviderName = "Microsoft Enhanced RSA and AES Cryptographic Provider";

    private sealed class CertificateStoreContext
    {
        public List<X509Certificate2> Certificates { get; }

        public StoreLocation Location { get; }

        public StoreName Name { get; }

        public CertificateStoreContext(StoreLocation location, StoreName name)
        {
            Certificates = new List<X509Certificate2>();
            Location = location;
            Name = name;
        }
    }

    private readonly List<CertificateStoreContext> _certificateStoreModifications = new List<CertificateStoreContext>();

    protected X509Certificate2 CreateCertificate(string subjectName, IEnumerable<string> dnsNames, IEnumerable<string> ipAddresses, bool forceCsp = false)
    {
        // This will always generate a certificate with:
        // * Start date: 24hrs ago
        // * End date: 24hrs in the future
        // * Subject: {subjectName}
        // * Subject alternative names: {subjectName}, {dnsNames}, {ipAddresses}
        // * Public key: 2048-bit RSA
        // * Hash algorithm: SHA256
        // * Key usage: digital signature, key encipherment
        // * Enhanced key usage: server authentication, client authentication
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddDays(1);
        byte[] passwordBytes = new byte[32];
        string? password = null;
        Random rnd = new Random();

        rnd.NextBytes(passwordBytes);
        password = Convert.ToBase64String(passwordBytes);
#if NET
        X500DistinguishedNameBuilder subjectBuilder = new X500DistinguishedNameBuilder();
        SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
        RSA rsaKey = CreateRSA(forceCsp);

        subjectBuilder.AddCommonName(subjectName);

        sanBuilder.AddDnsName(subjectName);
        foreach (string dnsName in dnsNames)
        {
            sanBuilder.AddDnsName(dnsName);
        }
        foreach (string ipAddress in ipAddresses)
        {
            sanBuilder.AddIpAddress(System.Net.IPAddress.Parse(ipAddress));
        }

        CertificateRequest request = new CertificateRequest(subjectBuilder.Build(), rsaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection() { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, true));

        request.CertificateExtensions.Add(sanBuilder.Build());

        // Generate an ephemeral certificate, then export it and return it as a new certificate with the correct key storage flags set.
        // This is to ensure that it's imported into the certificate stores with its private key.
        using (X509Certificate2 ephemeral = request.CreateSelfSigned(notBefore, notAfter))
        {
            X509KeyStorageFlags keyStorageFlags = OperatingSystem.IsMacOS()
                ? X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable
                : X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable;

#if NET9_0_OR_GREATER
                return X509CertificateLoader.LoadPkcs12(
                    ephemeral.Export(X509ContentType.Pkcs12, password),
                    password,
                    keyStorageFlags,
                    new Pkcs12LoaderLimits(Pkcs12LoaderLimits.Defaults) 
                    {
                        PreserveStorageProvider = true,
                        PreserveKeyName = true
                    });
#else
            return new X509Certificate2(
                ephemeral.Export(X509ContentType.Pkcs12, password),
                password,
                keyStorageFlags);
#endif
        }
#else
            // The CertificateRequest API is available in .NET Core, but was only added to .NET Framework 4.7.2; it thus can't be used in the test projects.
            // Instead, fall back to running a PowerShell script which calls New-SelfSignedCertificate. This cmdlet also adds the certificate to a specific,
            // certificate store, so remove it from there.
            // Normally, the PowerShell script will return zero and print the base64-encoded certificate to stdout. If there's an exception, it'll return 1 and
            // print the message instead.
            const string PowerShellCommandTemplate = @"$notBefore = [DateTime]::ParseExact(""{0}"", ""O"", $null)
$notAfter = [DateTime]::ParseExact(""{1}"", ""O"", $null)
$subject = ""CN={2}""
$sAN = @({3})

try
{{
    $x509 = PKI\New-SelfSignedCertificate -Subject $subject -TextExtension $sAN -KeyLength 2048 -KeyAlgorithm RSA `
        -CertStoreLocation ""Cert:\CurrentUser\My"" -NotBefore $notBefore -NotAfter $notAfter `
        -KeyExportPolicy Exportable -HashAlgorithm SHA256 -Provider ""{5}"" -KeySpec KeyExchange

    if ($x509 -eq $null)
    {{ throw ""Certificate was null!"" }}

    $exportedArray = $x509.Export(""Pkcs12"", ""{4}"")
    Write-Output $([Convert]::ToBase64String($exportedArray))

    Remove-Item ""Cert:\CurrentUser\My\$($x509.Thumbprint)""

    exit 0
}}
catch [Exception]
{{
    Write-Output $_.Exception.Message
    exit 1
}}";
            const int PowerShellCommandTimeout = 15_000;

            string sanString = string.Empty;
            bool hasSans = false;

            foreach (string dnsName in dnsNames)
            {
                sanString += string.Format("DNS={0}&", dnsName);
                hasSans = true;
            }
            foreach (string ipAddress in ipAddresses)
            {
                sanString += string.Format("IPAddress={0}&", ipAddress);
                hasSans = true;
            }

            sanString = hasSans ? "\"2.5.29.17={text}" + sanString.Substring(0, sanString.Length - 1) + "\"" : string.Empty;

            string formattedCommand = string.Format(PowerShellCommandTemplate, notBefore.ToString("O"), notAfter.ToString("O"), subjectName, sanString, password, CspProviderName);

            ProcessStartInfo startInfo = new()
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                // Pass the Base64-encoded command to remove the need to escape quote marks
                Arguments = "-EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(formattedCommand)),
                // Run as Administrator, since we're manipulating the system
                // certificate store.
                Verb = "RunAs",
                LoadUserProfile = true
            };

            // This command sometimes fails with:
            //
            //   Access is denied. 0x80070005 (WIN32: 5 ERROR_ACCESS_DENIED)
            //
            // We will retry it a few times with a short delay to avoid spurious
            // failures in CI pipeline runs.
            //
            // See ADO issue for more details:
            //
            //  Issue 34304: #3223 Fix Functional test failures in CI
            //
            //  https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/34304
            //
            // Delay 5 seconds between retries, and retry 3 times.
            const int delay = 5;
            const int retries = 3;

            string commandOutput = string.Empty;

            for (int attempt = 1; attempt <= retries; ++attempt)
            {
                using Process psProcess = new() { StartInfo = startInfo };
            
                psProcess.Start();
                commandOutput = psProcess.StandardOutput.ReadToEnd();

                if (!psProcess.WaitForExit(PowerShellCommandTimeout))
                {
                    psProcess.Kill();
                    throw new Exception("Process did not complete in time, exiting.");
                }

                // Process completed successfully if it had an exit code of zero, the command output will be the base64-encoded certificate
                var code = psProcess.ExitCode;
                if (code == 0)
                {
                    return new X509Certificate2(Convert.FromBase64String(commandOutput), password, X509KeyStorageFlags.Exportable);
                }
                
                Console.WriteLine(
                    $"PowerShell command failed with exit code {code} on " +
                    $"attempt {attempt} of {retries}; " +
                    $"retrying in {delay} seconds...");
                
                Thread.Sleep(TimeSpan.FromSeconds(delay));
            }
                
            throw new Exception(
                "PowerShell command raised exception: " +
                $"{commandOutput}; command was: {formattedCommand}");
#endif
    }

#if NET
    private static RSA CreateRSA(bool forceCsp)
    {
        const int KeySize = 2048;
        const int CspProviderType = 24;

        return forceCsp && OperatingSystem.IsWindows()
            ? new RSACryptoServiceProvider(KeySize, new CspParameters(CspProviderType, CspProviderName, Guid.NewGuid().ToString()))
            : RSA.Create(KeySize);
    }
#endif

    protected void AddToStore(X509Certificate2 cert, StoreLocation storeLocation, StoreName storeName)
    {
        CertificateStoreContext? storeContext = _certificateStoreModifications.Find(csc => csc.Location == storeLocation && csc.Name == storeName);

        if (storeContext == null)
        {
            storeContext = new(storeLocation, storeName);
            _certificateStoreModifications.Add(storeContext);
        }

        using X509Store store = new X509Store(storeContext.Name, storeContext.Location);

        store.Open(OpenFlags.ReadWrite);
        if (store.Certificates.Contains(cert))
        {
            store.Remove(cert);
        }
        store.Add(cert);

        storeContext.Certificates.Add(cert);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        foreach (CertificateStoreContext storeContext in _certificateStoreModifications)
        {
            using X509Store store = new X509Store(storeContext.Name, storeContext.Location);

            try
            {
                store.Open(OpenFlags.ReadWrite);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (X509Certificate2 cert in storeContext.Certificates)
            {
                try
                {
                    if (store.Certificates.Contains(cert))
                    {
                        store.Remove(cert);
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                cert.Dispose();
            }

            storeContext.Certificates.Clear();
        }
    }
}
