<#
.SYNOPSIS
    Installs the docker CLI on an Intel macOS agent from a Homebrew bottle.

.DESCRIPTION
    Homebrew ships no Intel macOS bottle for the current docker formula, so
    'brew install docker' compiles the CLI - and builds Go in order to do it -
    which takes minutes and routinely exhausts the pipeline step timeout.

    This installs the newest docker version that *is* bottled for Intel macOS,
    read from Homebrew's own OCI registry on ghcr.io. Bottles are
    content-addressed, so the download is verified against the digest the
    registry advertises rather than a checksum pinned here that someone has to
    remember to bump. Nothing is downloaded from outside Homebrew, and the
    result is exactly what 'brew install' would have produced.

    Only the docker CLI is handled this way. colima and lima still install
    through brew: their bottles carry payloads outside bin/ and colima depends
    on lima at runtime, so neither can be installed by lifting a single binary.

.PARAMETER DestinationPath
    Directory the docker binary is written to. Prepended to PATH for subsequent
    pipeline steps.

.NOTES
    Intel macOS only. On any other architecture this fails rather than
    installing a binary the agent cannot run.
#>
param(
    [string]$DestinationPath = (Join-Path -Path $HOME -ChildPath '.docker-cli/bin')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# tar's exit code is checked explicitly below so the failure carries the member
# path rather than PowerShell's generic native-command error.
$PSNativeCommandUseErrorActionPreference = $false

$Registry = 'https://ghcr.io'
$RegistryRepository = 'homebrew/core/docker'

# Newest first. A bottle built for an older macOS still runs on a newer one, so
# any of these work on the current agents.
$PreferredCodenames = @('sequoia', 'sonoma', 'ventura')

#region Helper Functions

function Get-RegistryToken {
    <#
    .SYNOPSIS
        An anonymous pull token for the formula's registry repository.
    #>

    $uri = "$Registry/token?service=ghcr.io&scope=repository:${RegistryRepository}:pull"
    return (Invoke-RestMethod -Uri $uri -MaximumRetryCount 3 -RetryIntervalSec 5).token
}

function Get-BottleVersion {
    <#
    .SYNOPSIS
        Every docker version tag in the registry, newest first.
    #>
    param(
        [Parameter(Mandatory)][string]$Token
    )

    $uri = "$Registry/v2/$RegistryRepository/tags/list?n=1000"
    $tags = @()

    while ($uri) {
        $response = Invoke-RestMethod `
            -Uri $uri `
            -Headers @{ Authorization = "Bearer $Token" } `
            -MaximumRetryCount 3 -RetryIntervalSec 5 `
            -ResponseHeadersVariable 'responseHeaders'

        $tags += $response.tags

        # ghcr.io orders tags lexically, which puts the newest on the last page.
        $link = if ($responseHeaders.ContainsKey('Link')) { @($responseHeaders['Link'])[0] } else { $null }
        $uri = if ($link -match '<([^>]+)>') { $Registry + $Matches[1] } else { $null }
    }

    return $tags |
        Where-Object { $_ -match '^\d+\.\d+\.\d+(-\d+)?$' } |
        Sort-Object -Property { [version]($_ -replace '-', '.') } -Descending
}

function Get-BottleRefName {
    <#
    .SYNOPSIS
        The bottle ref name an OCI index entry advertises, or $null.
    #>
    param(
        [Parameter(Mandatory)]$IndexEntry
    )

    $annotations = $IndexEntry.PSObject.Properties['annotations']
    if (-not $annotations) { return $null }

    $refName = $annotations.Value.PSObject.Properties['org.opencontainers.image.ref.name']
    if (-not $refName) { return $null }

    return $refName.Value
}

function Find-Bottle {
    <#
    .SYNOPSIS
        The newest given version carrying an Intel macOS bottle, with that
        bottle's manifest digest.
    #>
    param(
        [Parameter(Mandatory)][string]$Token,
        [Parameter(Mandatory)][string[]]$Version
    )

    $headers = @{
        Authorization = "Bearer $Token"
        Accept        = 'application/vnd.oci.image.index.v1+json'
    }

    foreach ($candidate in $Version) {
        $index = Invoke-RestMethod `
            -Uri "$Registry/v2/$RegistryRepository/manifests/$candidate" `
            -Headers $headers `
            -MaximumRetryCount 3 -RetryIntervalSec 5

        foreach ($codename in $PreferredCodenames) {
            foreach ($entry in $index.manifests) {
                # The index already belongs to this version, so only the
                # platform component needs matching. Splitting on '.' is what
                # keeps 'sonoma' from also matching 'arm64_sonoma', and rejects
                # the linux and ':all' refs outright.
                $ref = Get-BottleRefName -IndexEntry $entry
                if ($ref -and ($ref.Split('.') -contains $codename)) {
                    return [pscustomobject]@{
                        Version = $candidate
                        Digest  = $entry.digest
                        Ref     = $ref
                    }
                }
            }
        }
    }

    throw "No Intel macOS docker bottle in any of the $($Version.Count) published versions."
}

function Save-BottleBlob {
    <#
    .SYNOPSIS
        Downloads a bottle archive and verifies it against its layer digest.
    #>
    param(
        [Parameter(Mandatory)][string]$Token,
        [Parameter(Mandatory)][string]$ManifestDigest,
        [Parameter(Mandatory)][string]$Path
    )

    $manifest = Invoke-RestMethod `
        -Uri "$Registry/v2/$RegistryRepository/manifests/$ManifestDigest" `
        -Headers @{
            Authorization = "Bearer $Token"
            Accept        = 'application/vnd.oci.image.manifest.v1+json'
        } `
        -MaximumRetryCount 3 -RetryIntervalSec 5

    $layerDigest = $manifest.layers[0].digest

    Invoke-WebRequest `
        -Uri "$Registry/v2/$RegistryRepository/blobs/$layerDigest" `
        -Headers @{ Authorization = "Bearer $Token" } `
        -MaximumRetryCount 3 -RetryIntervalSec 5 `
        -OutFile $Path

    $expected = $layerDigest -replace '^sha256:', ''
    $actual = (Get-FileHash -Path $Path -Algorithm SHA256).Hash

    # -ne is case-insensitive, so Get-FileHash's uppercase output compares equal.
    if ($actual -ne $expected) {
        throw "Bottle digest mismatch: expected $expected, got $actual."
    }
}

#endregion Helper Functions

$architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
if ($architecture -ne 'X64') {
    throw "This installs an Intel macOS bottle, but the agent architecture is $architecture."
}

$token = Get-RegistryToken
# Every version scanned costs a registry round-trip (~0.2s), but the whole list
# is walked rather than a fixed window: Homebrew has stopped publishing Intel
# bottles, so the newest usable version sinks further down the list over time.
$versions = @(Get-BottleVersion -Token $token)
$bottle = Find-Bottle -Token $token -Version $versions

$archive = Join-Path ([System.IO.Path]::GetTempPath()) "docker-bottle-$([guid]::NewGuid().ToString('n')).tar.gz"

try {
    Save-BottleBlob -Token $token -ManifestDigest $bottle.Digest -Path $archive

    New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null

    # Homebrew keeps the cellar directory at the plain version even for a
    # revision build, so the '29.7.2-1' bottle unpacks from 'docker/29.7.2'.
    $cellarVersion = $bottle.Version -replace '-\d+$', ''
    $member = "docker/$cellarVersion/bin/docker"

    # Naming the one member we want is what keeps the rest of the archive - an
    # anonymous download - from ever being written to disk.
    tar -xz -f $archive -C $DestinationPath --strip-components 3 $member
    if ($LASTEXITCODE -ne 0) {
        throw "Extracting $member from the docker bottle failed (tar exit $LASTEXITCODE)."
    }
}
finally {
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
}

Write-Host "Installed docker $($bottle.Version) ($($bottle.Ref)) to $DestinationPath"
Write-Host "##vso[task.prependpath]$DestinationPath"
