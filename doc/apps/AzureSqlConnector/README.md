# Azure SQL Connector (WinForms)

A small Windows Forms test application that lets a user fill in Azure SQL Database connection
parameters in a UI, builds the corresponding ADO.NET connection string via
`SqlConnectionStringBuilder`, and tests connectivity using `Microsoft.Data.SqlClient`.

It is intended as a quick, repeatable scratch tool for manually validating connection-string
combinations (server / database / authentication mode / encryption / etc.) against an Azure SQL DB
or SQL Server instance, **and as a manual repro** for
`ActiveDirectoryAuthenticationProvider`'s WAM-broker behavior.

On Windows, the sample multi-targets:

| TFM              | Purpose                                                                                          |
| ---------------- | ------------------------------------------------------------------------------------------------ |
| `net481`         | Exercises both the legacy `SetIWin32WindowFunc` and modern `SetParentActivityOrWindowFunc` APIs. |
| `net10.0-windows` | Exercises the modern `SetParentActivityOrWindowFunc` API used on .NET 8+.                       |

On Linux/macOS, only `net10.0-windows` is selected. It can be cross-compiled even though the resulting
binary only runs on Windows, so the project no longer needs a separate no-op cross-platform
fallback.

> **Note:** `SetParentActivityOrWindowFunc` is also available on `net481` and is the
> recommended API for new code on any framework. On `net481`, the sample registers both APIs.
> Both callbacks use a window handle captured on the UI thread, rather than reading `Form.Handle`
> when MSAL invokes the callback.

## Mode selector

When the app launches it shows a small `ModeSelectorForm` that picks between two top-level forms:

| Mode                               | Form              | What it exercises                                                                                                       |
| ---------------------------------- | ----------------- | ----------------------------------------------------------------------------------------------------------------------- |
| **UI thread (`OpenAsync`)**        | `MainForm`        | Defaults to `OpenAsync`; its **Open mode** dropdown also allows synchronous `Open` on the UI thread.                    |
| **Worker thread (`Open`, sync)**   | `MainFormWorker`  | Calls `SqlConnection.Open()` on a background worker thread; the parent window handle is captured up-front on the UI thread. |

Both forms capture the parent window handle on the UI thread before registering callbacks.
Use the default async mode or the worker-thread form to keep the UI responsive; synchronous
`Open` in the UI-thread form blocks that thread while the connection opens.

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

- The SDK pinned in the repository's [global.json](../../../global.json); see the
  [build prerequisites](../../../BUILDGUIDE.md#prerequisites).
- Windows to run either target, with .NET Framework **4.8.1** for `net481` or the .NET 10 Windows
  Desktop runtime for `net10.0-windows`.
- Network connectivity to your Azure SQL Database (server firewall must allow your client IP).
- For Entra ID authentication modes, valid credentials available through Azure CLI / environment
  variables / managed identity / the WAM broker, depending on the chosen method.

## Build & run

From the project folder on Windows:

```pwsh
dotnet build .\AzureSqlConnector.csproj
dotnet run --project .\AzureSqlConnector.csproj -f net10.0-windows   # modern WAM API
dotnet run --project .\AzureSqlConnector.csproj -f net481            # legacy IWin32Window API
```

Or load [src/Microsoft.Data.SqlClient.slnx](../../../src/Microsoft.Data.SqlClient.slnx) in Visual Studio, set **AzureSqlConnector** as the
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

## Entra ID parent-window plumbing

For any `ActiveDirectory*` authentication method (especially **ActiveDirectoryInteractive**) the
app installs an `ActiveDirectoryAuthenticationProvider` and tells it which window should host the
sign-in UI:

- On **both targets**, the form registers `SetParentActivityOrWindowFunc` with a callback
  returning the captured window handle. This is the modern API used by the WAM broker.
- On **`net481`**, the form also registers `SetIWin32WindowFunc` with a `Win32WindowHandle`
  wrapper around that same handle for the legacy embedded WebView.

The provider is registered for every `SqlAuthenticationMethod.ActiveDirectory*` value at startup.

### Threading patterns

| Form              | Open mode                | Parent window callback                                                                                                                          |
| ----------------- | ------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `MainForm`        | `OpenAsync` or `Open` on UI thread | Callback returns the captured handle without accessing controls, even if MSAL invokes it on a worker thread.                                  |
| `MainFormWorker`  | `Open` (sync) on worker  | The form captures `this.Handle` into a field on the UI thread before kicking off the worker; the callback closes over that captured value so it never needs to marshal back. |

Do not assume MSAL invokes a callback on the UI thread, even when the connection is opened there.
Accessing a control's handle from a worker thread can fail; the captured-handle pattern avoids this.

## Notes

- This is a sample / diagnostic tool, **not** a product. It does not persist credentials.
- From the repo root on Windows:
  `dotnet run --project .\doc\apps\AzureSqlConnector\AzureSqlConnector.csproj -f net10.0-windows`
