<#
.SYNOPSIS
    Pester tests for validate-localization.ps1.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'validate-localization.ps1'

    function Set-ResourceFile {
        param(
            [Parameter(Mandatory)][string]$Path,
            [Parameter(Mandatory)][hashtable]$Strings
        )

        $document = [System.Xml.XmlDocument]::new()
        $root = $document.CreateElement('root')
        $null = $document.AppendChild($root)
        foreach ($entry in $Strings.GetEnumerator()) {
            $data = $document.CreateElement('data')
            $data.SetAttribute('name', $entry.Key)

            $value = $document.CreateElement('value')
            $value.InnerText = $entry.Value
            $null = $data.AppendChild($value)
            $null = $root.AppendChild($data)
        }

        $document.Save($Path)
    }

    function New-ResourcesDirectory {
        $path = Join-Path $TestDrive ([guid]::NewGuid().ToString('n'))
        New-Item -ItemType Directory -Path $path | Out-Null
        return $path
    }
}

Describe 'validate-localization.ps1' {
    It 'accepts complete localized files with translated values' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello'; Farewell = 'Goodbye' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Bonjour'; Farewell = 'Au revoir' }

        { & $scriptPath -ResourcesDirectory $resources } | Should -Not -Throw
    }

    It 'fails when a localized file is missing an English key' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello'; Farewell = 'Goodbye' }
        Set-ResourceFile (Join-Path $resources 'Strings.de.resx') @{ Greeting = 'Hallo' }

        { & $scriptPath -ResourcesDirectory $resources } |
            Should -Throw '*Localization validation failed with 1 error. Review the preceding errors.*'
    }

    It 'fails when a localized value matches a non-empty English value' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello'; Unused = '' }
        Set-ResourceFile (Join-Path $resources 'Strings.ja.resx') @{ Greeting = 'Hello'; Unused = '' }

        { & $scriptPath -ResourcesDirectory $resources } |
            Should -Throw '*Localization validation failed with 1 error. Review the preceding errors.*'
    }

    It 'fails when no localized resource files exist' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }

        { & $scriptPath -ResourcesDirectory $resources } |
            Should -Throw '*No localized Strings.*.resx files were found*'
    }

    It 'fails when a non-empty English string has an empty localized value' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.es.resx') @{ Greeting = ' ' }

        { & $scriptPath -ResourcesDirectory $resources } |
            Should -Throw '*Localization validation failed with 1 error. Review the preceding errors.*'
    }

    It 'accepts empty localized values when the English value is also empty' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Unused = '' }
        Set-ResourceFile (Join-Path $resources 'Strings.ko.resx') @{ Unused = '' }

        { & $scriptPath -ResourcesDirectory $resources } | Should -Not -Throw
    }

    It 'fails when a resource data element has no value' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Bonjour' }
        [xml]$localized = Get-Content -LiteralPath (Join-Path $resources 'Strings.fr.resx')
        $valueNode = $localized.SelectSingleNode('/root/data/value')
        $null = $valueNode.ParentNode.RemoveChild($valueNode)
        $localized.Save((Join-Path $resources 'Strings.fr.resx'))

        { & $scriptPath -ResourcesDirectory $resources } |
            Should -Throw '*contains a <data> element without a name or value*'
    }
}
