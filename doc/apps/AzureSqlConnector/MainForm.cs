using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;

namespace Microsoft.Data.SqlClient.Samples.AzureSqlConnector
{
    /// <summary>
    /// "UI-thread" variant of the connector form. Opens the SQL connection via
    /// <see cref="SqlConnection.OpenAsync()"/> on the UI thread; the WinForms
    /// <see cref="System.Threading.SynchronizationContext"/> keeps the message pump alive while
    /// the async I/O completes, so the form remains responsive and MSAL.NET's embedded sign-in
    /// browser (for <c>ActiveDirectoryInteractive</c>) parents itself correctly.
    /// </summary>
    public partial class MainForm : Form
    {
        #region Construction

        public MainForm()
        {
            InitializeComponent();
            this.Text = "Azure SQL Connector — UI thread";
            PopulateAuthenticationMethods();
            PopulateEncryptOptions();
            PopulateOpenModes();
            UpdateCredentialFieldsAvailability();

            // Force the underlying Win32 window to be created NOW (on the UI thread) so we can
            // safely hand its HWND to MSAL later. Even in async mode, MSAL.NET may invoke the
            // parent-window callback from a worker thread (e.g. when the driver blocks on a
            // synchronous Open()), and touching Form.Handle from a non-UI thread throws
            // InvalidOperationException ("Cross-thread operation not valid").
            _ownerHwnd = this.Handle;

            RegisterActiveDirectoryProvider();
        }

        #endregion

        #region UI Initialization

        private void PopulateAuthenticationMethods()
        {
            foreach (SqlAuthenticationMethod method in Enum.GetValues(typeof(SqlAuthenticationMethod)))
            {
                cmbAuthentication.Items.Add(method);
            }

            cmbAuthentication.SelectedItem = SqlAuthenticationMethod.SqlPassword;
        }

        private void PopulateEncryptOptions()
        {
            cmbEncrypt.Items.Add(EncryptDisplay.Mandatory);
            cmbEncrypt.Items.Add(EncryptDisplay.Optional);
            cmbEncrypt.Items.Add(EncryptDisplay.Strict);
            cmbEncrypt.SelectedIndex = 0;
        }

        private void PopulateOpenModes()
        {
            cmbOpenMode.Items.Add(OpenModeDisplay.Async);
            cmbOpenMode.Items.Add(OpenModeDisplay.Sync);
            cmbOpenMode.SelectedIndex = 0;
        }

