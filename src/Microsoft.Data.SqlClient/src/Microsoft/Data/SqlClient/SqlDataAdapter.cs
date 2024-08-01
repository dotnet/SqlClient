// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using Microsoft.Data.Common;

// NOTE: The current Microsoft.VSDesigner editor attributes are implemented for System.Data.SqlClient, and are not publicly available.
// New attributes that are designed to work with Microsoft.Data.SqlClient and are publicly documented should be included in future.
namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/SqlDataAdapter/*' />
    [DefaultEvent("RowUpdated")]
    [DesignerCategory("")]
    // TODO: Add designer and toolbox attribute when Microsoft.VSDesigner.Data.VS.SqlDataAdapterDesigner uses Microsoft.Data.SqlClient
    public sealed class SqlDataAdapter : DbDataAdapter, IDbDataAdapter, ICloneable
    {
        private static readonly object s_eventRowUpdated = new();
        private static readonly object s_eventRowUpdating = new();

        private SqlCommand _deleteCommand, _insertCommand, _selectCommand, _updateCommand;

        private SqlCommandSet _commandSet;
        private int _updateBatchSize = 1;

        private static int s_objectTypeCount; // EventSource Counter
        internal readonly int _objectID = Interlocked.Increment(ref s_objectTypeCount);

        internal int ObjectID => _objectID;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctor2/*' />
        public SqlDataAdapter() : base()
        {
            GC.SuppressFinalize(this);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommand/*' />
        public SqlDataAdapter(SqlCommand selectCommand) : this()
        {
            SelectCommand = selectCommand;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommandTextSelectConnectionString/*' />
        public SqlDataAdapter(string selectCommandText, string selectConnectionString) : this()
        {
            SqlConnection connection = new(selectConnectionString);
            SelectCommand = new SqlCommand(selectCommandText, connection);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommandTextSelectConnection/*' />
        public SqlDataAdapter(string selectCommandText, SqlConnection selectConnection) : this()
        {
            SelectCommand = new SqlCommand(selectCommandText, selectConnection);
        }

        private SqlDataAdapter(SqlDataAdapter from) : base(from)
        {   // Clone
            GC.SuppressFinalize(this);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/DeleteCommand/*' />
        [DefaultValue(null)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_DeleteCommand)]
        new public SqlCommand DeleteCommand
        {
            get { return _deleteCommand; }
            set { _deleteCommand = value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.Data.IDbDataAdapter.DeleteCommand/*' />
        IDbCommand IDbDataAdapter.DeleteCommand
        {
            get { return _deleteCommand; }
            set { _deleteCommand = (SqlCommand)value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/InsertCommand/*' />
        [DefaultValue(null)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_InsertCommand)]
        new public SqlCommand InsertCommand
        {
            get { return _insertCommand; }
            set { _insertCommand = value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.Data.IDbDataAdapter.InsertCommand/*' />
        IDbCommand IDbDataAdapter.InsertCommand
        {
            get { return _insertCommand; }
            set { _insertCommand = (SqlCommand)value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/SelectCommand/*' />
        [DefaultValue(null)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Fill)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_SelectCommand)]
        new public SqlCommand SelectCommand
        {
            get { return _selectCommand; }
            set { _selectCommand = value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.Data.IDbDataAdapter.SelectCommand/*' />
        IDbCommand IDbDataAdapter.SelectCommand
        {
            get { return _selectCommand; }
            set { _selectCommand = (SqlCommand)value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/UpdateCommand/*' />
        [DefaultValue(null)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_UpdateCommand)]
        new public SqlCommand UpdateCommand
        {
            get { return _updateCommand; }
            set { _updateCommand = value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.Data.IDbDataAdapter.UpdateCommand/*' />
        IDbCommand IDbDataAdapter.UpdateCommand
        {
            get { return _updateCommand; }
            set { _updateCommand = (SqlCommand)value; }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/UpdateBatchSize/*' />
        public override int UpdateBatchSize
        {
            get
            {
                return _updateBatchSize;
            }
            set
            {
                if (0 > value)
                {
                    throw ADP.ArgumentOutOfRange(nameof(UpdateBatchSize));
                }
                _updateBatchSize = value;
                SqlClientEventSource.Log.TryTraceEvent("SqlDataAdapter.Set_UpdateBatchSize | API | Object Id {0}, Update Batch Size value {1}", ObjectID, value);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/AddToBatch/*' />
        protected override int AddToBatch(IDbCommand command)
        {
            int commandIdentifier = _commandSet.CommandCount;
            _commandSet.Append((SqlCommand)command);
            return commandIdentifier;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ClearBatch/*' />
        protected override void ClearBatch()
        {
            _commandSet.Clear();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ExecuteBatch/*' />
        protected override int ExecuteBatch()
        {
            Debug.Assert(_commandSet != null && (0 < _commandSet.CommandCount), "no commands");
            SqlClientEventSource.Log.TryCorrelationTraceEvent("SqlDataAdapter.ExecuteBatch | Info | Correlation | Object Id {0}, Activity Id {1}, Command Count {2}", ObjectID, ActivityCorrelator.Current, _commandSet.CommandCount);
            return _commandSet.ExecuteNonQuery();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/GetBatchedParameter/*' />
        protected override IDataParameter GetBatchedParameter(int commandIdentifier, int parameterIndex)
        {
            Debug.Assert(commandIdentifier < _commandSet.CommandCount, "commandIdentifier out of range");
            Debug.Assert(parameterIndex < _commandSet.GetParameterCount(commandIdentifier), "parameter out of range");
            IDataParameter parameter = _commandSet.GetParameter(commandIdentifier, parameterIndex);
            return parameter;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/GetBatchedRecordsAffected/*' />
        protected override bool GetBatchedRecordsAffected(int commandIdentifier, out int recordsAffected, out Exception error)
        {
            Debug.Assert(commandIdentifier < _commandSet.CommandCount, "commandIdentifier out of range");
            return _commandSet.GetBatchedAffected(commandIdentifier, out recordsAffected, out error);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/InitializeBatching/*' />
        protected override void InitializeBatching()
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlDataAdapter.InitializeBatching | API | Object Id {0}", ObjectID);
            _commandSet = new SqlCommandSet();
            SqlCommand command = SelectCommand;
            if (command == null)
            {
                command = InsertCommand;
                if (command == null)
                {
                    command = UpdateCommand;
                    if (command == null)
                    {
                        command = DeleteCommand;
                    }
                }
            }
            if (command != null)
            {
                _commandSet.Connection = command.Connection;
                _commandSet.Transaction = command.Transaction;
                _commandSet.CommandTimeout = command.CommandTimeout;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/TerminateBatching/*' />
        protected override void TerminateBatching()
        {
            if (_commandSet != null)
            {
                _commandSet.Dispose();
                _commandSet = null;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.ICloneable.Clone/*' />
        object ICloneable.Clone()
        {
            return new SqlDataAdapter(this);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/CreateRowUpdatedEvent/*' />
        protected override RowUpdatedEventArgs CreateRowUpdatedEvent(DataRow dataRow, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        {
            return new SqlRowUpdatedEventArgs(dataRow, command, statementType, tableMapping);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/CreateRowUpdatingEvent/*' />
        protected override RowUpdatingEventArgs CreateRowUpdatingEvent(DataRow dataRow, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        {
            return new SqlRowUpdatingEventArgs(dataRow, command, statementType, tableMapping);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/RowUpdated/*' />
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_RowUpdated)]
        public event SqlRowUpdatedEventHandler RowUpdated
        {
            add
            {
                Events.AddHandler(s_eventRowUpdated, value);
            }
            remove
            {
                Events.RemoveHandler(s_eventRowUpdated, value);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/RowUpdating/*' />
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_RowUpdating)]
        public event SqlRowUpdatingEventHandler RowUpdating
        {
            add
            {
                SqlRowUpdatingEventHandler handler = (SqlRowUpdatingEventHandler)Events[s_eventRowUpdating];

                // Prevent someone from registering two different command builders on the adapter by
                // silently removing the old one.
                if (handler != null && value.Target is DbCommandBuilder)
                {
                    SqlRowUpdatingEventHandler d = (SqlRowUpdatingEventHandler)ADP.FindBuilder(handler);
                    if (d != null)
                    {
                        Events.RemoveHandler(s_eventRowUpdating, d);
                    }
                }
                Events.AddHandler(s_eventRowUpdating, value);
            }
            remove
            {
                Events.RemoveHandler(s_eventRowUpdating, value);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/OnRowUpdated/*' />
        override protected void OnRowUpdated(RowUpdatedEventArgs value)
        {
            SqlRowUpdatedEventHandler handler = (SqlRowUpdatedEventHandler)Events[s_eventRowUpdated];
            if (handler != null && value is SqlRowUpdatedEventArgs args)
            {
                handler(this, args);
            }
            base.OnRowUpdated(value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/OnRowUpdating/*' />
        override protected void OnRowUpdating(RowUpdatingEventArgs value)
        {
            SqlRowUpdatingEventHandler handler = (SqlRowUpdatingEventHandler)Events[s_eventRowUpdating];
            if (handler != null && value is SqlRowUpdatingEventArgs args)
            {
                handler(this, args);
            }
            base.OnRowUpdating(value);
        }
    }
}
