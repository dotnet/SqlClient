<#
.SYNOPSIS
    Validates the XML documentation cross-references that feed dotnet/sqlclient-api-docs.

.DESCRIPTION
    Open Publishing resolves every <see>/<seealso>/<exception> cref in our XML documentation
    against the Learn xref map. A cref whose documentation ID is malformed cannot resolve, and the
    resulting xref-not-found warnings only surface after an API Docs pull request has already been
    opened. This script is the local preflight for that round trip.

    Validation runs in two modes, and a single invocation may use either or both:

      Source mode (-SnippetsDirectory) reads the doc/snippets files that the compiler pulls into
      the generated XML via <include>. It needs no build, so it gates in seconds and reports the
      file and line a developer actually edits.

      Documentation mode (-DocumentationPath) reads generated or packaged XML documentation. It
      sees what actually ships, so it additionally catches crefs the compiler failed to bind (which
      it rewrites to a "!:" prefix) and can resolve our own UIDs against the members the build
      really emitted.

    The rules are deliberately offline. Every xref-not-found warning reported against
    dotnet/sqlclient-api-docs PR 99 is a documentation-ID syntax defect or a namespace typo, both
    of which are detectable without consulting the Learn xref service. Avoiding that service keeps
    the check usable where outbound network access is unavailable, and avoids downloading the
    published Learn .NET xref map, which is roughly 338 MB. Resolution against that map is
    optional, via -ExternalXrefMapPath.

    Findings are collected rather than thrown one at a time, so a single run reports every defect.

    Package mode additionally checks how documentation was mapped into the package. The driver
    ships two XML documentation files per target framework and they are required to differ: lib/
    carries the full text for the .NET API docs pipeline that builds the Learn pages, while ref/
    has <remarks> and <example> stripped, because those render badly in Visual Studio IntelliSense,
    which reads the ref/ copy. The two have been silently collapsed before -- in 7.1.0 the modern
    lib/ XML was byte-identical to the trimmed ref/ XML, so the Learn pages lost every remark and
    example. The lib/ref pairs are therefore compared against each other rather than
    against an absolute expectation, which keeps the rule meaningful for packages that legitimately
    have no ref/ folder at all.

.PARAMETER SnippetsDirectory
    One or more directories scanned recursively for documentation snippet .xml files.

.PARAMETER DocumentationPath
    One or more generated XML documentation files, or directories scanned recursively for them.
    Only files carrying a <doc>/<members> structure are treated as XML documentation; anything else
    found in a scanned directory is ignored, so a build output tree may be passed wholesale.

.PARAMETER PackagesPath
    Optional directory scanned recursively for .nupkg files. Each is expanded beneath -ExtractPath
    and the XML documentation inside it is validated, which is what a consumer actually receives.
    Requires -ExtractPath.

.PARAMETER DependencyDocumentationPath
    Optional XML documentation files, or directories scanned recursively for them, supplied only so
    that references resolve against them. Their own contents are never validated.

    A package carries only its own documentation, so a cref from one package into a sibling package
    resolves against nothing whenever that sibling was not built in the same run -- the state a
    pipeline is in when it depends on the published version of a package instead of building it.
    Build output does not have this problem, because a dependency's documentation file is copied
    next to the assembly that consumed it.

    Supply the documentation from the dependency version the packages under validation actually
    declare, so that what resolves here is what a consumer will resolve against.

.PARAMETER ExtractPath
    Directory that -PackagesPath expands into. Existing expansions are replaced so a rerun cannot
    validate stale content.

.PARAMETER AllowlistPath
    Optional JSON file carrying approved exceptions and namespace configuration. Recognized keys:

      AllowedNamespaceRoots   Leading identifiers a prefixed cref may use. Default: Microsoft,
                              System, Interop. Interop is included because the implementation
                              assembly documents its internal P/Invoke types, which never reach
                              the published documentation but do appear in the lib/ XML.
      LocalNamespacePrefixes  Namespaces owned by this repository, which must resolve against the
                              documentation being validated. Default: Microsoft.Data, Microsoft.SqlServer.
      IgnoredCrefs            Exact cref values to exempt from all cref rules.

    Ignored crefs that no longer appear are reported as stale, so an obsolete exception cannot
    silently weaken future validation.

.PARAMETER ExternalXrefMapPath
    Optional path to a downloaded Learn .NET xref map (.xrefmap.json). When supplied, crefs outside
    the local namespaces are additionally resolved against it. The published map is roughly 338 MB
    and must be downloaded separately, so this is intended for local investigation rather than
    routine use.

.PARAMETER ReportPath
    Optional path of a JSON report to write. Parent directories are created as needed. The report
    is always written before gating, so it exists even when validation fails.

.PARAMETER FailOn
    Finding severities and/or categories that fail the build. Severities are error, warning and
    info; categories are listed in the table below. Defaults to error.

    Accepts either an array or a single comma-separated string, because an Azure Pipelines task
    argument line collapses to one token and PowerShell's -File mode does not split it.

.PARAMETER ReportOnly
    Report findings without failing, overriding -FailOn. Used to shake the gate out on a pipeline
    before its findings are fixed. The official pipeline never sets this.

    Does not cover a file that could not be parsed. Such a file was never examined, so suppressing
    it would report an all-clear for content nobody read.

.PARAMETER ProjectSearchRoot
    Directory scanned recursively for project files, used with -PackagesPath to check that every
    assembly whose project sets GenerateDocumentationFile ships its XML documentation beside it in
    lib/ and ref/. Derived from the projects rather than a list, so it stays correct as packages
    are added or change.

.PARAMETER ProjectPath
    Project file whose documentation expectations apply to the supplied inputs. The project is the
    declaration, so nothing needs restating in the caller:

      GenerateDocumentationFile=true   XML documentation must be produced; its absence is an error.
      otherwise                        No XML documentation is expected. Its absence is reported as
                                       information naming the reason, and its presence is reported
                                       as a warning, so the project and the build cannot disagree
                                       silently.

    When the project's sources reference documentation snippets, only the snippet files they
    reference are validated; a project referencing none is reported as information.

.OUTPUTS
    Findings are categorized as:

      malformed-xml           error    File is not well-formed XML. Fails the step even in
                                       report-only mode: the file was never examined, so none of
                                       its cross-references were checked and there is no result
                                       to downgrade.
      unresolved-cref         error    Compiler could not bind the cref and emitted a "!:" prefix.
      invalid-docid           error    Documentation ID violates the documentation-ID grammar.
      unknown-namespace-root  error    Leading identifier is not an allowed namespace root.
      stale-allowlist-entry   error    Allowlisted cref no longer appears in any validated file.
      lib-documentation-trimmed
                              error    A package's lib/ XML has no remarks or examples, so the
                                       Learn API pages built from it would show only summaries.
      ref-documentation-untrimmed
                              error    A package's ref/ XML still carries remarks or examples.
      lib-ref-documentation-identical
                              error    A package's lib/ and ref/ XML are byte-identical, so the
                                       nuspec mapped one artifact into both targets.
      missing-public-uid      error    Cref names this repository but no such member was emitted,
                                       and it is referenced from a public API member, so the
                                       published page carries an unresolved reference. Reported
                                       only when reference documentation identifies which members
                                       are public.
      missing-local-uid       info     As above, but referenced from a member that is not public,
                                       or from any member when the public API surface is unknown.
                                       Not an error because such a member is never published, and
                                       because a reference may legitimately resolve elsewhere: to
                                       another target framework, or to a sibling assembly that this
                                       run did not include.
      mismatched-public-docid-prefix
                              error    Cref names a real member but with the wrong kind prefix,
                                       such as M: on a property, and is referenced from a public
                                       API member. The wrong prefix produces a UID that matches
                                       nothing, so the published page carries an unresolved
                                       reference.
      mismatched-docid-prefix warning  As above, but referenced from a member that is not public,
                                       or from any member when the public API surface is unknown.
      missing-external-uid    warning  Cref is absent from the supplied Learn xref map.
      unprefixed-cref         info     Cref carries no "T:"/"M:"/... prefix. Legal; the compiler
                                       binds it. Reported for visibility only.
      missing-documentation   error    Documentation that should exist does not. A project sets
                                       GenerateDocumentationFile but produced none, or a package
                                       ships an assembly without its documentation file.
      unexpected-documentation
                              warning  Documentation was found for a project that does not set
                                       GenerateDocumentationFile. The project and the build
                                       disagree; the documentation is still validated.

