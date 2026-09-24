<#
.SYNOPSIS
    Publishes symbols to the Microsoft symbol publishing service (SymWeb/MSDL).

.DESCRIPTION
    This script is Step 2 of the two-step symbols publishing process. It requests
    the Symbols Publishing Pipeline to publish previously uploaded PDB files to
    internal (SymWeb) and/or public (MSDL) Microsoft symbol servers.

    The two-step process:
      Step 1 (PublishSymbols@2 task in publish-symbols-step.yml):
        Uploads PDB files to the Azure DevOps symbol store under a unique
        artifact name (SymbolsArtifactName). This stores the symbols but does
        NOT make them available on SymWeb or MSDL.

      Step 2 (this script):
        Calls the Symbols Publishing Pipeline REST API to request that the
        uploaded symbols be published to the symbol servers.

    Step 2 depends on Step 1: the -ArtifactName parameter MUST match the
    SymbolsArtifactName used by the PublishSymbols@2 upload task so that
    both steps reference the same uploaded artifact.

    This script performs four sub-steps:
      1. Acquires a bearer token from Azure CLI for the symbol publishing service.
      2. Registers a unique request name with the publishing service.
      3. Submits the request to publish symbols to the specified servers.
      4. Queries the publishing status for confirmation.

    Diagnostics emitted on every run:
      - The commands executed. Secrets are always redacted; no switch relaxes this.
      - The token's SHA-256 fingerprint and its header/payload claims (aud, appid, oid,
        tid, roles, exp, ...), which identify the principal and audience the service sees.
        The signature segment is never decoded or logged.
      - On a failed call, the HTTP status code and any service correlation identifiers
        (such as mise-correlation-id) alongside the response body.

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
    The name of the publishing request. This must match the SymbolsArtifactName used by
    the PublishSymbols@2 upload task so that upload and publish reference the same artifact.

.PARAMETER PublishToInternal
    Whether to publish symbols to the internal symbol server. Defaults to $true.

.PARAMETER PublishToPublic
    Whether to publish symbols to the public symbol server. Defaults to $true.

.EXAMPLE
    .\publish-symbols.ps1 `
        -PublishServer "mysymbolserver" `
        -PublishTokenUri "https://login.microsoftonline.com/..." `
        -PublishProjectName "Microsoft.Data.SqlClient.SNI" `
        -ArtifactName "mds_symbols_MyProject_dotnet-sqlclient_main_7.0.0_abc123_1"

    Publishes symbols to both internal and public servers using the specified parameters.

