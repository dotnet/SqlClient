// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/SqlClientLogger/*'/>
    public class SqlClientLogger
    {
        internal enum LogLevel
        {
            Info = 0,
            Warning,
            Error
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/LogInfo/*'/>
        public void LogInfo(string type, string method, string message)
        {
            SqlClientEventSource.Log.TryTraceEvent("<sc|{0}|{1}|{2}>{3}", type, method, LogLevel.Info, message);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/LogWarning/*'/>
        public void LogWarning(string type, string method, string message)
        {
            Console.Out.WriteLine(message);
            SqlClientEventSource.Log.TryTraceEvent("<sc|{0}|{1}|{2}>{3}", type, method, LogLevel.Warning, message);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/LogError/*'/>
        public void LogError(string type, string method, string message)
        {
            SqlClientEventSource.Log.TryTraceEvent("<sc|{0}|{1}|{2}>{3}", type, method, LogLevel.Error, message);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/LogAssert/*'/>
        public bool LogAssert(bool value, string type, string method, string message)
        {
            if (!value)
                LogError(type, method, message);
            return value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientLogger.xml' path='docs/members[@name="SqlClientLogger"]/IsLoggingEnabled/*'/>	
        public bool IsLoggingEnabled => SqlClientEventSource.Log.IsEnabled();
    }
}
