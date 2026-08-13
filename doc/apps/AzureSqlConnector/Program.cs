using System;
using System.Windows.Forms;

namespace Microsoft.Data.SqlClient.Samples.AzureSqlConnector
{
    /// <summary>
    /// Application entry point for the Azure SQL Connector WinForms test app.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application. Shows a small chooser dialog at startup so
        /// the user can pick between the UI-thread <see cref="MainForm"/> and the worker-thread
        /// <see cref="MainFormWorker"/> variant of the connector.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ConnectionMode mode;
            using (ModeSelectorForm selector = new ModeSelectorForm())
            {
                if (selector.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                mode = selector.SelectedMode;
            }

            Form main = mode == ConnectionMode.WorkerThreadOpen
                ? (Form)new MainFormWorker()
                : new MainForm();

            Application.Run(main);
        }
    }
}