.EXAMPLE
    .\publish-symbols.ps1 `
        -PublishServer "mysymbolserver" `
        -PublishTokenUri "https://login.microsoftonline.com/..." `
        -PublishProjectName "Microsoft.Data.SqlClient.SNI" `
        -ArtifactName "mds_symbols_MyProject_dotnet-sqlclient_main_7.0.0_abc123_2" `
        -PublishToPublic $false

    Publishes symbols to the internal server only (retry attempt 2).

.NOTES
    File Name : publish-symbols.ps1
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

    [Parameter(Mandatory = $true, HelpMessage = "Artifact name for the publishing request (must match PublishSymbols@2 SymbolsArtifactName).")]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactName,

    [Parameter(Mandatory = $false, HelpMessage = "Publish symbols to the internal symbol server.")]
    [bool]$PublishToInternal = $true,

    [Parameter(Mandatory = $false, HelpMessage = "Publish symbols to the public symbol server.")]
    [bool]$PublishToPublic = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Command logging helpers ---
# Every command this script runs is echoed to the log. Secrets within those commands are
# always replaced with $redactedPlaceholder: no switch, parameter or pipeline setting can
# cause a credential to be written to the log. Identity is reported instead through the
# token's fingerprint and claims, which are not secrets.
$redactedPlaceholder = '<redacted>'

function Format-HeaderTable {
    param([hashtable]$Headers)

    $parts = foreach ($key in ($Headers.Keys | Sort-Object)) {
        $value = if ($key -eq 'Authorization') { $redactedPlaceholder } else { $Headers[$key] }
        "${key} = '${value}'"
    }
    return '@{ ' + ($parts -join '; ') + ' }'
}

function Format-RestCommand {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers,
        [AllowNull()][string]$Body
    )

    $command = "Invoke-RestMethod -Method ${Method} -Uri '${Uri}' -Headers $(Format-HeaderTable $Headers) -ContentType 'application/json'"
    if (-not [string]::IsNullOrEmpty($Body)) {
        $command += " -Body '${Body}'"
    }
    return $command
}

function Write-CommandLog {
    param([string]$Command)

    Write-Host "   [command] ${Command}"
}

# --- Token diagnostics ---
# The access token itself is a credential and is never logged. Its SHA-256 fingerprint and
# its header/payload claims are
# not credentials: they identify which principal and audience the service sees, which is what
# an authorization failure turns on. The signature segment is never decoded or logged.

# Claims worth reporting, in the order they are printed. Anything outside this list is skipped
# so that unexpected or personal claims are not written to the log.
$tokenClaimAllowList = @(
    'typ', 'alg', 'kid',
    'ver', 'iss', 'aud', 'tid', 'appid', 'appidacr', 'azp', 'azpacr',
    'oid', 'sub', 'idtyp', 'roles', 'scp',
    'iat', 'nbf', 'exp'
)

# Response headers that carry the service-side correlation identifiers support teams ask for.
$diagnosticResponseHeaders = @(
    'mise-correlation-id',
    'x-ms-correlation-request-id',
    'x-ms-request-id',
    'x-ms-activity-id',
    'ActivityId',
    'WWW-Authenticate'
)

function Get-Sha256Hex {
    param([string]$Value)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
    } finally {
        $sha256.Dispose()
    }
    return (($hash | ForEach-Object { $_.ToString('x2') }) -join '')
}

function ConvertFrom-Base64Url {
    param([string]$Value)

    $normalized = $Value.Replace('-', '+').Replace('_', '/')
    switch ($normalized.Length % 4) {
        2 { $normalized += '==' }
        3 { $normalized += '=' }
        1 { throw "Invalid base64url segment length." }
    }
    return [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($normalized))
}

function Format-ClaimValue {
    param([string]$Name, [AllowNull()]$Value)

    if ($Value -is [array]) {
        return ($Value -join ', ')
    }

    if ($Name -in @('iat', 'nbf', 'exp')) {
        $seconds = [int64]0
        if ([int64]::TryParse([string]$Value, [ref]$seconds)) {
            $utc = [System.DateTimeOffset]::FromUnixTimeSeconds($seconds).UtcDateTime.ToString('u')
            return "${Value} (${utc})"
        }
    }

    return [string]$Value
}

function Write-TokenDiagnostics {
    param([string]$Token)

    Write-Host "=== Access Token Diagnostics ==="
    Write-Host "SHA-256: $(Get-Sha256Hex $Token)"
    Write-Host "Length:  $($Token.Length)"

    $segments = $Token.Split('.')
    if ($segments.Count -lt 3) {
        Write-Host "Claims:  unavailable (token is not a three-segment JWT)"
        Write-Host "==============================="
        return
    }

    # Index 0 is the header and index 1 is the payload. Index 2 is the signature and is
    # deliberately never touched.
    foreach ($part in @(@{ Name = 'Header'; Index = 0 }, @{ Name = 'Payload'; Index = 1 })) {
        try {
            $decoded = ConvertFrom-Base64Url $segments[$part.Index] | ConvertFrom-Json
        } catch {
            Write-Host "$($part.Name):  could not be decoded ($_)"
            continue
        }

        Write-Host "$($part.Name):"
        foreach ($claimName in $tokenClaimAllowList) {
            $property = $decoded.PSObject.Properties[$claimName]
            if ($null -eq $property) {
                continue
            }
            Write-Host ("  {0,-9}: {1}" -f $claimName, (Format-ClaimValue -Name $claimName -Value $property.Value))
        }
    }

    Write-Host "==============================="
}

# --- HTTP failure diagnostics ---
# Invoke-RestMethod surfaces only a message, discarding the status code, response headers and
# body. Those carry the correlation identifiers needed to chase a failure with the service
# owners, so they are extracted here and appended to the error that is thrown.
#
# The extraction is deliberately defensive and self-reporting: exception shapes differ between
# PowerShell versions and between transport-level and HTTP-level failures, so the exception type
# is always reported and a missing status is stated explicitly rather than silently omitted. A
# diagnostic must never hide the very information it exists to surface.
function Format-HttpErrorDetail {
    param($ErrorRecord)

    $details = @()

    $exception = $ErrorRecord.Exception
    if ($null -ne $exception) {
        $details += "Exception: $($exception.GetType().FullName)"

        $inner = $null
        try { $inner = $exception.InnerException } catch { }
        if ($null -ne $inner) {
            $details += "Inner exception: $($inner.GetType().FullName): $($inner.Message)"
        }
    }

    # HttpResponseException exposes StatusCode directly and also carries the HttpResponseMessage.
    # Other shapes (for example a transport failure) carry neither, so both are probed.
    $response = $null
    $statusCode = $null
    try { $response = $exception.Response } catch { }
    try { $statusCode = $exception.StatusCode } catch { }

    if ($null -ne $statusCode) {
        $details += "HTTP status: $([int]$statusCode) ${statusCode}"
    } elseif ($null -ne $response) {
        $details += "HTTP status: $([int]$response.StatusCode) $($response.ReasonPhrase)"
    } else {
        # No HTTP response reached us, which usually means the request failed below the HTTP
        # layer (DNS, TCP, TLS or a blocked egress path) rather than being rejected by the service.
        $details += "HTTP status: <none - no HTTP response was received>"
    }

    if ($null -ne $response) {
        foreach ($headerName in $diagnosticResponseHeaders) {
            try {
                $headerValues = $null
                if ($response.Headers.TryGetValues($headerName, [ref]$headerValues)) {
                    $details += "${headerName}: $($headerValues -join ', ')"
                }
            } catch { }
        }
    }

    $errorDetails = $ErrorRecord.ErrorDetails
    if ($null -ne $errorDetails -and -not [string]::IsNullOrWhiteSpace($errorDetails.Message)) {
        $details += "Response body: $($errorDetails.Message)"
    }

    $details += "Error: ${ErrorRecord}"

    return ($details -join ' | ')
}

# --- Log input parameters ---
Write-Host "=== Publish Symbols Parameters ==="
Write-Host "PublishServer:      ${PublishServer}"
Write-Host "PublishTokenUri:    ${PublishTokenUri}"
Write-Host "PublishProjectName: ${PublishProjectName}"
Write-Host "ArtifactName:       ${ArtifactName}"
Write-Host "PublishToInternal:  ${PublishToInternal}"
Write-Host "PublishToPublic:    ${PublishToPublic}"
Write-Host "=================================="



# --- Build request name and URLs ---
$requestName = ${ArtifactName}
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
Write-CommandLog "az account get-access-token --resource ${PublishTokenUri} --query accessToken -o tsv"
$symbolPublishingToken = az account get-access-token --resource ${PublishTokenUri} --query accessToken -o tsv
if ($LASTEXITCODE -ne 0) {
    throw "Failed to acquire symbol publishing token via Azure CLI (exit code: ${LASTEXITCODE})."
}
if ($null -ne $symbolPublishingToken) {
    $symbolPublishingToken = $symbolPublishingToken.Trim()
}
if ([string]::IsNullOrWhiteSpace($symbolPublishingToken)) {
    throw "Failed to acquire symbol publishing token via Azure CLI: received an empty or whitespace-only access token."
}
Write-Host ">  1. Symbol publishing token acquired."
Write-TokenDiagnostics -Token $symbolPublishingToken

$authHeaders = @{ Authorization = "Bearer ${symbolPublishingToken}" }

# --- Step 2: Register request name ---
Write-Host ">  2. Registering request name..."
$requestNameRegistrationBody = @{ requestName = $requestName } | ConvertTo-Json -Compress
Write-CommandLog (Format-RestCommand -Method 'POST' -Uri ${registerUrl} -Headers ${authHeaders} -Body ${requestNameRegistrationBody})
try {
    Invoke-RestMethod -Method POST -Uri ${registerUrl} -Headers ${authHeaders} -ContentType "application/json" -Body ${requestNameRegistrationBody}
} catch {
    throw "Failed to register request name. URI: ${registerUrl} | Body: ${requestNameRegistrationBody} | $(Format-HttpErrorDetail $_)"
}
Write-Host ">  2. Request name registered successfully."

# --- Step 3: Publish symbols ---
Write-Host ">  3. Submitting request to publish symbols..."
$publishSymbolsBody = @{
    publishToInternalServer = $PublishToInternal
    publishToPublicServer   = $PublishToPublic
} | ConvertTo-Json -Compress
Write-Host "Publishing symbols request body: ${publishSymbolsBody}"
Write-CommandLog (Format-RestCommand -Method 'POST' -Uri ${requestUrl} -Headers ${authHeaders} -Body ${publishSymbolsBody})
try {
    Invoke-RestMethod -Method POST -Uri ${requestUrl} -Headers ${authHeaders} -ContentType "application/json" -Body ${publishSymbolsBody}
} catch {
    throw "Failed to publish symbols. URI: ${requestUrl} | Body: ${publishSymbolsBody} | $(Format-HttpErrorDetail $_)"
}
Write-Host ">  3. Request to publish symbols submitted successfully."

# --- Step 4: Check status ---
Write-Host ">  4. Checking the status of the request..."
Write-CommandLog (Format-RestCommand -Method 'GET' -Uri ${requestUrl} -Headers ${authHeaders})
try {
    $status = Invoke-RestMethod -Method GET -Uri ${requestUrl} -Headers ${authHeaders} -ContentType "application/json"
    $status
} catch {
    throw "Failed to check request status. URI: ${requestUrl} | $(Format-HttpErrorDetail $_)"
}

# Validate publishing results — fail the task when the service reports a terminal failure.
# PublishingResult: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled
$resultLabels = @{ 0 = 'Pending'; 1 = 'Succeeded'; 2 = 'Failed'; 3 = 'Cancelled' }
$failures = @()

if ($PublishToInternal) {
    $internalResult = $status.publishToInternalServerResult
    if ($null -ne $internalResult -and $internalResult -ge 2) {
        $label = if ($resultLabels.ContainsKey([int]$internalResult)) { $resultLabels[[int]$internalResult] } else { "Unknown($internalResult)" }
        $failures += "Internal server publishing result: ${label} (${internalResult})"
    }
}

if ($PublishToPublic) {
    $publicResult = $status.publishToPublicServerResult
    if ($null -ne $publicResult -and $publicResult -ge 2) {
        $label = if ($resultLabels.ContainsKey([int]$publicResult)) { $resultLabels[[int]$publicResult] } else { "Unknown($publicResult)" }
        $failures += "Public server publishing result: ${label} (${publicResult})"
    }
}

if ($failures.Count -gt 0) {
    $failureMessage = $failures -join '; '
    throw "Symbol publishing reported a terminal failure. ${failureMessage}. URI: ${requestUrl}"
}

Write-Host ">  4. Status check completed - no terminal failures detected."

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
