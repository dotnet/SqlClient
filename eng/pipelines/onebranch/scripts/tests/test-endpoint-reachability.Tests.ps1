<#
.SYNOPSIS
    Pester tests for test-endpoint-reachability.ps1

.NOTES
    These avoid external network dependencies. Loopback with a closed port gives a
    deterministic connection refusal, and the .invalid TLD is reserved by RFC 2606 and is
    guaranteed never to resolve.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'test-endpoint-reachability.ps1'

    # A port on loopback that nothing is listening on.
    $script:closedPort = 9
    $script:unresolvableHost = 'this-host-should-never-resolve.invalid'
}

Describe 'test-endpoint-reachability.ps1 Parameter Handling' {

    It 'Should reject an empty host list' {
        { & $scriptPath -HostName '' } | Should -Throw
    }

    It 'Should split a comma-separated host list into separate probes' {
        # 'pwsh -File' passes arguments as plain strings, so the script must split them
        # itself rather than relying on PowerShell array binding.
        $output = & $scriptPath -HostName "127.0.0.1,$($script:unresolvableHost)" -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike "*=== 127.0.0.1:$($script:closedPort) ===*"
        $output | Should -BeLike "*=== $($script:unresolvableHost):$($script:closedPort) ===*"
    }

    It 'Should accept a real array of hosts' {
        $output = & $scriptPath -HostName @('127.0.0.1', $script:unresolvableHost) -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike '*=== 127.0.0.1*'
        $output | Should -BeLike "*=== $($script:unresolvableHost)*"
    }

    It 'Should tolerate whitespace around hostnames' {
        # Each section header is built from the parsed hostname, so an untrimmed entry
        # would render as '===  127.0.0.1:9 ===' with a doubled space.
        $output = & $scriptPath -HostName '127.0.0.1 , 127.0.0.1' -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike "*=== 127.0.0.1:$($script:closedPort) ===*"
        $output | Should -Not -BeLike "*===  127.0.0.1*"
    }
}

Describe 'test-endpoint-reachability.ps1 Failure Reporting' {

    It 'Should report the socket error code and errno for a refused connection' {
        # The distinction that matters: a refused or denied connection carries a socket
        # error code, which proves the failure happened below the HTTP layer.
        $output = & $scriptPath -HostName '127.0.0.1' -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike '*ConnectionRefused*'
        $output | Should -BeLike '*errno*'
    }

    It 'Should distinguish a DNS failure from a connection failure' {
        $output = & $scriptPath -HostName $script:unresolvableHost -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike '*DNS      : FAILED*'
        $output | Should -BeLike '*name resolution failed; nothing was attempted at the TCP layer*'
    }

    It 'Should state that a TCP failure means the service was never reached' {
        $output = & $scriptPath -HostName '127.0.0.1' -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike '*the request never*'
        $output | Should -BeLike '*reached the service*'
    }

    It 'Should list every probed host in the summary' {
        $output = & $scriptPath -HostName "127.0.0.1,$($script:unresolvableHost)" -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike '*=== Summary ===*'
        $output | Should -BeLike '*BLOCKED     127.0.0.1*'
        $output | Should -BeLike "*DNS FAILURE $($script:unresolvableHost)*"
    }

    It 'Should not throw when an endpoint is unreachable' {
        # The probe reports findings; an unreachable endpoint is the expected result and
        # must not fail the step before the remaining hosts are probed.
        { & $scriptPath -HostName "127.0.0.1,$($script:unresolvableHost)" -Port $script:closedPort -TimeoutSeconds 3 } |
            Should -Not -Throw
    }
}

Describe 'test-endpoint-reachability.ps1 Environment Reporting' {

    It 'Should report the OS and PowerShell version' {
        # The whole investigation turns on a Windows/Linux difference, so the probe must
        # record which platform produced the result.
        $output = & $scriptPath -HostName '127.0.0.1' -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike '*PowerShell     :*'
        $output | Should -BeLike '*OS             :*'
    }
}
