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

    # Bind an ephemeral loopback port, record it, then release it. Port 9 (discard) is not
    # guaranteed to be closed, so a host running that service would defeat every
    # connection-refused assertion below.
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $script:closedPort = $listener.LocalEndpoint.Port
    $listener.Stop()
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

    It 'Should state that a TCP failure means no HTTP exchange occurred' {
        $output = & $scriptPath -HostName '127.0.0.1' -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike '*no HTTP exchange*'
        $output | Should -BeLike '*never received a request*'
    }

    It 'Should not attribute a connection failure to a particular owner' {
        # A refused connection comes from the destination, not from an egress policy, so the
        # verdict must report the socket error rather than assign blame.
        $output = & $scriptPath -HostName '127.0.0.1' -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -Not -BeLike '*egress policy problem*'
        $output | Should -Not -BeLike '*network path or egress*'
        $output | Should -Not -BeLike '*BLOCKED*'
    }

    It 'Should not attribute ownership anywhere in the script, including its documentation' {
        # The first attempt at this fixed only the verdict text and left the same claim in
        # the comment-based help and the summary label, so the guard covers the whole file.
        $source = Get-Content -Path $scriptPath -Raw

        $source | Should -Not -Match 'egress policy or'
        $source | Should -Not -Match 'is attributable'
        $source | Should -Not -Match "'BLOCKED"
    }

    It 'Should list every probed host in the summary' {
        $output = & $scriptPath -HostName "127.0.0.1,$($script:unresolvableHost)" -Port $script:closedPort -TimeoutSeconds 3 6>&1 | Out-String

        $output | Should -BeLike '*=== Summary ===*'
        $output | Should -BeLike '*NO CONNECT  127.0.0.1*'
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
