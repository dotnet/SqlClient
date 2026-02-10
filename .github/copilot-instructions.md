# GitHub Copilot Instructions for dotnet/SqlClient

## 🧑‍💻 Roles and Responsibilities
- **Triage**: Review new issues, categorize them, and assign them to appropriate team members.
- **Fixing Issues**: Address bugs, implement feature requests, and complete tasks as assigned.
- **Writing Tests**: Ensure all changes are covered by unit tests and integration tests.
- **Documentation**: Update documentation for public APIs, features, and usage examples.
- **Pull Requests**: Create and submit PRs for review, ensuring they follow project conventions and include necessary tests and documentation.
- **Code Reviews**: Review PRs from other contributors, providing feedback and suggestions for improvements.
- **Continuous Improvement**: Identify areas for improvement in the code base, documentation, and processes, and implement changes to enhance the project.

## 📚 Project Overview
This project is a .NET data provider for SQL Server, enabling .NET applications to interact with SQL Server databases. It supports various features like connection pooling, transaction management, and asynchronous operations.
The project builds from a **single unified project** at `src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj` that multi-targets `net462`, `net8.0`, and `net9.0`. The legacy `netfx/` and `netcore/` directories are being phased out — only their `ref/` folders (which define the public API surface) remain active.
The project includes:
- **Public APIs**: Defined in `netcore/ref/` and `netfx/ref/` directories.
- **Implementations**: All source code in `src/Microsoft.Data.SqlClient/src/`.
- **Tests**: Located in the `tests/` directory, covering unit and integration tests.
  - **Unit Tests**: Located in `tests/UnitTests/` directory, which includes tests for individual components and methods.
  - **Functional Tests**: Located in `tests/FunctionalTests/` directory, which includes tests for various features and functionalities that can be run without a SQL Server instance.
  - **Manual Tests**: Located in `tests/ManualTests/` directory, which includes tests that require a SQL Server instance to run.
- **Documentation**: Found in the `doc/` directory, including API documentation, usage examples.
- **Policies**: Contribution guidelines, coding standards, and review policies in the `policy/` directory.
- **Building**: The project uses MSBuild for building and testing, with configurations and targets defined in the `build.proj` file, whereas instructions are provided in the `BUILDGUIDE.md` file.
- **CI/CD**: ADO Pipelines for CI/CD and Pull request validation are defined in the `eng/` directory, ensuring code quality and automated testing.

## 📦 Products
This project includes several key products and libraries that facilitate SQL Server connectivity and data access:
- **Microsoft.Data.SqlClient**: The primary library for SQL Server data access, providing a rich set of APIs for connecting to SQL Server databases, executing commands, and retrieving data.
- **Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider**: Enables Always Encrypted with Azure Key Vault integration, allowing SQL Server column encryption and decryption using keys managed in Azure Key Vault.
- **Microsoft.SqlServer.Server**: Supplies APIs for SQL Server-specific features, including user-defined types (UDTs) and SQL Server-specific attributes.

## 🛠️ Key Features
- **Connectivity to SQL Server**: Provides robust and secure connections to SQL Server databases, using various authentication methods, such as Windows Authentication, SQL Server Authentication, and Azure Active Directory authentication, e.g. `ActiveDirectoryIntegrated`, `ActiveDirectoryPassword`, `ActiveDirectoryServicePrincipal`,`ActiveDirectoryInteractive`, `ActiveDirectoryDefault`, and `ActiveDirectoryManagedIdentity`.
- **Connection Resiliency**: Implements connection resiliency features to handle transient faults and network issues, ensuring reliable database connectivity.
- **TLS Encryption**: Supports secure connections using TLS protocols to encrypt data in transit. Supports TLS 1.2 and higher, ensuring secure communication with SQL Server. Supported encryption modes are: 
  - **Optional**: Encryption is used if available, but not required.
  - **Mandatory**: Encryption is mandatory for the connection.
  - **Strict**: Enforces strict TLS requirements, ensuring only secure connections are established.
