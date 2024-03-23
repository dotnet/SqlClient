# This script is used to generate the assign the self-signed certificate to the first sql server instance found in the list.
# the self-signed certificate is also exported in the current working directory which is used by the server certificate validation tests
# NOTE: this script must be run with administrative privileges; otherwise, it will fail because it modifies certificate private key read permissions,
# it moves self-signed certificate to the root trust store, and it restarts the sql server instance service.

# A helper function to set Private Key permissions
# ref: https://stackoverflow.com/a/31175117/85936
function Set-PrivateKeyPermissions {
param(
[Parameter(Mandatory=$true)][string]$thumbprint,
[Parameter(Mandatory=$false)][string]$account = "NT AUTHORITY\NETWORK SERVICE"
)
	#Open Certificate store and locate certificate based on provided thumbprint
	$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("My","LocalMachine")
	$store.Open("ReadWrite")
	$cert = $store.Certificates | where {$_.Thumbprint -eq $thumbprint}

	#Create new CSP object based on existing certificate provider and key name
	$csp = New-Object System.Security.Cryptography.CspParameters($cert.PrivateKey.CspKeyContainerInfo.ProviderType, $cert.PrivateKey.CspKeyContainerInfo.ProviderName, $cert.PrivateKey.CspKeyContainerInfo.KeyContainerName)

	# Set flags and key security based on existing cert
	$csp.Flags = "UseExistingKey","UseMachineKeyStore"
	$csp.CryptoKeySecurity = $cert.PrivateKey.CspKeyContainerInfo.CryptoKeySecurity
	$csp.KeyNumber = $cert.PrivateKey.CspKeyContainerInfo.KeyNumber

	# Create new access rule - could use parameters for permissions, but I only needed GenericRead
	$access = New-Object System.Security.AccessControl.CryptoKeyAccessRule($account,"GenericRead","Allow")
	# Add access rule to CSP object
	$csp.CryptoKeySecurity.AddAccessRule($access)

	#Create new CryptoServiceProvider object which updates Key with CSP information created/modified above
	$rsa2 = New-Object System.Security.Cryptography.RSACryptoServiceProvider($csp)

	#Close certificate store
	$store.Close()
}

$FQDN = ([System.Net.Dns]::GetHostByName($env:computerName).HostName)
$Subject = "CN=$FQDN"
$Env:TDS8_EXTERNAL_IP = (Invoke-WebRequest ifconfig.me/ip).Content.Trim()
$Env:TDS8_Test_Certificate_FriendlyName = "TDS8SqlClientCert"
$Env:TDS8_Test_Certificate_MismatchFriendlyName = "TDS8SqlClientCertMismatch"
$MismatchSubject = "CN=$Env:TDS8_EXTERNAL_IP"

$existingCert = (Get-ChildItem Cert:\LocalMachine\My | where-object -Property Subject -eq -Value $Subject)
$existingMismatchCert = (Get-ChildItem Cert:\LocalMachine\My | where-object -Property Subject -eq -Value $MismatchSubject)

Write-Host "Make self-signed certificates in the Personal cert store"

if ([string]::IsNullOrEmpty($existingCert)) {
	New-SelfSignedCertificate -Subject $Subject -KeyAlgorithm RSA -KeyLength 2048 -CertStoreLocation "cert:\LocalMachine\My" -FriendlyName $Env:TDS8_Test_Certificate_FriendlyName -TextExtension @("2.5.29.17={text}DNS=localhost&IPAddress=127.0.0.1&IPAddress=::1") -KeyExportPolicy Exportable -HashAlgorithm "SHA256" -Type SSLServerAuthentication -Provider "Microsoft RSA SChannel Cryptographic Provider" | Select 
} else {
	Write-Host "The cert with $Subject already exists"
}

if ([string]::IsNullOrEmpty($existingMismatchCert)) {
	New-SelfSignedCertificate -Subject $MismatchSubject -KeyAlgorithm RSA -KeyLength 2048 -CertStoreLocation "cert:\LocalMachine\My" -FriendlyName $Env:TDS8_Test_Certificate_MismatchFriendlyName -TextExtension @("2.5.29.17={text}DNS=$FQDN&IPAddress=127.0.0.1&IPAddress=::1&upn=$FQDN") -KeyExportPolicy Exportable -HashAlgorithm "SHA256" -Type SSLServerAuthentication -Provider "Microsoft RSA SChannel Cryptographic Provider" | Select 
} else {
	Write-Host "The cert with $MismatchSubject already exists"
}

$certThumbprint = (Get-ChildItem Cert:\LocalMachine\My | where-object -Property Subject -eq -Value $Subject).thumbprint
$mismatchCertThumbprint = (Get-ChildItem Cert:\LocalMachine\My | where-object -Property Subject -eq -Value $MismatchSubject).thumbprint

