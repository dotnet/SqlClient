# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.
# Script: Invoke-SqlServerCertificateCommand# 
# Author: SqlClient Team
# Date: March 20, 2024
# Comments: This scripts creates SSL Self-Signed Certificate for TestTdsServer in pfx format.
# This script is not intended to be used in any production environments.

param ($OutDir)

function Invoke-SqlServerCertificateCommand {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [string] $certificateName = "localhostcert.cer",
    [string] $myCertStoreLocation = "Cert:\LocalMachine\My",
    [string] $rootCertStoreLocation = "Cert:\LocalMachine\Root",
    [string] $sqlAliasName = "SQLAliasName",
    [string] $localhost = "localhost",
    [string] $LoopBackIPV4 = "127.0.0.1",
    [string] $LoopBackIPV6 = "::1"
  )
  Write-Output "Certificate generation started..."

  # Change directory to where the tests are
  Write-Output "Change directory to $OutDir ..."
  cd $OutDir
  pwd

  try {
    # Get FQDN of the machine
    Write-Output "Get FQDN of the machine..."
    $fqdn = [System.Net.Dns]::GetHostByName(($env:computerName)).HostName
    Write-Output "FQDN = $fqdn"

    $OS = [System.Environment]::OSVersion.Platform
    Write-Output "Operating System is $OS"

    # Create a self-signed certificate
    if ($OS -eq "Unix") {
        chmod 777 $OutDir
        # Install OpenSSL module
        Install-Module -Name OpenSSL -Repository PSGallery -Force
        # Show version of OpenSSL just to make sure it is installed
        openssl version

        # Create self signed certificate using openssl
        Write-Output "Creating certificate for linux..."
        if ($fqdn.length -gt 64) {
            $machineId = $fqdn.Substring(0,15)
            openssl req -x509 -newkey rsa:4096 -sha256 -days 1095 -nodes -keyout $OutDir/localhostcert.key -out $OutDir/localhostcert.cer -subj "/CN=$machineId" -addext "subjectAltName=DNS:$fqdn,DNS:localhost,IP:127.0.0.1,IP:::1"
        }
        else {
            openssl req -x509 -newkey rsa:4096 -sha256 -days 1095 -nodes -keyout $OutDir/localhostcert.key -out $OutDir/localhostcert.cer -subj "/CN=$fqdn" -addext "subjectAltName=DNS:$fqdn,DNS:localhost,IP:127.0.0.1,IP:::1"
        }
        chmod 777 $OutDir/localhostcert.key $OutDir/localhostcert.cer
        # Copy the certificate to the clientcert folder
        cp $OutDir/localhostcert.cer $OutDir/clientcert/
        # Export the certificate to pfx
        Write-Output "Exporting certificate to pfx..."
        openssl pkcs12 -export -in $OutDir/localhostcert.cer -inkey $OutDir/localhostcert.key -out $OutDir/localhostcert.pfx -password pass:nopassword
        chmod 777 $OutDir/localhostcert.pfx

        Write-Output "Converting certificate to pem..."
        # Create pem from cer
        cp $OutDir/localhostcert.cer $OutDir/localhostcert.pem
        chmod 777 $OutDir/localhostcert.pem

        # Add trust to the pem certificate
        Write-Output "Adding trust to pem certificate..."
        openssl x509 -trustout -addtrust "serverAuth" -in $OutDir/localhostcert.pem

        # Import the certificate to the Root store ------------------------------------------------------------------------------
        # NOTE:  The process must have root privileges to add the certificate to the Root store. If not, then use  
        #        "chmod 777 /usr/local/share/ca-certificates" to give read, write and execute privileges to anyone on that folder 
        # Copy the certificate to /usr/local/share/ca-certificates folder while changing the extension to "crt". 
        # Only certificates with extension "crt" gets added for some reason.
        Write-Output "Copy the pem certificate to /usr/local/share/ca-certificates folder..."
        cp $OutDir/localhostcert.pem /usr/local/share/ca-certificates/localhostcert.crt

        # Add trust to the mismatched certificate as well
        $ openssl x509 -in $OutDir/mismatchedcert.cer -inform der -out $OutDir/mismatchedcert.pem
        # Copy the mismatched certificate to the clientcert folder
        cp $OutDir/mismatchedcert.cer $OutDir/clientcert/
        openssl x509 -trustout -addtrust "serverAuth" -in $OutDir/mismatchedcert.pem
        cp $OutDir/mismatchedcert.pem /usr/local/share/ca-certificates/mismatchedcert.crt

        # enable certificate as CA certificate
        dpkg-reconfigure ca-certificates -f noninteractive -p critical

        # Update the certificates store
        Write-Output "Updating the certificates store..."
        update-ca-certificates -v
    } else {
        Write-Output "Creating a self-signed certificate..."
        $params = @{
          Type              = "SSLServerAuthentication"
          Subject           = "CN=$fqdn"
          KeyAlgorithm      = "RSA"
          KeyLength         = 4096
          HashAlgorithm     = "SHA256"
          TextExtension     = "2.5.29.37={text}1.3.6.1.5.5.7.3.1", "2.5.29.17={text}DNS=$fqdn&DNS=$localhost&IPAddress=$LoopBackIPV4&DNS=$sqlAliasName&IPAddress=$LoopBackIPV6"
          NotAfter          = (Get-Date).AddMonths(36)
          KeySpec           = "KeyExchange"
          Provider          = "Microsoft RSA SChannel Cryptographic Provider"
          CertStoreLocation = $myCertStoreLocation
          FriendlyName      = "TestTDSServerCertificate"
        }

        $certificate = New-SelfSignedCertificate @params
        Write-Output "Certificate created successfully"
        Write-Output "Certificate Thumbprint: $($certificate.Thumbprint)"

        # Export the certificate to a file
        Write-Output "Exporting the certificate to a file..."
        Export-Certificate -Cert $certificate -FilePath "$OutDir/$certificateName" -Type CERT

        # Copy the certificate to the clientcert folder
        copy $OutDir/$certificateName $OutDir/clientcert/
        copy $OutDir/mismatchedcert.cer $OutDir/clientcert/

        # Import the certificate to the Root store
        Write-Output "Importing the certificate to the Root store..."
        $params = @{
          FilePath          = "$OutDir/$certificateName"
          CertStoreLocation = $rootCertStoreLocation
        }
        Import-Certificate @params

        Write-Output "Converting certificate to pfx..."
        Write-Output "Cert:\LocalMachine\my\$($certificate.Thumbprint)"

        # PSScriptAnalyzer rule suppression
        # Suppress the 'PSAvoidUsingConvertToSecureStringWithPlainText' rule for the next line as this is a test certificate with no password
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSAvoidUsingConvertToSecureStringWithPlainText", "", Justification="Test certificate with no real secret")]
        $pwd = ConvertTo-SecureString -String 'nopassword' -Force -AsPlainText
        
        # Export the certificate to a pfx format
        Export-PfxCertificate -Password $pwd -FilePath "$OutDir\localhostcert.pfx" -Cert "Cert:\LocalMachine\my\$($certificate.Thumbprint)"

        # Write the certificate thumbprint to a file
        echo $certificate.Thumbprint | Out-File -FilePath "$OutDir\thumbprint.txt" -Encoding ascii
    } 

    Write-Output "Done creating pfx certificate..."
  }
  catch {
    $e = $_.Exception
    $msg = $e.Message
    while ($e.InnerException) {
      $e = $e.InnerException
      $msg += "`n" + $e.Message
    }

    Write-Output "Certificate generation was not successful. $msg"
  }

  Write-Output "Certificate generation task completed."
}

Invoke-SqlServerCertificateCommand
