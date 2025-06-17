// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Server
{
    internal partial class SmiEventSink_Default : SmiEventSink
    {
        private SqlErrorCollection Errors
        {
            get
            {
                if (_errors == null)
                {
                    _errors = new SqlErrorCollection();
                }

                return _errors;
            }
        }

        private SqlErrorCollection Warnings
        {
            get
            {
                if (_warnings == null)
                {
                    _warnings = new SqlErrorCollection();
                }

                return _warnings;
            }
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

        // <summary>
        //NOTE: See the note in SmiEventSink about throwing from these methods;
        // We're throwing here because we don't want to miss something, but
        //you'll need to turn on Bid tracing to figure out what it is that
        //was thrown, because they will be eaten by the server and replaced
        //with a different exception.
        // Called at end of stream
        //</summary>
        internal override void BatchCompleted() =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.BatchCompleted);

        internal override void ParametersAvailable(SmiParameterMetaData[] metaData, ITypedGettersV3 paramValues) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.ParametersAvailable);

        internal override void ParameterAvailable(
            SmiParameterMetaData metaData,
            SmiTypedGetterSetter paramValue,
            int ordinal) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.ParameterAvailable);
        

        // Called when the server database context changes (ENVCHANGE token)
        internal override void DefaultDatabaseChanged(string databaseName) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.DefaultDatabaseChanged);

        // Called for messages and errors (ERROR and INFO tokens)
        internal override void MessagePosted(int number, byte state, byte errorClass, string server, string message, string procedure, int lineNumber)
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

        // Called for new resultset starting (COLMETADATA token)
        internal override void MetaDataAvailable(SmiQueryMetaData[] metaData, bool nextEventIsRow) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.MetaDataAvailable);

        // Called when a new row arrives (ROW token)
        internal override void RowAvailable(ITypedGetters rowData) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.RowAvailable);

        // Called when a new row arrives (ROW token)
        internal override void RowAvailable(ITypedGettersV3 rowData) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.RowAvailable);

        // Called when any statement completes on server (DONE token)
        internal override void StatementCompleted(int rowsAffected) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.StatementCompleted);

        // Called when a transaction is committed (ENVCHANGE token)
        internal override void TransactionCommitted(long transactionId) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionCommitted);

        // Called when a transaction is committed (ENVCHANGE token)
        internal override void TransactionDefected(long transactionId) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionDefected);

        // Called when a transaction is committed (ENVCHANGE token)
        internal override void TransactionEnlisted(long transactionId) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionEnlisted);

        // Called when a transaction is forcibly ended in the server, not requested
        // by the provider's batch (ENVCHANGE token)
        internal override void TransactionEnded(long transactionId) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionEnded);

        // Called when a transaction is rolled back (ENVCHANGE token)
        internal override void TransactionRolledBack(long transactionId) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionRolledBack);

        // Called when a transaction is started (ENVCHANGE token)
        internal override void TransactionStarted(long transactionId) =>
            throw SQL.UnexpectedSmiEvent(UnexpectedEventType.TransactionStarted);
    }
}

