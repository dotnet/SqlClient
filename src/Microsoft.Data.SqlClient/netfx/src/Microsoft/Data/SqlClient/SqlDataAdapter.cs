// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/SqlDataAdapter/*' />
    [
    DefaultEvent("RowUpdated"),
    ToolboxItem("Microsoft.VSDesigner.Data.VS.SqlDataAdapterToolboxItem, " + AssemblyRef.MicrosoftVSDesigner),
    Designer("Microsoft.VSDesigner.Data.VS.SqlDataAdapterDesigner, " + AssemblyRef.MicrosoftVSDesigner)
    ]
    public sealed class SqlDataAdapter : DbDataAdapter, IDbDataAdapter, ICloneable
    {

        static private readonly object EventRowUpdated = new object();
        static private readonly object EventRowUpdating = new object();

        private SqlCommand _deleteCommand, _insertCommand, _selectCommand, _updateCommand;

        private SqlCommandSet _commandSet;
        private int _updateBatchSize = 1;

        private static int _objectTypeCount; // EventSource Counter
        internal readonly int _objectID = System.Threading.Interlocked.Increment(ref _objectTypeCount);

        internal int ObjectID
        {
            get
            {
                return _objectID;
            }
        }
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctor2/*' />
        public SqlDataAdapter() : base()
        {
            GC.SuppressFinalize(this);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommand/*' />
        public SqlDataAdapter(SqlCommand selectCommand) : this()
        {
            SelectCommand = selectCommand;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommandTextSelectConnectionString/*' />
        public SqlDataAdapter(string selectCommandText, string selectConnectionString) : this()
        {
            SqlConnection connection = new SqlConnection(selectConnectionString);
            SelectCommand = new SqlCommand(selectCommandText, connection);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ctorSelectCommandTextSelectConnection/*' />
        public SqlDataAdapter(string selectCommandText, SqlConnection selectConnection) : this()
        {
            SelectCommand = new SqlCommand(selectCommandText, selectConnection);
        }

        private SqlDataAdapter(SqlDataAdapter from) : base(from)
        { // Clone
            GC.SuppressFinalize(this);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/DeleteCommand/*' />
        [
        DefaultValue(null),
        Editor("Microsoft.VSDesigner.Data.Design.DBCommandEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update),
        ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_DeleteCommand),
        ]
        new public SqlCommand DeleteCommand
        {
            get { return _deleteCommand; }
            set { _deleteCommand = value; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.Data.IDbDataAdapter.DeleteCommand/*' />
        IDbCommand IDbDataAdapter.DeleteCommand
        {
            get { return _deleteCommand; }
            set { _deleteCommand = (SqlCommand)value; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/InsertCommand/*' />
        [
        DefaultValue(null),
        Editor("Microsoft.VSDesigner.Data.Design.DBCommandEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update),
        ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_InsertCommand),
        ]
        new public SqlCommand InsertCommand
        {
            get { return _insertCommand; }
            set { _insertCommand = value; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.Data.IDbDataAdapter.InsertCommand/*' />
        IDbCommand IDbDataAdapter.InsertCommand
        {
            get { return _insertCommand; }
            set { _insertCommand = (SqlCommand)value; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/SelectCommand/*' />
        [
        DefaultValue(null),
        Editor("Microsoft.VSDesigner.Data.Design.DBCommandEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Fill),
        ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_SelectCommand),
        ]
        new public SqlCommand SelectCommand
        {
            get { return _selectCommand; }
            set { _selectCommand = value; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.Data.IDbDataAdapter.SelectCommand/*' />
        IDbCommand IDbDataAdapter.SelectCommand
        {
            get { return _selectCommand; }
            set { _selectCommand = (SqlCommand)value; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/UpdateBatchSize/*' />
        override public int UpdateBatchSize
        {
            get
            {
                return _updateBatchSize;
            }
            set
            {
                if (0 > value)
                { // WebData 98157
                    throw ADP.ArgumentOutOfRange("UpdateBatchSize");
                }
                _updateBatchSize = value;
                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlDataAdapter.set_UpdateBatchSize|API> {0}, {1}", ObjectID, value);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/UpdateCommand/*' />
        [
        DefaultValue(null),
        Editor("Microsoft.VSDesigner.Data.Design.DBCommandEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update),
        ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_UpdateCommand),
        ]
        new public SqlCommand UpdateCommand
        {
            get { return _updateCommand; }
            set { _updateCommand = value; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.Data.IDbDataAdapter.UpdateCommand/*' />
        IDbCommand IDbDataAdapter.UpdateCommand
        {
            get { return _updateCommand; }
            set { _updateCommand = (SqlCommand)value; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/RowUpdated/*' />
        [
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update),
        ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_RowUpdated),
        ]
        public event SqlRowUpdatedEventHandler RowUpdated
        {
            add
            {
                Events.AddHandler(EventRowUpdated, value);
            }
            remove
            {
                Events.RemoveHandler(EventRowUpdated, value);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/RowUpdating/*' />
        [
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update),
        ResDescriptionAttribute(StringsHelper.ResourceNames.DbDataAdapter_RowUpdating),
        ]
        public event SqlRowUpdatingEventHandler RowUpdating
        {
            add
            {
                SqlRowUpdatingEventHandler handler = (SqlRowUpdatingEventHandler)Events[EventRowUpdating];

                // MDAC 58177, 64513
                // prevent someone from registering two different command builders on the adapter by
                // silently removing the old one
                if ((null != handler) && (value.Target is DbCommandBuilder))
                {
                    SqlRowUpdatingEventHandler d = (SqlRowUpdatingEventHandler)ADP.FindBuilder(handler);
                    if (null != d)
                    {
                        Events.RemoveHandler(EventRowUpdating, d);
                    }
                }
                Events.AddHandler(EventRowUpdating, value);
            }
            remove
            {
                Events.RemoveHandler(EventRowUpdating, value);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/AddToBatch/*' />
        override protected int AddToBatch(IDbCommand command)
        {
            int commandIdentifier = _commandSet.CommandCount;
            _commandSet.Append((SqlCommand)command);
            return commandIdentifier;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ClearBatch/*' />
        override protected void ClearBatch()
        {
            _commandSet.Clear();
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/System.ICloneable.Clone/*' />
        object ICloneable.Clone()
        {
            return new SqlDataAdapter(this);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/CreateRowUpdatedEvent/*' />
        override protected RowUpdatedEventArgs CreateRowUpdatedEvent(DataRow dataRow, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        {
            return new SqlRowUpdatedEventArgs(dataRow, command, statementType, tableMapping);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/CreateRowUpdatingEvent/*' />
        override protected RowUpdatingEventArgs CreateRowUpdatingEvent(DataRow dataRow, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
        {
            return new SqlRowUpdatingEventArgs(dataRow, command, statementType, tableMapping);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/ExecuteBatch/*' />
        override protected int ExecuteBatch()
        {
            Debug.Assert(null != _commandSet && (0 < _commandSet.CommandCount), "no commands");
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlDataAdapter.ExecuteBatch|Info|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);

            return _commandSet.ExecuteNonQuery();
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/GetBatchedParameter/*' />
        override protected IDataParameter GetBatchedParameter(int commandIdentifier, int parameterIndex)
        {
            Debug.Assert(commandIdentifier < _commandSet.CommandCount, "commandIdentifier out of range");
            Debug.Assert(parameterIndex < _commandSet.GetParameterCount(commandIdentifier), "parameter out of range");
            IDataParameter parameter = _commandSet.GetParameter(commandIdentifier, parameterIndex);
            return parameter;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/GetBatchedRecordsAffected/*' />
        override protected bool GetBatchedRecordsAffected(int commandIdentifier, out int recordsAffected, out Exception error)
        {
            Debug.Assert(commandIdentifier < _commandSet.CommandCount, "commandIdentifier out of range");
            return _commandSet.GetBatchedAffected(commandIdentifier, out recordsAffected, out error);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/InitializeBatching/*' />
        override protected void InitializeBatching()
        {
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlDataAdapter.InitializeBatching|API> {0}", ObjectID);
            _commandSet = new SqlCommandSet();
            SqlCommand command = SelectCommand;
            if (null == command)
            {
                command = InsertCommand;
                if (null == command)
                {
                    command = UpdateCommand;
                    if (null == command)
                    {
                        command = DeleteCommand;
                    }
                }
            }
            if (null != command)
            {
                _commandSet.Connection = command.Connection;
                _commandSet.Transaction = command.Transaction;
                _commandSet.CommandTimeout = command.CommandTimeout;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/OnRowUpdated/*' />
        override protected void OnRowUpdated(RowUpdatedEventArgs value)
        {
            SqlRowUpdatedEventHandler handler = (SqlRowUpdatedEventHandler)Events[EventRowUpdated];
            if ((null != handler) && (value is SqlRowUpdatedEventArgs))
            {
                handler(this, (SqlRowUpdatedEventArgs)value);
            }
            base.OnRowUpdated(value);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/OnRowUpdating/*' />
        override protected void OnRowUpdating(RowUpdatingEventArgs value)
        {
            SqlRowUpdatingEventHandler handler = (SqlRowUpdatingEventHandler)Events[EventRowUpdating];
            if ((null != handler) && (value is SqlRowUpdatingEventArgs))
            {
                handler(this, (SqlRowUpdatingEventArgs)value);
            }
            base.OnRowUpdating(value);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlDataAdapter.xml' path='docs/members[@name="SqlDataAdapter"]/TerminateBatching/*' />
        override protected void TerminateBatching()
        {
            if (null != _commandSet)
            {
                _commandSet.Dispose();
                _commandSet = null;
            }
        }
    }
}
