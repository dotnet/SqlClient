using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlDiagnosticListener : IObservable<KeyValuePair<string, object>>
    {
        public SqlDiagnosticListener(string name)
        {
        }

        public IDisposable Subscribe(IObserver<KeyValuePair<string, object>> observer)
        {
            throw new NotSupportedException();
        }

        public bool IsEnabled()
        {
            return false;
        }

        public bool IsEnabled(string name)
        {
            return false;
        }

        internal void Write(string sqlBeforeExecuteCommand, object p)
        {
            throw new NotImplementedException();
        }
    }
}
