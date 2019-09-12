// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// Sql client logger.
    /// </summary>
    public class SqlClientLogger {
        internal enum LogLevel {
            Info = 0,
            Error,
        }

        /// <summary>
        /// Log info.
        /// </summary>
        public void LogInfo(string type, string method, string message) {
            Bid.Trace($"<sc|{type}|{method}|{LogLevel.Info}>{message}\n");
        }

        /// <summary>
        /// Log error.
        /// </summary>
        public void LogError(string type, string method, string message) {
            Bid.Trace($"<sc|{type}|{method}|{LogLevel.Error}>{message}\n");
        }

        /// <summary>
        /// Log message if value is not true.
        /// </summary>
        public bool LogAssert(bool value, string type, string method, string message) {
            if (!value) LogError(type, method, message);
            return value;
        }

        /// <summary>
        /// Whether bid tracing is enabled.
        /// </summary>
        public bool IsLoggingEnabled => Bid.TraceOn;
    }
}
