using System;
using System.Drawing;
using System.Windows.Forms;

namespace Microsoft.Data.SqlClient.Samples.AzureSqlConnector
{
    /// <summary>
    /// Choice exposed by <see cref="ModeSelectorForm"/>.
    /// </summary>
    internal enum ConnectionMode
    {
        /// <summary>
        /// Use <see cref="MainForm"/>, which calls <c>SqlConnection.OpenAsync()</c> on the UI
        /// thread. Relies on the WinForms SynchronizationContext to keep the message pump alive.
        /// </summary>
        UiThreadOpenAsync,

        /// <summary>
        /// Use <see cref="MainFormWorker"/>, which calls <c>SqlConnection.Open()</c> inside
        /// <c>Task.Run</c> on a thread-pool worker. The captured form HWND is passed to MSAL.
        /// </summary>
        WorkerThreadOpen,
    }

    /// <summary>
    /// Tiny modal dialog shown at startup that lets the user pick which connector form
    /// (UI-thread async or worker-thread sync) to launch.
    /// </summary>
    internal sealed class ModeSelectorForm : Form
    {
        private readonly RadioButton _rdoUiThread;
        private readonly RadioButton _rdoWorker;

        internal ConnectionMode SelectedMode =>
            _rdoWorker.Checked ? ConnectionMode.WorkerThreadOpen : ConnectionMode.UiThreadOpenAsync;

        internal ModeSelectorForm()
        {
            Text = "Azure SQL Connector ΓÇö Choose Mode";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 200);

            Label lblHeader = new Label
            {
                AutoSize = false,
                Text = "Select how SqlConnection.Open should be invoked:",
                Location = new Point(16, 14),
                Size = new Size(420, 20),
                Font = new Font(Font, FontStyle.Bold),
            };

            _rdoUiThread = new RadioButton
            {
                Text = "&UI thread",
                Location = new Point(20, 42),
                Size = new Size(420, 20),
                Checked = true,
            };

            Label lblUiHint = new Label
            {
                AutoSize = false,
                Text = "    Async/Sync open on the UI thread; SynchronizationContext keeps the form responsive.",
                Location = new Point(20, 62),
                Size = new Size(420, 18),
                ForeColor = SystemColors.GrayText,
            };

            _rdoWorker = new RadioButton
            {
                Text = "&Worker thread",
                Location = new Point(20, 90),
                Size = new Size(420, 20),
            };

            Label lblWorkerHint = new Label
            {
                AutoSize = false,
                Text = "    Sync open on a thread-pool worker; HWND is captured up-front for MSAL.",
                Location = new Point(20, 110),
                Size = new Size(420, 18),
                ForeColor = SystemColors.GrayText,
            };

            Button btnOk = new Button
            {
                Text = "&Launch",
                DialogResult = DialogResult.OK,
                Location = new Point(268, 152),
                Size = new Size(82, 28),
            };

            Button btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(358, 152),
                Size = new Size(82, 28),
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                lblHeader,
                _rdoUiThread, lblUiHint,
                _rdoWorker,   lblWorkerHint,
                btnOk, btnCancel,
            });
        }
    }
}
