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
            // virtual because we want a default implementation in the cases
            // where we don't have a connection to process stuff, but we want to
            // provide the connection the ability to fire info messages when it
            // hooks up.
            #if NETFRAMEWORK
            SqlException errors = ProcessMessages(true, ignoreNonFatalMessages);
            #else
            SqlException errors = ProcessMessages(true);
            #endif
            
            if (errors != null)
            {
                throw errors;
            }

        }

        #if NETFRAMEWORK
        protected SqlException ProcessMessages(bool ignoreWarnings, bool ignoreNonFatalMessages)
        #else
        protected SqlException ProcessMessages(bool ignoreWarnings)
        #endif
        {
            SqlException result = null;
            SqlErrorCollection temp = null;  // temp variable to store that which is being thrown - so that local copies can be deleted

            {
                Debug.Assert(_warnings == null || 0 != _warnings.Count, "empty warning collection?");// must be something in the collection

                if (!ignoreWarnings)
                {
                    temp = _warnings;
                }
                _warnings = null;
            }

            if (temp != null)
            {
                result = SqlException.CreateException(temp, ServerVersion);
            }
            return result;
        }

        internal void ProcessMessagesAndThrow()
        {
        }
    }
}
