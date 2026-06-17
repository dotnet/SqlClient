using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient.Samples.AzureSqlConnector
{
    /// <summary>
    /// "Worker thread" variant of the connector form. Opens the SQL connection synchronously
    /// inside a <see cref="Task.Run(System.Action)"/> call so the UI thread never blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The form's HWND is captured on the UI thread in the constructor and stashed in
    /// <see cref="_ownerHwnd"/>. Both Entra ID parent-window callbacks return that captured
    /// handle, so they are safe to invoke from the worker thread (touching <c>Form.Handle</c>
    /// from a non-UI thread is illegal).
    /// </para>
    /// <para>
    /// Compare with <see cref="MainForm"/>, which keeps Open on the UI thread and relies on
    /// <see cref="SqlConnection.OpenAsync()"/> for responsiveness.
    /// </para>
    /// </remarks>
    public partial class MainFormWorker : Form
    {
        // ──────────────────────────────────────────────────────────────────
        #region Construction

        public MainFormWorker()
        {
            InitializeComponent();
            PopulateAuthenticationMethods();
            PopulateEncryptOptions();
            UpdateCredentialFieldsAvailability();

            // Force the underlying Win32 window to be created NOW (on the UI thread) so we can
            // safely capture its HWND for MSAL to use later from a worker thread. Touching
            // Form.Handle from a non-UI thread is illegal, so we read it here once and stash it.
            _ownerHwnd = this.Handle;

            RegisterActiveDirectoryProvider();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
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

        /// <summary>
        /// Registers a single <see cref="ActiveDirectoryAuthenticationProvider"/> for every
        /// Entra ID authentication method and gives it the form's captured HWND as the parent
        /// window owner. Both callbacks intentionally use the HWND captured in the constructor
        /// (<see cref="_ownerHwnd"/>) rather than <c>this.Handle</c>; they are invoked by MSAL on
        /// the worker thread that called <see cref="SqlConnection.Open"/>.
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

        #endregion

        // ──────────────────────────────────────────────────────────────────
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

            SetBusy(true, "Testing connection...");
            AppendStatus(string.Empty);
            AppendStatus("Testing connectivity to " + builder.DataSource + " ...");

            try
            {
                // Run Open() on a thread-pool worker so the UI thread never blocks. The await
                // continuation hops back onto the UI thread automatically (the awaiter captures
                // the current SynchronizationContext), so it is safe to touch the form's controls
                // after the await.
                //
                // The Entra ID interactive / WAM flows still find a parent window because we
                // captured the form's HWND on the UI thread in the constructor and the callbacks
                // registered in RegisterActiveDirectoryProvider return that captured handle (no
                // UI-thread-only Form.Handle access from the worker thread).
                string connectionString = builder.ConnectionString;
                string serverVersion = await Task.Run(() =>
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        return connection.ServerVersion;
                    }
                }).ConfigureAwait(true);

                SetStatus("Connected successfully.", isError: false);
                AppendStatus("Connected successfully! Server version: " + serverVersion);
            }
            catch (SqlException ex)
            {
                SetStatus("Connection failed (SqlException).", isError: true);
                AppendStatus("SqlException [" + ex.Number + "]: " + ex.Message);
            }
            catch (Exception ex)
            {
                SetStatus("Connection failed.", isError: true);
                AppendStatus(ex.GetType().Name + ": " + ex.Message);
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

            SetBusy(true, "Querying logged-in identity...");
            AppendStatus(string.Empty);
            AppendStatus("Running identity query against " + builder.DataSource + " ...");

            try
            {
                // Run the whole open + query + read on a worker thread so the UI never blocks.
                // We materialize the single result row into a List<(name, value)> on the worker
                // and then format it on the UI thread once the await returns.
                string connectionString = builder.ConnectionString;
                List<(string Name, object Value)> row = await Task.Run(() =>
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        using (SqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = IdentityQuery.CommandText;

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    return null;
                                }

                                var fields = new List<(string, object)>(reader.FieldCount);
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    object value = reader.IsDBNull(i) ? "(null)" : reader.GetValue(i);
                                    fields.Add((reader.GetName(i), value));
                                }
                                return fields;
                            }
                        }
                    }
                }).ConfigureAwait(true);

                if (row is null)
                {
                    SetStatus("Identity query returned no rows.", isError: true);
                    AppendStatus("(no rows returned)");
                }
                else
                {
                    AppendStatus("Identity:");
                    foreach (var (name, value) in row)
                    {
                        AppendStatus("  " + name.PadRight(16) + ": " + value);
                    }
                    SetStatus("Identity query succeeded.", isError: false);
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
            chkTrustServerCertificate.Checked = false;
            numTimeout.Value = 30;
            SetStatus("Ready", isError: false);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
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

        #endregion

        // ──────────────────────────────────────────────────────────────────
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
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            if (statusText != null)
            {
                SetStatus(statusText, isError: false);
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region Nested Types

        private static class EncryptDisplay
        {
            public const string Mandatory = "Mandatory";
            public const string Optional = "Optional";
            public const string Strict = "Strict";
        }

        /// <summary>
        /// Tiny <see cref="IWin32Window"/> wrapper around a raw HWND captured on the UI thread.
        /// Used so that MSAL.NET's <c>IWin32WindowFunc</c> callback can safely return a window
        /// owner from a worker thread without ever touching <see cref="Control.Handle"/> off-UI.
        /// Only needed on .NET Framework where the legacy <c>SetIWin32WindowFunc</c> API is used.
        /// </summary>
#if NETFRAMEWORK
        private sealed class Win32WindowHandle : IWin32Window
        {
            private readonly IntPtr _hwnd;
            public Win32WindowHandle(IntPtr hwnd) => _hwnd = hwnd;
            public IntPtr Handle => _hwnd;
        }
#endif

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region Private Fields

        /// <summary>
        /// The form's Win32 window handle, captured on the UI thread in the constructor.
        /// Read from worker threads by the Entra ID provider callbacks to parent MSAL's
        /// sign-in / WAM broker UI without illegally touching <see cref="Control.Handle"/>.
        /// </summary>
        private readonly IntPtr _ownerHwnd;

        #endregion
    }
}
