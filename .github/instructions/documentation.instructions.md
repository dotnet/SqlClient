---
applyTo: "doc/**,**/samples/**"
---
# Documentation and Samples Guide

## Documentation Structure

```
doc/
├── Directory.Packages.props    # Package versions for doc projects
├── samples/                    # Code samples for documentation
│   ├── AADAuthenticationCustomDeviceFlowCallback.cs
│   ├── AzureKeyVaultProviderExample.cs
│   ├── ConnectionStrings_Encrypt.cs
│   └── ...
└── snippets/                   # Documentation snippets
```

## Writing Samples

### Sample File Naming
Use descriptive names following the pattern:
```
{ClassName}_{MethodOrFeature}.cs
{FeatureName}_Example.cs
{ClassName}_{Scenario}.cs
```

Examples:
- `SqlConnection_Open.cs`
- `SqlBulkCopy_ColumnMapping.cs`
- `AlwaysEncrypted_AzureKeyVault.cs`

### Sample Structure
```csharp
// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        // Sample code here
    }
}
// </Snippet1>
```

### Snippet Tags
Use XML comment tags to define reusable snippets:
```csharp
// <Snippet_OpenConnection>
using var connection = new SqlConnection(connectionString);
connection.Open();
Console.WriteLine($"Connected to: {connection.Database}");
// </Snippet_OpenConnection>
```

## Sample Categories

### Connection Samples
Demonstrate connection scenarios:
- Basic connection
- Connection string building
- Authentication methods
- Connection pooling

### Command Samples
Show command execution:
- ExecuteReader
- ExecuteNonQuery
- ExecuteScalar
- Async execution
- Parameterized queries

### Transaction Samples
Transaction management:
- Local transactions
- Distributed transactions (MSDTC)
- SavePoints

### Data Type Samples
Working with SQL data types:
- DateTime handling
- Binary data
- XML data
- JSON (SQL Server 2025+)
- Vector (SQL Server 2025+)

### Security Samples
Authentication and encryption:
- Entra ID authentication
- Always Encrypted
- Azure Key Vault integration
- SSL/TLS configuration

### Performance Samples
Optimization techniques:
- Bulk copy operations
- Async patterns
- Connection pooling
- Batch operations

## Documentation Standards

### XML Documentation
All public APIs must have XML documentation:

```csharp
/// <summary>
/// Opens a database connection with the settings specified by the
/// <see cref="ConnectionString"/> property.
/// </summary>
/// <exception cref="InvalidOperationException">
/// A connection was already open.
/// </exception>
/// <exception cref="SqlException">
/// A connection-level error occurred while opening the connection.
/// </exception>
/// <example>
/// <code>
/// using var connection = new SqlConnection(connectionString);
/// connection.Open();
/// </code>
/// </example>
public override void Open()
```

### Required XML Elements

| Element | Usage |
|---------|-------|
| `<summary>` | Brief description (required) |
| `<param>` | Parameter description |
| `<returns>` | Return value description |
| `<exception>` | Exceptions that may be thrown |
| `<example>` | Usage example |
| `<remarks>` | Additional details |
| `<seealso>` | Related members |

### Cross-References (`cref`)

