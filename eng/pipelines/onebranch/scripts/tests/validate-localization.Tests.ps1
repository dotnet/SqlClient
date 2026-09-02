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

    It 'warns without failing when enforcement is disabled' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.ja.resx') @{ Greeting = 'Hello' }

        $output = (& $scriptPath -ResourcesDirectory $resources -Enforce $false *>&1) -join [Environment]::NewLine

        $output | Should -Match ([regex]::Escape(
            '##vso[task.logissue type=warning]Strings.ja.resx: untranslated values match Strings.resx: Greeting'))
        $output | Should -Match (
            'Localization validation found 1 issue. Enforcement is disabled for this build; review the preceding warnings.')
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

    It 'accepts an approved English-value match from the allowlist' {
        $resources = New-ResourcesDirectory
        $allowlist = Join-Path $resources 'allowlist.json'
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Hello' }
        @{ AllowedEnglishValueMatches = @{ 'Strings.fr.resx' = @('Greeting') } } |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $allowlist

        { & $scriptPath -ResourcesDirectory $resources -AllowlistPath $allowlist } |
            Should -Not -Throw
    }

    It 'does not allowlist a missing localized key' {
        $resources = New-ResourcesDirectory
        $allowlist = Join-Path $resources 'allowlist.json'
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello'; Farewell = 'Goodbye' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Bonjour' }
        @{ AllowedEnglishValueMatches = @{ 'Strings.fr.resx' = @('Farewell') } } |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $allowlist

        { & $scriptPath -ResourcesDirectory $resources -AllowlistPath $allowlist } |
            Should -Throw '*Localization validation failed with 1 error. Review the preceding errors.*'
    }

    It 'rejects allowlist for unknown resource keys' {
        $resources = New-ResourcesDirectory
        $allowlist = Join-Path $resources 'allowlist.json'
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Bonjour' }
        @{ AllowedEnglishValueMatches = @{ 'Strings.fr.resx' = @('Unknown') } } |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $allowlist

        { & $scriptPath -ResourcesDirectory $resources -AllowlistPath $allowlist } |
            Should -Throw '*Localization validation failed with 1 error. Review the preceding errors.*'
    }

    It 'warns when an allowlist key is no longer in Strings.resx and enforcement is disabled' {
        $resources = New-ResourcesDirectory
        $allowlist = Join-Path $resources 'allowlist.json'
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Bonjour' }
        @{ AllowedEnglishValueMatches = @{ 'Strings.fr.resx' = @('Removed') } } |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $allowlist

        $output = (& $scriptPath -ResourcesDirectory $resources -AllowlistPath $allowlist -Enforce $false *>&1) -join [Environment]::NewLine

        $output | Should -Match ([regex]::Escape(
            "##vso[task.logissue type=warning]Localization allowlist file references unknown resource key 'Removed' for 'Strings.fr.resx'."))
        $output | Should -Match (
            'Localization validation found 1 issue. Enforcement is disabled for this build; review the preceding warnings.')
    }

    It 'still fails for malformed allowlist JSON when enforcement is disabled' {
        $resources = New-ResourcesDirectory
        $allowlist = Join-Path $resources 'allowlist.json'
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Bonjour' }
        Set-Content -LiteralPath $allowlist -Value '{'

        { & $scriptPath -ResourcesDirectory $resources -AllowlistPath $allowlist -Enforce $false } |
            Should -Throw
    }

    It 'scopes approved English-value matches to one localized file' {
        $resources = New-ResourcesDirectory
        $allowlist = Join-Path $resources 'allowlist.json'
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.de.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Hello' }
        @{ AllowedEnglishValueMatches = @{ 'Strings.de.resx' = @('Greeting') } } |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $allowlist

        { & $scriptPath -ResourcesDirectory $resources -AllowlistPath $allowlist } |
            Should -Throw '*Localization validation failed with 1 error. Review the preceding errors.*'
    }

    It 'fails when a localized file contains a key absent from Strings.resx' {
        $resources = New-ResourcesDirectory
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Bonjour'; Obsolete = 'Ancien' }

        { & $scriptPath -ResourcesDirectory $resources } |
            Should -Throw '*Localization validation failed with 1 error. Review the preceding errors.*'
    }

    It 'rejects an allowlist entry after the localized value is translated' {
        $resources = New-ResourcesDirectory
        $allowlist = Join-Path $resources 'allowlist.json'
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Greeting = 'Hello' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Greeting = 'Bonjour' }
        @{ AllowedEnglishValueMatches = @{ 'Strings.fr.resx' = @('Greeting') } } |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $allowlist

        { & $scriptPath -ResourcesDirectory $resources -AllowlistPath $allowlist } |
            Should -Throw '*Localization validation failed with 1 error. Review the preceding errors.*'
    }

    It 'rejects allowlist entries for empty English values' {
        $resources = New-ResourcesDirectory
        $allowlist = Join-Path $resources 'allowlist.json'
        Set-ResourceFile (Join-Path $resources 'Strings.resx') @{ Unused = '' }
        Set-ResourceFile (Join-Path $resources 'Strings.fr.resx') @{ Unused = '' }
        @{ AllowedEnglishValueMatches = @{ 'Strings.fr.resx' = @('Unused') } } |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $allowlist

        { & $scriptPath -ResourcesDirectory $resources -AllowlistPath $allowlist } |
            Should -Throw '*does not have a non-empty English value*'
    }
}
