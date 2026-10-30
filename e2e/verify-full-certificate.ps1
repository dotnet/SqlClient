param(
    [string]$Evidence = (Join-Path $PSScriptRoot 'full-certificate-cbee4b1'),
    [string]$Baseline = (Join-Path $PSScriptRoot 'cipher-id-a5e71a')
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path $Baseline)) {
    Write-Warning 'Historical baseline is unavailable; only current-run validation will execute.'
}
$summary = @()
$parity = 0
$changes = @()
foreach ($file in Get-ChildItem $Evidence -Filter 'net*.tsv' | Sort-Object Name) {
    $rows = @(Get-Content $file.FullName | Where-Object { $_ -and !$_.StartsWith('#') } |
        ForEach-Object { ,($_ -split "`t") })
    $counts = @{}
    foreach ($verdict in 'PASS','FAIL','BLOCKED','KNOWN-GAP') {
        $counts[$verdict] = @($rows | Where-Object { $_[5] -eq $verdict }).Count
    }
    if ($counts['FAIL'] -or $counts['KNOWN-GAP']) { throw "Non-passing product verdict in $($file.Name)" }
    $log = [IO.Path]::ChangeExtension($file.FullName, '.log')
    if (!(Select-String -Path $log -Pattern '^API-SHAPE PASS:' -Quiet)) { throw "API shape not checked: $log" }
    $expected = "PASS=$($counts.PASS) FAIL=0 BLOCKED=$($counts.BLOCKED) KNOWN-GAP=0"
    if (!(Select-String -Path $log -SimpleMatch -Pattern $expected -Quiet)) { throw "Summary disagrees with rows: $log" }
    $stderr = [IO.Path]::ChangeExtension($file.FullName, '.stderr.log')
    if ((Get-Item $stderr).Length) { throw "Unexpected stderr: $stderr" }
    foreach ($row in $rows) {
        if ($row[0] -eq 'net10.0' -and $row.Count -ge 18) {
            if ($row[16] -ne $row[17]) { throw "Cipher ID/typed mismatch in $($file.Name)" }
            $parity++
        }
        if ($row[5] -eq 'PASS' -and $row[10] -eq 'MISMATCH') { throw 'Passed DER mismatch' }
        if ($row[5] -eq 'PASS' -and $row[1] -eq 'Native-oldSNI' -and $row[10] -ne '-' -and $row[10] -ne '') {
            throw 'Stock SNI exposed a certificate.'
        }
    }
    $summary += [pscustomobject]@{
        Run = $file.BaseName; PASS = $counts.PASS; FAIL = $counts.FAIL
        BLOCKED = $counts.BLOCKED; KNOWN_GAP = $counts['KNOWN-GAP']
    }
    $oldFile = Join-Path $Baseline $file.Name
    if ($file.BaseName -eq 'net10.0-managed-main' -and
        (Test-Path (Join-Path $Baseline 'net10.0-managed-main-retry.tsv'))) {
        $oldFile = Join-Path $Baseline 'net10.0-managed-main-retry.tsv'
    }
    if (Test-Path $oldFile) {
        $old = @{}
        foreach ($line in Get-Content $oldFile | Where-Object { $_ -and !$_.StartsWith('#') }) {
            $r = $line -split "`t"
            $key = ($r[2] -replace 'No hash in', 'No certificate in') + '|' + $r[3] + '|' + $r[4]
            $old[$key] = $r
        }
        foreach ($row in $rows) {
            $key = $row[2] + '|' + $row[3] + '|' + $row[4]
            if ($old.ContainsKey($key)) { $before = $old[$key] }
            else {
                $candidates = @($old.Values | Where-Object { $_[2] -eq $row[2] -and $_[4] -eq $row[4] })
                if ($row[5] -ne 'BLOCKED' -or $candidates.Count -ne 1) {
                    throw "Missing or ambiguous baseline row: $($file.Name) $key"
                }
                $before = $candidates[0]
            }
            if ($before[5] -ne $row[5]) {
                if ($before[5] -ne 'PASS' -or $row[5] -ne 'BLOCKED' -or $row[3] -notmatch 'SQL2022($|\+)') {
                    throw "Unexpected verdict change: $($file.Name) $key"
                }
                $changes += [pscustomobject]@{ Run = $file.BaseName; Case = $key; Before = $before[5]; After = $row[5] }
            }
            elseif ($row[5] -eq 'PASS' -and $row[6] -in 'ok','fail') {
                foreach ($column in 6,7,8,9,10,11,12,16,17) {
                    if ($row[$column] -ne $before[$column]) { throw "Behaviour changed: $($file.Name) $key column $column" }
                }
            }
        }
    }
}
if ($summary.Count -ne 23) { throw "Expected 23 runs, found $($summary.Count)." }
$summary | Export-Csv (Join-Path $Evidence 'summary.tsv') -Delimiter "`t" -NoTypeInformation
$changes | Export-Csv (Join-Path $Evidence 'baseline-changes.tsv') -Delimiter "`t" -NoTypeInformation
$summary | Format-Table
"Cipher typed/ID observations: $parity; baseline PASS -> BLOCKED changes: $($changes.Count)"
foreach ($verdict in 'PASS','FAIL','BLOCKED','KNOWN_GAP') {
    "$verdict=$((($summary | Measure-Object $verdict -Sum).Sum))"
}