Our XML documentation is ingested into [dotnet/sqlclient-api-docs](https://github.com/dotnet/sqlclient-api-docs), where Open Publishing resolves every `cref` against the Learn xref map. A malformed documentation ID cannot resolve and produces an `xref-not-found` warning on the API Docs pull request, long after the change left this repository.

A `cref` may be written unqualified (`<see cref="SqlConnection.Open"/>`), in which case the compiler binds it from the surrounding source. Once you write an explicit `T:`/`M:`/`P:`/`F:`/`E:`/`N:` prefix, the compiler passes the value through verbatim and no longer checks it, so the rules below are yours to get right.

| Rule | Wrong | Right |
|------|-------|-------|
| Use CLR type names, not C# aliases | `M:...GetSchema(string)` | `M:...GetSchema(System.String)` |
| Omit parentheses on a parameterless member | `M:...GetSchema()` | `M:...GetSchema` |
| Never include whitespace | `M:...Add(System.String, System.String)` | `M:...Add(System.String,System.String)` |
| `T:` names a type, never an array | `T:System.Byte[]` | `T:System.Byte` array |
| Match the prefix to the member kind | `M:...SqlCommand.CommandTimeout` | `P:...SqlCommand.CommandTimeout` |
| Generic arguments use braces | `T:...List<System.String>` | `T:...List{System.String}` |

Array, pointer and by-reference markers are legal *inside* a member signature (`M:...Decrypt(System.Byte[])`); they are only invalid as the whole target of a `T:` reference.

### Validating Cross-References Locally

`eng/pipelines/onebranch/scripts/validate-xml-docs.ps1` enforces the rules above. It runs in the OneBranch build jobs against snippet sources, generated documentation, and the assembled packages, so run it before pushing documentation changes:

```powershell
./eng/pipelines/onebranch/scripts/validate-xml-docs.ps1 -SnippetsDirectory ./doc/snippets
```

To also resolve references against the members the build actually emitted, which additionally catches wrong-kind prefixes and cross-references the compiler failed to bind:

```powershell
dotnet build ./src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.csproj -c Release
./eng/pipelines/onebranch/scripts/validate-xml-docs.ps1 -DocumentationPath ./artifacts/Microsoft.Data.SqlClient.ref
```

Validation is offline by design; it needs no network access and no xref map download.

### Validating Learn Preview Output

Local validation proves that XML is well formed and that references resolve, but it does not prove that Open Publishing rendered every documentation element. After an API docs ingestion PR is available in [dotnet/sqlclient-api-docs](https://github.com/dotnet/sqlclient-api-docs), compare the Learn previews with the source XML before approving the update.

1. Find the latest **Learn Build status** comment and record the commit it validated. Do not use preview links from an older comment.
2. Enumerate every changed API XML file in the PR. The bot comment lists only the first 25 files, including framework indexes and package metadata, so its table is not the complete API-page list:

   ```bash
   gh api repos/dotnet/sqlclient-api-docs/pulls/<pr>/files --paginate \
     --jq '.[] | select(.filename | test("/xml/.+/.+\\.xml$")) | .filename'
   ```

3. Open every API type preview. Use the `FullName` from the file's `<Type>` element as the lowercase API slug, preserve the `branch=pr-en-us-<pr>` query, and select the matching view:
   - Main provider: `sqlclient-dotnet-core-<version>`
   - Azure Key Vault provider: `akvprovider-dotnet-core-<version>`
4. Follow the preview's own links to every changed member or overload. Do not derive explicit-interface or operator URLs by string replacement; Learn uses special slugs for some members. Enum fields intentionally have no standalone pages and must be checked in the type's fields table.
5. Compare the rendered content through the entire publication path:
   - Local snippet or source XML
   - The `<include>` path on the public declaration
   - Generated or packaged XML documentation
   - The API docs PR XML
   - The rendered type and member previews
6. Check summaries, remarks, examples, parameters, returns or values, exceptions, overload descriptions, code samples, and xrefs. A healthy type landing page is not proof that each overload page is complete.
7. Classify expected renderer transformations before reporting a discrepancy:
   - Learn adds display signatures to xrefs, such as `GetSchema()`.
   - Markdown tables become separate cells and included snippets become rendered code.
   - `To be added.` placeholders are suppressed.
   - `<remarks>` on enum fields are discarded; move required text into `<summary>`.
8. Treat content present in a snippet but absent from generated XML as a source or build-wiring problem. Common causes include an `<include>` XPath that matches nothing or too much, documentation attached only to a ref declaration, and `lib/` packaging from trimmed `ref/` XML.

The review site requires Microsoft authentication. On a corp-joined Windows device running WSL, the Linux browser may not have the required session. Launch a separate Windows Edge profile with Chrome DevTools Protocol enabled so Edge can use seamless Entra SSO:

```bash
EDGE="/mnt/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"
"$EDGE" --remote-debugging-port=9222 --remote-allow-origins=* \
  --user-data-dir=C:\\Temp\\edge-cdp-profile \
  --no-first-run --no-default-browser-check about:blank
```

Drive the browser from the Windows side because the Windows firewall can block WSL-to-Windows access to the debugging port:

```bash
/mnt/c/Windows/System32/curl.exe -s http://localhost:9222/json/version
/mnt/c/Windows/System32/curl.exe -s http://localhost:9222/json
```

Never automate credentials or copy authentication tokens. Navigate the authenticated browser through CDP and capture the rendered article text or DOM for comparison. Keep crawl output outside the repository.

### Trimmed vs. Full Documentation

The driver package ships **two** XML documentation files per target framework, and they are deliberately different:

| Package folder | Content | Consumer |
|----------------|---------|----------|
| `lib/<tfm>/` | Full documentation, including `<remarks>` and `<example>` | The .NET API docs pipeline, which builds the Learn pages |
| `ref/<tfm>/` | Trimmed by `tools/intellisense/TrimDocs.ps1`, which strips `<remarks>` and `<example>` | Visual Studio IntelliSense |

Remarks and examples render poorly in Visual Studio tooltips, which is why the `ref/` copy is trimmed. The two files must never be the same: if `lib/` is sourced from the trimmed artifact, the published Learn pages silently lose every remark and example. That regression shipped in 7.1.0, so the packaged-documentation gate now fails the build when a package's `lib/` XML is trimmed, its `ref/` XML is not, or the two are byte-identical.

When changing the `<file>` mappings in `Microsoft.Data.SqlClient.nuspec`, keep `lib/` pointed at the implementation artifact and `ref/` at the reference artifact.

### Writing Style
- Use third person ("Opens a connection" not "Open a connection")
- Be concise but complete
- Include common use cases
- Note any platform-specific behavior

## Sample Best Practices

### DO
```csharp
// Use meaningful variable names
using var connection = new SqlConnection(connectionString);

// Include error handling in samples
try
{
    connection.Open();
}
catch (SqlException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// Show proper resource cleanup
using var reader = command.ExecuteReader();

// Use async when demonstrating async features
await connection.OpenAsync();
```

### DON'T
```csharp
// Don't use hardcoded credentials
var conn = "Server=x;User=sa;Password=secret";  // BAD!

// Don't leave resources unmanaged
var reader = cmd.ExecuteReader();  // Missing using/Dispose

// Don't suppress exceptions silently
try { ... } catch { }  // BAD!
```

## Adding New Samples

1. **Create sample file** in `doc/samples/`
2. **Follow naming convention** for discoverability
3. **Use snippet tags** for documentation inclusion
4. **Test the sample** to ensure it compiles and runs
5. **Link from documentation** where relevant

## Sample Project Testing

Samples should be testable:
```csharp
// Sample helper for testing
public static class SampleRunner
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("SQLCLIENT_TEST_CONNSTR")
        ?? "Server=localhost;Database=master;Trusted_Connection=True;";
}
```

## Microsoft Learn Integration

Samples may be referenced from Microsoft Learn documentation:
- https://learn.microsoft.com/sql/connect/ado-net/

When creating samples for external documentation:
1. Verify snippet tags are correctly formatted
2. Ensure sample compiles standalone
3. Include all necessary using statements
4. Document any prerequisites

## Changelog Documentation

Do not edit `CHANGELOG.md` directly. The changelog is updated as part of the release workflow based on the contents of `release-notes/` and the `release-notes` prompt.

When adding features, fixes, or breaking changes, create or update the appropriate entry under `release-notes/` instead. For example:
```markdown
## [Unreleased]

### Added
- New `SqlCommand.ExecuteJsonAsync()` method for JSON result sets
- Support for SQL Server 2025 JSON data type

### Changed
- Connection encryption now defaults to Mandatory

### Fixed
- Issue with connection pool exhaustion under high load (#1234)

### Deprecated
- `SqlConnection.GetSchema(string)` overload
```

## Release Notes

Release notes in `release-notes/` follow version structure:
```
release-notes/
├── README.md           # Index of all versions
├── 5.0/                # Major version folder
│   └── 5.0.md         # Version release notes
├── 5.1/
│   └── 5.1.md
└── template/           # Release notes template
```

## External Documentation Resources

- [Microsoft Learn - ADO.NET](https://learn.microsoft.com/dotnet/framework/data/adonet/)
- [SQL Server Documentation](https://learn.microsoft.com/sql/sql-server/)
- [Microsoft.Data.SqlClient NuGet](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