.EXAMPLE
    ./validate-xml-docs.ps1 -SnippetsDirectory ./doc/snippets

.EXAMPLE
    ./validate-xml-docs.ps1 `
        -DocumentationPath ./artifacts/bin `
        -ReportPath ./out/xml-docs-validation.json `
        -FailOn error,missing-local-uid
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [string[]]$SnippetsDirectory,

    [string[]]$DocumentationPath,

    [string[]]$DependencyDocumentationPath,

    [string]$PackagesPath,

    [string]$ExtractPath,

    [string]$AllowlistPath,

    [string]$ExternalXrefMapPath,

    [string]$ReportPath,

    [string[]]$FailOn = @('error'),

    [switch]$ReportOnly,

    [string]$ProjectPath,

    [string]$ProjectSearchRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Severity of each finding category. Only the categories named here may be produced, and -FailOn
# gates on either a category or the severity it maps to.
$script:CategorySeverities = [ordered]@{
    'malformed-xml'          = 'error'
    'unresolved-cref'        = 'error'
    'invalid-docid'          = 'error'
    'unknown-namespace-root' = 'error'
    'stale-allowlist-entry'  = 'error'
    'lib-documentation-trimmed'      = 'error'
    'ref-documentation-untrimmed'    = 'error'
    'lib-ref-documentation-identical' = 'error'
    'missing-public-uid'     = 'error'
    'mismatched-public-docid-prefix' = 'error'
    'missing-local-uid'      = 'info'
    'mismatched-docid-prefix' = 'warning'
    'missing-documentation'  = 'error'
    'unexpected-documentation' = 'warning'
    'documentation-not-expected' = 'info'
    'missing-external-uid'   = 'warning'
    'unprefixed-cref'        = 'info'
}

# C# keyword aliases. A documentation ID names CLR types, so an alias in a cref is always a defect
# even though it reads correctly in source.
$script:CSharpAliases = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        'bool', 'byte', 'char', 'decimal', 'double', 'float', 'int', 'long', 'nint', 'nuint',
        'object', 'sbyte', 'short', 'string', 'uint', 'ulong', 'ushort', 'void'
    ),
    [System.StringComparer]::Ordinal)

# Documentation ID prefixes defined by the C# specification, plus "!" which the compiler emits for
# a cref it could not bind.
$script:KnownDocIdPrefixes = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('N', 'T', 'F', 'P', 'M', 'E', '!'),
    [System.StringComparer]::Ordinal)

$script:Findings = [System.Collections.Generic.List[object]]::new()

function Add-Finding {
    param(
        [Parameter(Mandatory)][string]$Category,
        [Parameter(Mandatory)][string]$Message,
        [string]$Path,
        [int]$LineNumber,
        [string]$Cref,
        [string]$Member
    )

    if (-not $script:CategorySeverities.Contains($Category)) {
        throw "Internal error: unknown finding category '$Category'."
    }

    $script:Findings.Add([pscustomobject]@{
            Category   = $Category
            Severity   = $script:CategorySeverities[$Category]
            Message    = $Message
            Path       = $Path
            LineNumber = $LineNumber
            Cref       = $Cref
            Member     = $Member
        })
}

<#
    Splits a documentation ID argument list on commas that sit outside any nesting. Generic
    arguments use braces in a documentation ID (List{System.String}), so a naive split on comma
    would tear nested generics apart and report phantom parameters.
#>
function Split-DocIdArguments {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Arguments)

    $parts = [System.Collections.Generic.List[string]]::new()
    $depth = 0
    $start = 0
    for ($index = 0; $index -lt $Arguments.Length; $index++) {
        switch ($Arguments[$index]) {
            '{' { $depth++ }
            '(' { $depth++ }
            '[' { $depth++ }
            '}' { $depth-- }
            ')' { $depth-- }
            ']' { $depth-- }
            ',' {
                if ($depth -eq 0) {
                    $parts.Add($Arguments.Substring($start, $index - $start))
                    $start = $index + 1
                }
            }
        }
    }
    $parts.Add($Arguments.Substring($start))

    return $parts
}

<#
    Reduces a documentation ID parameter to the bare type identifier so it can be compared against
    the C# alias set: array, pointer and by-reference markers are stripped, as are generic
    arguments, which are validated separately as parameters in their own right.
#>
function Get-DocIdCoreTypeName {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$TypeName)

    $core = $TypeName.Trim()
    $braceIndex = $core.IndexOf('{')
    if ($braceIndex -ge 0) {
        $core = $core.Substring(0, $braceIndex)
    }

    return $core.TrimEnd('[', ']', '@', '*', '&')
}

<#
    Applies the documentation-ID grammar to one cref and records every violation it carries.

    Ordering matters: a cref is reported against the most specific rule that explains it, and
    reporting stops there. A cref such as "T:string" is an alias defect, not an unknown namespace
    root, and emitting both would send a reader chasing the wrong fix.
#>
function Test-Cref {
    param(
        [Parameter(Mandatory)][string]$Cref,
        [Parameter(Mandatory)][hashtable]$Context
    )

    $trimmed = $Cref.Trim()
    if ([string]::IsNullOrEmpty($trimmed)) {
        Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message 'Cref is empty.'
        return
    }

    # An unprefixed cref is bound by the compiler from the surrounding source context, so its
    # documentation ID is generated rather than authored and none of the grammar rules below apply.
    if ($trimmed.Length -lt 2 -or $trimmed[1] -ne ':') {
        Add-Finding @Context -Category 'unprefixed-cref' -Cref $Cref -Message (
            "Cref '$trimmed' has no documentation-ID prefix. The compiler binds it from source " +
            'context, so it is not validated here.')
        return
    }

    $prefix = [string]$trimmed[0]
    $body = $trimmed.Substring(2)

    if (-not $script:KnownDocIdPrefixes.Contains($prefix)) {
        Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
            "Cref '$trimmed' uses unknown documentation-ID prefix '${prefix}:'. Expected one of " +
            'N:, T:, F:, P:, M: or E:.')
        return
    }

    if ($prefix -eq '!') {
        Add-Finding @Context -Category 'unresolved-cref' -Cref $Cref -Message (
            "Cref '$body' could not be bound by the compiler, which emitted it as '!:'. It will " +
            'never resolve in the published documentation.')
        return
    }

    if ([string]::IsNullOrWhiteSpace($body)) {
        Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
            "Cref '$trimmed' has a '${prefix}:' prefix but no member identifier.")
        return
    }

    # A documentation ID is a single token. Whitespace anywhere inside it, most commonly a space
    # after a comma in a signature copied from C# source, prevents the xref from resolving.
    # Validation continues against the whitespace-stripped form, because such a cref is usually
    # pasted from source and carries alias defects too, and reporting only the whitespace would
    # send the author back for a second round.
    if ($body -match '\s') {
        $body = ($body -replace '\s', '')
        Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
            "Cref '$trimmed' contains whitespace. Documentation IDs contain no whitespace; use " +
            "'${prefix}:$body'.")
    }

    # A documentation ID writes generic arguments in braces, as List{System.String}. Angle brackets
    # are C# source syntax and never appear in a documentation ID, so they are rejected wherever
    # they occur rather than only in a signature.
    if ($body -match '[<>]') {
        $suggestion = ($body -replace '<', '{') -replace '>', '}'
        Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
            "Cref '$trimmed' uses angle brackets for its generic arguments. Documentation IDs use " +
            "braces; use '${prefix}:$suggestion'.")
        return
    }

    # Those braces must pair up. An unmatched or misnested one leaves an identifier that names
    # nothing, and it would otherwise survive: the generic argument list is read from the first
    # brace to the last, so a missing delimiter silently yields a different set of arguments than
    # the text suggests, or none at all.
    $depth = 0
    foreach ($character in $body.ToCharArray()) {
        if ($character -eq '{') {
            $depth++
        }
        elseif ($character -eq '}') {
            $depth--

            # A closing brace with nothing open cannot be balanced by anything later, and leaving
            # the count negative reports it below.
            if ($depth -lt 0) {
                break
            }
        }
    }

    if ($depth -ne 0) {
        Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
            "Cref '$trimmed' has unbalanced braces around its generic arguments. Documentation " +
            'IDs pair every { with a later }.')
        return
    }

    $signatureStart = $body.IndexOf('(')
    $namePart = if ($signatureStart -ge 0) { $body.Substring(0, $signatureStart) } else { $body }

    if ($signatureStart -ge 0) {
        # The conversion-operator return marker (~) trails the parameter list, so the argument text
        # ends at the last ')' rather than at the end of the body.
        $signatureEnd = $body.LastIndexOf(')')

        # Catches a missing ')' and one that precedes the '(', which is not a parameter list at
        # all. LastIndexOf answers -1 when the character is absent, which is below every valid
        # opening position, so both forms fail this comparison. Reaching the arithmetic below with
        # either would ask Substring for a negative length, and the resulting exception would
        # abandon the run without writing the report that report-only mode exists to produce.
        if ($signatureEnd -lt $signatureStart) {
            Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
                "Cref '$trimmed' has an unterminated parameter list.")
            return
        }

        $arguments = $body.Substring($signatureStart + 1, $signatureEnd - $signatureStart - 1)

        # A parameterless method's documentation ID is written without parentheses. Emitting "()"
        # produces a UID that matches nothing.
        if ([string]::IsNullOrWhiteSpace($arguments)) {
            Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
                "Cref '$trimmed' declares an empty parameter list. A parameterless member omits " +
                "the parentheses entirely; use '${prefix}:$namePart'.")
            return
        }

        # Only a conversion operator may carry anything after its parameter list, written as the
        # return marker ~ followed by a type. Anything else there is outside the grammar, and would
        # otherwise go unexamined: the checks below read the name before the '(' and the arguments
        # within it, so text beyond the ')' belongs to neither.
        $returnType = $null
        $trailing = $body.Substring($signatureEnd + 1)
        if ($trailing.Length -gt 0) {
            if ($trailing[0] -ne '~') {
                Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
                    "Cref '$trimmed' has unexpected text after its parameter list. Only a " +
                    "conversion operator's return marker, written as '~' followed by a type, may " +
                    'follow it.')
                return
            }

            $returnType = $trailing.Substring(1)
            if ($returnType.Length -eq 0) {
                Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
                    "Cref '$trimmed' ends with a conversion operator return marker but names no " +
                    'return type.')
                return
            }
        }

        # A signature may repeat the same alias (string and string[] both reduce to string), so
        # report the distinct offenders once rather than once per parameter. A conversion
        # operator's return type is part of its signature, so it is scanned with the parameters.
        $signatureTypes = [System.Collections.Generic.List[string]]::new()
        foreach ($argument in (Split-DocIdArguments -Arguments $arguments)) {
            $signatureTypes.Add($argument)
        }
        if ($null -ne $returnType) {
            $signatureTypes.Add($returnType)
        }

        $aliases = [System.Collections.Generic.List[string]]::new()
        foreach ($argument in $signatureTypes) {
            foreach ($alias in (Get-DocIdAlias -TypeName $argument)) {
                if (-not $aliases.Contains($alias)) {
                    $aliases.Add($alias)
                }
            }
        }
        if ($aliases.Count -gt 0) {
            $quoted = ($aliases | ForEach-Object { "'$_'" }) -join ', '
            $noun = if ($aliases.Count -eq 1) { 'the C# alias' } else { 'the C# aliases' }
            Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
                "Cref '$trimmed' uses $noun $quoted in its signature. Documentation IDs name " +
                'CLR types, so use the full type name instead.')
            return
        }
    }

    # An array, pointer or by-reference construction is not a named type, so it has no type page
    # and no UID in the Learn xref map. Only a T: cref can make this mistake; the same suffixes are
    # legal inside a member signature.
    #
    # The array suffix is matched in any of its forms: [] for one dimension, [,] for more, and the
    # documentation-ID spelling [0:,0:] that records lower bounds.
    if ($prefix -eq 'T' -and $body -match '(\[[\d:,]*\]|\*|@|&)$') {
        $element = $body -replace '(\[[\d:,]*\]|\*|@|&)+$', ''
        Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
            "Cref '$trimmed' names a constructed type, which has no documentation page. " +
            "Reference the element type instead, for example <see cref=`"T:$element`" /> array.")
        return
    }

    # Aliases are gathered recursively rather than from the outer name alone, because a generic
    # argument is itself a type reference: T:List{string} is as wrong as T:string, and the outer
    # name of the former is a perfectly ordinary type. One cref can carry several distinct
    # aliases, so they are reported together instead of one finding per argument.
    $typeAliases = [System.Collections.Generic.List[string]]::new()
    foreach ($alias in (Get-DocIdAlias -TypeName $namePart)) {
        if (-not $typeAliases.Contains($alias)) {
            $typeAliases.Add($alias)
        }
    }
    if ($typeAliases.Count -gt 0) {
        $quotedTypeAliases = ($typeAliases | ForEach-Object { "'$_'" }) -join ', '
        $aliasNoun = if ($typeAliases.Count -eq 1) { 'the C# alias' } else { 'the C# aliases' }
        Add-Finding @Context -Category 'invalid-docid' -Cref $Cref -Message (
            "Cref '$trimmed' uses $aliasNoun $quotedTypeAliases. Documentation IDs name CLR " +
            'types, so use the full type name instead.')
        return
    }

    # The leading identifier catches misspelled namespaces, which are otherwise indistinguishable
    # from a valid reference to a type this build does not contain.
    $root = ($namePart -split '[.`{]', 2)[0]
    if (-not $script:AllowedNamespaceRoots.Contains($root)) {
        $allowed = ($script:AllowedNamespaceRoots | Sort-Object) -join ', '
        Add-Finding @Context -Category 'unknown-namespace-root' -Cref $Cref -Message (
            "Cref '$trimmed' starts with unknown namespace root '$root'. Allowed roots: $allowed. " +
            'Check for a misspelled namespace.')
        return
    }

    # Anything this repository owns must be present in the documentation under validation. This can
    # only be judged when generated documentation was supplied; source mode has no member list.
    if ($null -ne $script:LocalUids) {
        $isLocal = $false
        foreach ($localPrefix in $script:LocalNamespacePrefixes) {
            if ($namePart -eq $localPrefix -or $namePart.StartsWith("$localPrefix.", [System.StringComparison]::Ordinal)) {
                $isLocal = $true
                break
            }
        }

        if ($isLocal) {
            # Compared against the normalized UID rather than the original: the whitespace rule
            # above rewrote $body, so a cref whose only defect is whitespace still resolves here
            # and is reported once, for the whitespace, instead of also as a prefix mismatch.
            $normalized = "${prefix}:$body"
            if (-not $script:LocalUids.Contains($normalized)) {
                # The same member under a different prefix is the common case here: a cref written
                # as M: for a property, or T: for a member, names something real but produces a UID
                # that matches nothing. Say which prefix was expected rather than reporting a bare
                # lookup failure.
                #
                # The identifier is looked up with its signature first and without it second. The
                # second form explains a prefix mismatch only when the prefixes actually emitted
                # differ from the one written, because a cref naming an overload that does not
                # exist shares its prefix with the overload that does. Reporting that as a
                # mismatch would advise replacing a prefix with itself and would hide a member
                # this build never emitted.
                $key = $null
                if ($script:LocalUidsByBody.ContainsKey($body)) {
                    $key = $body
                }
                elseif ($script:LocalUidsByBody.ContainsKey($namePart) -and
                        -not $script:LocalUidsByBody[$namePart].Contains("${prefix}:")) {
                    $key = $namePart
                }

                if ($null -ne $key) {
                    $actual = ($script:LocalUidsByBody[$key] | Sort-Object) -join ', '
                    $message = "Cref '$trimmed' uses prefix '${prefix}:', but '$key' was emitted " +
                        "as '$actual'. Use the prefix matching the member kind."

                    # Classified the same way as an unresolvable reference below, and for the same
                    # reason: the wrong prefix produces a UID that matches nothing, so from a
                    # public member it leaves an unresolved reference on the published page.
                    if (Test-ContainingMemberIsPublic -Context $Context) {
                        Add-Finding @Context -Category 'mismatched-public-docid-prefix' -Cref $Cref -Message $message
                    }
                    else {
                        Add-Finding @Context -Category 'mismatched-docid-prefix' -Cref $Cref -Message $message
                    }
                }
                elseif ($prefix -eq 'N' -and $script:LocalNamespaces.Contains($body)) {
                    # A namespace has no <member> entry of its own, so it resolves against the
                    # namespaces derived from the members that were emitted. Reached only after
                    # the member index above has ruled out the body naming a type, so an N: cref
                    # written for a type is still reported as a prefix mismatch.
                    return
                }
                else {
                    # A namespace is described by the members occupying it rather than by an entry
                    # of its own, so it needs its own wording: naming a member that was not emitted
                    # says nothing about a cref that never named a member.
                    $subject = if ($prefix -eq 'N') {
                        "names a namespace this repository does not contain, as no documented " +
                        'member occupies it'
                    }
                    else {
                        'names this repository but no matching documented member was emitted by ' +
                        'the build'
                    }

                    if (Test-ContainingMemberIsPublic -Context $Context) {
                        Add-Finding @Context -Category 'missing-public-uid' -Cref $Cref -Message (
                            "Cref '$trimmed' $subject. It is referenced from a public API " +
                            'member, so the published page will carry an unresolved reference.')
                    }
                    else {
                        Add-Finding @Context -Category 'missing-local-uid' -Cref $Cref -Message (
                            "Cref '$trimmed' $subject.")
                    }
                }
            }
            return
        }
    }

    if ($null -ne $script:ExternalUids -and -not $script:ExternalUids.Contains($body)) {
        Add-Finding @Context -Category 'missing-external-uid' -Cref $Cref -Message (
            "Cref '$trimmed' was not found in the supplied Learn xref map.")
    }
}

