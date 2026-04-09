<#
.SYNOPSIS
    Pester tests for Publish-Symbols.ps1
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'Publish-Symbols.ps1'
}

Describe 'Publish-Symbols.ps1 Parameter Validation' {

    It 'Should reject an empty PublishServer' {
        { & $scriptPath -PublishServer '' -PublishTokenUri 'https://token' -PublishProjectName 'proj' -ArtifactName 'art' -JobAttempt 1 } |
            Should -Throw
    }

    It 'Should reject an empty PublishTokenUri' {
        { & $scriptPath -PublishServer 'server' -PublishTokenUri '' -PublishProjectName 'proj' -ArtifactName 'art' -JobAttempt 1 } |
            Should -Throw
    }

    It 'Should reject an empty PublishProjectName' {
        { & $scriptPath -PublishServer 'server' -PublishTokenUri 'https://token' -PublishProjectName '' -ArtifactName 'art' -JobAttempt 1 } |
            Should -Throw
    }

    It 'Should reject an empty ArtifactName' {
        { & $scriptPath -PublishServer 'server' -PublishTokenUri 'https://token' -PublishProjectName 'proj' -ArtifactName '' } |
            Should -Throw
    }
}

Describe 'Publish-Symbols.ps1 URL Construction' {

    BeforeAll {
        # Mock az CLI to return a fake token
        Mock -CommandName 'az' -MockWith { 'fake-token-12345' }

        # Mock Invoke-RestMethod to capture calls without making real HTTP requests
        $script:restCalls = @()
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $script:restCalls += @{
                Method = $Method
                Uri    = $Uri
                Body   = $Body
            }
            # Return a fake response for the GET status call
            return @{ internalServerStatus = 0; publicServerStatus = 0 }
        }
    }

    BeforeEach {
        $script:restCalls = @()
    }

    It 'Should construct the correct base URL from PublishServer and PublishProjectName' {
        & $scriptPath `
            -PublishServer 'myserver' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'My.Project' `
            -ArtifactName 'test_artifact'

        $script:restCalls.Count | Should -Be 3

        # Registration call
        $script:restCalls[0].Uri | Should -Be 'https://myserver.trafficmanager.net/projects/My.Project/requests'
        $script:restCalls[0].Method | Should -Be 'POST'

        # Publish call
        $script:restCalls[1].Uri | Should -Be 'https://myserver.trafficmanager.net/projects/My.Project/requests/test_artifact'
        $script:restCalls[1].Method | Should -Be 'POST'

        # Status call
        $script:restCalls[2].Uri | Should -Be 'https://myserver.trafficmanager.net/projects/My.Project/requests/test_artifact'
        $script:restCalls[2].Method | Should -Be 'GET'
    }

    It 'Should use ArtifactName directly as the request name' {
        & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'myartifact_3'

        # All request-specific URLs should use the artifact name as-is
        $script:restCalls[1].Uri | Should -BeLike '*myartifact_3'
        $script:restCalls[2].Uri | Should -BeLike '*myartifact_3'
    }
}

Describe 'Publish-Symbols.ps1 Request Bodies' {

    BeforeAll {
        Mock -CommandName 'az' -MockWith { 'fake-token-12345' }

        $script:restCalls = @()
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $script:restCalls += @{
                Method = $Method
                Uri    = $Uri
                Body   = $Body
            }
            return @{ internalServerStatus = 0; publicServerStatus = 0 }
        }
    }

    BeforeEach {
        $script:restCalls = @()
    }

    It 'Should send the correct request name in the registration body' {
        & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'my_artifact_1'

        $body = $script:restCalls[0].Body | ConvertFrom-Json
        $body.requestName | Should -Be 'my_artifact_1'
    }

    It 'Should default to publishing to both internal and public servers' {
        & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art'

        $body = $script:restCalls[1].Body | ConvertFrom-Json
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

        $body = $script:restCalls[1].Body | ConvertFrom-Json
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

        $body = $script:restCalls[1].Body | ConvertFrom-Json
        $body.publishToInternalServer | Should -Be $true
        $body.publishToPublicServer | Should -Be $false
    }
}

Describe 'Publish-Symbols.ps1 Error Handling' {

    BeforeAll {
        Mock -CommandName 'az' -MockWith { 'fake-token-12345' }
    }

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
        $callCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $callCount++
            if ($callCount -eq 1) { return @{} }       # registration succeeds
            throw "Service unavailable"                  # publish fails
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
        $callCount = 0
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $callCount++
            if ($callCount -le 2) { return @{} }       # registration + publish succeed
            throw "Timeout"                              # status check fails
        }

        { & $scriptPath `
            -PublishServer 'srv' `
            -PublishTokenUri 'https://token-uri' `
            -PublishProjectName 'proj' `
            -ArtifactName 'art' } |
            Should -Throw '*Failed to check*'
    }
}
