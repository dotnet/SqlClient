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
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
