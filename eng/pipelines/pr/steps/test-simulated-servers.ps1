# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

param(
    [Parameter(Mandatory)][string] $Framework,
    [Parameter(Mandatory)][ValidateSet('true', 'false')][string] $ManagedSni,
    [ValidateRange(1, 100)][int] $Repetitions = 1,
    [Parameter(Mandatory)][string] $ResultsPath
)

$ErrorActionPreference = 'Stop'
$project = 'src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj'
$filter = 'FullyQualifiedName~SimulatedServerTests.ConnectionFailoverTests|FullyQualifiedName~SimulatedServerTests.ConnectionTests.TransientFault|FullyQualifiedName~SimulatedServerTests.ConnectionTests.Network|FullyQualifiedName~SimulatedServerTests.ServerLifecycleTests|FullyQualifiedName~SimulatedServerTests.ValidationModeTest'
$expected = @{
    'TransientFault_RetryEnabled_ShouldSucceed_Async' = 3
    'TransientFault_RetryDisabled_ShouldFail' = 3
    'NetworkError_RetryEnabled_ShouldSucceed_Async' = 2
    'TransientFault_NoFailover_DoesNotClearPool' = 3
    'NetworkError_TriggersFailover_ClearsPool' = 1
    'NetworkError_WithUserProvidedPartner_RetryDisabled_ShouldConnectToFailoverPartner' = 1
    'NetworkError_WithUserProvidedPartner_RetryEnabled_ShouldConnectToFailoverPartner' = 1
    'TransientFault_WithUserProvidedPartner_Async_ShouldConnectToPrimary_NotFailover' = 3
    'TransientFault_WithUserProvidedPartner_Pooling_ShouldNotClearPool_NotFailover' = 3
}

dotnet build $project -f $Framework -c Debug -v:q
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }

if ($Framework -ne 'net462') {
    $configPath = "src/Microsoft.Data.SqlClient/tests/UnitTests/bin/Debug/$Framework/UnitTests.runtimeconfig.json"
    $config = Get-Content -Raw $configPath | ConvertFrom-Json -AsHashtable
    if (-not $config.runtimeOptions.configProperties) { $config.runtimeOptions.configProperties = @{} }
    $config.runtimeOptions.configProperties['Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows'] = [bool]::Parse($ManagedSni)
    $config | ConvertTo-Json -Depth 20 | Set-Content $configPath
}
$env:SQLCLIENT_EXPECT_MANAGED_SNI = $ManagedSni
New-Item -ItemType Directory -Force $ResultsPath | Out-Null

dotnet test $project --no-build --no-restore -f $Framework --list-tests --filter $filter -v:q
if ($LASTEXITCODE -ne 0) { throw "Discovery failed: $LASTEXITCODE" }

for ($iteration = 1; $iteration -le $Repetitions; $iteration++) {
    Write-Host "Iteration $iteration/$Repetitions; framework=$Framework; managed=$ManagedSni; commit=$env:BUILD_SOURCEVERSION"
    $file = "simulated-$iteration.trx"
    dotnet test $project --no-build --no-restore -f $Framework --filter $filter --blame-hang-timeout 2m --logger "trx;LogFileName=$file" --results-directory $ResultsPath -v:q
    if ($LASTEXITCODE -ne 0) { throw "Test iteration $iteration failed: $LASTEXITCODE" }
    [xml] $trx = Get-Content -Raw (Join-Path $ResultsPath $file)
    $results = @($trx.TestRun.Results.UnitTestResult)
    if ($results.Count -eq 0 -or @($results | Where-Object outcome -ne 'Passed').Count -ne 0) {
        throw "Missing, skipped, or failed tests in iteration $iteration"
    }
    foreach ($method in $expected.Keys) {
        $matches = @($results | Where-Object { $_.testName -match "\.$method(\(|$)" })
        if ($matches.Count -lt $expected[$method]) {
            throw "Missing cases for $method in iteration $iteration"
        }
    }
}
