using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlDiagnosticListener : DiagnosticListener
    {
        public SqlDiagnosticListener(string name) : base(name)
        {
        }
    }
}