        /// <summary>
        /// Registers a single <see cref="ActiveDirectoryAuthenticationProvider"/> for every
        /// Entra ID authentication method and gives it the form's captured HWND as the parent
        /// window owner. Both callbacks intentionally use the HWND captured in the constructor
        /// (<see cref="_ownerHwnd"/>) rather than <c>this.Handle</c>, because MSAL.NET can invoke
        /// them from a worker thread (e.g. when the driver blocks on a synchronous <c>Open()</c>
        /// or when its internal continuations resume off-UI).
        /// </summary>
        private void RegisterActiveDirectoryProvider()
        {
            ActiveDirectoryAuthenticationProvider provider = new ActiveDirectoryAuthenticationProvider();
            IntPtr ownerHwnd = _ownerHwnd;

#if NETFRAMEWORK
            // .NET Framework: parent the embedded WebView via the legacy IWin32Window API.
            provider.SetIWin32WindowFunc(() => new Win32WindowHandle(ownerHwnd));
#endif

            // Modern API: works on both .NET Framework and .NET 8+, and is the one MSAL's WAM
            // broker consults on Windows.
            provider.SetParentActivityOrWindowFunc(() => ownerHwnd);

            // Without this, MSAL's default device-code callback writes the prompt to
            // Console.WriteLine, which is invisible in a WinForms host — the connection
            // appears to hang while MSAL polls for a code the user never sees.
            provider.SetDeviceCodeFlowCallback(DeviceCodeFlowCallback);

            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryIntegrated, provider);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, provider);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, provider);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, provider);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, provider);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryMSI, provider);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, provider);
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity, provider);
            #pragma warning disable CS0618 // Type or member is obsolete
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, provider);
            #pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Device Code Flow callback. MSAL invokes this on a worker thread before it begins
        /// polling the token endpoint. We surface the user code three ways so the user always
        /// sees it: (1) appended to the log textbox via BeginInvoke (works whenever the UI
        /// thread is pumping — async <c>OpenAsync</c>), (2) the verification URL launched in
        /// the default browser, and (3) a modal owned by the MSAL worker thread (works even
        /// when the UI thread is blocked by a synchronous <c>Open()</c>). MSAL polling waits
        /// for the returned Task to complete, so dismissing the dialog also resumes polling.
        /// </summary>
        private Task DeviceCodeFlowCallback(DeviceCodeResult result)
        {
            string message = result.Message;
            string url = result.VerificationUrl;
            string code = result.UserCode;

            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        AppendStatus(string.Empty);
                        AppendStatus("=== Device Code Flow ===");
                        AppendStatus(message);
                    }));
                }
                catch (InvalidOperationException)
                {
                    // Form is closing or handle was destroyed; fall through to the modal.
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Best-effort; the modal below still shows the URL and code.
            }

            MessageBox.Show(
                "Sign in to complete Device Code Flow:" + Environment.NewLine + Environment.NewLine +
                "  URL : " + url + Environment.NewLine +
                "  Code: " + code + Environment.NewLine + Environment.NewLine +
                "A browser window has been opened. Enter the code above, complete sign-in," +
                Environment.NewLine + "then click OK to resume the connection.",
                "Device Code Flow",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return Task.CompletedTask;
        }

        #endregion

        #region Event Handlers

        private void cmbAuthentication_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCredentialFieldsAvailability();
        }

        private void btnBuild_Click(object sender, EventArgs e)
        {
            try
            {
                SqlConnectionStringBuilder builder = BuildConnectionString();
                txtConnectionString.Text = MaskPassword(builder);
                SetStatus("Connection string built successfully.", isError: false);
                AppendStatus("Connection string built:\r\n" + MaskPassword(builder));
            }
            catch (Exception ex)
            {
                txtConnectionString.Text = string.Empty;
                SetStatus("Failed to build connection string.", isError: true);
                AppendStatus("ERROR: " + ex.Message);
            }
        }

        private async void btnTest_Click(object sender, EventArgs e)
        {
            SqlConnectionStringBuilder builder;
            try
            {
                builder = BuildConnectionString();
                txtConnectionString.Text = MaskPassword(builder);
            }
            catch (Exception ex)
            {
                SetStatus("Failed to build connection string.", isError: true);
                AppendStatus("ERROR: " + ex.Message);
                return;
            }

            bool useAsync = IsAsyncOpenSelected();
            SetBusy(true, useAsync ? "Testing connection (OpenAsync)..." : "Testing connection (Open)...");
            AppendStatus(string.Empty);
            AppendStatus("Testing connectivity to " + builder.DataSource + " ("
                + (useAsync ? "OpenAsync" : "sync Open") + ") ...");

            try
            {
                string serverVersion;
                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    await OpenConnectionAsync(connection, useAsync).ConfigureAwait(true);
                    serverVersion = connection.ServerVersion;
                }

                SetStatus("Connected successfully.", isError: false);
                AppendStatus("Connected successfully! Server version: " + serverVersion);
            }
            catch (SqlException ex)
            {
                SetStatus("Connection failed (SqlException).", isError: true);
                AppendStatus("SqlException [" + ex.Number + "]: " + ex.Message + "\r\n" + ex.StackTrace);
            }
            catch (Exception ex)
            {
                SetStatus("Connection failed.", isError: true);
                AppendStatus(ex.GetType().Name + ": " + ex.Message + "\r\n" + ex.StackTrace);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async void btnWhoAmI_Click(object sender, EventArgs e)
        {
            SqlConnectionStringBuilder builder;
            try
            {
                builder = BuildConnectionString();
                txtConnectionString.Text = MaskPassword(builder);
            }
            catch (Exception ex)
            {
                SetStatus("Failed to build connection string.", isError: true);
                AppendStatus("ERROR: " + ex.Message);
                return;
            }

            bool useAsync = IsAsyncOpenSelected();
            SetBusy(true, useAsync
                ? "Querying logged-in identity (OpenAsync)..."
                : "Querying logged-in identity (Open)...");
            AppendStatus(string.Empty);
            AppendStatus("Running identity query against " + builder.DataSource + " ("
                + (useAsync ? "OpenAsync" : "sync Open") + ") ...");

            try
            {
                // Same UI-thread reasoning as btnTest_Click — keep the message pump alive for any
                // ActiveDirectoryInteractive sign-in that may be required.
                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    await OpenConnectionAsync(connection, useAsync).ConfigureAwait(true);

                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = IdentityQuery.CommandText;

                        using (SqlDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(true))
                        {
                            if (await reader.ReadAsync().ConfigureAwait(true))
                            {
                                AppendStatus("Identity:");
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string name = reader.GetName(i);
                                    object value = reader.IsDBNull(i) ? "(null)" : reader.GetValue(i);
                                    AppendStatus("  " + name.PadRight(16) + ": " + value);
                                }
                                SetStatus("Identity query succeeded.", isError: false);
                            }
                            else
                            {
                                SetStatus("Identity query returned no rows.", isError: true);
                                AppendStatus("(no rows returned)");
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                SetStatus("Identity query failed (SqlException).", isError: true);
                AppendStatus("SqlException [" + ex.Number + "]: " + ex.Message);
            }
            catch (Exception ex)
            {
                SetStatus("Identity query failed.", isError: true);
                AppendStatus(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtConnectionString.Text))
            {
                SetStatus("Nothing to copy. Build the connection string first.", isError: true);
                return;
            }

            try
            {
                Clipboard.SetText(BuildConnectionString().ConnectionString);
                SetStatus("Connection string copied to clipboard.", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus("Failed to copy to clipboard.", isError: true);
                AppendStatus("ERROR: " + ex.Message);
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtServer.Clear();
            txtDatabase.Clear();
            txtUserId.Clear();
            txtPassword.Clear();
            txtConnectionString.Clear();
            txtStatus.Clear();
            cmbAuthentication.SelectedItem = SqlAuthenticationMethod.SqlPassword;
            cmbEncrypt.SelectedIndex = 0;
            cmbOpenMode.SelectedIndex = 0;
            chkTrustServerCertificate.Checked = false;
            numTimeout.Value = 30;
            SetStatus("Ready", isError: false);
        }

        #endregion

        #region Connection String Construction

        private SqlConnectionStringBuilder BuildConnectionString()
        {
            string server = (txtServer.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(server))
            {
                throw new InvalidOperationException("Server name is required.");
            }

            SqlAuthenticationMethod authMethod = (SqlAuthenticationMethod)cmbAuthentication.SelectedItem;

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                ConnectTimeout = (int)numTimeout.Value,
            };

            string database = (txtDatabase.Text ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(database))
            {
                builder.InitialCatalog = database;
            }

            if (authMethod != SqlAuthenticationMethod.NotSpecified)
            {
                builder.Authentication = authMethod;
            }

            if (RequiresUserAndPassword(authMethod))
            {
                string userId = (txtUserId.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new InvalidOperationException(
                        "User ID is required for " + authMethod + " authentication.");
                }

                builder.UserID = userId;
                builder.Password = txtPassword.Text ?? string.Empty;
            }
            else if (authMethod == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                  || authMethod == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                  || authMethod == SqlAuthenticationMethod.ActiveDirectoryMSI
                  || authMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive
                  || authMethod == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                  || authMethod == SqlAuthenticationMethod.ActiveDirectoryDefault
                  || authMethod == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)
            {
                string userId = (txtUserId.Text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(userId))
                {
                    builder.UserID = userId;
                }

                if (authMethod == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                    && !string.IsNullOrEmpty(txtPassword.Text))
                {
                    builder.Password = txtPassword.Text;
                }
            }

            string encryptValue = cmbEncrypt.SelectedItem as string ?? EncryptDisplay.Mandatory;
            switch (encryptValue)
            {
                case EncryptDisplay.Mandatory:
                    builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
                    break;
                case EncryptDisplay.Optional:
                    builder.Encrypt = SqlConnectionEncryptOption.Optional;
                    break;
                case EncryptDisplay.Strict:
                    builder.Encrypt = SqlConnectionEncryptOption.Strict;
                    break;
            }

            builder.TrustServerCertificate = chkTrustServerCertificate.Checked;

            return builder;
        }

        private static bool RequiresUserAndPassword(SqlAuthenticationMethod method)
        {
            switch (method)
            {
                case SqlAuthenticationMethod.SqlPassword:
#pragma warning disable CS0618 // Type or member is obsolete
                case SqlAuthenticationMethod.ActiveDirectoryPassword:
#pragma warning restore CS0618
                    return true;
                default:
                    return false;
            }
        }

        private static string MaskPassword(SqlConnectionStringBuilder builder)
        {
            if (string.IsNullOrEmpty(builder.Password))
            {
                return builder.ConnectionString;
            }

            SqlConnectionStringBuilder copy = new SqlConnectionStringBuilder(builder.ConnectionString)
            {
                Password = "********",
            };
            return copy.ConnectionString;
        }

        /// <summary>
        /// Returns <see langword="true"/> when the user picked <c>Async (OpenAsync)</c> in the
        /// open-mode selector. Defaults to async if the selector has not been initialized yet.
        /// </summary>
        private bool IsAsyncOpenSelected()
        {
            return cmbOpenMode.SelectedItem as string != OpenModeDisplay.Sync;
        }

        /// <summary>
        /// Opens <paramref name="connection"/> on the calling thread using either
        /// <see cref="SqlConnection.OpenAsync()"/> or the synchronous <see cref="SqlConnection.Open"/>
        /// based on <paramref name="useAsync"/>. The method itself is always async-returning so
        /// callers can <c>await</c> uniformly; for the sync case it runs <c>Open()</c> inline on
        /// the UI thread (which is supported with WAM broker because the broker dialog is hosted
        /// by a separate process and does not need this thread's message pump).
        /// </summary>
        private static Task OpenConnectionAsync(SqlConnection connection, bool useAsync)
        {
            if (useAsync)
            {
                return connection.OpenAsync();
            }

            connection.Open();
            return Task.CompletedTask;
        }

        #endregion

        #region UI Helpers

        private void UpdateCredentialFieldsAvailability()
        {
            if (cmbAuthentication.SelectedItem == null)
            {
                return;
            }

            SqlAuthenticationMethod method = (SqlAuthenticationMethod)cmbAuthentication.SelectedItem;

            bool userEnabled = method != SqlAuthenticationMethod.ActiveDirectoryIntegrated;
            bool passwordEnabled = RequiresUserAndPassword(method)
                || method == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;

            txtUserId.Enabled = userEnabled;
            txtPassword.Enabled = passwordEnabled;

            if (!passwordEnabled)
            {
                txtPassword.Clear();
            }
        }

        private void SetStatus(string text, bool isError)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = isError ? System.Drawing.Color.Firebrick : System.Drawing.Color.Black;
        }

        private void AppendStatus(string line)
        {
            if (txtStatus.TextLength > 0)
            {
                txtStatus.AppendText(Environment.NewLine);
            }
            txtStatus.AppendText(line ?? string.Empty);
        }

        private void SetBusy(bool busy, string statusText)
        {
            btnBuild.Enabled = !busy;
            btnTest.Enabled = !busy;
            btnCopy.Enabled = !busy;
            btnClear.Enabled = !busy;
            btnWhoAmI.Enabled = !busy;
            cmbOpenMode.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            if (statusText != null)
            {
                SetStatus(statusText, isError: false);
            }
        }

        #endregion

        #region Nested Types

        private static class EncryptDisplay
        {
            public const string Mandatory = "Mandatory";
            public const string Optional = "Optional";
            public const string Strict = "Strict";
        }

        private static class OpenModeDisplay
        {
            public const string Async = "Async (OpenAsync)";
            public const string Sync = "Sync (Open)";
        }

#if NETFRAMEWORK
        // Tiny IWin32Window wrapper around a raw HWND captured on the UI thread so MSAL.NET's
        // legacy IWin32WindowFunc callback can safely return a window owner from a worker thread
        // without ever touching Control.Handle off-UI.
        private sealed class Win32WindowHandle : IWin32Window
        {
            private readonly IntPtr _hwnd;
            public Win32WindowHandle(IntPtr hwnd) => _hwnd = hwnd;
            public IntPtr Handle => _hwnd;
        }
#endif

        #endregion

        #region Private Fields

        // The form's Win32 window handle, captured on the UI thread in the constructor.
        // Read from worker threads by the Entra ID provider callbacks to parent MSAL's sign-in
        // / WAM broker UI without illegally touching Control.Handle.
        private readonly IntPtr _ownerHwnd;

        #endregion
    }
}
