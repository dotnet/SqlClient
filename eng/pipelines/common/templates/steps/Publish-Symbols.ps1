<#
.SYNOPSIS
    Publishes symbols to the Microsoft symbol publishing service (SymWeb/MSDL).

.DESCRIPTION
    This script uploads and publishes debug symbols (.pdb files) to internal and/or public
    Microsoft symbol servers via the Symbols Publishing Pipeline REST API.

    It performs three steps:
      1. Acquires a bearer token from Azure CLI for the symbol publishing service.
      2. Registers a unique request name with the publishing service.
      3. Submits the request to publish symbols to the specified servers.
      4. Queries the publishing status for confirmation.

    For more details on the Symbols Publishing Pipeline, see:
    https://www.osgwiki.com/wiki/Symbols_Publishing_Pipeline_to_SymWeb_and_MSDL

.PARAMETER PublishServer
    The hostname prefix of the symbol publishing service. This value is prepended to
    '.trafficmanager.net' to construct the service base URL.

.PARAMETER PublishTokenUri
    The resource URI used to acquire a bearer token from Azure CLI
    (via 'az account get-access-token --resource <uri>').

.PARAMETER PublishProjectName
    The project name registered with the symbol publishing service (decided during onboarding).

.PARAMETER ArtifactName
    The base name for the publishing request. A job attempt suffix is appended to ensure
    uniqueness across retries.

