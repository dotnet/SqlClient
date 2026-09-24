<#
.SYNOPSIS
    Pester tests for publish-symbols.ps1
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'publish-symbols.ps1'
}

AfterAll {
    # Clean up global variables used by mocks
    Remove-Variable -Name 'restCalls' -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name 'mockCallCount' -Scope Global -ErrorAction SilentlyContinue
}

Describe 'publish-symbols.ps1 Parameter Validation' {

    It 'Should reject an empty PublishServer' {
        { & $scriptPath -PublishServer '' -PublishTokenUri 'https://token' -PublishProjectName 'proj' -ArtifactName 'art' } |
            Should -Throw
    }

    It 'Should reject an empty PublishTokenUri' {
        { & $scriptPath -PublishServer 'server' -PublishTokenUri '' -PublishProjectName 'proj' -ArtifactName 'art' } |
            Should -Throw
    }

    It 'Should reject an empty PublishProjectName' {
        { & $scriptPath -PublishServer 'server' -PublishTokenUri 'https://token' -PublishProjectName '' -ArtifactName 'art' } |
            Should -Throw
    }

    It 'Should reject an empty ArtifactName' {
        { & $scriptPath -PublishServer 'server' -PublishTokenUri 'https://token' -PublishProjectName 'proj' -ArtifactName '' } |
            Should -Throw
    }
}

Describe 'publish-symbols.ps1 URL Construction' {

    BeforeAll {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token-12345' }

        $global:restCalls = @()
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:restCalls += @{
                Method = $Method
                Uri    = $Uri
                Body   = $Body
            }
            return @{ publishToInternalServerStatus = 0; publishToPublicServerStatus = 0; publishToInternalServerResult = 0; publishToPublicServerResult = 0 }
        }
    }

    BeforeEach {
        $global:restCalls = @()
    }

    It 'Should construct the correct base URL from PublishServer and PublishProjectName' {
        & $scriptPath `
            -PublishServer 'myserver' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'My.Project' `
            -ArtifactName 'test_artifact'

        $global:restCalls.Count | Should -Be 3

        # Registration call
        $global:restCalls[0].Uri | Should -Be 'https://myserver.trafficmanager.net/projects/My.Project/requests'
        $global:restCalls[0].Method | Should -Be 'POST'

        # Publish call
        $global:restCalls[1].Uri | Should -Be 'https://myserver.trafficmanager.net/projects/My.Project/requests/test_artifact'
        $global:restCalls[1].Method | Should -Be 'POST'

        # Status call
        $global:restCalls[2].Uri | Should -Be 'https://myserver.trafficmanager.net/projects/My.Project/requests/test_artifact'
        $global:restCalls[2].Method | Should -Be 'GET'
    }

    It 'Should use ArtifactName directly as the request name' {
        & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'myartifact_3'

        $global:restCalls[1].Uri | Should -BeLike '*myartifact_3'
        $global:restCalls[2].Uri | Should -BeLike '*myartifact_3'
    }
}

Describe 'publish-symbols.ps1 Request Bodies' {

    BeforeAll {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token-12345' }

        $global:restCalls = @()
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:restCalls += @{
                Method = $Method
                Uri    = $Uri
                Body   = $Body
            }
            return @{ publishToInternalServerStatus = 0; publishToPublicServerStatus = 0; publishToInternalServerResult = 0; publishToPublicServerResult = 0 }
        }
    }

    BeforeEach {
        $global:restCalls = @()
    }

    It 'Should send the correct request name in the registration body' {
        & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'my_artifact_1'

        $body = $global:restCalls[0].Body | ConvertFrom-Json
        $body.requestName | Should -Be 'my_artifact_1'
    }

    It 'Should default to publishing to both internal and public servers' {
        & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art'

        $body = $global:restCalls[1].Body | ConvertFrom-Json
        $body.publishToInternalServer | Should -Be $true
        $body.publishToPublicServer | Should -Be $true
    }

    It 'Should respect PublishToInternal=false' {
        & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' `
            -PublishToInternal $false

        $body = $global:restCalls[1].Body | ConvertFrom-Json
        $body.publishToInternalServer | Should -Be $false
        $body.publishToPublicServer | Should -Be $true
    }

    It 'Should respect PublishToPublic=false' {
        & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' `
            -PublishToPublic $false

        $body = $global:restCalls[1].Body | ConvertFrom-Json
        $body.publishToInternalServer | Should -Be $true
        $body.publishToPublicServer | Should -Be $false
    }
}

