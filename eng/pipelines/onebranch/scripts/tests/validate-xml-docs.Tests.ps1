<#
.SYNOPSIS
    Pester tests for validate-xml-docs.ps1.

.DESCRIPTION
    Every cross-reference form that produced an xref-not-found warning against
    dotnet/sqlclient-api-docs PR 99 is covered here, alongside the valid controls those rules must
    not flag. The controls matter as much as the failures: the rules run over every cref in
    doc/snippets, so a rule that over-reports would block the build on correct documentation.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'validate-xml-docs.ps1'

    function New-TestDirectory {
        $path = Join-Path $TestDrive ([guid]::NewGuid().ToString('n'))
        New-Item -ItemType Directory -Path $path | Out-Null
        return $path
    }

    <#
        Writes a snippet file shaped like doc/snippets: a <docs><members> tree whose members carry
        the cref-bearing elements the compiler pulls in via <include>.
    #>
    function New-SnippetDirectory {
        param([string[]]$Crefs = @(), [string]$RawBody)

        $path = New-TestDirectory
        $body = if ($PSBoundParameters.ContainsKey('RawBody')) {
            $RawBody
        }
        else {
            ($Crefs | ForEach-Object { "      <see cref=""$_"" />" }) -join "`n"
        }

        @"
<docs>
  <members name="SampleType">
    <SampleType>
      <summary>Sample.</summary>
$body
    </SampleType>
  </members>
</docs>
"@ | Set-Content -LiteralPath (Join-Path $path 'Sample.xml') -Encoding utf8

        return $path
    }

    <#
        Writes a compiler-shaped XML documentation file: <doc><members><member name="..."> with the
        documented members that the local UID index is built from.
    #>
    function New-DocumentationDirectory {
        param(
            [string[]]$Members = @('T:Microsoft.Data.SqlClient.SqlConnection'),
            [string[]]$Crefs = @()
        )

        $path = New-TestDirectory
        $memberXml = ($Members | ForEach-Object { "    <member name=""$_""><summary>Doc.</summary></member>" }) -join "`n"
        $crefXml = ($Crefs | ForEach-Object { "      <see cref=""$_"" />" }) -join "`n"

        @"
<doc>
  <assembly><name>Microsoft.Data.SqlClient</name></assembly>
  <members>
$memberXml
    <member name="T:Microsoft.Data.SqlClient.Sample">
      <summary>Sample.</summary>
$crefXml
    </member>
  </members>
</doc>
"@ | Set-Content -LiteralPath (Join-Path $path 'Microsoft.Data.SqlClient.xml') -Encoding utf8

        return $path
    }

    function Get-Report {
        param([Parameter(Mandatory)][string]$Path)
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }

    <#
        Writes a project file, and optionally a source file whose documentation comment pulls in a
        snippet, so the script's expectation derivation has something real to read.
    #>
    function New-Project {
        param(
            [switch]$GeneratesDocumentation,
            [switch]$ThenDisables,
            [string]$Comment,
            [string]$SnippetReference
        )

        $directory = New-TestDirectory
        $projectPath = Join-Path $directory 'Sample.csproj'

        $properties = ''
        if ($GeneratesDocumentation) {
            $properties += '    <GenerateDocumentationFile>true</GenerateDocumentationFile>' + [Environment]::NewLine
        }
        if ($ThenDisables) {
            $properties += '    <GenerateDocumentationFile>false</GenerateDocumentationFile>' + [Environment]::NewLine
        }

        $commentXml = if ([string]::IsNullOrEmpty($Comment)) { '' } else { "  <!-- $Comment -->" }

        @"
<Project Sdk="Microsoft.NET.Sdk">
$commentXml
  <PropertyGroup>
$properties  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath $projectPath -Encoding utf8

        if (-not [string]::IsNullOrEmpty($SnippetReference)) {
            # The include path is relative to the source file that declares it, matching how the
            # compiler resolves it.
            $relative = [System.IO.Path]::GetRelativePath($directory, $SnippetReference) -replace '\\', '/'
            @"
/// <include file='$relative' path='docs/members[@name="Used"]/Used/*' />
public class Sample { }
"@ | Set-Content -LiteralPath (Join-Path $directory 'Sample.cs') -Encoding utf8
        }

        return $projectPath
    }
}