- **TLS 1.3 Support**: Supports TLS 1.3 for enhanced security and performance in data transmission when connecting with 'Strict' encryption mode.
- **TDS Protocol**: Implements the Tabular Data Stream (TDS) protocol for communication with SQL Server, supported protocol versions include TDS 7.4 and 8.0.
- **Data Access**: Supports executing SQL commands, retrieving data using `SqlDataReader`, and managing data with `SqlCommand` and `SqlConnection` objects.
- **MultipleActiveResultSets (MARS)**: Supports Multiple Active Result Sets, allowing multiple active commands to be executed on a single connection.
- **Asynchronous Operations**: Supports async/await patterns for non-blocking database operations.
- **Connection Pooling**: Efficiently manages database connections in pools to improve performance.
- **Transaction Management**: Supports transaction management using local transaction management and Global transactions using MSDTC for data integrity and consistency.
- **Parameterization**: Supports prevention of SQL injection attacks by using parameterized queries.
- **Data Types**: Supports a wide range of SQL Server data types, including Json, Vector, custom types and UDTs.
- **Data Encryption**: Supports data encryption for secure data transmission.
- **Logging and Diagnostics**: Provides event source tracing diagnostic capabilities for troubleshooting.
- **Failover Support**: Handles automatic failover scenarios for high availability.
- **Cross-Platform Support**: Compatible with both .NET Framework and .NET Core, allowing applications to run on Windows, Linux, and macOS.
- **Column Encryption AKV Provider**: Supports Azure Key Vault (AKV) provider for acquiring keys from Azure Key Vault to be used for encryption and decryption.

## 🧩 SNI Implementations
There are two implementations of the SQL Server Network Interface (SNI) layer used in this project:
- **Managed SNI**: A managed implementation of SNI that is used in .NET Core and .NET 5+ environments. It provides cross-platform support for SQL Server connectivity.
- **Native SNI**: A native implementation of SNI that is used in .NET Framework and .NET Core environments on Windows. It's shipped as part of the `Microsoft.Data.SqlClient.SNI` and `Microsoft.Data.SqlClient.SNI.Runtime` packages.
  - **Microsoft.Data.SqlClient.SNI**: This package provides the native SNI layer for .NET Framework applications.
  - **Microsoft.Data.SqlClient.SNI.Runtime**: This package provides the native SNI layer for .NET Core applications on Windows.

## 📌 Instructions for Copilot

### 🐛 Triage Issues
When a new issue is created, follow these steps:
1. **Acknowledge Receipt**: Respond within 48 hours to confirm the issue is being triaged.
2. **Set Issue Type**:
   - Update issue type as `Bug`, `Feature`, `Task`, or `Epic` if not done already, based on:
     - Use `Bug` for issues opened following `ISSUE_TEMPLATE\bug-report.md` template, and is complaining about an unexpected behavior.
     - Use `Feature` for issues opened following `ISSUE_TEMPLATE\feature_request.md` template, containing proposals to incorporate.
     - Use `Task` for issues opened as sub-issues.
     - Use `Epic` for issues acting as a high-level work linked to multiple sub-issues.
3. **Request Missing Information**: If any required details are missing based on the issue template, ask the reporter to provide them.
4. **Labeling**: Apply appropriate labels like `Area\Engineering`, `Area\Json`, `Area\AKV Provider`, `Area\Connection Pooling`, etc. based on the issue content.
    - Use `Triage Needed :new:` for new issues that need initial review.
    - Use `Performance :chart_with_upwards_trend:` for issues that are targeted to performance improvements/concerns.
    - Use `Needs more info :information_source:` for issues that require additional information.
5. **Link Related Issues**: If the issue is related to other issues, link them for better context.
6. **Add Comments**: As you triage issues, comment on the issue to:
    - Confirm receipt and triage status.
    - Ask for any missing information.
    - Provide initial thoughts or questions to clarify the issue.
    - Include links to relevant documentation or code examples.
    - Use the `@` mention feature to notify relevant team members or stakeholders.