Describe 'publish-symbols.ps1 Error Handling' {

    It 'Should throw when token acquisition fails' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 1; return '' }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*token*'
    }

    It 'Should throw when token is empty' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return '  ' }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*empty*'
    }

    It 'Should throw with URI details when registration fails' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        Mock -CommandName 'Invoke-RestMethod' -MockWith { throw "Connection refused" }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*Failed to register*'
    }

    It 'Should throw with URI details when publish fails' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -eq 1) { return @{} }
            throw "Service unavailable"
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*Failed to publish*'
    }

    It 'Should throw with URI details when status check fails' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -le 2) { return @{} }
            throw "Timeout"
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*Failed to check*'
    }
}

Describe 'publish-symbols.ps1 Status Failure Detection' {

    It 'Should throw when internal server result is Failed (2)' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -le 2) { return @{} }
            return @{ publishToInternalServerResult = 2; publishToPublicServerResult = 0 }
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*terminal failure*Internal server*Failed*'
    }

    It 'Should throw when public server result is Cancelled (3)' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -le 2) { return @{} }
            return @{ publishToInternalServerResult = 1; publishToPublicServerResult = 3 }
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*terminal failure*Public server*Cancelled*'
    }

    It 'Should throw when both servers report failure' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -le 2) { return @{} }
            return @{ publishToInternalServerResult = 2; publishToPublicServerResult = 3 }
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*terminal failure*Internal server*Public server*'
    }

    It 'Should not throw when both servers report Succeeded (1)' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -le 2) { return @{} }
            return @{ publishToInternalServerResult = 1; publishToPublicServerResult = 1 }
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Not -Throw
    }

    It 'Should not throw when results are Pending (0)' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -le 2) { return @{} }
            return @{ publishToInternalServerResult = 0; publishToPublicServerResult = 0 }
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Not -Throw
    }

    It 'Should not check internal result when PublishToInternal is false' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -le 2) { return @{} }
            return @{ publishToInternalServerResult = 2; publishToPublicServerResult = 1 }
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' `
            -PublishToInternal $false } |
            Should -Not -Throw
    }

    It 'Should not check public result when PublishToPublic is false' {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
        $global:mockCallCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:mockCallCount++
            if ($global:mockCallCount -le 2) { return @{} }
            return @{ publishToInternalServerResult = 1; publishToPublicServerResult = 2 }
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' `
            -PublishToPublic $false } |
            Should -Not -Throw
    }
}

