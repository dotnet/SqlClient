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

        It 'rejects a signature whose parameter list is never closed' {
            $snippets = New-SnippetDirectory -Crefs @('M:Microsoft.Data.SqlClient.SqlConnection.GetSchema(System.String')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'invalid-docid'
            $finding.Message | Should -BeLike '*unterminated parameter list*'
        }

        <#
            The closing parenthesis precedes the opening one, so the argument arithmetic would ask
            Substring for a negative length. An exception there ends the run before the report is
            written, so this asserts the report exists rather than only the finding.
        #>
        It 'rejects a signature whose closing parenthesis precedes the opening one' {
            $snippets = New-SnippetDirectory -Crefs @('M:Microsoft.Data.SqlClient.SqlConnection.GetSchema)(')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $report | Should -Exist
            $finding = (Get-Report -Path $report).Findings | Select-Object -First 1
            $finding.Category | Should -Be 'invalid-docid'
            $finding.Message | Should -BeLike '*unterminated parameter list*'
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

        It 'rejects a C# alias nested in a generic type cref' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Collections.Generic.List{string}')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'invalid-docid'
            $findings[0].Message | Should -BeLike "*C# alias 'string'*"
        }

        It 'reports each distinct alias nested in a generic type cref' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Collections.Generic.Dictionary{string,int}')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Message | Should -BeLike "*C# aliases 'string', 'int'*"
        }

        It 'accepts a generic type cref whose arguments are fully qualified' {
            $snippets = New-SnippetDirectory -Crefs @('T:System.Collections.Generic.List{System.String}')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings).Count | Should -Be 0
        }

        It 'rejects a C# alias nested in the declaring type of a member cref' {
            $snippets = New-SnippetDirectory -Crefs @('M:System.Collections.Generic.List{string}.Add(System.String)')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Message | Should -BeLike "*C# alias 'string'*"
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

        It 'rejects unbalanced generic argument braces' -ForEach @(
            @{ Cref = 'T:System.Collections.Generic.List{System.String' }
            @{ Cref = 'T:System.Collections.Generic.List}System.String{' }
            @{ Cref = 'M:Microsoft.Data.SqlClient.Sample.Use(System.Collections.Generic.List{System.String)' }
        ) {
            $snippets = New-SnippetDirectory -Crefs @($Cref)
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'invalid-docid'
            $findings[0].Message | Should -BeLike '*unbalanced braces*'
        }

        It 'accepts balanced generic argument braces' -ForEach @(
            @{ Cref = 'T:System.Collections.Generic.List{System.String}' }
            @{ Cref = 'T:System.Collections.Generic.Dictionary{System.String,System.Collections.Generic.List{System.Int32}}' }
            @{ Cref = 'M:Microsoft.Data.SqlClient.Sample.Use(System.Collections.Generic.List{System.String})' }
        ) {
            $snippets = New-SnippetDirectory -Crefs @($Cref)
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings) | Should -BeNullOrEmpty
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

        <#
            A conversion operator's return type sits after the parameter list, which is where the
            alias scan stops and where the name taken from before the '(' has already ended. These
            cover that the return type is scanned like any other part of the signature.
        #>
        It 'rejects a C# alias in a conversion operator return type' -ForEach @(
            @{ Cref = 'M:Microsoft.Data.SqlTypes.SqlJson.op_Implicit(System.Int32)~string' }
            @{ Cref = 'M:Microsoft.Data.SqlTypes.SqlJson.op_Explicit(System.Int32)~System.Collections.Generic.List{int}' }
        ) {
            $snippets = New-SnippetDirectory -Crefs @($Cref)
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'invalid-docid'
            $findings[0].Message | Should -BeLike '*in its signature*'
        }

        It 'reports the parameter and return aliases of one signature together' {
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlTypes.SqlJson.op_Implicit(int)~string')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Message | Should -BeLike "*C# aliases 'int', 'string'*"
        }

        It 'accepts a fully qualified generic conversion operator return type' {
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlTypes.SqlJson.op_Explicit(System.Int32)~System.Collections.Generic.List{System.Int32}')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'rejects text after a parameter list that is not a return marker' {
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlTypes.SqlJson.Parse(System.String)garbage')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'invalid-docid'
            $findings[0].Message | Should -BeLike '*unexpected text after its parameter list*'
        }

        It 'rejects a return marker that names no type' {
            $snippets = New-SnippetDirectory -Crefs @(
                'M:Microsoft.Data.SqlTypes.SqlJson.op_Implicit(System.String)~')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -SnippetsDirectory $snippets -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'invalid-docid'
            $findings[0].Message | Should -BeLike '*names no return type*'
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

        <#
            A namespace has no <member> entry of its own, so these three cover the set derived
            from the members that do: a namespace they occupy resolves, a misspelling of it does
            not, and a type named with N: is still reported for its prefix rather than being
            admitted by that set.
        #>
        It 'resolves a namespace that the emitted members occupy' {
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('N:Microsoft.Data.SqlClient')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings) | Should -BeNullOrEmpty
        }

        It 'reports a misspelled local namespace' {
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('N:Microsoft.Data.SqlClinet')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'missing-local-uid'
            $findings[0].Message | Should -BeLike '*namespace this repository does not contain*'
        }

        It 'reports a type named with the namespace prefix as a prefix mismatch' {
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('N:Microsoft.Data.SqlClient.SqlConnection')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'mismatched-docid-prefix'
            $findings[0].Message | Should -BeLike "*was emitted as 'T:'*"
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

        <#
            An overload that was never emitted shares its prefix with the overload that was, so
            matching on the identifier alone would advise replacing a prefix with itself. It is a
            member this build does not contain, not a mistyped prefix.
        #>
        It 'reports an overload that was not emitted as a missing member' {
            $docs = New-DocumentationDirectory `
                -Members @('M:Microsoft.Data.SqlClient.SqlCommand.ExecuteReader') `
                -Crefs @('M:Microsoft.Data.SqlClient.SqlCommand.ExecuteReader(System.String)')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'missing-local-uid'
        }

        It 'still names the expected prefix when an overload carries the wrong member kind' {
            $docs = New-DocumentationDirectory `
                -Members @('P:Microsoft.Data.SqlClient.SqlCommand.CommandTimeout') `
                -Crefs @('M:Microsoft.Data.SqlClient.SqlCommand.CommandTimeout(System.String)')
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report -ReportOnly

            $findings = @((Get-Report -Path $report).Findings)
            $findings.Count | Should -Be 1
            $findings[0].Category | Should -Be 'mismatched-docid-prefix'
            $findings[0].Message | Should -BeLike "*was emitted as 'P:'*"
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

    Context 'dependency documentation' {

        BeforeAll {
            <#
                Builds the shape the packaged gate hits when a sibling package was not built this
                run: a public member referencing a type that lives in another package of this
                repository. Written as a package so the ref/ folder establishes the public API
                surface, which is what makes the unresolved reference an error rather than
                information.
            #>
            function New-PackageReferencingSibling {
                $staging = New-TestDirectory
                New-Item -ItemType Directory -Path (Join-Path $staging 'lib/net8.0') -Force | Out-Null
                New-Item -ItemType Directory -Path (Join-Path $staging 'ref/net8.0') -Force | Out-Null

                '<doc><members><member name="T:Microsoft.Data.SqlClient.Widget"><summary>W.</summary>' +
                '<remarks>R.</remarks><see cref="T:Microsoft.SqlServer.Server.Sibling" /></member></members></doc>' |
                    Set-Content -LiteralPath (Join-Path $staging 'lib/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8
                '<doc><members><member name="T:Microsoft.Data.SqlClient.Widget"><summary>W.</summary>' +
                '<see cref="T:Microsoft.SqlServer.Server.Sibling" /></member></members></doc>' |
                    Set-Content -LiteralPath (Join-Path $staging 'ref/net8.0/Microsoft.Data.SqlClient.xml') -Encoding utf8

                $packages = New-TestDirectory
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $packages 'P.1.0.0.nupkg'))
                return $packages
            }

            function New-SiblingDocumentation {
                param([string]$Member = 'T:Microsoft.SqlServer.Server.Sibling')

                $path = New-TestDirectory
                "<doc><members><member name=""$Member""><summary>S.</summary></member></members></doc>" |
                    Set-Content -LiteralPath (Join-Path $path 'Microsoft.SqlServer.Server.xml') -Encoding utf8
                return $path
            }
        }

        It 'reports a cref into a package that was not built without dependency documentation' {
            # Establishes the defect the parameter exists to address, so the test below is shown to
            # be suppressing a real finding rather than passing for an unrelated reason.
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath (New-PackageReferencingSibling) -ExtractPath (New-TestDirectory) `
                -ReportPath $report -ReportOnly

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Cref -eq 'T:Microsoft.SqlServer.Server.Sibling' })[0]
            $finding.Category | Should -Be 'missing-public-uid'
        }

        It 'resolves that cref against the dependency documentation' {
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -PackagesPath (New-PackageReferencingSibling) -ExtractPath (New-TestDirectory) `
                    -DependencyDocumentationPath (New-SiblingDocumentation) -ReportPath $report } |
                Should -Not -Throw

            @((Get-Report -Path $report).Findings.Cref) | Should -Not -Contain 'T:Microsoft.SqlServer.Server.Sibling'
        }

        It 'still reports a cref the dependency documentation does not contain' {
            # The parameter supplies members to resolve against; it does not exempt the namespace.
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath (New-PackageReferencingSibling) -ExtractPath (New-TestDirectory) `
                -DependencyDocumentationPath (New-SiblingDocumentation -Member 'T:Microsoft.SqlServer.Server.Other') `
                -ReportPath $report -ReportOnly

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Cref -eq 'T:Microsoft.SqlServer.Server.Sibling' })[0]
            $finding.Category | Should -Be 'missing-public-uid'
        }

        It 'does not validate the dependency documentation itself' {
            # A defect in an already-published package cannot be fixed by the run that reports it.
            $dependency = New-TestDirectory
            '<doc><members><member name="T:Microsoft.SqlServer.Server.Sibling"><summary>S.</summary>' +
            '<see cref="T:System.Byte[]" /></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $dependency 'Microsoft.SqlServer.Server.xml') -Encoding utf8
            $report = Join-Path (New-TestDirectory) 'report.json'

            # T:System.Byte[] is an invalid-docid, and would be an error had the file been validated.
            { & $scriptPath -PackagesPath (New-PackageReferencingSibling) -ExtractPath (New-TestDirectory) `
                    -DependencyDocumentationPath $dependency -ReportPath $report } | Should -Not -Throw

            $result = Get-Report -Path $report
            @($result.Findings.Cref) | Should -Not -Contain 'T:System.Byte[]'
            $result.DependencyDocumentationFiles | Should -Be 1
            # Counted separately, so the summary still describes only what was validated.
            $result.FilesValidated | Should -Be 2
        }

        It 'does not let dependency documentation establish the public API surface' {
            # Public classification must describe this build's surface. A member is public because
            # this run emitted it into ref/, never because a dependency package did.
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('T:Microsoft.Data.SqlClient.DoesNotExist')
            $dependency = New-TestDirectory
            New-Item -ItemType Directory -Path (Join-Path $dependency 'ref/net8.0') -Force | Out-Null
            '<doc><members><member name="T:Microsoft.Data.SqlClient.Sample"><summary>S.</summary></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $dependency 'ref/net8.0/Other.xml') -Encoding utf8
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -DependencyDocumentationPath $dependency -ReportPath $report

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Cref -eq 'T:Microsoft.Data.SqlClient.DoesNotExist' })[0]
            $finding.Category | Should -Be 'missing-local-uid'
            $finding.Severity | Should -Be 'info'
        }

        It 'resolves a namespace cref against the dependency documentation' {
            # A namespace has no member entry of its own, so it resolves against the namespaces the
            # indexed members occupy -- which must include those the dependencies contribute.
            $docs = New-DocumentationDirectory `
                -Members @('T:Microsoft.Data.SqlClient.SqlConnection') `
                -Crefs @('N:Microsoft.SqlServer.Server')
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -DocumentationPath $docs `
                    -DependencyDocumentationPath (New-SiblingDocumentation) -ReportPath $report } |
                Should -Not -Throw

            @((Get-Report -Path $report).Findings.Cref) | Should -Not -Contain 'N:Microsoft.SqlServer.Server'
        }

        It 'fails when the dependency documentation path does not exist' {
            $docs = New-DocumentationDirectory

            { & $scriptPath -DocumentationPath $docs `
                    -DependencyDocumentationPath (Join-Path (New-TestDirectory) 'absent') } |
                Should -Throw '*Dependency documentation path*was not found*'
        }

        It 'does not resolve against dependencies when nothing is under validation' {
            # An index built from dependencies alone would describe members this build never
            # emitted, so supplying them cannot by itself enable local resolution.
            $snippets = New-SnippetDirectory -Crefs @('T:Microsoft.SqlServer.Server.Sibling')
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -SnippetsDirectory $snippets `
                    -DependencyDocumentationPath (New-SiblingDocumentation) -ReportPath $report } |
                Should -Not -Throw

            (Get-Report -Path $report).DocumentationFiles | Should -Be 0
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

    <#
        Pins the severity of every category.

        The step template documents which categories the default 'error' gate covers, and nothing
        else ties that prose to this table, so a severity changed here would silently leave the
        documented contract wrong. Failing this test is the prompt to update
        eng/pipelines/onebranch/steps/validate-xml-docs-step.yml alongside the change.
    #>
    Context 'severity contract' {

        It 'assigns the documented severity to every category' {
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $scriptPath, [ref]$null, [ref]$null)
            $assignment = $ast.Find({
                param($node)
                $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
                $node.Left.Extent.Text -eq '$script:CategorySeverities'
            }, $true)
            $assignment | Should -Not -BeNullOrEmpty

            $actual = [scriptblock]::Create($assignment.Right.Extent.Text).Invoke()[0]

            $expected = [ordered]@{
                'malformed-xml'                   = 'error'
                'unresolved-cref'                 = 'error'
                'invalid-docid'                   = 'error'
                'unknown-namespace-root'          = 'error'
                'stale-allowlist-entry'           = 'error'
                'lib-documentation-trimmed'       = 'error'
                'ref-documentation-untrimmed'     = 'error'
                'lib-ref-documentation-identical' = 'error'
                'missing-public-uid'              = 'error'
                'mismatched-public-docid-prefix'  = 'error'
                'enum-field-remarks'              = 'error'
                'unresolved-include'              = 'error'
                'missing-documentation'           = 'error'
                'missing-local-uid'               = 'info'
                'mismatched-docid-prefix'         = 'warning'
                'unexpected-documentation'        = 'warning'
                'documentation-not-expected'      = 'info'
                'missing-external-uid'            = 'warning'
                'unprefixed-cref'                 = 'info'
            }

            ($actual.Keys | Sort-Object) -join ',' |
                Should -Be (($expected.Keys | Sort-Object) -join ',')
            foreach ($category in $expected.Keys) {
                $actual[$category] | Should -Be $expected[$category] -Because "$category is documented as $($expected[$category])"
            }
        }
    }

    Context 'enum field remarks' {

        BeforeAll {
            <#
                Writes a source tree declaring an enum, which is the only thing that says which
                documented members are enum fields.
            #>
            function New-EnumSource {
                param(
                    [string]$TypeName = 'Widget',
                    [string[]]$Members = @('First', 'Second'),
                    [string]$Extra = ''
                )

                $path = New-TestDirectory
                $body = ($Members | ForEach-Object { "        $_," }) -join "`n"
                @"
namespace Contoso
{
    public enum $TypeName
    {
$body
    }
$Extra
}
"@ | Set-Content -LiteralPath (Join-Path $path 'Widget.cs') -Encoding utf8

                # A project file, because the enum search root follows the project the caller names.
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>' +
                '<GenerateDocumentationFile>true</GenerateDocumentationFile>' +
                '</PropertyGroup></Project>' |
                    Set-Content -LiteralPath (Join-Path $path 'Sample.csproj') -Encoding utf8

                return $path
            }

            <#
                Writes a snippet in the shape the repository uses: one block per member, nested
                under a <members> element naming the type.
            #>
            function New-EnumSnippet {
                param(
                    [Parameter(Mandatory)][string]$Body,
                    [string]$TypeName = 'Widget'
                )

                $path = New-TestDirectory
                "<docs><members name=`"$TypeName`">$Body</members></docs>" |
                    Set-Content -LiteralPath (Join-Path $path "$TypeName.xml") -Encoding utf8
                return $path
            }
        }

        It 'reports remarks on an enum field' {
            $source = New-EnumSource
            $snippets = New-EnumSnippet -Body (
                '<Widget><summary>W.</summary></Widget>' +
                '<First><summary>F.</summary><remarks>Discarded.</remarks></First>')
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -SnippetsDirectory $snippets -ProjectSearchRoot $source -ReportPath $report } |
                Should -Throw

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Category -eq 'enum-field-remarks' })[0]
            $finding.Severity | Should -Be 'error'
            $finding.Member | Should -Be 'Widget.First'
        }

        It 'accepts remarks on the block documenting the type itself' {
            # A type's remarks are rendered; only a field's are discarded.
            $source = New-EnumSource
            $snippets = New-EnumSnippet -Body '<Widget><summary>W.</summary><remarks>Kept.</remarks></Widget>'

            { & $scriptPath -SnippetsDirectory $snippets -ProjectSearchRoot $source } | Should -Not -Throw
        }

        It 'accepts remarks on a platform-variant block documenting the type' {
            # WidgetNetfx documents the type for another platform, and is not a declared member, so
            # it must not be mistaken for a field merely because its name differs from the type's.
            $source = New-EnumSource
            $snippets = New-EnumSnippet -Body (
                '<Widget><summary>W.</summary></Widget>' +
                '<WidgetNetfx><summary>W.</summary><remarks>Kept.</remarks></WidgetNetfx>')

            { & $scriptPath -SnippetsDirectory $snippets -ProjectSearchRoot $source } | Should -Not -Throw
        }

        It 'accepts remarks on a member of a type that is not an enum' {
            $source = New-EnumSource
            $snippets = New-EnumSnippet -TypeName 'Gadget' -Body (
                '<Gadget><summary>G.</summary></Gadget>' +
                '<First><summary>F.</summary><remarks>Kept.</remarks></First>')

            { & $scriptPath -SnippetsDirectory $snippets -ProjectSearchRoot $source } | Should -Not -Throw
        }

        It 'reports remarks on a public enum field in generated documentation' {
            # A documentation ID states the member kind, so the field is recognized there too.
            $source = New-EnumSource
            $staging = New-TestDirectory
            New-Item -ItemType Directory -Path (Join-Path $staging 'lib/net8.0') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $staging 'ref/net8.0') -Force | Out-Null
            '<doc><members><member name="F:Contoso.Widget.First"><summary>F.</summary>' +
            '<remarks>Discarded.</remarks></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'lib/net8.0/Contoso.xml') -Encoding utf8
            '<doc><members><member name="F:Contoso.Widget.First"><summary>F.</summary></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'ref/net8.0/Contoso.xml') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $packages 'P.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ProjectSearchRoot $source -ReportPath $report -ReportOnly

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Category -eq 'enum-field-remarks' })[0]
            $finding.Member | Should -Be 'F:Contoso.Widget.First'
        }

        It 'ignores an internal enum field in generated documentation' {
            # The implementation assembly documents its own P/Invoke enums. They reach no published
            # page, so nothing is lost by remarks the build discards.
            $source = New-EnumSource
            $staging = New-TestDirectory
            New-Item -ItemType Directory -Path (Join-Path $staging 'lib/net8.0') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $staging 'ref/net8.0') -Force | Out-Null
            '<doc><members><member name="F:Contoso.Widget.First"><summary>F.</summary>' +
            '<remarks>Internal.</remarks></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'lib/net8.0/Contoso.xml') -Encoding utf8
            # The field is absent from ref/, so it is not part of the public API surface.
            '<doc><members><member name="T:Contoso.Widget"><summary>W.</summary></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $staging 'ref/net8.0/Contoso.xml') -Encoding utf8

            $packages = New-TestDirectory
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $packages 'P.1.0.0.nupkg'))
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -PackagesPath $packages -ExtractPath (New-TestDirectory) `
                -ProjectSearchRoot $source -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings.Category) | Should -Not -Contain 'enum-field-remarks'
        }

        It 'does not report a generated field when the public API surface is unknown' {
            # Without reference documentation nothing says which members publish, and the rule is
            # not escalated on a guess.
            $source = New-EnumSource
            $docs = New-TestDirectory
            '<doc><members><member name="F:Contoso.Widget.First"><summary>F.</summary>' +
            '<remarks>Unknown.</remarks></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $docs 'Contoso.xml') -Encoding utf8

            { & $scriptPath -DocumentationPath $docs -ProjectSearchRoot $source } | Should -Not -Throw
        }

        It 'accepts remarks on a property of a class in generated documentation' {
            $source = New-EnumSource
            $docs = New-TestDirectory
            '<doc><members><member name="P:Contoso.Widget.First"><summary>F.</summary>' +
            '<remarks>Kept.</remarks></member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $docs 'Contoso.xml') -Encoding utf8

            { & $scriptPath -DocumentationPath $docs -ProjectSearchRoot $source } | Should -Not -Throw
        }

        It 'does not apply the rule when no source tree identifies the enums' {
            # Without declarations there is nothing to distinguish a field from a type, and
            # guessing would report the very blocks that are legitimate.
            $snippets = New-EnumSnippet -Body (
                '<Widget><summary>W.</summary></Widget>' +
                '<First><summary>F.</summary><remarks>Unknown.</remarks></First>')

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'ignores an enum member that is only mentioned in a comment' {
            $source = New-EnumSource -Members @('First') -Extra @'
    // Second, is commented out and is not a member.
'@
            $snippets = New-EnumSnippet -Body (
                '<Widget><summary>W.</summary></Widget>' +
                '<Second><summary>S.</summary><remarks>Kept.</remarks></Second>')

            { & $scriptPath -SnippetsDirectory $snippets -ProjectSearchRoot $source } | Should -Not -Throw
        }

        It 'reads every member of an enum whose entries carry attributes and values' {
            $source = New-TestDirectory
            @'
namespace Contoso
{
    public enum Widget
    {
        [System.ComponentModel.Description("a, b")]
        First = 1,
        Second = (2 + 3),
        Third,
    }
}
'@ | Set-Content -LiteralPath (Join-Path $source 'Widget.cs') -Encoding utf8

            $snippets = New-EnumSnippet -Body (
                '<Widget><summary>W.</summary></Widget>' +
                '<Third><summary>T.</summary><remarks>Discarded.</remarks></Third>')
            $report = Join-Path (New-TestDirectory) 'report.json'

            # Third is the last entry and follows one carrying a comma inside an attribute, so it is
            # only found when entries are split on the commas that actually separate members.
            { & $scriptPath -SnippetsDirectory $snippets -ProjectSearchRoot $source -ReportPath $report } |
                Should -Throw

            @((Get-Report -Path $report).Findings.Category) | Should -Contain 'enum-field-remarks'
        }
    }

    Context 'unresolved include' {

        It 'reports an include the compiler could not resolve' {
            # The compiler copies an unmatched include into its output rather than failing, so the
            # member ships with no documentation and nothing else says so.
            $docs = New-TestDirectory
            '<doc><members><member name="M:Microsoft.Data.SqlClient.SqlCommand.BeginExecuteXmlReader(System.AsyncCallback,System.Object)">' +
            '<!-- No matching elements were found for the following include tag -->' +
            '<include file="SqlCommand.xml" path="docs/members[@name=&quot;SqlCommand&quot;]/BeginExecuteXmlReader[@name=&quot;AsyncCallbackAndstateObject&quot;]/*" />' +
            '</member></members></doc>' |
                Set-Content -LiteralPath (Join-Path $docs 'Microsoft.Data.SqlClient.xml') -Encoding utf8
            $report = Join-Path (New-TestDirectory) 'report.json'

            { & $scriptPath -DocumentationPath $docs -ReportPath $report } | Should -Throw

            $finding = @((Get-Report -Path $report).Findings |
                    Where-Object { $_.Category -eq 'unresolved-include' })[0]
            $finding.Severity | Should -Be 'error'
            $finding.Member | Should -Be 'M:Microsoft.Data.SqlClient.SqlCommand.BeginExecuteXmlReader(System.AsyncCallback,System.Object)'
            # The path is quoted back, because the mismatch is usually a letter's case within it.
            $finding.Message | Should -BeLike '*AsyncCallbackAndstateObject*'
        }

        It 'accepts documentation whose includes all resolved' {
            $docs = New-DocumentationDirectory -Members @('T:Microsoft.Data.SqlClient.SqlConnection')

            { & $scriptPath -DocumentationPath $docs } | Should -Not -Throw
        }

        It 'does not report an include inside a snippet' {
            # A snippet is the include's target, not its consumer; the compiler never reads one
            # looking for includes, so an element there says nothing about a member losing its
            # documentation.
            $snippets = New-TestDirectory
            '<docs><members name="SqlCommand"><ExecuteReader><summary>S.</summary>' +
            '<include file="Other.xml" path="docs/members/Thing/*" /></ExecuteReader></members></docs>' |
                Set-Content -LiteralPath (Join-Path $snippets 'SqlCommand.xml') -Encoding utf8

            { & $scriptPath -SnippetsDirectory $snippets } | Should -Not -Throw
        }

        It 'reports every unresolved include rather than only the first' {
            $docs = New-TestDirectory
            '<doc><members>' +
            '<member name="P:Microsoft.Data.SqlClient.A"><include file="S.xml" path="docs/members/A/*" /></member>' +
            '<member name="P:Microsoft.Data.SqlClient.B"><include file="S.xml" path="docs/members/B/*" /></member>' +
            '</members></doc>' |
                Set-Content -LiteralPath (Join-Path $docs 'Microsoft.Data.SqlClient.xml') -Encoding utf8
            $report = Join-Path (New-TestDirectory) 'report.json'

            & $scriptPath -DocumentationPath $docs -ReportPath $report -ReportOnly

            @((Get-Report -Path $report).Findings |
                Where-Object { $_.Category -eq 'unresolved-include' }).Count | Should -Be 2
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