<#
    Reads GenerateDocumentationFile from a project file.

    The project is parsed as XML rather than searched as text, because a comment mentioning the
    property would otherwise be read as setting it. Only the last assignment is honoured, matching
    MSBuild's last-one-wins evaluation within a file.
#>
function Test-ProjectGeneratesDocumentation {
    param([Parameter(Mandatory)][string]$Path)

    $document = [System.Xml.Linq.XDocument]::Load($Path)
    $value = $null
    foreach ($element in $document.Descendants()) {
        if ($element.Name.LocalName -eq 'GenerateDocumentationFile') {
            $value = $element.Value.Trim()
        }
    }

    return $value -eq 'true'
}

<#
    Returns the documentation files referenced by a project's sources.

    Snippets reach the compiler through <include file='...'/> in a doc comment, so the references
    live in the source files rather than in the project file. Paths are relative to the file that
    declares them. Every include is returned; the caller narrows them to the snippet directory it
    was given, so no assumption is made about where snippets live.
#>
function Get-ReferencedSnippetFile {
    param([Parameter(Mandatory)][string]$ProjectDirectory)

    $pattern = [regex]"include\s+file\s*=\s*'([^']+)'"
    $referenced = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($source in Get-ChildItem -LiteralPath $ProjectDirectory -Filter '*.cs' -File -Recurse -ErrorAction SilentlyContinue) {
        foreach ($match in $pattern.Matches([System.IO.File]::ReadAllText($source.FullName))) {
            $candidate = Join-Path $source.DirectoryName $match.Groups[1].Value
            try {
                $resolved = [System.IO.Path]::GetFullPath($candidate)
            }
            catch {
                continue
            }
            [void]$referenced.Add($resolved)
        }
    }

    # Comma prevents PowerShell from unrolling the set on return, which would yield $null for an
    # empty set and a bare string for a single entry.
    return , $referenced
}

