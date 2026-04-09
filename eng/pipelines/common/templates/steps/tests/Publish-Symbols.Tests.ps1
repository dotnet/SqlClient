<#
    Licensed to the .NET Foundation under one or more agreements.
    The .NET Foundation licenses this file to you under the MIT license.
    See the LICENSE file in the project root for more information.

    Pester tests for Publish-Symbols.ps1
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'Publish-Symbols.ps1'
}

AfterAll {
    # Clean up global variables used by mocks
    Remove-Variable -Name 'restCalls' -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name 'mockCallCount' -Scope Global -ErrorAction SilentlyContinue
}

Describe 'Publish-Symbols.ps1 Parameter Validation' {

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

Describe 'Publish-Symbols.ps1 URL Construction' {

    BeforeAll {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token-12345' }

        $global:restCalls = @()
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:restCalls += @{
                Method = $Method
                Uri    = $Uri
                Body   = $Body
            }
            return @{ internalServerStatus = 0; publicServerStatus = 0 }
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

Describe 'Publish-Symbols.ps1 Request Bodies' {

    BeforeAll {
        Mock -CommandName 'az' -MockWith { $global:LASTEXITCODE = 0; return 'fake-token-12345' }

        $global:restCalls = @()
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:restCalls += @{
                Method = $Method
                Uri    = $Uri
                Body   = $Body
            }
            return @{ internalServerStatus = 0; publicServerStatus = 0 }
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

Describe 'Publish-Symbols.ps1 Error Handling' {

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