.PARAMETER JobAttempt
    The job attempt number (from Azure Pipelines' System.JobAttempt). Appended to ArtifactName
    to create a unique request name per execution.

.PARAMETER PublishToInternal
    Whether to publish symbols to the internal symbol server. Defaults to $true.

.PARAMETER PublishToPublic
    Whether to publish symbols to the public symbol server. Defaults to $true.

.EXAMPLE
    .\Publish-Symbols.ps1 `
        -PublishServer "mysymbolserver" `
        -PublishTokenUri "https://login.microsoftonline.com/..." `
        -PublishProjectName "Microsoft.Data.SqlClient.SNI" `
        -ArtifactName "mds_symbols_MyProject_dotnet-sqlclient_main_6.1.5_abc123" `
        -JobAttempt 1

    Publishes symbols to both internal and public servers using the specified parameters.

.EXAMPLE
    .\Publish-Symbols.ps1 `
        -PublishServer "mysymbolserver" `
        -PublishTokenUri "https://login.microsoftonline.com/..." `
        -PublishProjectName "Microsoft.Data.SqlClient.SNI" `
        -ArtifactName "mds_symbols_MyProject_dotnet-sqlclient_main_6.1.5_abc123" `
        -JobAttempt 2 `
        -PublishToPublic $false

    Publishes symbols to the internal server only (retry attempt 2).

.NOTES
    File Name : Publish-Symbols.ps1
    Requires  : Azure CLI (az) must be installed and authenticated.
    Called by : publish-symbols-step.yml (Azure Pipelines template)

    Publishing status codes returned by the service:

    PublishingStatus:
      0 - NotRequested: The request has not been requested to publish.
      1 - Submitted: The request is submitted to be published.
      2 - Processing: The request is still being processed.
      3 - Completed: Processing finished. Check PublishingResult for details.

    PublishingResult:
      0 - Pending: The request has not completed or has not been requested.
      1 - Succeeded: The request published successfully.
      2 - Failed: The request failed to publish.
      3 - Cancelled: The request was cancelled.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Hostname prefix of the symbol publishing service (prepended to .trafficmanager.net).")]
    [ValidateNotNullOrEmpty()]
    [string]$PublishServer,

    [Parameter(Mandatory = $true, HelpMessage = "Resource URI for acquiring a bearer token via Azure CLI.")]
    [ValidateNotNullOrEmpty()]
    [string]$PublishTokenUri,

    [Parameter(Mandatory = $true, HelpMessage = "Project name registered with the symbol publishing service.")]
    [ValidateNotNullOrEmpty()]
    [string]$PublishProjectName,

    [Parameter(Mandatory = $true, HelpMessage = "Base artifact name for the publishing request.")]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactName,

    [Parameter(Mandatory = $true, HelpMessage = "Job attempt number (System.JobAttempt) for uniqueness.")]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$JobAttempt,

    [Parameter(Mandatory = $false, HelpMessage = "Publish symbols to the internal symbol server.")]
    [bool]$PublishToInternal = $true,

    [Parameter(Mandatory = $false, HelpMessage = "Publish symbols to the public symbol server.")]
    [bool]$PublishToPublic = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Log input parameters ---
Write-Host "=== Publish Symbols Parameters ==="
Write-Host "PublishServer:      ${PublishServer}"
Write-Host "PublishTokenUri:    ${PublishTokenUri}"
Write-Host "PublishProjectName: ${PublishProjectName}"
Write-Host "ArtifactName:       ${ArtifactName}"
Write-Host "JobAttempt:         ${JobAttempt}"
Write-Host "PublishToInternal:  ${PublishToInternal}"
Write-Host "PublishToPublic:    ${PublishToPublic}"
Write-Host "=================================="

# --- Build request name and URLs ---
$requestName = "${ArtifactName}_${JobAttempt}"
$baseUrl     = "https://${PublishServer}.trafficmanager.net/projects/${PublishProjectName}"
$registerUrl = "${baseUrl}/requests"
$requestUrl  = "${baseUrl}/requests/${requestName}"

Write-Host "=== Constructed URLs ==="
Write-Host "Request Name: ${requestName}"
Write-Host "Base URL:     ${baseUrl}"
Write-Host "Register URL: ${registerUrl}"
Write-Host "Request URL:  ${requestUrl}"
Write-Host "========================"

# --- Step 1: Acquire token ---
Write-Host ">  1. Acquiring symbol publishing token..."
$symbolPublishingToken = az account get-access-token --resource ${PublishTokenUri} --query accessToken -o tsv
if ($LASTEXITCODE -ne 0) {
    throw "Failed to acquire symbol publishing token via Azure CLI (exit code: ${LASTEXITCODE})."
}
Write-Host ">  1. Symbol publishing token acquired."

$authHeaders = @{ Authorization = "Bearer ${symbolPublishingToken}" }

# --- Step 2: Register request name ---
Write-Host ">  2. Registering request name..."
$requestNameRegistrationBody = @{ requestName = $requestName } | ConvertTo-Json -Compress
try {
    Invoke-RestMethod -Method POST -Uri ${registerUrl} -Headers ${authHeaders} -ContentType "application/json" -Body ${requestNameRegistrationBody}
} catch {
    Write-Error "Failed to register request name. URI: ${registerUrl} | Body: ${requestNameRegistrationBody} | Error: $_"
    throw
}
Write-Host ">  2. Request name registered successfully."

# --- Step 3: Publish symbols ---
Write-Host ">  3. Submitting request to publish symbols..."
$publishSymbolsBody = @{
    publishToInternalServer = $PublishToInternal
    publishToPublicServer   = $PublishToPublic
} | ConvertTo-Json -Compress
Write-Host "Publishing symbols request body: ${publishSymbolsBody}"
try {
    Invoke-RestMethod -Method POST -Uri ${requestUrl} -Headers ${authHeaders} -ContentType "application/json" -Body ${publishSymbolsBody}
} catch {
    Write-Error "Failed to publish symbols. URI: ${requestUrl} | Body: ${publishSymbolsBody} | Error: $_"
    throw
}
Write-Host ">  3. Request to publish symbols submitted successfully."

# --- Step 4: Check status ---
Write-Host ">  4. Checking the status of the request..."
try {
    $status = Invoke-RestMethod -Method GET -Uri ${requestUrl} -Headers ${authHeaders} -ContentType "application/json"
    $status
} catch {
    Write-Error "Failed to check request status. URI: ${requestUrl} | Error: $_"
    throw
}

Write-Host ""
Write-Host "Use below tables to interpret the xxxServerStatus and xxxServerResult fields from the response."
Write-Host ""
Write-Host "PublishingStatus"
Write-Host "-----------------"
Write-Host "0  NotRequested - The request has not been requested to publish."
Write-Host "1  Submitted    - The request is submitted to be published."
Write-Host "2  Processing   - The request is still being processed."
Write-Host "3  Completed    - Processing finished. Check PublishingResult for details."
Write-Host ""
Write-Host "PublishingResult"
Write-Host "-----------------"
Write-Host "0  Pending   - The request has not completed or has not been requested."
Write-Host "1  Succeeded - The request published successfully."
Write-Host "2  Failed    - The request failed to publish."
Write-Host "3  Cancelled - The request was cancelled."
