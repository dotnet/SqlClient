# Name: TrimDocs.ps1
# Description: This script removes all <remarks> and <example> nodes from an XML documentation file.
# Usage: .\TrimDocs.ps1 -inputFile "path\to\input.xml" -outputFile "path\to\output.xml"

param (
    [string]$inputFile="",
    [string]$outputFile=""
)

# Validate inputFile exists
if (-not (Test-Path $inputFile)) {
    Write-Host "XML File not found: $inputFile"
    exit
}

[xml]$xml = Get-Content $inputFile

# Remove all <remarks> nodes
$nodes = $xml.SelectNodes("//remarks")
foreach ($node in $nodes) {
    $node.ParentNode.RemoveChild($node) | Out-Null
}
# Remove all <example> nodes
$nodes = $xml.SelectNodes("//example")
foreach ($node in $nodes) {
    $node.ParentNode.RemoveChild($node) | Out-Null
}

# Save modified XML
$xml.Save($outputFile)

Write-Host "Trimmed XML documentation file saved to: $outputFile"