$cert = Get-ChildItem Cert:\LocalMachine\My\$certThumbprint

$mismatchcert = Get-ChildItem Cert:\LocalMachine\My\$mismatchCertThumbprint

$PASSWORD = ConvertTo-SecureString -String "PLACEHOLDER" -Force -AsPlainText

# creates the certificates in the same directory as this script
$outputDirectory = $PSScriptRoot

$Env:TDS8_Test_Certificate_On_FileSystem = "$outputDirectory\sqlservercert.cer"
$Env:TDS8_Test_MismatchCertificate_On_FileSystem = "$outputDirectory\mismatchsqlservercert.cer"
$Env:TDS8_Test_InvalidCertificate_On_FileSystem = "$outputDirectory\sqlservercert.pfx"

Write-Host "Export certificate in pfx"
Export-PfxCertificate -Cert "Cert:\LocalMachine\My\$certThumbprint" -FilePath $Env:TDS8_Test_InvalidCertificate_On_FileSystem -Password $PASSWORD -Force

Write-Host "Export certificate in cer"
Export-Certificate -Cert "Cert:\LocalMachine\My\$certThumbprint" -FilePath $Env:TDS8_Test_Certificate_On_FileSystem -Force
Export-Certificate -Cert "Cert:\LocalMachine\My\$mismatchCertThumbprint" -FilePath $Env:TDS8_Test_MismatchCertificate_On_FileSystem -Force

Write-Host "Set private key permissions for the certificate "

# The service name and the account running the service are the same. Therefore, we get the instance name from registry.
$SqlInstanceNames = (Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server" -name "InstalledInstances").InstalledInstances
$SqlInstanceName = If ($SqlInstanceNames -is [array]) { $SqlInstanceNames[0] } else { $SqlInstanceNames }

Set-PrivateKeyPermissions -thumbprint $certThumbprint -account "NT Service\$SqlInstanceName"
Set-PrivateKeyPermissions -thumbprint $mismatchCertThumbprint -account "NT Service\$SqlInstanceName"

Write-Host "Importing the certificate to the trusted root certificate authorities on the local machine"

# Add the self-signed certificate into the trusted root store
# Move-Item -path cert:\LocalMachine\My\$certThumbprint -Destination cert:\LocalMachine\Root\
# Move-Item -path cert:\LocalMachine\My\$mismatchCertThumbprint -Destination cert:\LocalMachine\Root\
Import-Certificate -FilePath $Env:TDS8_Test_Certificate_On_FileSystem -CertStoreLocation cert:\LocalMachine\Root\
Import-Certificate -FilePath $Env:TDS8_Test_MismatchCertificate_On_FileSystem -CertStoreLocation cert:\LocalMachine\Root\

Write-Host "Set the Sql Server Instance to reference the self-signed certificate"

# Retrieves the registry name e.g. MSSQL15.MSSQLSERVER
$SqlInstanceRegName = (Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL" -name $SqlInstanceName).$SqlInstanceName

# This environment variable is used in the test
$Env:TDS8_Test_SqlServerVersion = ($SqlInstanceRegName.split("."))[0]
Write-Host "The sql server instance version is $Env:SqlServerVersion"
# The alternative to get the version
# Import-Module "sqlps"
# $Env:TDS8_Test_SqlServerVersion = $(Invoke-sqlcmd "SELECT @@version").Column1


# This is for Sql Server 2019 i.e. MSSQL15 with instance name MSSQLSERVER
# If it's running Sql Server 2022 i.e. MSSQL16 with instance name MSSQLSERVER01 if both 2019 and 2022 are on the same machine
$SqlInstanceRegPath = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$SqlInstanceRegName\MSSQLServer\SuperSocketNetLib"

$prevCertificate = (Get-ItemProperty -Path $SqlInstanceRegPath -name "Certificate").Certificate

if (Test-Path $SqlInstanceRegPath) {
	Write-Host "The certificate for $SqlInstanceName was previously set to $prevCertificate will be replaced."
	Set-ItemProperty -Path $SqlInstanceRegPath -name "Certificate" -value $mismatchCertThumbprint -Type String -Force
} else {
	New-ItemProperty -Path $SqlInstanceRegPath -name "Certificate" -value $mismatchCertThumbprint -Type String -Force
}

if ($?) {
	Write-Host "The certificate has been set to $mismatchCertThumbprint"
	
	Write-Host "Restarting the Sql Server Instance"
	
	Restart-Service -Name "$SqlInstanceName"

	Write-Host "Verifies the certificate is installed"
	Import-Module "sqlps"
	Invoke-sqlcmd "EXEC sp_readerrorlog 0, 1, 'encryption'"
} else {
	Write-Host "Failed to set the certificate."
	Exit 1
}

