using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient.Samples.AzureSqlConnector
{
    /// <summary>
    /// Main UI form that collects Azure SQL connection parameters from the user, builds a
    /// connection string via <see cref="SqlConnectionStringBuilder"/>, and optionally tests
    /// connectivity using <see cref="SqlConnection"/>.
    /// </summary>
    public partial class MainForm : Form
    {
        // ──────────────────────────────────────────────────────────────────
        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm"/> class.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            PopulateAuthenticationMethods();
            PopulateEncryptOptions();
            UpdateCredentialFieldsAvailability();
            RegisterActiveDirectoryProvider();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region UI Initialization

        /// <summary>
        /// Populates the authentication combo box with every supported
        /// <see cref="SqlAuthenticationMethod"/> value.
        /// </summary>
        private void PopulateAuthenticationMethods()
        {
            foreach (SqlAuthenticationMethod method in Enum.GetValues(typeof(SqlAuthenticationMethod)))
            {
                cmbAuthentication.Items.Add(method);
            }

            cmbAuthentication.SelectedItem = SqlAuthenticationMethod.SqlPassword;
        }

        /// <summary>
        /// Populates the Encrypt combo box with the three supported encryption modes.
        /// </summary>
        private void PopulateEncryptOptions()
        {
            cmbEncrypt.Items.Add(EncryptDisplay.Mandatory);
            cmbEncrypt.Items.Add(EncryptDisplay.Optional);
            cmbEncrypt.Items.Add(EncryptDisplay.Strict);
            cmbEncrypt.SelectedIndex = 0;
        }

        /// <summary>
        /// Registers a single <see cref="ActiveDirectoryAuthenticationProvider"/> instance for
        /// every Entra ID authentication method and gives it this form as the parent window
        /// owner. This is what enables the interactive (browser) sign-in popup to actually appear
        /// on top of the WinForms host on .NET Framework — without parenting MSAL can fail to
        /// display its UI.
        /// </summary>
        private void RegisterActiveDirectoryProvider()
        {
            ActiveDirectoryAuthenticationProvider provider = new ActiveDirectoryAuthenticationProvider();
            provider.SetIWin32WindowFunc(() => this);

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
                // NOTE: We intentionally call OpenAsync on the UI thread (instead of wrapping in
                // Task.Run) so that the SynchronizationContext is preserved. The
                // ActiveDirectoryInteractive flow needs a running UI message pump on the calling
                // thread for MSAL.NET to display its embedded sign-in browser parented to this
                // form (see RegisterActiveDirectoryProvider). OpenAsync still keeps the UI
                // responsive because the I/O wait does not block the message loop.
                string serverVersion;
                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(true);
                    serverVersion = connection.ServerVersion;
                }

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
                builder = BuildConnectionString();
                txtConnectionString.Text = MaskPassword(builder);
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
                // NOTE: Same UI-thread reasoning as btnTest_Click — keep the message pump alive
                // for any ActiveDirectoryInteractive sign-in that may be required.
                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(true);

                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT " +
                            "    SUSER_SNAME()        AS LoggedInUser, " +
                            "    ORIGINAL_LOGIN()     AS OriginalLogin, " +
                            "    USER_NAME()          AS DatabaseUser, " +
                            "    SUSER_ID()           AS LoginSid, " +
                            "    DB_NAME()            AS DatabaseName, " +
                            "    @@SERVERNAME         AS ServerName, " +
                            "    HOST_NAME()          AS ClientHost, " +
                            "    APP_NAME()           AS AppName, " +
                            "    SESSION_USER         AS SessionUser, " +
                            "    CURRENT_USER         AS CurrentUser, " +
                            "    @@SPID               AS SessionId, " +
                            "    @@VERSION            AS ServerVersion;";

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
            chkTrustServerCertificate.Checked = false;
            numTimeout.Value = 30;
            SetStatus("Ready", isError: false);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region Connection String Construction

        /// <summary>
        /// Builds a <see cref="SqlConnectionStringBuilder"/> from the current form values.
        /// </summary>
        /// <returns>The populated builder.</returns>
        /// <exception cref="InvalidOperationException">When required fields are missing.</exception>
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

            // Credentials are only required for password-based authentication methods.
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
                // User ID is optional for these methods (e.g. for ManagedIdentity it can hold the
                // client id of a user-assigned identity; for ServicePrincipal it holds the app id).
                string userId = (txtUserId.Text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(userId))
                {
                    builder.UserID = userId;
                }

                // ServicePrincipal needs the client secret in the Password field.
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

        /// <summary>
        /// Returns true when the supplied authentication method requires both User ID and Password.
        /// </summary>
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

        /// <summary>
        /// Returns a copy of the connection string with the password redacted.
        /// </summary>
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

        /// <summary>
        /// Updates the enabled state of the User ID / Password fields based on the selected
        /// authentication method.
        /// </summary>
        private void UpdateCredentialFieldsAvailability()
        {
            if (cmbAuthentication.SelectedItem == null)
            {
                return;
            }

            SqlAuthenticationMethod method = (SqlAuthenticationMethod)cmbAuthentication.SelectedItem;

            // User ID is meaningful for most methods (some make it optional). Disable for
            // Integrated/Default/ManagedIdentity-style flows where the OS / environment supplies it.
            bool userEnabled =
                method != SqlAuthenticationMethod.ActiveDirectoryIntegrated;

            bool passwordEnabled = RequiresUserAndPassword(method)
                || method == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;

            txtUserId.Enabled = userEnabled;
            txtPassword.Enabled = passwordEnabled;

            if (!passwordEnabled)
            {
                txtPassword.Clear();
            }
        }

        /// <summary>
        /// Updates the bottom status bar label.
        /// </summary>
        private void SetStatus(string text, bool isError)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = isError ? System.Drawing.Color.Firebrick : System.Drawing.Color.Black;
        }

        /// <summary>
        /// Appends a line to the result/status log.
        /// </summary>
        private void AppendStatus(string line)
        {
            if (txtStatus.TextLength > 0)
            {
                txtStatus.AppendText(Environment.NewLine);
            }
            txtStatus.AppendText(line ?? string.Empty);
        }

        /// <summary>
        /// Toggles the form's busy state during the asynchronous test-connection call.
        /// </summary>
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

        /// <summary>
        /// String constants used to populate the Encrypt combo box.
        /// </summary>
        private static class EncryptDisplay
        {
            public const string Mandatory = "Mandatory";
            public const string Optional = "Optional";
            public const string Strict = "Strict";
        }

        #endregion
    }
}
