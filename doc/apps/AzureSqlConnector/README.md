# Azure SQL Connector (WinForms)

A small Windows Forms test application that lets a user fill in Azure SQL Database connection
parameters in a UI, builds the corresponding ADO.NET connection string via
`SqlConnectionStringBuilder`, and tests connectivity using `Microsoft.Data.SqlClient`.

It is intended as a quick, repeatable scratch tool for manually validating connection-string
combinations (server / database / authentication mode / encryption / etc.) against an Azure SQL DB
or SQL Server instance, **and as a manual repro** for the WAM-broker behavior added in this
branch's `ActiveDirectoryAuthenticationProvider`.

The sample targets **`net481`** on Windows and **`net9.0-windows`** on non-Windows hosts, exercising the modern
`SetParentActivityOrWindowFunc` API (used on .NET 8+) and the WAM broker integration.

`net9.0-windows` restores and builds cleanly on Linux/macOS hosts (via
`EnableWindowsTargeting`) even though the resulting binary only runs on Windows.

> **Note:** The upstream PR on `main` multi-targets `net481` and `net10.0-windows`.
> On the `release/6.1` branch the .NET Framework and .NET Core implementations live in
> separate csproj files, which makes cross-TFM `ProjectReference` selection unreliable
> during NuGet restore. This sample therefore ships a single modern TFM.

## Mode selector

When the app launches it shows a small `ModeSelectorForm` that picks between two top-level forms:

| Mode                               | Form              | What it exercises                                                                                                       |
| ---------------------------------- | ----------------- | ----------------------------------------------------------------------------------------------------------------------- |
| **UI thread (`OpenAsync`)**        | `MainForm`        | Calls `SqlConnection.OpenAsync()` on the UI thread so the Windows Forms message pump stays alive during MSAL sign-in.   |
| **Worker thread (`Open`, sync)**   | `MainFormWorker`  | Calls `SqlConnection.Open()` on a background worker thread; the parent window handle is captured up-front on the UI thread. |

Both forms demonstrate the supported patterns for parenting the WAM broker (or the legacy
embedded WebView on .NET Framework).

## Form inputs

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
| Test Connection         | Builds the connection string and opens the connection.                |
| Copy to Clipboard       | Copies the currently-built connection string to the clipboard.        |
| Clear All               | Resets every input field to its default state.                         |
| Who Am I?               | Connects and runs an identity query (`SUSER_SNAME()`, `ORIGINAL_LOGIN()`, `USER_NAME()`, `DB_NAME()`, `@@SPID`, etc.) and prints the results. |

The result pane shows the built connection string with the password masked, the test connection
outcome (including SQL error number when applicable), and the server version on success.

## Prerequisites

- .NET 9 SDK (or Visual Studio 2022 17.12+).
- Network connectivity to your Azure SQL Database (server firewall must allow your client IP).
- For Entra ID authentication modes, valid credentials available through Azure CLI / environment
  variables / managed identity / the WAM broker, depending on the chosen method.

## Build & run

From the project folder:

```pwsh
dotnet build .\AzureSqlConnector.csproj
dotnet run --project .\AzureSqlConnector.csproj
```

Or from the repository root:

```pwsh
dotnet build doc\apps\AzureSqlConnector\AzureSqlConnector.csproj
```

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

## Entra ID parent-window plumbing

For any `ActiveDirectory*` authentication method (especially **ActiveDirectoryInteractive**) the
app installs an `ActiveDirectoryAuthenticationProvider` and tells it which window should host the
sign-in UI by calling `provider.SetParentActivityOrWindowFunc(() => this.Handle)`. This is the
modern API that integrates with the WAM broker on Windows.

The provider is registered for every `SqlAuthenticationMethod.ActiveDirectory*` value at startup.

### Threading patterns

| Form              | Open mode                | Parent window callback                                                                                                                          |
| ----------------- | ------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `MainForm`        | `OpenAsync` on UI thread | Callback runs on the UI thread when MSAL invokes it, so `this.Handle` is naturally safe to access.                                              |
| `MainFormWorker`  | `Open` (sync) on worker  | The form captures `this.Handle` into a field on the UI thread before kicking off the worker; the callback closes over that captured value so it never needs to marshal back. |

Without one of these patterns the WAM broker can fail to render or stay unresponsive while it
waits for the user.

## Notes

- This is a sample / diagnostic tool, **not** a product. It does not persist credentials.
- From the repo root: `dotnet run --project doc\apps\AzureSqlConnector\AzureSqlConnector.csproj`