<#
    Returns the assembly names whose projects generate XML documentation.

    AssemblyName falls back to the project file name, matching MSBuild. Two projects may share an
    assembly name, such as an implementation and its reference assembly, so the result is a set.
#>
function Get-DocumentedAssemblyName {
    param([Parameter(Mandatory)][string]$SearchRoot)

    $names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($project in Get-ChildItem -LiteralPath $SearchRoot -Filter '*.csproj' -File -Recurse -ErrorAction SilentlyContinue) {
        if (-not (Test-ProjectGeneratesDocumentation -Path $project.FullName)) {
            continue
        }

        $document = [System.Xml.Linq.XDocument]::Load($project.FullName)
        $assemblyName = $null
        foreach ($element in $document.Descendants()) {
            if ($element.Name.LocalName -eq 'AssemblyName') {
                $assemblyName = $element.Value.Trim()
            }
        }

        if ([string]::IsNullOrWhiteSpace($assemblyName)) {
            $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($project.FullName)
        }

        [void]$names.Add($assemblyName)
    }

    return , $names
}

<#
    Reports whether a documentation file sits in the ref/ folder of an expanded package.

    Only a package has this structure, so this is how reference documentation is recognized without
    the caller naming it. The path is compared against the expanded package roots rather than
    searched for a "ref" segment anywhere, so an unrelated directory called ref cannot be mistaken
    for one.
