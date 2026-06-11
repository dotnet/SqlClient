# Azure SQL Connector (WinForms)

A small **.NET Framework 4.8.1** Windows Forms test application that lets a user fill in Azure SQL
Database connection parameters in a UI, builds the corresponding ADO.NET connection string via
`SqlConnectionStringBuilder`, and tests connectivity using
[`Microsoft.Data.SqlClient`](https://www.nuget.org/packages/Microsoft.Data.SqlClient).

It is intended as a quick, repeatable scratch tool for manually validating connection-string
combinations (server / database / authentication mode / encryption / etc.) against an Azure SQL DB
or SQL Server instance.

## Form Inputs

| Field                      | Maps to connection string keyword               |
| -------------------------- | ----------------------------------------------- |
| Server name                | `Data Source`                                   |
| Database name              | `Initial Catalog` *(only added when non-empty)* |
| Authentication             | `Authentication` *(SqlAuthenticationMethod)*    |
| User ID                    | `User ID`                                       |
| Password                   | `Password`                                      |
| Encrypt                    | `Encrypt` *(Mandatory / Optional / Strict)*     |
| Trust server certificate   | `TrustServerCertificate`                        |
| Connect timeout (s)        | `Connect Timeout`                               |

The **Authentication** dropdown is populated from every member of
`Microsoft.Data.SqlClient.SqlAuthenticationMethod`. The User ID and Password fields are enabled /
disabled automatically based on the selected method:

- **SqlPassword** / **ActiveDirectoryPassword** — both User ID and Password are required.
- **ActiveDirectoryServicePrincipal** — User ID = App (Client) ID, Password = client secret.
- **ActiveDirectoryManagedIdentity / MSI / Default / Interactive / DeviceCodeFlow / WorkloadIdentity**
  — User ID is optional (e.g. user-assigned MI client id), Password is disabled.
- **ActiveDirectoryIntegrated** — credentials come from the OS, both fields disabled.

## Buttons

| Button                  | Action                                                                 |
| ----------------------- | ---------------------------------------------------------------------- |
| Build Connection String | Builds the connection string from the form values and displays it.    |
| Test Connection         | Builds the connection string and calls `SqlConnection.Open()`.        |
| Copy to Clipboard       | Copies the currently-built connection string to the clipboard.        |
| Clear All               | Resets every input field to its default state.                         |
| Who Am I?               | Connects and runs an identity query (`SUSER_SNAME()`, `ORIGINAL_LOGIN()`, `USER_NAME()`, `DB_NAME()`, `@@SPID`, etc.) and prints the results. |

The result pane shows the built connection string with the password masked, the test connection
outcome (including SQL error number when applicable), and the server version on success.

## Prerequisites

- .NET Framework **4.8.1** Developer Pack (Visual Studio 2026 Enterprise installs this by default).
- Network connectivity to your Azure SQL Database (server firewall must allow your client IP).
- For Entra ID authentication modes, valid credentials available through Azure CLI / environment
  variables / managed identity, depending on the chosen method.

## Build & Run

From the project folder:

```pwsh
dotnet build .\AzureSqlConnector.csproj
dotnet run --project .\AzureSqlConnector.csproj
```

Or load `src\Microsoft.Data.SqlClient.slnx` in Visual Studio, set **AzureSqlConnector** as the
startup project, and press **F5**.

## Example

1. **Server name:** `myserver.database.windows.net`
2. **Database name:** `MyDb`
3. **Authentication:** `SqlPassword`
4. **User ID:** `sqladmin`
5. **Password:** *your password*
6. **Encrypt:** `Mandatory`
7. **Trust server certificate:** unchecked
8. Click **Test Connection** — the result pane should display
   `Connected successfully! Server version: 12.00.xxxx`.

## Entra ID (Azure AD) Authentication Notes

For any `ActiveDirectory*` authentication method (especially **ActiveDirectoryInteractive**)
the app does two things at startup that are required for the interactive browser sign-in window
to appear:

1. References [`Microsoft.Data.SqlClient.Extensions.Azure`](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure/)
   which contains the `ActiveDirectoryAuthenticationProvider`. Starting with
   `Microsoft.Data.SqlClient` 7.0, the AD providers were moved out of the core driver into this
   extension package, so without it SqlClient throws:
   `Cannot find an authentication provider for 'ActiveDirectoryInteractive'`.

2. In the `MainForm` constructor it calls `provider.SetIWin32WindowFunc(() => this)` and
   registers the provider for every `SqlAuthenticationMethod.ActiveDirectory*` value. This
   gives MSAL.NET the parent `IWin32Window` it needs to display its embedded sign-in browser
   on top of the form.

The **Test Connection** button intentionally calls `SqlConnection.OpenAsync()` on the **UI
thread** (no `Task.Run` wrapper) so the Windows Forms message pump stays alive on the calling
thread while MSAL is waiting for the user to sign in — without this the popup can fail to render
or remain unresponsive.

## Notes

- Like the sibling [`AzureAuthentication`](../AzureAuthentication/README.md) sample, this project
  opts out of inherited `Directory.Build.props` / `Directory.Packages.props` and uses its own
  `NuGet.config` pointing at the governed SqlClient ADO feed (plus a local `packages/` folder for
  developer overrides).
- This is a sample / diagnostic tool, **not** a product. It does not persist credentials.
- From the repo root: `dotnet run --project .\doc\apps\AzureSqlConnector\AzureSqlConnector.csproj`
