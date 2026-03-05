---
applyTo: "**"
---
# External Resources Reference

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

> This table describes runtime/test compatibility. The main driver project currently targets `net462`, `net8.0`, and `net9.0`; newer runtimes may be used for testing even if no TFM is shipped for them.

| .NET Version | Status | Notes |
|--------------|--------|-------|
| .NET Framework 4.6.2 | Supported | Minimum for netfx |
| .NET Framework 4.8.1 | Supported | Latest netfx |
| .NET 8.0 | Supported | LTS; shipped TFM |
| .NET 9.0 | Supported | STS; shipped TFM |
| .NET 10.0 | In testing | Runtime/test-only; package does not currently ship a `net10.0` TFM |

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

## Workflow: Research Before Implementation

When implementing a feature or fixing a bug:

1. **Discover existing code** — Search the codebase for relevant classes/methods and patterns
2. **Search documentation** — Verify API behavior using official Microsoft Learn documentation
3. **Check for related issues** — Search `dotnet/SqlClient` issues and PRs on GitHub
4. **Find code samples** — Look for official code examples in Microsoft Learn
5. **Check pipeline status** — Review CI/CD build status if relevant

This research workflow ensures implementations align with official documentation and existing patterns.
