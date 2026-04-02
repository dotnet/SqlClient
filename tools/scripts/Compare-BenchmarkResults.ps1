<#
.SYNOPSIS
    Compares two BenchmarkDotNet JSON result files and produces a regression report.

.DESCRIPTION
    Reads baseline and current BenchmarkDotNet JSON results, computes percentage
    change per benchmark, flags regressions exceeding a configurable threshold,
    and outputs a Markdown summary table.

.PARAMETER BaselinePath
    Path to the baseline BenchmarkDotNet JSON results file.

.PARAMETER CurrentPath
    Path to the current BenchmarkDotNet JSON results file.

.PARAMETER ThresholdPercent
    Regression threshold percentage. Benchmarks exceeding this are flagged. Default: 10.

.PARAMETER OutputPath
    Optional path to write a Markdown comparison file. Defaults to benchmark-comparison.md.

.EXAMPLE
    .\Compare-BenchmarkResults.ps1 -BaselinePath baseline.json -CurrentPath current.json -ThresholdPercent 10
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$BaselinePath,

    [Parameter(Mandatory = $true)]
    [string]$CurrentPath,

    [Parameter(Mandatory = $false)]
    [double]$ThresholdPercent = 10.0,

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "benchmark-comparison.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Parse-BenchmarkJson {
    param([string]$Path)
    
    $json = Get-Content -Path $Path -Raw | ConvertFrom-Json
    $results = @{}
    
    foreach ($benchmark in $json.Benchmarks) {
        $name = $benchmark.FullName
        if (-not $name) { $name = $benchmark.Method }
        
        $mean = $null
        if ($benchmark.Statistics -and $benchmark.Statistics.Mean) {
            $mean = $benchmark.Statistics.Mean
        }
        
        $allocated = $null
        if ($benchmark.Memory -and $benchmark.Memory.BytesAllocatedPerOperation) {
            $allocated = $benchmark.Memory.BytesAllocatedPerOperation
        }
        
        if ($mean) {
            $results[$name] = @{
                Mean      = $mean
                Allocated = $allocated
            }
        }
    }
    return $results
}

# Parse both files
Write-Host "Loading baseline: $BaselinePath"
$baseline = Parse-BenchmarkJson -Path $BaselinePath

Write-Host "Loading current: $CurrentPath"
$current = Parse-BenchmarkJson -Path $CurrentPath

# Compare
$comparisons = @()
$hasRegression = $false

foreach ($name in $current.Keys) {
    if ($baseline.ContainsKey($name)) {
        $baselineMean = $baseline[$name].Mean
        $currentMean = $current[$name].Mean
        
        $changePercent = 0
        if ($baselineMean -gt 0) {
            $changePercent = (($currentMean - $baselineMean) / $baselineMean) * 100
        }
        
        $status = "pass"
        if ($changePercent -gt $ThresholdPercent) {
            $status = "REGRESSION"
            $hasRegression = $true
        }
        elseif ($changePercent -lt -$ThresholdPercent) {
            $status = "improvement"
        }
        
        $comparisons += [PSCustomObject]@{
            Name           = $name
            BaselineMean   = [math]::Round($baselineMean / 1000000, 3)  # ns to ms
            CurrentMean    = [math]::Round($currentMean / 1000000, 3)
            ChangePercent  = [math]::Round($changePercent, 2)
            Status         = $status
        }
    }
    else {
        $comparisons += [PSCustomObject]@{
            Name           = $name
            BaselineMean   = "N/A"
            CurrentMean    = [math]::Round($current[$name].Mean / 1000000, 3)
            ChangePercent  = "NEW"
            Status         = "new"
        }
    }
}

# Generate Markdown
$md = @()
$md += "# Benchmark Comparison Report"
$md += ""
$md += "| Benchmark | Baseline (ms) | Current (ms) | Change (%) | Status |"
$md += "|-----------|--------------|-------------|-----------|--------|"

foreach ($c in $comparisons | Sort-Object -Property Status -Descending) {
    $statusIcon = switch ($c.Status) {
        "REGRESSION"  { "❌" }
        "improvement" { "✅" }
        "new"         { "🆕" }
        default       { "✅" }
    }
    $md += "| $($c.Name) | $($c.BaselineMean) | $($c.CurrentMean) | $($c.ChangePercent) | $statusIcon |"
}

$md += ""
if ($hasRegression) {
    $md += "**⚠️ Regressions detected!** One or more benchmarks exceeded the $ThresholdPercent% threshold."
}
else {
    $md += "**✅ No regressions detected.** All benchmarks within $ThresholdPercent% threshold."
}

$markdownContent = $md -join "`n"

# Write output
$markdownContent | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Comparison written to: $OutputPath"
Write-Host ""
Write-Host $markdownContent

# Exit code
if ($hasRegression) {
    Write-Host ""
    Write-Host "FAIL: Performance regressions detected." -ForegroundColor Red
    exit 1
}
else {
    Write-Host ""
    Write-Host "PASS: No performance regressions." -ForegroundColor Green
    exit 0
}
