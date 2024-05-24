# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.
# Script: removecert.ps1 
# Author: SqlClient Team
# Date: May 24, 2024
# Comments: This script deletes the SSL Self-Signed Certificate from Linux certificate store.
# This script is not intended to be used in any production environments.

param ($OutDir)

# Delete all certificates
rm $OutDir/clientcer/*.cer
rm $OutDir/localhostcert.pem
rm $OutDir/mismatchedcert.pem
rm /usr/local/share/ca-certificates/localhostcert.crt
rm /usr/local/share/ca-certificates/mismatchedcert.crt

# Update the certificates store
Write-Output "Updating the certificates store..."
update-ca-certificates -v
