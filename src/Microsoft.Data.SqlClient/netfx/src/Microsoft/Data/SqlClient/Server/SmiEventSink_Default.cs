// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Server
{
    internal class SmiEventSink_Default : SmiEventSink
    {

        private SmiEventSink _parent;     // next level up, which we'll defer to if we don't need to handle the event.

        private SqlErrorCollection _errors;
        private SqlErrorCollection _warnings;

        private SqlErrorCollection Errors
        {
            get
            {
                if (null == _errors)
                {
                    _errors = new SqlErrorCollection();
                }

                return _errors;
            }
        }

        internal bool HasMessages
        {
            get
            {
                SmiEventSink_Default parent = (SmiEventSink_Default)_parent;
                if (null != parent)
                {
                    return parent.HasMessages;
                }
                else
                {
                    bool result = (null != _errors || null != _warnings);
                    return result;
                }
            }
        }

        virtual internal string ServerVersion
        {
            get
            {
                return null;
            }
        }

        internal SmiEventSink Parent
        {
            get
            {
                return _parent;
            }
            set
            {
                _parent = value;
            }
        }

        private SqlErrorCollection Warnings
        {
            get
            {
                if (null == _warnings)
                {
                    _warnings = new SqlErrorCollection();
                }

                return _warnings;
            }
        }

        protected virtual void DispatchMessages(bool ignoreNonFatalMessages)
        {
            // virtual because we want a default implementation in the cases
            // where we don't have a connection to process stuff, but we want to
            // provide the connection the ability to fire info messages when it
            // hooks up.
            SmiEventSink_Default parent = (SmiEventSink_Default)_parent;
            if (null != parent)
            {
                parent.DispatchMessages(ignoreNonFatalMessages);
            }
            else
            {
                SqlException errors = ProcessMessages(true, ignoreNonFatalMessages);   // ignore warnings, because there's no place to send them...
                if (null != errors)
                {
                    throw errors;
                }
            }
        }

        protected SqlException ProcessMessages(bool ignoreWarnings, bool ignoreNonFatalMessages)
        {
            SqlException result = null;
            SqlErrorCollection temp = null;  // temp variable to store that which is being thrown - so that local copies can be deleted

            if (null != _errors)
            {
                Debug.Assert(0 != _errors.Count, "empty error collection?"); // must be something in the collection

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
                {
                    if (null != _warnings)
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
                Debug.Assert(null == _warnings || 0 != _warnings.Count, "empty warning collection?");// must be something in the collection

                if (!ignoreWarnings)
                {
                    temp = _warnings;
                }
                _warnings = null;
            }

            if (null != temp)
            {
                result = SqlException.CreateException(temp, ServerVersion);
            }
            return result;
        }

        internal void CleanMessages()
        {
            SmiEventSink_Default parent = (SmiEventSink_Default)_parent;
            if (null != parent)
            {
                parent.CleanMessages();
            }
            else
            {
                _errors = null;
                _warnings = null;
            }
        }

        internal void ProcessMessagesAndThrow()
        {
            ProcessMessagesAndThrow(false);
        }

        internal void ProcessMessagesAndThrow(bool ignoreNonFatalMessages)
        {
            if (HasMessages)
            {
                DispatchMessages(ignoreNonFatalMessages);
            }
        }

        internal enum UnexpectedEventType
        {
            BatchCompleted,
            ColumnInfoAvailable,
            DefaultDatabaseChanged,
            MessagePosted,
            MetaDataAvailable,
            ParameterAvailable,
            ParametersAvailable,
            RowAvailable,
            StatementCompleted,
            TableNameAvailable,
            TransactionCommitted,
            TransactionDefected,
            TransactionEnlisted,
            TransactionEnded,
            TransactionRolledBack,
            TransactionStarted,
        }


        internal SmiEventSink_Default()
        {
        }

        internal SmiEventSink_Default(SmiEventSink parent)
        {
            _parent = parent;
        }


        // NOTE: See the note in SmiEventSink about throwing from these methods;
        //       We're throwing here because we don't want to miss something, but
        //       you'll need to turn on Bid tracing to figure out what it is that
        //       was thrown, because they will be eaten by the server and replaced
        //       with a different exception.


        // Called at end of stream
        internal override void BatchCompleted()
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.BatchCompleted);
            }
            _parent.BatchCompleted();
        }

        internal override void ParametersAvailable(SmiParameterMetaData[] metaData, ITypedGettersV3 paramValues)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.ParametersAvailable);
            }
            _parent.ParametersAvailable(metaData, paramValues);
        }

        internal override void ParameterAvailable(SmiParameterMetaData metaData, SmiTypedGetterSetter paramValue, int ordinal)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.ParameterAvailable);
            }
            _parent.ParameterAvailable(metaData, paramValue, ordinal);
        }

        // Called when the server database context changes (ENVCHANGE token)
        internal override void DefaultDatabaseChanged(string databaseName)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.DefaultDatabaseChanged);
            }
            _parent.DefaultDatabaseChanged(databaseName);
        }

        // Called for messages and errors (ERROR and INFO tokens)
        internal override void MessagePosted(int number, byte state, byte errorClass, string server, string message, string procedure, int lineNumber)
        {
            if (null == _parent)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SmiEventSink_Default.MessagePosted|ADV> {0}, number={1} state={2} errorClass={3} server='{4}' message='{5}' procedure='{6}' linenumber={7}.", 0, number, state, errorClass, server, message, procedure, lineNumber);
                SqlError error = new SqlError(number, state, errorClass, server, message, procedure, lineNumber);

                if (error.Class < TdsEnums.MIN_ERROR_CLASS)
                {
                    Warnings.Add(error);
                }
                else
                {
                    Errors.Add(error);
                }
            }
            else
            {
                _parent.MessagePosted(number, state, errorClass, server, message, procedure, lineNumber);
            }
        }

        // Called for new resultset starting (COLMETADATA token)
        internal override void MetaDataAvailable(SmiQueryMetaData[] metaData, bool nextEventIsRow)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.MetaDataAvailable);
            }
            _parent.MetaDataAvailable(metaData, nextEventIsRow);
        }

        // Called when a new row arrives (ROW token)
        internal override void RowAvailable(ITypedGetters rowData)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.RowAvailable);
            }
            _parent.RowAvailable(rowData);
        }

        // Called when a new row arrives (ROW token)
        internal override void RowAvailable(ITypedGettersV3 rowData)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.RowAvailable);
            }
            _parent.RowAvailable(rowData);
        }

        // Called when any statement completes on server (DONE token)
        internal override void StatementCompleted(int rowsAffected)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.StatementCompleted);
            }
            _parent.StatementCompleted(rowsAffected);
        }

        // Called when a transaction is committed (ENVCHANGE token)
        internal override void TransactionCommitted(long transactionId)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionCommitted);
            }
            _parent.TransactionCommitted(transactionId);
        }

        // Called when a transaction is committed (ENVCHANGE token)
        internal override void TransactionDefected(long transactionId)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionDefected);
            }
            _parent.TransactionDefected(transactionId);
        }

        // Called when a transaction is committed (ENVCHANGE token)
        internal override void TransactionEnlisted(long transactionId)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionEnlisted);
            }
            _parent.TransactionEnlisted(transactionId);
        }

        // Called when a transaction is forcibly ended in the server, not requested
        // by the provider's batch (ENVCHANGE token)
        internal override void TransactionEnded(long transactionId)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionEnded);
            }
            _parent.TransactionEnded(transactionId);
        }

        // Called when a transaction is rolled back (ENVCHANGE token)
        internal override void TransactionRolledBack(long transactionId)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionRolledBack);
            }
            _parent.TransactionRolledBack(transactionId);
        }

        // Called when a transaction is started (ENVCHANGE token)
        internal override void TransactionStarted(long transactionId)
        {
            if (null == _parent)
            {
                throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionStarted);
            }
            _parent.TransactionStarted(transactionId);
        }
    }
}