### 🛠️ Fixing Issues
- Focus on issues assigned to you.
- For issues labeled `Needs more info :information_source:`, ask for clarifications or additional details.
- For issues labeled `Performance :chart_with_upwards_trend:`, prioritize performance-related improvements.
- Use the issue description to understand the problem and identify the root cause.
    - If the issue is a bug, ensure you can reproduce it with the provided code sample.
    - If the issue is a feature request, review the proposal and assess its feasibility.
    - If the issue is a task, follow the instructions provided in the issue description.
    - If the issue is an epic, break it down into smaller tasks or bugs and create sub-issues as needed.
- Cross-reference issue descriptions with code in `src/Microsoft.Data.SqlClient/src/`. Do NOT add code to the legacy `netfx/src/` or `netcore/src/` directories.
- If public APIs are changed, update corresponding `ref/` projects.
- Add or update tests in `tests/` to validate the fix.

### 🧪 Writing Tests
- For every bug fix, ensure there are unit tests and manual (integration) tests that cover the scenario.
- For new features, write tests that validate the functionality.
- Follow a test-driven approach: write failing tests before implementing fixes.
- **Cover both sync and async code paths** where the API exposes both variants (e.g., `Open`/`OpenAsync`, `ExecuteReader`/`ExecuteReaderAsync`). Sync and async implementations often differ internally.
- Use the existing test framework in the `tests/` directory.
- Follow the naming conventions and structure of existing tests.
- Ensure tests are comprehensive and cover edge cases.
- If the issue involves changes to public APIs, update the corresponding `ref/` projects to reflect those changes.
- Add sample code snippets in the 'doc/samples/' directory to demonstrate the new or fixed functionality and link them in the documentation in `doc/` folder.

### 📝 Documentation
- All public documentation for APIs should be updated in the `doc/` directories.
- When adding or changing XML docs, ensure they are clear and follow the existing style.

### 🔁 Creating Pull Requests
- Use the `Fixes #issue_number` syntax in the PR description to automatically close the issue when the PR is merged.
- Include a summary of the fix and link to the related issue
- Add `[x]` checkboxes for:
  - [ ] Tests added or updated
  - [ ] Public API changes documented
  - [ ] Verified against customer repro (if applicable)
  - [ ] Ensure no breaking changes introduced
- Ensure the PR passes all CI checks before merging.

### ✅ Closing Issues
- Add a comment summarizing the fix and referencing the PR 

### ⚙️ Automating Workflows
- Auto-label PRs based on folder paths (e.g., changes in `src/Microsoft.Data.SqlClient/src/` → `Area\SqlClient`, changes in `tests/` → `Area\Testing`) and whether they add new public APIs or introduce a breaking change.
- Suggest changelog entries for fixes in `CHANGELOG.md`
- Tag reviewers based on `CODEOWNERS` file

## 🧠 Contextual Awareness
- All source code is in `src/Microsoft.Data.SqlClient/src/`. Do NOT add code to legacy `netfx/src/` or `netcore/src/` directories.
- Only `ref/` folders in `netcore/ref/` and `netfx/ref/` remain active for defining the public API surface.
- Check for platform-specific differences using file suffixes (`.netfx.cs`, `.netcore.cs`, `.windows.cs`, `.unix.cs`) and conditional compilation (`#if NETFRAMEWORK`, `#if NET`, `#if _WINDOWS`, `#if _UNIX`).
- Respect API compatibility rules across .NET versions
- Do not introduce breaking changes without proper justification and documentation
- Use the `doc/` directory for any new documentation or updates to existing documentation
- Use the `tests/` directory for any new tests or updates to existing tests
- Use the `doc/samples/` directory for any new code samples or updates to existing samples
- Use the `policy/` directory for any new policies or updates to existing policies

## Constraints
- Do not modify the `CODEOWNERS` file directly.
- Do not modify `CHANGELOG.md` unless executing a release workflow (see `release-notes` prompt).
- Do not close issues without a fix or without providing a clear reason.

## 📝 Notes
- Update policies and guidelines in the `policy/` directory as needed based on trending practices and team feedback.
- Regularly review and update the `doc/` directory to ensure it reflects the current state of the project.
