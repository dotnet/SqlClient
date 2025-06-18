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

        internal bool HasMessages => _errors is not null || _warnings is not null;

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

            if (_errors != null)
            {
                Debug.Assert(0 != _errors.Count, "empty error collection?"); // must be something in the collection
#if NETFRAMEWORK
                if (ignoreNonFatalMessages)
                {
                    temp = new SqlErrorCollection();
                    foreach (SqlError error in _errors)
                    {
                        if (error.Class >= TdsEnums.FATAL_ERROR_CLASS)
                        {
                            temp.Add(error);
                        }
                    }
                    if (temp.Count <= 0)
                    {
                        temp = null;
                    }
                }
                else
#endif
                {
                    if (_warnings != null)
                    {
                        // When we throw an exception we place all the warnings that
                        // occurred at the end of the collection - after all the errors.
                        // That way the user can see all the errors AND warnings that
                        // occurred for the exception.
                        foreach (SqlError warning in _warnings)
                        {
                            _errors.Add(warning);
                        }
                    }
                    temp = _errors;
                }

                _errors = null;
                _warnings = null;
            }
            else
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
            if (HasMessages)
            {
                #if NETFRAMEWORK
                DispatchMessages(false);
                #else
                DispatchMessages();
                #endif
            }
        }
    }
}
