// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Server
{
    internal class SmiEventSink_Default
    {
        private SqlErrorCollection _errors;
        private SqlErrorCollection _warnings;

        internal virtual string ServerVersion => null;

        internal SmiEventSink_Default()
        {
        }

        internal bool HasMessages => false;

        #if NETFRAMEWORK
        protected virtual void DispatchMessages(bool ignoreNonFatalMessages)
        #else
        protected virtual void DispatchMessages()
        #endif
        {
        }

        #if NETFRAMEWORK
        protected SqlException ProcessMessages(bool ignoreWarnings, bool ignoreNonFatalMessages)
        #else
        protected SqlException ProcessMessages(bool ignoreWarnings)
        #endif
        {
            return null;
        }

        internal void ProcessMessagesAndThrow()
        {
        }
    }
}
