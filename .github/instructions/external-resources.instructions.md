---
applyTo: "**"
---
# External Resources and MCP Tools Reference

## Microsoft Learn Documentation

When working with Microsoft.Data.SqlClient, reference official documentation for accurate API behavior.

### Key Documentation URLs

| Topic | URL |
|-------|-----|
| **Overview** | https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace |
| **Getting Started** | https://learn.microsoft.com/sql/connect/ado-net/get-started-sqlclient-driver |
| **Connection Strings** | https://learn.microsoft.com/sql/connect/ado-net/connection-string-syntax |
| **Connection Pooling** | https://learn.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling |
| **Data Types** | https://learn.microsoft.com/sql/connect/ado-net/sql/sql-server-data-types |
| **Always Encrypted** | https://learn.microsoft.com/sql/relational-databases/security/encryption/always-encrypted-database-engine |
| **Azure AD Auth** | https://learn.microsoft.com/sql/connect/ado-net/sql/azure-active-directory-authentication |

### MS-TDS Protocol

| Topic | URL |
|-------|-----|
| **MS-TDS Specification** | https://learn.microsoft.com/openspecs/windows_protocols/ms-tds |
| **Protocol Overview** | https://learn.microsoft.com/openspecs/windows_protocols/ms-tds/b46a581a-39de-4745-b076-ec4dbb7d13ec |
| **Token Types** | https://learn.microsoft.com/openspecs/windows_protocols/ms-tds/5e02042c-a741-4b5a-b91d-af5e236c5252 |

## MCP Tool Usage

MCP servers for this repository are configured in [.vscode/mcp.json](../../.vscode/mcp.json).

### Microsoft Docs MCP (VS Code extension)

Provided by the **GitHub Copilot for Azure** extension — no mcp.json entry needed.

```
# Search documentation
microsoft_docs_search
  query: "Microsoft.Data.SqlClient connection pooling"

# Fetch specific page
microsoft_docs_fetch
  url: "https://learn.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling"

# Search code samples
microsoft_code_sample_search
  query: "SqlConnection async"
  language: "csharp"
```

**When to use:**
- Verifying API behavior specifications
- Finding official code samples
- Researching SQL Server feature compatibility
- Looking up connection string keywords

### Azure DevOps MCP (`ado`)

Configured in `.vscode/mcp.json` for the `sqlclientdrivers` organization.

**When to use:**
- Checking CI/CD pipeline status
- Finding related work items for issues
- Understanding pipeline configuration
- Reviewing test results

### GitHub MCP (`github`)

Configured in `.vscode/mcp.json` for GitHub Copilot-authenticated access.

**When to use:**
- Searching for related issues
- Finding existing PRs for features
- Checking code history
- Cross-referencing with upstream

### Engineering Copilot (`bluebird-sqlclient`, `bluebird-sni`)

Configured in `.vscode/mcp.json` for code discovery across ADO repositories.

- **bluebird-sqlclient**: Searches the `dotnet-sqlclient` internal repo (sqlclientdrivers/ADO.NET)
- **bluebird-sni**: Searches the `Microsoft.Data.SqlClient.SNI` repo (sqlclientdrivers/ADO.NET)

**When to use:**
- Discovering classes, methods, and architecture patterns
- Finding implementations of interfaces
- Searching for code across the internal ADO repo
- Cross-referencing SNI native layer code

## NuGet Package Information

| Package | NuGet URL |
|---------|-----------|
| Microsoft.Data.SqlClient | https://www.nuget.org/packages/Microsoft.Data.SqlClient |
| Microsoft.Data.SqlClient.SNI | https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI |
| Microsoft.Data.SqlClient.SNI.runtime | https://www.nuget.org/packages/Microsoft.Data.SqlClient.SNI.runtime |
| Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider | https://www.nuget.org/packages/Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider |

## SQL Server Version Feature Matrix

| Feature | SQL Server Version |
|---------|-------------------|
| TDS 7.4 | 2012+ |
| TDS 8.0 (Strict Encryption) | 2022+ |
| Always Encrypted | 2016+ |
| Secure Enclaves | 2019+ |
| JSON Data Type | 2025+ |
| Vector Data Type | 2025+ |
| UTF-8 Collations | 2019+ |

## .NET Version Compatibility

| .NET Version | Status | Notes |
|--------------|--------|-------|
| .NET Framework 4.6.2 | Supported | Minimum for netfx |
| .NET Framework 4.8.1 | Supported | Latest netfx |
| .NET 8.0 | Supported | LTS |
| .NET 9.0 | Supported | STS |
| .NET 10.0 | Supported | Preview/LTS |

## Related Projects

| Project | Repository | Purpose |
|---------|------------|---------|
| Entity Framework Core | https://github.com/dotnet/efcore | ORM using SqlClient |
| Dapper | https://github.com/DapperLib/Dapper | Micro-ORM |
| SqlServer Provider for EF Core | https://github.com/dotnet/efcore | EF Core SQL Server provider |

## Community Resources

- **GitHub Issues**: https://github.com/dotnet/SqlClient/issues
- **GitHub Discussions**: https://github.com/dotnet/SqlClient/discussions
- **Stack Overflow**: Tag `microsoft.data.sqlclient`
- **NuGet Issues**: Report via GitHub

## Workflow: Using MCP for Research

When implementing a feature or fixing a bug:

1. **Discover existing code** — Use Engineering Copilot (`bluebird-sqlclient`) to find relevant classes/methods in the internal repo
2. **Search documentation** — Use Microsoft Learn MCP to verify API behavior and find official guidance
3. **Check for related issues** — Use GitHub MCP to search `dotnet/SqlClient` issues and PRs
4. **Find code samples** — Use Microsoft Learn code sample search for official examples
5. **Check pipeline status** — Use ADO MCP to check build status if relevant

This research workflow ensures implementations align with official documentation and existing patterns.