#>
function Test-IsReferenceDocumentationPath {
    param([Parameter(Mandatory)][string]$Path)

    foreach ($packageRoot in $script:ExpandedPackageRoots.Keys) {
        $rootFull = (Resolve-Path -LiteralPath $packageRoot).Path
        if (-not $Path.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $relative = $Path.Substring($rootFull.Length).TrimStart([char]'/', [char]'\')
        $segments = $relative -split '[/\\]'
        if ($segments.Count -ge 2 -and $segments[0] -eq 'ref') {
            return $true
        }
    }

    return $false
}

<#
    Returns every C# alias used anywhere in a documentation ID parameter.

    Generic arguments are inspected recursively rather than discarded, because an alias nested
    inside one, as in List{string}, is just as wrong as an alias at the top level and would
    otherwise pass unreported.
#>
function Get-DocIdAlias {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$TypeName)

    $found = [System.Collections.Generic.List[string]]::new()

    $core = Get-DocIdCoreTypeName -TypeName $TypeName
    if ($script:CSharpAliases.Contains($core)) {
        $found.Add($core)
    }

    # Recurse into the generic argument list, which Get-DocIdCoreTypeName deliberately drops.
    $trimmedType = $TypeName.Trim()
    $braceIndex = $trimmedType.IndexOf('{')
    if ($braceIndex -ge 0) {
        $closeIndex = $trimmedType.LastIndexOf('}')
        if ($closeIndex -gt $braceIndex) {
            $inner = $trimmedType.Substring($braceIndex + 1, $closeIndex - $braceIndex - 1)
            foreach ($argument in (Split-DocIdArguments -Arguments $inner)) {
                foreach ($alias in (Get-DocIdAlias -TypeName $argument)) {
                    $found.Add($alias)
                }
            }
        }
    }

    return , $found
}

<#
    Reports whether the member holding a cref is part of the published API surface.

    Severity follows the member that holds a reference, not the reference itself: a public member's
    page is published, so a reference it cannot resolve becomes a visible xref-not-found. An
    internal member is never published, so the same reference reaches no reader. Every rule that
    reports an unresolvable reference uses this, so they cannot classify the same situation
    differently.

    Answers false when no reference documentation identified the surface, which leaves such a
    finding at its lower severity rather than escalating on a guess.
#>
function Test-ContainingMemberIsPublic {
    param([Parameter(Mandatory)][hashtable]$Context)

    return $null -ne $script:PublicUids -and
        -not [string]::IsNullOrEmpty($Context.Member) -and
        $script:PublicUids.Contains($Context.Member)
}

function Resolve-InputPaths {
    param(
        [string[]]$Paths,
        [Parameter(Mandatory)][string]$Description
    )

    $resolved = [System.Collections.Generic.List[string]]::new()
    foreach ($path in @($Paths)) {
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }
        if (-not (Test-Path -LiteralPath $path)) {
            throw "$Description path '$path' was not found."
        }

        $item = Get-Item -LiteralPath $path
        if ($item.PSIsContainer) {
            foreach ($file in Get-ChildItem -LiteralPath $item.FullName -Filter '*.xml' -File -Recurse) {
                $resolved.Add($file.FullName)
            }
        }
        else {
            $resolved.Add($item.FullName)
        }
    }

    return ($resolved | Sort-Object -Unique)
}

# Load configuration -------------------------------------------------------------------------

$script:AllowedNamespaceRoots = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@('Microsoft', 'System', 'Interop'), [System.StringComparer]::Ordinal)
$script:LocalNamespacePrefixes = @('Microsoft.Data', 'Microsoft.SqlServer')
$ignoredCrefs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$observedIgnoredCrefs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

if (-not [string]::IsNullOrWhiteSpace($AllowlistPath)) {
    if (-not (Test-Path -LiteralPath $AllowlistPath -PathType Leaf)) {
        throw "XML documentation allowlist file '$AllowlistPath' was not found."
    }

    $configuration = Get-Content -LiteralPath $AllowlistPath -Raw | ConvertFrom-Json

    $rootsProperty = $configuration.PSObject.Properties['AllowedNamespaceRoots']
    if ($null -ne $rootsProperty) {
        $script:AllowedNamespaceRoots = [System.Collections.Generic.HashSet[string]]::new(
            [string[]]@($rootsProperty.Value), [System.StringComparer]::Ordinal)
    }

    $localProperty = $configuration.PSObject.Properties['LocalNamespacePrefixes']
    if ($null -ne $localProperty) {
        $script:LocalNamespacePrefixes = @($localProperty.Value)
    }

    $ignoredProperty = $configuration.PSObject.Properties['IgnoredCrefs']
    if ($null -ne $ignoredProperty) {
        foreach ($cref in @($ignoredProperty.Value)) {
            if ([string]::IsNullOrWhiteSpace($cref)) {
                throw "XML documentation allowlist file '$AllowlistPath' contains an empty ignored cref."
            }
            if (-not $ignoredCrefs.Add($cref)) {
                throw "XML documentation allowlist file '$AllowlistPath' contains duplicate ignored cref '$cref'."
            }
        }
    }
}

# Gather inputs ------------------------------------------------------------------------------

$snippetFiles = @(Resolve-InputPaths -Paths $SnippetsDirectory -Description 'Snippets')

# The project file is the declaration for what documentation should exist, so nothing needs
# restating by the caller.
$documentationExpected = $false
$projectDescription = ''
$referencedSnippets = $null
$documentedAssemblies = $null

if (-not [string]::IsNullOrWhiteSpace($ProjectSearchRoot)) {
    if (-not (Test-Path -LiteralPath $ProjectSearchRoot)) {
        throw "Project search root '$ProjectSearchRoot' was not found."
    }

    $documentedAssemblies = Get-DocumentedAssemblyName -SearchRoot $ProjectSearchRoot
    Write-Host ("Assemblies whose projects generate XML documentation: " +
        "$(($documentedAssemblies | Sort-Object) -join ', ').")
}

if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
    if (-not (Test-Path -LiteralPath $ProjectPath -PathType Leaf)) {
        throw "Project file '$ProjectPath' was not found."
    }

    $projectFile = (Resolve-Path -LiteralPath $ProjectPath).Path
    $projectDescription = [System.IO.Path]::GetFileName($projectFile)
    $documentationExpected = Test-ProjectGeneratesDocumentation -Path $projectFile
    $referencedSnippets = Get-ReferencedSnippetFile -ProjectDirectory ([System.IO.Path]::GetDirectoryName($projectFile))

    Write-Host ("Project '$projectDescription': GenerateDocumentationFile=$documentationExpected; " +
        "documentation snippets referenced: $($referencedSnippets.Count).")

    # Validate only the snippets this project pulls in, rather than every snippet in the tree, so a
    # finding is attributed to a build that actually consumes it.
    if ($snippetFiles.Count -gt 0) {
        $snippetFiles = @($snippetFiles | Where-Object { $referencedSnippets.Contains($_) })
    }
}
$documentationRoots = [System.Collections.Generic.List[string]]::new()

# Expanded package directory -> package file name, used by the lib/ref layout checks below.
$script:ExpandedPackageRoots = [ordered]@{}
foreach ($path in @($DocumentationPath)) {
    if (-not [string]::IsNullOrWhiteSpace($path)) {
        $documentationRoots.Add($path)
    }
}

