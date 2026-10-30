param(
    [string]$Evidence = (Join-Path $PSScriptRoot 'full-certificate-cbee4b1'),
    [string]$StockRoot = (Join-Path $PSScriptRoot 'EncryptionInfoE2E\bin\old-followup'),
    [string]$StockOutput = (Join-Path $PSScriptRoot 'EncryptionInfoE2E\bin\full-certificate-stock')
)
$ErrorActionPreference = 'Stop'
$bin = Join-Path $PSScriptRoot 'EncryptionInfoE2E\bin\Release'
$oldBin = $StockOutput
$newHash = 'FFC43EB7C389FDDEBE3B176AFB5C528FAA3155C0A31632D4C6AC0EAB1C4D725E'
$stockHash = 'E61EE1F20BB345147F85820B0D03C2B154BF9CD6B4971708114BB4782FD35B26'
if (Test-Path $oldBin) { throw 'Refusing to overwrite stock-SNI control binaries.' }
New-Item -ItemType Directory -Path $Evidence -Force | Out-Null
Copy-Item $bin $oldBin -Recurse
foreach ($tfm in 'net462','net10.0') {
    $relative = if ($tfm -eq 'net462') { 'Microsoft.Data.SqlClient.SNI.x64.dll' } else { 'runtimes\win-x64\native\Microsoft.Data.SqlClient.SNI.dll' }
    if ((Get-FileHash (Join-Path $bin "$tfm\$relative")).Hash -ne $newHash) { throw 'Deployed preview SNI hash mismatch.' }
    $stock = Join-Path $StockRoot "$tfm\$relative"
    if ((Get-FileHash $stock).Hash -ne $stockHash) { throw 'Stock SNI hash mismatch.' }
    Copy-Item $stock (Join-Path $oldBin "$tfm\$relative")
    if ((Get-FileHash (Join-Path $oldBin "$tfm\$relative")).Hash -ne $stockHash) { throw 'Deployed stock SNI hash mismatch.' }
}
function Run([string]$name,[string]$tfm,[string]$sni,[string]$mode,[bool]$old,[bool]$poolV2) {
    $log = Join-Path $Evidence "$name.log"
    $err = Join-Path $Evidence "$name.stderr.log"
    $output = Join-Path $Evidence "$name.tsv"
    if ((Test-Path $log) -or (Test-Path $output)) { throw "Refusing to overwrite $name" }
    $certs = Join-Path $Evidence 'certs'
    if ($mode -eq 'local-tls') {
        $certs = Join-Path $Evidence "$name-certs"
        New-Item -ItemType Directory $certs | Out-Null
        Copy-Item (Join-Path $Evidence 'certs\*') $certs
        Copy-Item (Join-Path $Evidence 'certs\SQL2022b.cer') (Join-Path $certs 'CT.cer')
    }
    $arguments = @('--sni',$sni,'--certdir',$certs,'--out',$output,'--default-stopped')
    if ($mode -eq 'main') { $arguments += @('--health-gate','--skip-strict') }
    elseif ($mode -eq 'extra') { $arguments += @('--health-gate','--extra','--skip-strict') }
    else {
        $arguments += @('--mode',$mode)
        if ($mode -eq 'certificate') { $arguments += '--health-gate' }
    }
    if ($old) { $arguments += '--old-sni' }
    if ($poolV2) { $arguments += '--pool-v2' }
    $base = if ($old) { $oldBin } else { $bin }
    if ($tfm -eq 'net462') { $exe = Join-Path $base "$tfm\EncryptionInfoE2E.exe" }
    else { $exe = (Get-Command dotnet).Source; $arguments = @((Join-Path $base "$tfm\EncryptionInfoE2E.dll")) + $arguments }
    $quoted = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $process = Start-Process -FilePath $exe -ArgumentList $quoted -RedirectStandardOutput $log -RedirectStandardError $err -PassThru -NoNewWindow
    if (!$process.WaitForExit(600000)) {
        Stop-Process -Id $process.Id -Force
        'BLOCKED: run exceeded ten-minute limit; raw output retained.' | Add-Content $err
        throw "$name timed out; no product verdict inferred."
    }
    $process.WaitForExit()
    Get-Content $log -Tail 2
    if ($process.ExitCode -ne 0) { Get-Content $err; throw "$name exited $($process.ExitCode); inspect retained evidence before continuing." }
    if (!(Select-String -Path $log -Pattern '^SUMMARY ' -Quiet)) { throw "$name has no summary." }
}
foreach ($case in @(@('net10.0','managed'),@('net10.0','native'),@('net462','native'))) {
    $tfm,$sni = $case
    foreach ($mode in 'main','extra','no-tls','certificate') { Run "$tfm-$sni-$mode" $tfm $sni $mode $false $false }
    Run "$tfm-$sni-poolv2-extra" $tfm $sni 'extra' $false $true
    if ($tfm -eq 'net10.0') { Run "$tfm-$sni-local-tls" $tfm $sni 'local-tls' $false $false }
}
foreach ($tfm in 'net10.0','net462') {
    foreach ($mode in 'main','no-tls','certificate') { Run "$tfm-old-$mode" $tfm 'native' $mode $true $false }
}