Describe 'validate-xml-docs.ps1' {

    Context 'PR 99 failure forms' {

        It 'rejects a T: cref naming an array' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte[]')

            { & $scriptPath -SnippetsDirectory $snippets } |
                Should -Throw '*XML documentation validation failed with 1 issue*'
        }

        It 'suggests the element type when a T: cref names an array' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte[]')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'invalid-docid'
            $finding.Message | Should -BeLike '*T:System.Byte*array*'
        }

        It 'rejects a parameterless method cref written with empty parentheses' {
            $snippets = New-SnippetDirectory -Crefs @('M:Microsoft.Data.SqlClient.SqlConnection.GetSchema()')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'invalid-docid'
            $finding.Message | Should -BeLike "*use 'M:Microsoft.Data.SqlClient.SqlConnection.GetSchema'*"
        }

        It 'rejects a C# alias in a method signature' {
            $snippets = New-SnippetDirectory -Crefs @('M:Microsoft.Data.SqlClient.SqlConnection.GetSchema(string)')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'invalid-docid'
            $finding.Message | Should -BeLike "*C# alias 'string'*"
        }

        It 'reports both the whitespace and the alias in a signature carrying both' {
            $snippets = New-SnippetDirectory -Crefs @('M:Microsoft.Data.SqlClient.SqlConnection.GetSchema(string, string[])')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 2
            ($findings.Message -join ' ') | Should -BeLike '*whitespace*'
            ($findings.Message -join ' ') | Should -BeLike "*C# alias 'string'*"
        }

        It 'reports one alias finding when a signature repeats the same alias' {
            $snippets = New-SnippetDirectory -Crefs @('M:Sample.Type.Method(string,string[])')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings | Where-Object { $_.Message -like '*alias*' })
            $findings.Count | Should -Be 1
        }

        It 'rejects whitespace in an otherwise correct signature' {
            $snippets = New-SnippetDirectory -Crefs @('M:Microsoft.Data.SqlClient.SqlParameterCollection.Add(System.String, System.String)')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Message | Should -BeLike '*Add(System.String,System.String)*'
        }

        It 'rejects a misspelled namespace root' {
            $snippets = New-SnippetDirectory -Crefs @('P:Microssoft.Data.SqlClient.SqlCommand.EnableOptimizedParameterBinding')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'unknown-namespace-root'
            $finding.Message | Should -BeLike "*'Microssoft'*"
        }
    }

    Context 'valid controls' {

        It 'accepts the corrected forms of every PR 99 defect' {
            $snippets = New-SnippetDirectory -Crefs @(
                'T:System.Byte',
                'M:Microsoft.Data.SqlClient.SqlConnection.GetSchema',
                'M:Microsoft.Data.SqlClient.SqlConnection.GetSchema(System.String)',
                'M:Microsoft.Data.SqlClient.SqlConnection.GetSchema(System.String,System.String[])',
                'M:Microsoft.Data.SqlClient.SqlParameterCollection.Add(System.String,System.String)',
                'P:Microsoft.Data.SqlClient.SqlCommand.EnableOptimizedParameterBinding')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'accepts arrays, by-reference and pointer markers inside a member signature' {
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlClient.Sample.Decrypt(System.Byte[])',
                'M:Microsoft.Data.SqlClient.Sample.TryGet(System.Int32@)',
                'M:Microsoft.Data.SqlClient.Sample.Raw(System.Byte*)')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'rejects angle brackets in a generic type reference' {
            # Angle brackets are C# source syntax; a documentation ID writes generic arguments in
            # braces. Without this the cref has an allowed namespace root and passes the source
            # gate, then fails to resolve once published.
            $snippets = New-SnippetDirectory -Crefs @('T:System.Collections.Generic.List&lt;System.String&gt;')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'invalid-docid'
            $finding.Message | Should -BeLike '*braces*List{System.String}*'
        }

        It 'rejects angle brackets inside a member signature' {
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlClient.Sample.Use(System.Collections.Generic.List&lt;System.String&gt;)')

            { & $scriptPath -SnippetsDirectory $snippets } |
                Should -Throw '*failed with 1 issue*'
        }

        It 'rejects a multidimensional array type reference' -ForEach @(
            @{ Cref = 'T:System.Int32[,]' }
            @{ Cref = 'T:System.Int32[,,]' }
            @{ Cref = 'T:System.Int32[0:,0:]' }
        ) {
            # [] was recognized but the multidimensional forms were not, so an invalid T: array
            # cref passed.
            $snippets = New-SnippetDirectory -Crefs @($Cref)
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'invalid-docid'
            $finding.Message | Should -BeLike '*constructed type*'
        }

        It 'accepts a multidimensional array inside a member signature' {
            # The same suffix is legal as a parameter type; only a T: reference to it is wrong.
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlClient.Sample.Grid(System.Int32[0:,0:])')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'accepts generic arguments written in braces' {
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlClient.Sample.Map(System.Collections.Generic.Dictionary{System.String,System.Int32})',
                'T:System.Collections.Generic.IReadOnlyList`1')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'accepts a conversion operator documentation ID' {
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlTypes.SqlJson.op_Explicit(System.String)~Microsoft.Data.SqlTypes.SqlJson')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'does not fail on an unprefixed cref, which the compiler binds from source' {
            $snippets = New-SnippetDirectory -Crefs @('SqlJson', 'string', 'System.Text.Json.JsonDocument')
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -SnippetsDirectory $snippets -ReportPath $report } | Should -Not -Throw

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 3
            $findings.Severity | Should -Not -Contain 'error'
        }

        It 'accepts a namespace cref' {
            $snippets = New-SnippetDirectory -Crefs @('N:Microsoft.Data.SqlClient')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }
    }

    Context 'documentation mode' {

        It 'rejects a cref the compiler could not bind' {
            $docs = New-DocumentationDirectory -Crefs @('!:Microsoft.Data.SqlClient.Missing')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'unresolved-cref'
        }

        It 'resolves a local cref against the members the build emitted' {
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('T:Microsoft.Data.SqlClient.SqlConnection')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings) | Should -BeNullOrEmpty
        }

        It 'reports a local cref with no matching emitted member as information, not an error' {
            # Without reference documentation the public API surface is unknown, and such a
            # reference may resolve in another target framework or a sibling assembly.
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('T:Microsoft.Data.SqlClient.DoesNotExist')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'missing-local-uid'
            $finding.Severity | Should -Be 'info'
        }

        It 'fails on a missing local UID when that category is named in -FailOn' {
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('T:Microsoft.Data.SqlClient.DoesNotExist')

            { & $scriptPath -DocumentationPath $docs -FailOn error, missing-local-uid } |
                Should -Throw '*failed with 1 issue*'
        }

        It 'reports a wrong-kind prefix from a public member as an error' {
            # Same classification as an unresolvable reference, and for the same reason: the wrong
            # prefix produces a UID that matches nothing, so a public member's page carries an
            # unresolved reference.
            $staging = New-TestDirectory
            New-Item -ItemType Directory -Path (Join-Path $staging 'lib/net8.0') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $staging 'ref/net8.0') -Force | Out-Null

            '<doc><members>' +
            '<member name="P:Microsoft.Data.SqlClient.Widget.Size"><summary>S.</summary></member>' +
            '<member name="T:Microsoft.Data.SqlClient.Widget"><summary>W.</summary>' +
            '<see cref="M:Microsoft.Data.SqlClient.Widget.Size" /></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'lib/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8
            '<doc><members>' +
            '<member name="P:Microsoft.Data.SqlClient.Widget.Size"><summary>S.</summary></member>' +
            '<member name="T:Microsoft.Data.SqlClient.Widget"><summary>W.</summary></member>' +
            '</members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'ref/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $packages 'P.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ReportPath $report -ReportOnly

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Category -like 'mismatched*' })[0]
            $finding.Category | Should -Be 'mismatched-public-docid-prefix'
            $finding.Severity | Should -Be 'error'
        }

        It 'reports a wrong-kind prefix from a non-public member as a warning' {
            $staging = New-TestDirectory
            New-Item -ItemType Directory -Path (Join-Path $staging 'lib/net8.0') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $staging 'ref/net8.0') -Force | Out-Null

            # The holder is absent from ref/, so it is not part of the public API surface.
            '<doc><members>' +
            '<member name="P:Microsoft.Data.SqlClient.Widget.Size"><summary>S.</summary></member>' +
            '<member name="T:Microsoft.Data.SqlClient.Internals"><summary>I.</summary>' +
            '<see cref="M:Microsoft.Data.SqlClient.Widget.Size" /></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'lib/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8
            '<doc><members><member name="P:Microsoft.Data.SqlClient.Widget.Size"><summary>S.</summary></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'ref/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $packages 'P.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ReportPath $report -ReportOnly

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Category -like 'mismatched*' })[0]
            $finding.Category | Should -Be 'mismatched-docid-prefix'
            $finding.Severity | Should -Be 'warning'
        }

        It 'names the expected prefix when a cref uses the wrong member kind' {
            $docs = New-DocumentationDirectory `
                -Members @('P:Microsoft.Data.SqlClient.SqlCommand.CommandTimeout') `
                -Crefs @('M:Microsoft.Data.SqlClient.SqlCommand.CommandTimeout')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'mismatched-docid-prefix'
            $finding.Severity | Should -Be 'warning'
            $finding.Message | Should -BeLike "*was emitted as 'P:'*"
        }

        It 'does not resolve a namespace cref against the member index' {
            # The compiler never emits a <member> entry for a namespace, so an N: cref must not be
            # treated as an unresolved reference.
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('N:Microsoft.Data.SqlClient')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report

            @((Get-Report -Path $report).Findings) | Should -BeNullOrEmpty
        }

        It 'does not attempt local resolution in source mode' {
            $snippets = New-SnippetDirectory -Crefs @('T:Microsoft.Data.SqlClient.AnythingAtAll')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'ignores non-documentation XML found in a scanned directory' {
            $path = New-TestDirectory
            '<project><target /></project>' | Set-Content -LiteralPath (Join-Path $path 'build.xml') -Encoding utf8
            '<doc><members><member name="T:Microsoft.Data.SqlClient.SqlConnection" /></members></doc>' |
                Set-Content -LiteralPath (Join-Path $path 'Microsoft.Data.SqlClient.xml') -Encoding utf8
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $path -ReportPath $report

            (Get-Report -Path $report).DocumentationFiles | Should -Be 1
        }

        It 'validates the XML documentation inside a package' {
            $staging = New-TestDirectory
            $libPath = Join-Path $staging 'lib/net8.0'
            New-Item -ItemType Directory -Path $libPath -Force | Out-Null
            '<doc><members><member name="T:Microsoft.Data.SqlClient.Sample"><see cref="T:System.Byte[]" /></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $libPath 'Microsoft.Data.SqlClient.xml') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory(
                $staging, (Join-Path $packages 'Microsoft.Data.SqlClient.7.1.0.nupkg'))

            { & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) } |
                Should -Throw '*failed with 1 issue*'
        }
    }

    Context 'package lib/ref documentation layout' {

        BeforeAll {
            # A realistic pair: the implementation XML carries narrative documentation, and the
            # reference XML is the same content after TrimDocs.ps1 has removed it.
            function New-LayoutPackage {
                param(
                    [Parameter(Mandatory)][hashtable]$Files,
                    [string]$PackageName = 'Microsoft.Data.SqlClient.7.1.0.nupkg'
                )

                $staging = New-TestDirectory
                foreach ($entry in $Files.GetEnumerator()) {
                    $target = Join-Path $staging $entry.Key
                    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
                    Set-Content -LiteralPath $target -Value $entry.Value -Encoding utf8
                }

                $packages = New-TestDirectory
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $packages $PackageName))
                return $packages
            }

            $script:FullXml = '<doc><assembly><name>A</name></assembly><members>' +
                '<member name="T:Microsoft.Data.SqlClient.Sample"><summary>S.</summary>' +
                '<remarks>Detail.</remarks><example><code>x</code></example></member></members></doc>'

            $script:TrimmedXml = '<doc><assembly><name>A</name></assembly><members>' +
                '<member name="T:Microsoft.Data.SqlClient.Sample"><summary>S.</summary>' +
                '</member></members></doc>'
        }

        It 'accepts a full lib/ XML paired with a trimmed ref/ XML' {
            $packages = New-LayoutPackage -Files @{
                'lib/net8.0/Microsoft.Data.SqlClient.xml' = $script:FullXml
                'ref/net8.0/Microsoft.Data.SqlClient.xml' = $script:TrimmedXml
            }

            { & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) } | Should -Not -Throw
        }

        It 'rejects a lib/ XML that has been trimmed' {
            $packages = New-LayoutPackage -Files @{
                'lib/net8.0/Microsoft.Data.SqlClient.xml' = $script:TrimmedXml
                'ref/net8.0/Microsoft.Data.SqlClient.xml' = $script:TrimmedXml
            }
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) -ReportPath $report -ReportOnly

            $categories = @((Get-Report -Path $report).Findings.Category)
            $categories | Should -Contain 'lib-documentation-trimmed'
        }

        It 'rejects lib/ and ref/ XML that are byte-identical' {
            # The 7.1.0 regression: the nuspec mapped the trimmed reference artifact into both
            # targets, so IntelliSense silently lost every remark and example.
            $packages = New-LayoutPackage -Files @{
                'lib/net8.0/Microsoft.Data.SqlClient.xml' = $script:TrimmedXml
                'ref/net8.0/Microsoft.Data.SqlClient.xml' = $script:TrimmedXml
            }
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) -ReportPath $report -ReportOnly

            $categories = @((Get-Report -Path $report).Findings.Category)
            $categories | Should -Contain 'lib-ref-documentation-identical'
        }

        It 'rejects a ref/ XML that was never trimmed' {
            $packages = New-LayoutPackage -Files @{
                'lib/net8.0/Microsoft.Data.SqlClient.xml' = $script:FullXml
                'ref/net8.0/Microsoft.Data.SqlClient.xml' = ($script:FullXml -replace 'Detail\.', 'Other.')
            }
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) -ReportPath $report -ReportOnly

            $categories = @((Get-Report -Path $report).Findings.Category)
            $categories | Should -Contain 'ref-documentation-untrimmed'
        }

        It 'checks each target framework independently' {
            # Mirrors the real package, where net462 maps correctly but the modern frameworks do not.
            $packages = New-LayoutPackage -Files @{
                'lib/net462/Microsoft.Data.SqlClient.xml' = $script:FullXml
                'ref/net462/Microsoft.Data.SqlClient.xml' = $script:TrimmedXml
                'lib/net8.0/Microsoft.Data.SqlClient.xml' = $script:TrimmedXml
                'ref/net8.0/Microsoft.Data.SqlClient.xml' = $script:TrimmedXml
            }
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings | Where-Object { $_.Category -like '*documentation*' })
            $findings.Count | Should -Be 2
            @($findings | Where-Object { $_.Path -like '*net462*' }) | Should -BeNullOrEmpty
            @($findings | Where-Object { $_.Path -like '*net8.0*' }).Count | Should -Be 2
        }

        It 'ignores a package that has no ref/ folder' {
            # Most packages in the family ship lib/ only, and their documentation is never trimmed,
            # so there is nothing to compare and nothing to report.
            $packages = New-LayoutPackage `
                -PackageName 'Microsoft.Data.SqlClient.Internal.Logging.7.1.0.nupkg' `
                -Files @{ 'lib/net8.0/Microsoft.Data.SqlClient.Internal.Logging.xml' = $script:TrimmedXml }

            { & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) } | Should -Not -Throw
        }

        It 'reports missing documentation as an error when the project generates it' {
            # Documentation vanishing from a project that declares it is the regression this check
            # exists to catch, so it must not be downgraded to a warning.
            $project = New-Project -GeneratesDocumentation
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath (New-TestDirectory) -ProjectPath $project `
                -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'missing-documentation'
            $finding.Severity | Should -Be 'error'
        }

        It 'fails when a project that generates documentation produced none' {
            $project = New-Project -GeneratesDocumentation

            { & $scriptPath -DocumentationPath (New-TestDirectory) -ProjectPath $project } |
                Should -Throw '*failed with 1 issue*'
        }

        It 'reports why nothing was checked when the project generates no documentation' {
            # A project that deliberately emits none must pass, but must still say so rather than
            # leaving a silently empty run.
            $project = New-Project
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -DocumentationPath (New-TestDirectory) -ProjectPath $project -ReportPath $report } |
                Should -Not -Throw

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'documentation-not-expected'
            $finding.Severity | Should -Be 'info'
            $finding.Message | Should -BeLike '*does not set GenerateDocumentationFile*'
        }

        It 'reports documentation found for a project that declares none' {
            # The project and the build disagree; the documentation is still validated.
            $project = New-Project
            $docs = New-DocumentationDirectory -Members @('T:Microsoft.Data.SqlClient.SqlConnection')
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -DocumentationPath $docs -ProjectPath $project -ReportPath $report } |
                Should -Not -Throw

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'unexpected-documentation'
            $finding.Severity | Should -Be 'warning'
        }

        It 'does not treat a comment mentioning the property as setting it' {
            # The project file is parsed as XML: a comment naming GenerateDocumentationFile, such
            # as one recording why it is absent, must not be read as enabling it.
            $project = New-Project -Comment 'GenerateDocumentationFile is deliberately not set.'
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath (New-TestDirectory) -ProjectPath $project `
                -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings.Category) | Should -Be 'documentation-not-expected'
        }

        It 'honours the last assignment when the property is set more than once' {
            $project = New-Project -GeneratesDocumentation -ThenDisables
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath (New-TestDirectory) -ProjectPath $project `
                -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings.Category) | Should -Be 'documentation-not-expected'
        }

        It 'validates only the snippets the project references' {
            $snippets = New-TestDirectory
            '<docs><members name="Used"><Used><see cref="T:System.Byte[]" /></Used></members></docs>' |
                Set-Content -LiteralPath (Join-Path $snippets 'Used.xml') -Encoding utf8
            '<docs><members name="Other"><Other><see cref="T:System.Char[]" /></Other></members></docs>' |
                Set-Content -LiteralPath (Join-Path $snippets 'Other.xml') -Encoding utf8

            $project = New-Project -SnippetReference (Join-Path $snippets 'Used.xml')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ProjectPath $project `
                -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings | Where-Object { $_.Category -eq 'invalid-docid' })
            $findings.Count | Should -Be 1
            $findings[0].Cref | Should -Be 'T:System.Byte[]'
        }

        It 'reports when a project references no snippets' {
            $snippets = New-TestDirectory
            '<docs><members name="S"><S><summary>x</summary></S></members></docs>' |
                Set-Content -LiteralPath (Join-Path $snippets 'S.xml') -Encoding utf8
            $project = New-Project
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ProjectPath $project `
                -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'documentation-not-expected'
            $finding.Message | Should -BeLike '*references no documentation snippets*'
        }

        It 'requires every documented assembly in a package to ship its documentation' {
            # The regression this replaces the old blanket check with: a package that drops its
            # XML file leaves consumers with no IntelliSense, and nothing else would notice.
            $projects = New-TestDirectory
            $projectDir = Join-Path $projects 'Shipped'
            New-Item -ItemType Directory -Path $projectDir | Out-Null
            @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Contoso.Shipped</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $projectDir 'Shipped.csproj') -Encoding utf8

            $staging = New-TestDirectory
            $libPath = Join-Path $staging 'lib/net8.0'
            New-Item -ItemType Directory -Path $libPath -Force | Out-Null
            'binary' | Set-Content -LiteralPath (Join-Path $libPath 'Contoso.Shipped.dll') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory(
                $staging, (Join-Path $packages 'Contoso.Shipped.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ProjectSearchRoot $projects -ReportPath $report -ReportOnly

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Category -eq 'missing-documentation' }) | Select-Object -First 1
            $finding | Should -Not -BeNullOrEmpty
            $finding.Severity | Should -Be 'error'
            $finding.Message | Should -BeLike '*Contoso.Shipped.xml*'
        }

        It 'accepts a package that ships documentation beside its assembly' {
            $projects = New-TestDirectory
            $projectDir = Join-Path $projects 'Shipped'
            New-Item -ItemType Directory -Path $projectDir | Out-Null
            @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Contoso.Shipped</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $projectDir 'Shipped.csproj') -Encoding utf8

            $staging = New-TestDirectory
            $libPath = Join-Path $staging 'lib/net8.0'
            New-Item -ItemType Directory -Path $libPath -Force | Out-Null
            'binary' | Set-Content -LiteralPath (Join-Path $libPath 'Contoso.Shipped.dll') -Encoding utf8
            '<doc><members><member name="T:Contoso.Shipped.Widget"><summary>W.</summary></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $libPath 'Contoso.Shipped.xml') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory(
                $staging, (Join-Path $packages 'Contoso.Shipped.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ProjectSearchRoot $projects -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings | Where-Object { $_.Category -eq 'missing-documentation' }) |
                Should -BeNullOrEmpty
        }

        It 'does not require documentation for an assembly whose project generates none' {
            $projects = New-TestDirectory
            $projectDir = Join-Path $projects 'Plain'
            New-Item -ItemType Directory -Path $projectDir | Out-Null
            @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Contoso.Plain</AssemblyName>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $projectDir 'Plain.csproj') -Encoding utf8

            $staging = New-TestDirectory
            $libPath = Join-Path $staging 'lib/net8.0'
            New-Item -ItemType Directory -Path $libPath -Force | Out-Null
            'binary' | Set-Content -LiteralPath (Join-Path $libPath 'Contoso.Plain.dll') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory(
                $staging, (Join-Path $packages 'Contoso.Plain.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ProjectSearchRoot $projects -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings | Where-Object { $_.Category -eq 'missing-documentation' }) |
                Should -BeNullOrEmpty
        }

        It 'fails when the supplied project file does not exist' {
            { & $scriptPath -DocumentationPath (New-TestDirectory) `
                    -ProjectPath (Join-Path $TestDrive 'missing.csproj') } |
                Should -Throw '*was not found*'
        }

        It 'does not report a directory holding only non-documentation XML as unexpected' {
            $path = New-TestDirectory
            '<project><target /></project>' |
                Set-Content -LiteralPath (Join-Path $path 'build.xml') -Encoding utf8
            $project = New-Project
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -DocumentationPath $path -ProjectPath $project -ReportPath $report } |
                Should -Not -Throw

            @((Get-Report -Path $report).Findings.Category) | Should -Be 'documentation-not-expected'
        }

        It 'does not report missing documentation when the files were malformed' {
            # malformed-xml already names the cause; a second finding would misdirect.
            $path = New-TestDirectory
            '<doc><members>' | Set-Content -LiteralPath (Join-Path $path 'Broken.xml') -Encoding utf8
            $project = New-Project -GeneratesDocumentation
            $report = Join-Path (New-TestDirectory) 'report.json'

            # Throws because the file could not be read; the report is still written beforehand.
            { & $scriptPath -DocumentationPath $path -ProjectPath $project -ReportPath $report -ReportOnly } |
                Should -Throw '*could not read 1 file*'

            $categories = @((Get-Report -Path $report).Findings.Category)
            $categories | Should -Contain 'malformed-xml'
            $categories | Should -Not -Contain 'missing-documentation'
        }

        It 'reports an unresolved cref from a public member as an error' {
            # Severity follows the member holding the reference: a public member's page is
            # published, so the unresolved reference becomes visible.
            $staging = New-TestDirectory
            New-Item -ItemType Directory -Path (Join-Path $staging 'lib/net8.0') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $staging 'ref/net8.0') -Force | Out-Null

            # The public member appears in ref/; the reference it makes resolves nowhere.
            '<doc><members><member name="T:Microsoft.Data.SqlClient.Widget"><summary>W.</summary>' +
            '<see cref="T:Microsoft.Data.SqlClient.Missing" /></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'lib/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8
            '<doc><members><member name="T:Microsoft.Data.SqlClient.Widget"><summary>W.</summary></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'ref/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $packages 'P.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ReportPath $report -ReportOnly

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Cref -eq 'T:Microsoft.Data.SqlClient.Missing' })[0]
            $finding.Category | Should -Be 'missing-public-uid'
            $finding.Severity | Should -Be 'error'
        }

        It 'reports the same cref from a non-public member as information' {
            $staging = New-TestDirectory
            New-Item -ItemType Directory -Path (Join-Path $staging 'lib/net8.0') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $staging 'ref/net8.0') -Force | Out-Null

            # The holder is absent from ref/, so it is not part of the public API surface.
            '<doc><members><member name="T:Microsoft.Data.SqlClient.Internals"><summary>I.</summary>' +
            '<see cref="T:Microsoft.Data.SqlClient.Missing" /></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'lib/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8
            '<doc><members><member name="T:Microsoft.Data.SqlClient.Widget"><summary>W.</summary></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'ref/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $packages 'P.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ReportPath $report -ReportOnly

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Cref -eq 'T:Microsoft.Data.SqlClient.Missing' })[0]
            $finding.Category | Should -Be 'missing-local-uid'
            $finding.Severity | Should -Be 'info'
        }

        It 'does not classify as public when no reference documentation identifies the surface' {
            # Without a ref/ folder the public API surface is unknown, so nothing is escalated.
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('T:Microsoft.Data.SqlClient.DoesNotExist')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'missing-local-uid'
            $finding.Severity | Should -Be 'info'
        }

        It 'does not apply layout rules outside package mode' {
            $docs = New-TestDirectory
            $libPath = Join-Path $docs 'lib/net8.0'
            New-Item -ItemType Directory -Path $libPath -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $libPath 'Microsoft.Data.SqlClient.xml') `
                -Value $script:TrimmedXml -Encoding utf8

            { & $scriptPath -DocumentationPath $docs } | Should -Not -Throw
        }
    }

    Context 'allowlist' {

        It 'exempts an allowlisted cref' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte[]')
            $allowlist = Join-Path (New-TestDirectory) 'allowlist.json'
            '{ "IgnoredCrefs": [ "T:System.Byte[]" ] }' | Set-Content -LiteralPath $allowlist -Encoding utf8

            { & $scriptPath -SnippetsDirectory $snippets -AllowlistPath $allowlist } | Should -Not -Throw
        }

        It 'fails on an allowlisted cref that no longer appears' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte')
            $allowlist = Join-Path (New-TestDirectory) 'allowlist.json'
            '{ "IgnoredCrefs": [ "T:System.Byte[]" ] }' | Set-Content -LiteralPath $allowlist -Encoding utf8
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -SnippetsDirectory $snippets -AllowlistPath $allowlist -ReportPath $report } |
                Should -Throw '*failed with 1 issue*'

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'stale-allowlist-entry'
            $finding.Message | Should -BeLike '*stale*'
        }

        It 'honours a configured namespace root' {
            $snippets = New-SnippetDirectory -Crefs @('T:Contoso.Widgets.Widget')
            $allowlist = Join-Path (New-TestDirectory) 'allowlist.json'
            '{ "AllowedNamespaceRoots": [ "Microsoft", "System", "Contoso" ] }' |
                Set-Content -LiteralPath $allowlist -Encoding utf8

            { & $scriptPath -SnippetsDirectory $snippets -AllowlistPath $allowlist } | Should -Not -Throw
        }
    }

    Context 'reporting and gating' {

        It 'writes the report even when validation fails' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte[]')
            $report = Join-Path (New-TestDirectory) 'nested' 'report.json'

            { & $scriptPath -SnippetsDirectory $snippets -ReportPath $report } | Should -Throw

            Test-Path -LiteralPath $report | Should -BeTrue
            (Get-Report -Path $report).CountsByCategory.'invalid-docid' | Should -Be 1
        }

        It 'records the file and line of each finding' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte[]')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Path | Should -BeLike '*Sample.xml'
            $finding.LineNumber | Should -BeGreaterThan 0
            $finding.Member | Should -Be 'SampleType'
        }

        It 'marks the task succeeded-with-issues when findings are reported' {
            # task.logissue records an issue but leaves the task result alone, so without this the
            # step renders as a clean success despite reporting warnings.
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte[]')

            $output = & $scriptPath -SnippetsDirectory $snippets -ReportOnly *>&1 | Out-String

            $output | Should -Match '##vso\[task\.complete result=SucceededWithIssues;\]'
        }

        It 'marks the task succeeded-with-issues for non-gating findings in a gating run' {
            # mismatched-docid-prefix is a warning, so the run passes, but it must still be visible.
            $docs = New-DocumentationDirectory `
                -Members @('P:Microsoft.Data.SqlClient.SqlCommand.CommandTimeout') `
                -Crefs @('M:Microsoft.Data.SqlClient.SqlCommand.CommandTimeout')

            $output = & $scriptPath -DocumentationPath $docs *>&1 | Out-String

            $output | Should -Match 'mismatched-docid-prefix'
            $output | Should -Match '##vso\[task\.complete result=SucceededWithIssues;\]'
        }

        It 'does not mark the task succeeded-with-issues for informational findings alone' {
            # Informational findings describe things that are correct as they stand, so marking on
            # them would leave every run permanently flagged.
            $snippets = New-SnippetDirectory -Crefs @('SqlJson', 'string')

            $output = & $scriptPath -SnippetsDirectory $snippets *>&1 | Out-String

            $output | Should -Match 'unprefixed-cref'
            $output | Should -Not -Match 'task\.complete'
            $output | Should -Not -Match '##vso\[task\.logissue'
        }

        It 'does not mark the task succeeded-with-issues when there are no findings' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte')

            $output = & $scriptPath -SnippetsDirectory $snippets *>&1 | Out-String

            $output | Should -Not -Match 'task\.complete'
            $output | Should -Match 'validation passed'
        }

        It 'does not fail in report-only mode' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte[]')

            { & $scriptPath -SnippetsDirectory $snippets -ReportOnly } | Should -Not -Throw
        }

        It 'accepts -FailOn as a single comma-separated argument' {
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('T:Microsoft.Data.SqlClient.DoesNotExist')

            { & $scriptPath -DocumentationPath $docs -FailOn 'error,missing-local-uid' } |
                Should -Throw '*failed with 1 issue*'
        }

        It 'rejects an unknown -FailOn token' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte')

            { & $scriptPath -SnippetsDirectory $snippets -FailOn 'nonsense' } |
                Should -Throw "*Unknown -FailOn token 'nonsense'*"
        }

        It 'reports every finding in one run rather than stopping at the first' {
            $snippets = New-SnippetDirectory -Crefs @(
                'T:System.Byte[]',
                'T:System.Char[]',
                'M:Microsoft.Data.SqlClient.SqlConnection.GetSchema()')

            { & $scriptPath -SnippetsDirectory $snippets } |
                Should -Throw '*failed with 3 issues*'
        }
    }

    Context 'malformed input' {

        It 'reports a file that is not well-formed XML' {
            $path = New-TestDirectory
            '<docs><members>' | Set-Content -LiteralPath (Join-Path $path 'Broken.xml') -Encoding utf8
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -SnippetsDirectory $path -ReportPath $report } | Should -Throw

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'malformed-xml'
        }

        It 'fails on malformed XML even in report-only mode' {
            # Report-only downgrades findings, but a file that could not be parsed was never
            # examined, so suppressing it would report an all-clear for content nobody read.
            $path = New-TestDirectory
            '<docs><members>' | Set-Content -LiteralPath (Join-Path $path 'Broken.xml') -Encoding utf8

            { & $scriptPath -SnippetsDirectory $path -ReportOnly } |
                Should -Throw '*could not read 1 file*'
        }

        It 'still downgrades ordinary findings in report-only mode alongside readable files' {
            # The malformed-XML rule must not make report-only useless for everything else.
            $snippets = New-SnippetDirectory -Crefs @('T:System.Byte[]')

            { & $scriptPath -SnippetsDirectory $snippets -ReportOnly } | Should -Not -Throw
        }

        It 'continues past a malformed file to validate the rest' {
            $path = New-TestDirectory
            '<docs><members>' | Set-Content -LiteralPath (Join-Path $path 'Broken.xml') -Encoding utf8
            '<docs><members name="S"><S><see cref="T:System.Byte[]" /></S></members></docs>' |
                Set-Content -LiteralPath (Join-Path $path 'Good.xml') -Encoding utf8
            $report = Join-Path (New-TestDirectory) 'report.json'

            # The unreadable file decides the failure message, but the readable one is still
            # validated and its finding recorded.
            { & $scriptPath -SnippetsDirectory $path -ReportPath $report } |
                Should -Throw '*could not read 1 file*'

            $categories = @((Get-Report -Path $report).Findings.Category)
            $categories | Should -Contain 'malformed-xml'
            $categories | Should -Contain 'invalid-docid'
        }

        It 'fails when no input paths are supplied' {
            { & $scriptPath } | Should -Throw '*No input was supplied*'
        }

        It 'fails when a supplied path does not exist' {
            { & $scriptPath -SnippetsDirectory (Join-Path $TestDrive 'nope') } |
                Should -Throw '*was not found*'
        }

        It 'fails when -PackagesPath is supplied without -ExtractPath' {
            { & $scriptPath -PackagesPath (New-TestDirectory) } |
                Should -Throw '*requires -ExtractPath*'
        }
    }
}