# Expand any packages so the XML a consumer actually receives is validated, not only the build
# output it was assembled from.
if (-not [string]::IsNullOrWhiteSpace($PackagesPath)) {
    if ([string]::IsNullOrWhiteSpace($ExtractPath)) {
        throw '-PackagesPath requires -ExtractPath.'
    }
    if (-not (Test-Path -LiteralPath $PackagesPath)) {
        throw "Packages path '$PackagesPath' was not found."
    }

    $packages = @(Get-ChildItem -Path $PackagesPath -Recurse -File -Filter *.nupkg -ErrorAction SilentlyContinue)
    if ($packages.Count -eq 0) {
        throw "No .nupkg files were found under '$PackagesPath'."
    }

    # Cleared wholesale rather than per package. Removing only the destinations for the current
    # packages would leave expansions from an earlier invocation in place, and the whole extraction
    # root is scanned below, so that stale XML would be validated and its members added to the
    # local UID index.
    if (Test-Path -LiteralPath $ExtractPath) {
        Remove-Item -LiteralPath $ExtractPath -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $ExtractPath | Out-Null

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($package in $packages) {
        $destination = Join-Path $ExtractPath $package.BaseName

        Write-Host "Expanding $($package.Name)"
        [System.IO.Compression.ZipFile]::ExtractToDirectory($package.FullName, $destination)
        $script:ExpandedPackageRoots[$destination] = $package.Name
    }

    $documentationRoots.Add((Resolve-Path -LiteralPath $ExtractPath).Path)
}

$documentationCandidates = @(Resolve-InputPaths -Paths $documentationRoots -Description 'Documentation')

# Documentation supplied purely to resolve references against, never itself validated. A package
# carries only its own documentation, so a cref from one package into a sibling package resolves
# against nothing when that sibling was not built this run. Build output does not have the problem,
# because a dependency's documentation file is copied next to the assembly that consumed it.
#
# Resolution only, because these members belong to an already-published package: a defect found in
# them could not be fixed by the run that reports it, and the crefs they contain point back into
# the version of this repository they shipped against rather than the one being built.
$dependencyCandidates = @(Resolve-InputPaths -Paths $DependencyDocumentationPath -Description 'Dependency documentation')

# Distinguish "nothing was asked for", which is a caller error, from "what was asked for held no
# documentation", which is reported below as a finding.
$snippetsRequested = @(@($SnippetsDirectory) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -gt 0
$documentationRequested = $documentationRoots.Count -gt 0

if (-not $snippetsRequested -and -not $documentationRequested) {
    throw 'No input was supplied. Supply -SnippetsDirectory, -DocumentationPath, -PackagesPath, or a combination.'
}

# Parse every file up front so that a malformed file is reported as a finding rather than aborting
# the run, and so the local UID index is complete before any cref is resolved.
$documents = [System.Collections.Generic.List[object]]::new()
$dependencyDocuments = [System.Collections.Generic.List[object]]::new()
$malformedByKind = @{ snippet = 0; documentation = 0; dependency = 0 }
foreach ($entry in @(
        @{ Files = $snippetFiles; Kind = 'snippet' },
        @{ Files = $documentationCandidates; Kind = 'documentation' },
        @{ Files = $dependencyCandidates; Kind = 'dependency' })) {

    foreach ($file in $entry.Files) {
        try {
            $document = [System.Xml.Linq.XDocument]::Load($file, [System.Xml.Linq.LoadOptions]::SetLineInfo)
        }
        catch {
            Add-Finding -Category 'malformed-xml' -Path $file -Message (
                "File is not well-formed XML: $($_.Exception.Message)")
            $malformedByKind[$entry.Kind]++
            continue
        }

        # A scanned directory may hold far more .xml files than documentation. Recognize
        # documentation by its <doc><members> shape and silently skip everything else, so a whole
        # output directory can be supplied without pre-filtering it.
        if ($entry.Kind -ne 'snippet') {
            if ($null -eq $document.Root -or
                $document.Root.Name.LocalName -ne 'doc' -or
                $null -eq $document.Root.Element('members')) {
                continue
            }
        }

        $record = [pscustomobject]@{
            Path         = $file
            Kind         = $entry.Kind
            Document     = $document
            # Documentation shipped in a package's ref/ folder describes the reference
            # assembly, whose members are exactly the public API surface.
            #
            # Never set for a dependency: the public API surface being judged is this build's, and
            # admitting another package's would let a member count as public here on the strength
            # of where it sits in a package this run did not produce.
            IsReferenceDocumentation = ($entry.Kind -eq 'documentation') -and
                (Test-IsReferenceDocumentationPath -Path $file)
            RemarksCount = @($document.Descendants('remarks')).Count
            ExampleCount = @($document.Descendants('example')).Count
        }

        if ($entry.Kind -eq 'dependency') {
            $dependencyDocuments.Add($record)
        }
        else {
            $documents.Add($record)
        }
    }
}

$documentationDocuments = @($documents | Where-Object { $_.Kind -eq 'documentation' })

if ($dependencyDocuments.Count -gt 0) {
    Write-Host ("Resolving against $($dependencyDocuments.Count) dependency documentation file(s), " +
        'which are not themselves validated.')
}

# Whether documentation must exist is the caller's declaration, not an inference. Asserting it both
# ways is what keeps the declaration honest: documentation vanishing from a package that should
# have it is an error, and documentation appearing in one that should not have it means the
# declaration is stale. Without that, a package emitting nothing and a package that silently
# stopped emitting look identical.
#
# Files that were found but failed to parse are excluded: malformed-xml already names the problem,
# and adding a "nothing was found" finding on top of it would point at the wrong cause.
$snippetDocumentCount = @($documents | Where-Object { $_.Kind -eq 'snippet' }).Count

if ($snippetsRequested -and $snippetDocumentCount -eq 0 -and $malformedByKind['snippet'] -eq 0) {
    if ($null -ne $referencedSnippets) {
        Add-Finding -Category 'documentation-not-expected' -Message (
            "$projectDescription references no documentation snippets, so none were validated.")
    }
    elseif ($documentationExpected) {
        Add-Finding -Category 'missing-documentation' -Message (
            'No documentation snippet files were found under the supplied -SnippetsDirectory.')
    }
}

if ($documentationRequested -and $malformedByKind['documentation'] -eq 0) {
    $hasProject = -not [string]::IsNullOrEmpty($projectDescription)

    if ($documentationExpected -and $documentationDocuments.Count -eq 0) {
        $reason = if ($hasProject) {
            "$projectDescription sets GenerateDocumentationFile"
        }
        else {
            'documentation was required'
        }
        Add-Finding -Category 'missing-documentation' -Message (
            "No XML documentation was found under the supplied -DocumentationPath or " +
            "-PackagesPath, but $reason. Documentation that should exist is missing.")
    }
    # The remaining cases contradict a project's declaration, so they are only meaningful when a
    # project was supplied. Without one there is nothing for the result to disagree with.
    elseif ($hasProject -and -not $documentationExpected -and $documentationDocuments.Count -eq 0) {
        # Stated rather than passed over in silence, so a run shows why nothing was checked.
        Add-Finding -Category 'documentation-not-expected' -Message (
            "No XML documentation was found, and none is expected: $projectDescription does not " +
            'set GenerateDocumentationFile.')
    }
    elseif ($hasProject -and -not $documentationExpected -and $documentationDocuments.Count -gt 0) {
        # The project and the build disagree; the documentation is still validated.
        Add-Finding -Category 'unexpected-documentation' -Message (
            "$($documentationDocuments.Count) XML documentation file(s) were found although " +
            "$projectDescription does not set GenerateDocumentationFile.")
    }
}

# Build the local UID index from the documented members the build emitted. Source mode alone leaves
# this null, which disables local resolution rather than reporting every cref as unresolvable.
$script:LocalUids = $null
$script:LocalUidsByBody = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]::new(
    [System.StringComparer]::Ordinal)

# Members that form the published API surface. A reference assembly contains the public API and
# nothing else, so its documentation is exactly that set. Left null when no reference documentation
# was supplied, which disables the public/internal distinction rather than guessing at it.
$script:PublicUids = $null

# Namespaces that the documented members occupy. The compiler emits no <member> entry for a
# namespace, so an N: cref has nothing to resolve against unless the set is derived from the
# members that were emitted. Left null alongside LocalUids when no documentation was supplied.
$script:LocalNamespaces = $null

if ($documentationDocuments.Count -gt 0) {
    $script:LocalUids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $script:LocalNamespaces = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    # Dependency documentation is indexed alongside, so a cref into a package this run did not
    # build resolves. Gated on documentation being present rather than on the dependencies: with
    # nothing under validation there is no cref to resolve, and an index built from dependencies
    # alone would describe members this build never emitted.
    foreach ($entry in @($documentationDocuments) + @($dependencyDocuments)) {
        foreach ($member in $entry.Document.Root.Element('members').Elements('member')) {
            $name = $member.Attribute('name')
            if ($null -eq $name -or [string]::IsNullOrWhiteSpace($name.Value)) {
                continue
            }

            $uid = $name.Value.Trim()
            [void]$script:LocalUids.Add($uid)

            if ($entry.IsReferenceDocumentation) {
                if ($null -eq $script:PublicUids) {
                    $script:PublicUids = [System.Collections.Generic.HashSet[string]]::new(
                        [System.StringComparer]::Ordinal)
                }
                [void]$script:PublicUids.Add($uid)
            }

            # Index the identifier without its prefix so a cref carrying the wrong prefix can be
            # told apart from one naming a member that was never emitted at all.
            if ($uid.Length -gt 2 -and $uid[1] -eq ':') {
                $body = $uid.Substring(2)
                if (-not $script:LocalUidsByBody.ContainsKey($body)) {
                    $script:LocalUidsByBody[$body] = [System.Collections.Generic.HashSet[string]]::new(
                        [System.StringComparer]::Ordinal)
                }
                [void]$script:LocalUidsByBody[$body].Add("$($uid[0]):")

                # Every leading portion of an identifier names somewhere this repository really
                # has: a namespace, or a type containing a nested one. Both are recorded, because
                # a cref naming a type is recognized by the index above before the namespace set
                # is consulted, so admitting a type name here cannot hide a wrong prefix.
                $identifier = $body
                $parenthesis = $identifier.IndexOf('(')
                if ($parenthesis -ge 0) {
                    $identifier = $identifier.Substring(0, $parenthesis)
                }

                $segments = $identifier.Split('.')
                for ($index = 1; $index -lt $segments.Length; $index++) {
                    [void]$script:LocalNamespaces.Add(($segments[0..($index - 1)] -join '.'))
                }
            }
        }
    }
}

# Load the external Learn xref map when one was supplied. It is large, so only the UID column is
# retained.
$script:ExternalUids = $null
if (-not [string]::IsNullOrWhiteSpace($ExternalXrefMapPath)) {
    if (-not (Test-Path -LiteralPath $ExternalXrefMapPath -PathType Leaf)) {
        throw "External xref map '$ExternalXrefMapPath' was not found."
    }

    Write-Host "Loading external xref map '$ExternalXrefMapPath'. This may take several minutes."
    $map = Get-Content -LiteralPath $ExternalXrefMapPath -Raw | ConvertFrom-Json
    $script:ExternalUids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($reference in @($map.references)) {
        if ($null -ne $reference.uid) {
            [void]$script:ExternalUids.Add([string]$reference.uid)
        }
    }
    Write-Host "Loaded $($script:ExternalUids.Count) external UIDs."
}

# Validate -----------------------------------------------------------------------------------

$crefCount = 0
foreach ($entry in $documents) {
    foreach ($element in $entry.Document.Descendants()) {
        $crefAttribute = $element.Attribute('cref')
        if ($null -eq $crefAttribute) {
            continue
        }

        $crefCount++

        if ($ignoredCrefs.Contains($crefAttribute.Value)) {
            [void]$observedIgnoredCrefs.Add($crefAttribute.Value)
            continue
        }

        # Name the nearest enclosing documented member so a finding in a large generated file can
        # be traced back to the API it documents.
        $member = ''
        $ancestor = $element
        while ($null -ne $ancestor) {
            if ($ancestor.Name.LocalName -eq 'member') {
                $nameAttribute = $ancestor.Attribute('name')
                if ($null -ne $nameAttribute) {
                    $member = $nameAttribute.Value
                }
                break
            }
            if ($ancestor.Name.LocalName -eq 'members') {
                $nameAttribute = $ancestor.Attribute('name')
                if ($null -ne $nameAttribute) {
                    $member = $nameAttribute.Value
                }
                break
            }
            $ancestor = $ancestor.Parent
        }

        $lineInfo = [System.Xml.IXmlLineInfo]$crefAttribute
        $context = @{
            Path       = $entry.Path
            LineNumber = if ($lineInfo.HasLineInfo()) { $lineInfo.LineNumber } else { 0 }
            Member     = $member
        }

        Test-Cref -Cref $crefAttribute.Value -Context $context
    }
}

# Package layout ------------------------------------------------------------------------------

# The driver ships a full XML documentation file under lib/ and a trimmed one under ref/. Compare
# the two per target framework rather than testing either against an absolute expectation, so the
# rule stays correct for packages that have no ref/ folder and needs no list of which packages do.
if ($script:ExpandedPackageRoots.Count -gt 0) {
    $documentationByPath = @{}
    foreach ($entry in $documentationDocuments) {
        $documentationByPath[$entry.Path] = $entry
    }

    # An assembly whose project generates documentation must carry it into the package. Checked
    # here rather than from the build output because only the package shows what a consumer
    # receives, and a package can drop a file the build produced.
    if ($null -ne $documentedAssemblies) {
        foreach ($packageRoot in $script:ExpandedPackageRoots.Keys) {
            $packageName = $script:ExpandedPackageRoots[$packageRoot]

            foreach ($folder in @('lib', 'ref')) {
                $folderPath = Join-Path $packageRoot $folder
                if (-not (Test-Path -LiteralPath $folderPath -PathType Container)) {
                    continue
                }

                foreach ($frameworkDirectory in Get-ChildItem -LiteralPath $folderPath -Directory) {
                    # Not recursive: satellite resource assemblies sit in culture subdirectories
                    # and carry no documentation of their own.
                    foreach ($assembly in Get-ChildItem -LiteralPath $frameworkDirectory.FullName -Filter '*.dll' -File) {
                        $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($assembly.Name)
                        if (-not $documentedAssemblies.Contains($assemblyName)) {
                            continue
                        }

                        $expectedXml = [System.IO.Path]::ChangeExtension($assembly.FullName, '.xml')
                        if (-not (Test-Path -LiteralPath $expectedXml -PathType Leaf)) {
                            Add-Finding -Category 'missing-documentation' -Path $assembly.FullName -Message (
                                "$packageName ships $folder/$($frameworkDirectory.Name)/$($assembly.Name) " +
                                "without $assemblyName.xml, although its project generates XML " +
                                'documentation. Consumers of this package get no documentation ' +
                                'text for it.')
                        }
                    }
                }
            }
        }
    }

    foreach ($packageRoot in $script:ExpandedPackageRoots.Keys) {
        $packageName = $script:ExpandedPackageRoots[$packageRoot]
        $rootFull = (Resolve-Path -LiteralPath $packageRoot).Path

        # Index the documentation this package contains by folder kind, target framework and file
        # name, so lib/net8.0/X.xml can be matched with ref/net8.0/X.xml.
        $byKind = @{ 'lib' = @{}; 'ref' = @{} }
        foreach ($path in $documentationByPath.Keys) {
            if (-not $path.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $relative = $path.Substring($rootFull.Length).TrimStart([char]'/', [char]'\')
            $segments = $relative -split '[/\\]'
            if ($segments.Count -lt 3) {
                continue
            }

            $kind = $segments[0].ToLowerInvariant()
            if (-not $byKind.ContainsKey($kind)) {
                continue
            }

            $byKind[$kind]["$($segments[1])/$($segments[-1])"] = $path
        }

        foreach ($key in ($byKind['lib'].Keys | Sort-Object)) {
            if (-not $byKind['ref'].ContainsKey($key)) {
                continue
            }

            $libPath = $byKind['lib'][$key]
            $refPath = $byKind['ref'][$key]
            $lib = $documentationByPath[$libPath]
            $ref = $documentationByPath[$refPath]

            $libHasNarrative = ($lib.RemarksCount + $lib.ExampleCount) -gt 0
            $refHasNarrative = ($ref.RemarksCount + $ref.ExampleCount) -gt 0

            if (-not $libHasNarrative) {
                Add-Finding -Category 'lib-documentation-trimmed' -Path $libPath -Message (
                    "$packageName lib/$key contains no <remarks> or <example> elements. The lib/ " +
                    'documentation is the source the API docs pipeline consumes, so it must be the ' +
                    'full implementation XML; only ref/ is trimmed.')
            }

            if ($refHasNarrative) {
                Add-Finding -Category 'ref-documentation-untrimmed' -Path $refPath -Message (
                    "$packageName ref/$key still contains $($ref.RemarksCount) <remarks> and " +
                    "$($ref.ExampleCount) <example> elements. It must be trimmed by " +
                    'tools/intellisense/TrimDocs.ps1.')
            }

            # Identical content means a single artifact was used for both targets, which defeats the
            # purpose of shipping two files. Reported separately so the finding names the cause
            # rather than only its symptom.
            $libHash = (Get-FileHash -LiteralPath $libPath -Algorithm SHA256).Hash
            $refHash = (Get-FileHash -LiteralPath $refPath -Algorithm SHA256).Hash
            if ($libHash -eq $refHash) {
                Add-Finding -Category 'lib-ref-documentation-identical' -Path $libPath -Message (
                    "$packageName lib/$key and ref/$key are byte-identical. The nuspec must map " +
                    'the implementation XML to lib/ and the trimmed reference XML to ref/.')
            }
        }
    }
}

# An exception that no longer matches anything must be removed, otherwise a reintroduced defect
# would be silently suppressed by an obsolete entry.
foreach ($cref in ($ignoredCrefs | Sort-Object)) {
    if (-not $observedIgnoredCrefs.Contains($cref)) {
        Add-Finding -Category 'stale-allowlist-entry' -Path $AllowlistPath -Cref $cref -Message (
            "Allowlisted cref '$cref' no longer appears in any validated file. Remove the stale " +
            'entry from the allowlist.')
    }
}

# Report -------------------------------------------------------------------------------------

$findings = @($script:Findings | Sort-Object Path, LineNumber, Category, Cref)

$countsByCategory = [ordered]@{}
foreach ($category in $script:CategorySeverities.Keys) {
    $countsByCategory[$category] = @($findings | Where-Object { $_.Category -eq $category }).Count
}

# The report is written before gating so it survives a failing run.
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $reportDirectory = Split-Path -Parent $ReportPath
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory) -and -not (Test-Path -LiteralPath $reportDirectory)) {
        New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    }

    [pscustomobject]@{
        FilesValidated   = $documents.Count
        SnippetFiles     = @($documents | Where-Object { $_.Kind -eq 'snippet' }).Count
        DocumentationFiles = $documentationDocuments.Count
        DependencyDocumentationFiles = $dependencyDocuments.Count
        CrefsValidated   = $crefCount
        LocalUids        = if ($null -ne $script:LocalUids) { $script:LocalUids.Count } else { 0 }
        CountsByCategory = $countsByCategory
        Findings         = $findings
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ReportPath -Encoding utf8

    Write-Host "XML documentation validation report written to '$ReportPath'."
}

# Gate ---------------------------------------------------------------------------------------

$gateTokens = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($token in @($FailOn)) {
    foreach ($part in ([string]$token).Split(',')) {
        $trimmed = $part.Trim()
        if (-not [string]::IsNullOrEmpty($trimmed)) {
            [void]$gateTokens.Add($trimmed)
        }
    }
}

foreach ($token in $gateTokens) {
    if (-not $script:CategorySeverities.Contains($token) -and
        $token -notin @('error', 'warning', 'info')) {
        $categories = ($script:CategorySeverities.Keys | Sort-Object) -join ', '
        throw "Unknown -FailOn token '$token'. Expected a severity (error, warning, info) or a category ($categories)."
    }
}

$gatingFindings = @($findings | Where-Object {
        $gateTokens.Contains($_.Category) -or $gateTokens.Contains($_.Severity)
    })

# Findings that warrant drawing attention to the step. Informational findings are reported in the
# log and the JSON report but do not mark the step, because they describe things that are correct
# as they stand and would otherwise leave every run permanently marked.
$notableFindings = @($findings | Where-Object { $_.Severity -ne 'info' })

foreach ($finding in $findings) {
    $isGating = (-not $ReportOnly) -and
        ($gateTokens.Contains($finding.Category) -or $gateTokens.Contains($finding.Severity))

    $location = if ([string]::IsNullOrWhiteSpace($finding.Path)) {
        ''
    }
    else {
        ";sourcepath=$($finding.Path);linenumber=$($finding.LineNumber);columnnumber=1"
    }

    $detail = if ([string]::IsNullOrWhiteSpace($finding.Member)) {
        $finding.Message
    }
    else {
        "$($finding.Message) (in $($finding.Member))"
    }

    # task.logissue has no informational level, so an info finding is written as plain output
    # rather than being promoted to a warning it does not deserve.
    if ($finding.Severity -eq 'info' -and -not $isGating) {
        Write-Host "$($finding.Category): $detail"
        continue
    }

    $issueType = if ($isGating) { 'error' } else { 'warning' }
    Write-Host "##vso[task.logissue type=$issueType$location]$($finding.Category): $detail"
}

$summary = "XML documentation validation examined $crefCount cref(s) across $($documents.Count) file(s)."
foreach ($category in $countsByCategory.Keys) {
    if ($countsByCategory[$category] -gt 0) {
        $summary += " $category=$($countsByCategory[$category]);"
    }
}
Write-Host $summary

# task.logissue attaches an issue to the timeline record but leaves the task result untouched, so
# a step reporting only warnings would still render as a clean success. Setting the result marks it
# as succeeded-with-issues, which is what makes the warnings visible without failing the build.
function Set-SucceededWithIssues {
    Write-Host '##vso[task.complete result=SucceededWithIssues;]'
}

# A file that could not be parsed was never examined, so none of its cross-references were checked.
# Reporting that as a suppressible finding would let a report-only run give an all-clear for
# content nobody read, so it fails regardless of mode. This mirrors a missing input path, and the
# sibling validation scripts, which also fail outright rather than reporting.
$unreadableFindings = @($findings | Where-Object { $_.Category -eq 'malformed-xml' })
if ($unreadableFindings.Count -gt 0) {
    $noun = if ($unreadableFindings.Count -eq 1) { 'file' } else { 'files' }
    throw "XML documentation validation could not read $($unreadableFindings.Count) $noun. Review the preceding errors."
}

if ($ReportOnly) {
    if ($notableFindings.Count -gt 0) {
        Write-Host "##vso[task.logissue type=warning]XML documentation validation found $($notableFindings.Count) issue(s) but is running in report-only mode, so the build is not failed. $($gatingFindings.Count) of them would fail a gating run."
        Set-SucceededWithIssues
    }
    return
}

if ($gatingFindings.Count -gt 0) {
    $noun = if ($gatingFindings.Count -eq 1) { 'issue' } else { 'issues' }
    throw "XML documentation validation failed with $($gatingFindings.Count) $noun. Review the preceding errors."
}

if ($notableFindings.Count -gt 0) {
    # Nothing here fails the build, but findings above informational level were reported.
    Set-SucceededWithIssues
}

Write-Host 'XML documentation validation passed.'
