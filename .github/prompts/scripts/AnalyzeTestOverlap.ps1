<#
.SYNOPSIS
    Analyzes test coverage overlap between unit tests.

.DESCRIPTION
    This script performs the following actions:
    1. Sets up the Environment: Automatically installs 'dotnet-coverage' as a local tool if it's missing.
    2. Lists Tests: Uses 'dotnet test --list-tests' with the provided filter to identify which tests to run.
    3. Collects Granular Coverage: Runs each test individually wrapped in 'dotnet-coverage' to ensure completely isolated coverage data.
    4. Parses & Analyzes: Parses the resulting XML coverage files to extract the exact file paths and line ranges covered.
    5. Generates Output:
       - JSON Report: Saves a detailed JSON file (test-coverage-analysis.json) mapping tests to executed lines.
       - Console Summary: Prints a human-readable summary highlighting tests with high overlap (>90%).

.EXAMPLE
    # Run for a specific set of tests (Recommended)
    .\AnalyzeTestOverlap.ps1 -Filter "FullyQualifiedName~ConnectionEnhancedRoutingTests"

.PARAMETER Filter
    The dotnet test filter expression used to select which tests to analyze.
    Supports MSTest filter syntax (e.g., "FullyQualifiedName~ClassName" or
    "TestCategory=Unit").  Use "*" to include all tests (warning: very slow as
    each test runs in a separate process).  Default is
    "" (analyze all tests).

.PARAMETER Framework
    The target framework moniker (TFM) to build and run tests against.  Must
    match a valid <TargetFramework> in the test project (e.g., "net462",
    "net9.0").  Default is "net462".

.PARAMETER Project
    The relative path to the test project (.csproj) to analyze.  Default is
    the SqlClient unit tests project.

.PARAMETER Output
    The file path where the JSON coverage analysis report will be saved.  The
    report maps each test method to the source file lines it covers.  Default
    is "test-coverage-analysis.json".

.EXAMPLE
    # Run for all tests (Warning: Slow, as it runs each test in a separate process)
    .\AnalyzeTestOverlap.ps1 -Filter "*"
#>

param
(
    [string]$Filter = "",
    [string]$Framework = "net462",
    [string]$Project = "src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj",
    [string]$Output = "test-coverage-analysis.json"
)

$ErrorActionPreference = "Stop"

Write-Host "Checking for dotnet-coverage..."
try {
    dotnet tool run dotnet-coverage --version | Out-Null
} catch {
    Write-Host "Installing dotnet-coverage..."
    dotnet tool install dotnet-coverage --create-manifest-if-needed
}

Write-Host "Building project..."
dotnet build $Project --framework $Framework --configuration Debug | Out-Null

Write-Host "Listing tests..."
$tests = dotnet test $Project --list-tests --framework $Framework --filter $Filter | Select-String "    " | ForEach-Object { $_.ToString().Trim() }

if ($tests.Count -eq 0) {
    Write-Error "No tests found with filter '$Filter'"
}

# Group tests by base method name to handle parameterized tests
# xUnit outputs "Namespace.Class.Method(param: value)"
# We want to run "Namespace.Class.Method" once to avoid filter quoting issues and aggregate coverage
$uniqueTestMethods = $tests | ForEach-Object {
    if ($_ -match "^(.+?)\(.*\)$") {
        $matches[1]
    } else {
        $_
    }
} | Select-Object -Unique

Write-Host "Found $($tests.Count) test cases, aggregated to $($uniqueTestMethods.Count) test methods."

$results = @{}
$tempDir = Join-Path $PSScriptRoot "TempCoverage"
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $tempDir | Out-Null

foreach ($test in $uniqueTestMethods) {
    Write-Host "Running $test..."
    # Sanitize test name for filename
    $safeTestName = $test -replace '[^a-zA-Z0-9\.-]', '_'
    $coverageFile = Join-Path $tempDir "$safeTestName.xml"
    
    # Run test with coverage
    # We use -f xml to get xml output directly
    # We use 'dotnet dotnet-coverage' (without 'tool run') as verified to work likely due to path/global install fallback
    # Using ~ (Contains) for FullyQualifiedName handles both exact match and parameterized variants
    $testFilter = "FullyQualifiedName~$test"
    $innerCmd = "dotnet test `"$Project`" --filter `"$testFilter`" --framework $Framework --no-build"
    # Note: passing dotnet-coverage as the command to the dotnet driver
    $coverageArgs = @("dotnet-coverage", "collect", "-f", "xml", "-o", $coverageFile, $innerCmd)
    
    try {
        & dotnet $coverageArgs | Out-Null
    } catch {
        Write-Warning "Failed to run coverage for ${test}: $_"
    }
    
    if (Test-Path $coverageFile) {
        # Parse XML
        [xml]$xml = Get-Content $coverageFile
        
        # Map source file IDs to Paths
        $sourceMap = @{}
        $xml.results.modules.module.source_files.source_file | ForEach-Object {
            $sourceMap[$_.id] = $_.path
        }
        
        $coveredLines = @()
        
        # Get covered ranges
        if ($xml.results.modules.module.functions.function) {
            foreach ($func in $xml.results.modules.module.functions.function) {
                if ($func.ranges.range) {
                    foreach ($range in $func.ranges.range) {
                        if ($range.covered -eq "yes" -or $range.covered -eq "partially") {
                            $filePath = $sourceMap[$range.source_id]
                            $coveredLines += "$($filePath):$($range.start_line)-$($range.end_line)"
                        }
                    }
                }
            }
        }
        
        $results[$test] = $coveredLines
        Write-Host "  Collected $($coveredLines.Count) covered ranges."
    } else {
        Write-Warning "No coverage file generated for $test"
    }
}

# Clean up
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }

# Output to JSON
$json = $results | ConvertTo-Json -Depth 5
Set-Content $Output $json
Write-Host "Analysis saved to $Output"

# Perform basic overlap analysis
Write-Host "`nGenerating overlap summary..."
$testNames = $results.Keys | Sort-Object
foreach ($t1 in $testNames) {
    $lines1 = $results[$t1]
    if ($null -eq $lines1 -or $lines1.Count -eq 0) { continue }
    $set1 = New-Object System.Collections.Generic.HashSet[string]
    $lines1 | ForEach-Object { $set1.Add($_) | Out-Null }

    foreach ($t2 in $testNames) {
        if ($t1 -ge $t2) { continue } # Avoid self and duplicate comparisons

        $lines2 = $results[$t2]
        if ($null -eq $lines2 -or $lines2.Count -eq 0) { continue }
        
        $intersection = 0
        foreach ($line in $lines2) {
            if ($set1.Contains($line)) {
                $intersection++
            }
        }

        $overlapPct1 = ($intersection / $lines1.Count) * 100
        $overlapPct2 = ($intersection / $lines2.Count) * 100
        
        if ($overlapPct1 -gt 90 -and $overlapPct2 -gt 90) {
            Write-Host "HIGH OVERLAP: $t1 <-> $t2"
            Write-Host "  Shared lines: $intersection"
            Write-Host "  $t1 overlap: $([math]::Round($overlapPct1, 2))%"
            Write-Host "  $t2 overlap: $([math]::Round($overlapPct2, 2))%"
            Write-Host ""
        }
    }
}