Describe 'publish-symbols.ps1 Command Logging' {

    BeforeAll {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'super-secret-token' }
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            return @{ publishToInternalServerResult = 1; publishToPublicServerResult = 1 }
        }

        $script:output = (& $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' 6>&1 | Out-String)
    }

    It 'Should log the token acquisition command' {
        $script:output | Should -BeLike "*az account get-access-token --resource https://token-uri --query accessToken -o tsv*"
    }

    It 'Should log each REST command that is executed' {
        $script:output | Should -BeLike "*Invoke-RestMethod -Method POST -Uri 'https://srv.trafficmanager.net/projects/proj/requests'*"
        $script:output | Should -BeLike "*Invoke-RestMethod -Method POST -Uri 'https://srv.trafficmanager.net/projects/proj/requests/art'*"
        $script:output | Should -BeLike "*Invoke-RestMethod -Method GET -Uri 'https://srv.trafficmanager.net/projects/proj/requests/art'*"
    }

    It 'Should log the registration body' {
        $script:output | Should -BeLike '*-Body ''{"requestName":"art"}''*'
    }

    It 'Should never emit the access token anywhere in the log' {
        # The whole point of the redaction: no switch, parameter or setting may put a
        # credential in a retained build log.
        $script:output | Should -Not -BeLike '*super-secret-token*'
    }

    It 'Should redact the Authorization header exactly' {
        # Asserted with a regex rather than -BeLike: under -BeLike every '*' is a wildcard,
        # so a pattern of asterisks matches arbitrary text and proves nothing.
        $script:output | Should -Match "Authorization = '<redacted>'"
        $script:output | Should -Not -Match "Authorization = 'Bearer"
    }

    It 'Should not offer a switch that disables redaction' {
        # Guards against the unredacted-logging path being reintroduced.
        Get-Content -Path $scriptPath -Raw | Should -Not -Match 'LogUnredactedCommands'
    }
}
Describe 'publish-symbols.ps1 Token Diagnostics' {

    BeforeAll {
        function New-Base64Url {
            param([string]$Value)
            [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        }

        $header = New-Base64Url '{"typ":"JWT","alg":"RS256","kid":"test-kid"}'
        $payload = New-Base64Url ('{"aud":"api://test-audience","iss":"https://sts.windows.net/test-tenant/",' +
            '"tid":"test-tenant","appid":"test-appid","oid":"test-oid","idtyp":"app",' +
            '"roles":["SymbolPublisher"],"ver":"1.0","iat":1790000000,"exp":1790003600}')
        $script:signatureSegment = 'signature-segment-must-not-be-logged'
        $script:jwt = "$header.$payload.$($script:signatureSegment)"

        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            return @{ publishToInternalServerResult = 1; publishToPublicServerResult = 1 }
        }

        function Invoke-ScriptWithToken {
            param([string]$Token)

            Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return $Token }.GetNewClosure()

            return (& $scriptPath `
                -PublishServer 'srv' `
                -PublishTokenUri 'https://token-uri' `
                -PublishProjectName 'proj' `
                -ArtifactName 'art' 6>&1 | Out-String)
        }
    }

    Context 'JWT token' {

        BeforeAll {
            $script:output = Invoke-ScriptWithToken -Token $script:jwt
        }

        It 'Should log the SHA-256 fingerprint of the token' {
            $sha256 = [System.Security.Cryptography.SHA256]::Create()
            try {
                $expected = (($sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($script:jwt)) |
                    ForEach-Object { $_.ToString('x2') }) -join '')
            } finally {
                $sha256.Dispose()
            }

            $script:output | Should -BeLike "*SHA-256: ${expected}*"
        }

        It 'Should log the identifying payload claims' {
            $script:output | Should -BeLike '*aud*: api://test-audience*'
            $script:output | Should -BeLike '*appid*: test-appid*'
            $script:output | Should -BeLike '*oid*: test-oid*'
            $script:output | Should -BeLike '*idtyp*: app*'
            $script:output | Should -BeLike '*roles*: SymbolPublisher*'
        }

        It 'Should log the header claims' {
            $script:output | Should -BeLike '*alg*: RS256*'
            $script:output | Should -BeLike '*kid*: test-kid*'
        }

        It 'Should render lifetime claims with a UTC timestamp' {
            $script:output | Should -BeLike '*exp*: 1790003600 (*Z)*'
        }

        It 'Should never log the signature segment' {
            $script:output | Should -Not -BeLike "*$($script:signatureSegment)*"
        }

    }

    Context 'Non-JWT token' {

        It 'Should report that claims are unavailable rather than failing' {
            $output = Invoke-ScriptWithToken -Token 'not-a-jwt'
            $output | Should -BeLike '*Claims:  unavailable (token is not a three-segment JWT)*'
            $output | Should -BeLike '*SHA-256: *'
        }
    }
}

Describe 'publish-symbols.ps1 HTTP Error Diagnostics' {

    BeforeAll {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token' }
    }

    It 'Should report the status code and correlation id from a failed response' {
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::Forbidden)
            $response.Headers.TryAddWithoutValidation('mise-correlation-id', 'corr-1234') | Out-Null
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new(
                'Response status code does not indicate success: 403 (Forbidden).', $response)
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*HTTP status: 403 Forbidden*mise-correlation-id: corr-1234*'
    }

    It 'Should still report non-HTTP failures' {
        Mock -CommandName 'Invoke-RestMethod' -MockWith { throw "Connection refused" }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*Failed to register*Connection refused*'
    }

    It 'Should report the exception type' {
        Mock -CommandName 'Invoke-RestMethod' -MockWith { throw "Connection refused" }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*Exception: System.Management.Automation.RuntimeException*'
    }

    It 'Should state explicitly when no HTTP response was received' {
        # A transport-level failure carries no status, and the diagnostic must say so rather
        # than silently omitting the field.
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            throw [System.Net.Http.HttpRequestException]::new('Permission denied')
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*HTTP status: <none - no HTTP response was received>*'
    }

    It 'Should report the response body from ErrorDetails' {
        # The response body is carried on the ErrorRecord rather than the exception, and the
        # shape varies by PowerShell version, so it needs its own coverage.
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::Forbidden)
            $errorRecord = [System.Management.Automation.ErrorRecord]::new(
                [Microsoft.PowerShell.Commands.HttpResponseException]::new('Response status code does not indicate success: 403 (Forbidden).', $response),
                'WebCmdletWebResponseException',
                [System.Management.Automation.ErrorCategory]::InvalidOperation,
                $null)
            $errorRecord.ErrorDetails = [System.Management.Automation.ErrorDetails]::new('Permission denied')
            throw $errorRecord
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*Response body: Permission denied*'
    }

    It 'Should report the inner exception when one is present' {
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $inner = [System.Net.Sockets.SocketException]::new(13)
            throw [System.Net.Http.HttpRequestException]::new('Permission denied', $inner)
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*Inner exception: System.Net.Sockets.SocketException*'
    }
}
