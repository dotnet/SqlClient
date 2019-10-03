// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Data.Common;
using Microsoft.Data.Sql;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/SqlCommandBuilder/*'/>
    public sealed class SqlCommandBuilder : DbCommandBuilder
    {

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/ctor1/*'/>
        public SqlCommandBuilder() : base()
        {
            GC.SuppressFinalize(this);
            base.QuotePrefix = "["; // initialize base with defaults
            base.QuoteSuffix = "]";
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/ctor2/*'/>
        public SqlCommandBuilder(SqlDataAdapter adapter) : this()
        {
            DataAdapter = adapter;
        }

        /// <devnote>SqlServer only supports CatalogLocation.Start</devnote>
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/CatalogLocation/*'/>
        [
        Browsable(false),
        EditorBrowsableAttribute(EditorBrowsableState.Never),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ]
        public override CatalogLocation CatalogLocation
        {
            get
            {
                return CatalogLocation.Start;
            }
            set
            {
                if (CatalogLocation.Start != value)
                {
                    throw ADP.SingleValuedProperty("CatalogLocation", "Start");
                }
            }
        }

        /// <devnote>SqlServer only supports '.'</devnote>
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/CatalogSeparator/*'/>
        [
        Browsable(false),
        EditorBrowsableAttribute(EditorBrowsableState.Never),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ]
        public override string CatalogSeparator
        {
            get
            {
                return ".";
            }
            set
            {
                if ("." != value)
                {
                    throw ADP.SingleValuedProperty("CatalogSeparator", ".");
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/DataAdapter/*'/>
        [
        DefaultValue(null),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Update),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlCommandBuilder_DataAdapter), // MDAC 60524
        ]
        new public SqlDataAdapter DataAdapter
        {
            get
            {
                return (SqlDataAdapter)base.DataAdapter;
            }
            set
            {
                base.DataAdapter = value;
            }
        }

        /// <devnote>SqlServer only supports '.'</devnote>
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuotePrefix/*'/>
        [
        Browsable(false),
        EditorBrowsableAttribute(EditorBrowsableState.Never),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ]
        public override string QuotePrefix
        {
            get
            {
                return base.QuotePrefix;
            }
            set
            {
                if (("[" != value) && ("\"" != value))
                {
                    throw ADP.DoubleValuedProperty("QuotePrefix", "[", "\"");
                }
                base.QuotePrefix = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuoteSuffix/*'/>
        [
        Browsable(false),
        EditorBrowsableAttribute(EditorBrowsableState.Never),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ]
        public override string QuoteSuffix
        {
            get
            {
                return base.QuoteSuffix;
            }
            set
            {
                if (("]" != value) && ("\"" != value))
                {
                    throw ADP.DoubleValuedProperty("QuoteSuffix", "]", "\"");
                }
                base.QuoteSuffix = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/SchemaSeparator/*'/>
        [
        Browsable(false),
        EditorBrowsableAttribute(EditorBrowsableState.Never),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ]
        public override string SchemaSeparator
        {
            get
            {
                return ".";
            }
            set
            {
                if ("." != value)
                {
                    throw ADP.SingleValuedProperty("SchemaSeparator", ".");
                }
            }
        }

        private void SqlRowUpdatingHandler(object sender, SqlRowUpdatingEventArgs ruevent)
        {
            base.RowUpdatingHandler(ruevent);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetInsertCommand2/*'/>
        new public SqlCommand GetInsertCommand()
        {
            return (SqlCommand)base.GetInsertCommand();
        }
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetInsertCommand3/*'/>
        new public SqlCommand GetInsertCommand(bool useColumnsForParameterNames)
        {
            return (SqlCommand)base.GetInsertCommand(useColumnsForParameterNames);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetUpdateCommand2/*'/>
        new public SqlCommand GetUpdateCommand()
        {
            return (SqlCommand)base.GetUpdateCommand();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetUpdateCommand3/*'/>
        new public SqlCommand GetUpdateCommand(bool useColumnsForParameterNames)
        {
            return (SqlCommand)base.GetUpdateCommand(useColumnsForParameterNames);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetDeleteCommand2/*'/>
        new public SqlCommand GetDeleteCommand()
        {
            return (SqlCommand)base.GetDeleteCommand();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetDeleteCommand3/*'/>
        new public SqlCommand GetDeleteCommand(bool useColumnsForParameterNames)
        {
            return (SqlCommand)base.GetDeleteCommand(useColumnsForParameterNames);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/ApplyParameterInfo/*'/>
        override protected void ApplyParameterInfo(DbParameter parameter, DataRow datarow, StatementType statementType, bool whereClause)
        {
            SqlParameter p = (SqlParameter)parameter;
            object valueType = datarow[SchemaTableColumn.ProviderType];
            p.SqlDbType = (SqlDbType)valueType;
            p.Offset = 0;

            if ((p.SqlDbType == SqlDbType.Udt) && !p.SourceColumnNullMapping)
            {
                p.UdtTypeName = datarow["DataTypeName"] as string;
            }
            else
            {
                p.UdtTypeName = String.Empty;
            }

            object bvalue = datarow[SchemaTableColumn.NumericPrecision];
            if (DBNull.Value != bvalue)
            {
                byte bval = (byte)(short)bvalue;
                p.PrecisionInternal = ((0xff != bval) ? bval : (byte)0);
            }

            bvalue = datarow[SchemaTableColumn.NumericScale];
            if (DBNull.Value != bvalue)
            {
                byte bval = (byte)(short)bvalue;
                p.ScaleInternal = ((0xff != bval) ? bval : (byte)0);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetParameterName1/*'/>
        override protected string GetParameterName(int parameterOrdinal)
        {
            return "@p" + parameterOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetParameterName2/*'/>
        override protected string GetParameterName(string parameterName)
        {
            return "@" + parameterName;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetParameterPlaceholder/*'/>
        override protected string GetParameterPlaceholder(int parameterOrdinal)
        {
            return "@p" + parameterOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void ConsistentQuoteDelimiters(string quotePrefix, string quoteSuffix)
        {

            Debug.Assert(quotePrefix == "\"" || quotePrefix == "[");
            if ((("\"" == quotePrefix) && ("\"" != quoteSuffix)) ||
                (("[" == quotePrefix) && ("]" != quoteSuffix)))
            {
                throw ADP.InvalidPrefixSuffix();
            }

        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/DeriveParameters/*'/>
        static public void DeriveParameters(SqlCommand command)
        { // MDAC 65927\
            SqlConnection.ExecutePermission.Demand();

            if (null == command)
            {
                throw ADP.ArgumentNull("command");
            }
            TdsParser bestEffortCleanupTarget = null;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
#if DEBUG
                TdsParser.ReliabilitySection tdsReliabilitySection = new TdsParser.ReliabilitySection();

                RuntimeHelpers.PrepareConstrainedRegions();
                try {
                    tdsReliabilitySection.Start();
#else
                {
#endif
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(command.Connection);
                    command.DeriveParameters();
                }
#if DEBUG
                finally {
                    tdsReliabilitySection.Stop();
                }
#endif
            }
            catch (System.OutOfMemoryException e)
            {
                if (null != command && null != command.Connection)
                {
                    command.Connection.Abort(e);
                }
                throw;
            }
            catch (System.StackOverflowException e)
            {
                if (null != command && null != command.Connection)
                {
                    command.Connection.Abort(e);
                }
                throw;
            }
            catch (System.Threading.ThreadAbortException e)
            {
                if (null != command && null != command.Connection)
                {
                    command.Connection.Abort(e);
                }
                SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                throw;
            }
        }


        /*        private static void GetLiteralInfo (DataRow dataTypeRow, out string literalPrefix, out string literalSuffix) {

                    Object tempValue = dataTypeRow[DbMetaDataColumnNames.LiteralPrefix];
                    if (tempValue == DBNull.Value) {
                        literalPrefix = "";
                    }
                    else {
                        literalPrefix = (string)dataTypeRow[DbMetaDataColumnNames.LiteralPrefix];
                    }
                    tempValue = dataTypeRow[DbMetaDataColumnNames.LiteralSuffix];
                    if (tempValue == DBNull.Value) {
                        literalSuffix = "";
                    }
                    else {
                        literalSuffix = (string)dataTypeRow[DbMetaDataColumnNames.LiteralSuffix];
                    }
                }
        */

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetSchemaTable/*'/>
        protected override DataTable GetSchemaTable(DbCommand srcCommand)
        {
            SqlCommand sqlCommand = srcCommand as SqlCommand;
            SqlNotificationRequest notificationRequest = sqlCommand.Notification;
            bool notificationAutoEnlist = sqlCommand.NotificationAutoEnlist;

            sqlCommand.Notification = null;
            sqlCommand.NotificationAutoEnlist = false;

            try
            {
                using (SqlDataReader dataReader = sqlCommand.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo))
                {
                    return dataReader.GetSchemaTable();
                }
            }
            finally
            {
                sqlCommand.Notification = notificationRequest;
                sqlCommand.NotificationAutoEnlist = notificationAutoEnlist;
            }

        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/InitializeCommand/*'/>
        protected override DbCommand InitializeCommand(DbCommand command)
        {
            SqlCommand cmd = (SqlCommand)base.InitializeCommand(command);
            cmd.NotificationAutoEnlist = false;
            return cmd;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuoteIdentifier/*'/>
        public override string QuoteIdentifier(string unquotedIdentifier)
        {
            ADP.CheckArgumentNull(unquotedIdentifier, "unquotedIdentifier");
            string quoteSuffixLocal = QuoteSuffix;
            string quotePrefixLocal = QuotePrefix;
            ConsistentQuoteDelimiters(quotePrefixLocal, quoteSuffixLocal);
            return ADP.BuildQuotedString(quotePrefixLocal, quoteSuffixLocal, unquotedIdentifier);
            ;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/SetRowUpdatingHandler/*'/>
        override protected void SetRowUpdatingHandler(DbDataAdapter adapter)
        {
            Debug.Assert(adapter is SqlDataAdapter, "!SqlDataAdapter");
            if (adapter == base.DataAdapter)
            { // removal case
                ((SqlDataAdapter)adapter).RowUpdating -= SqlRowUpdatingHandler;
            }
            else
            { // adding case
                ((SqlDataAdapter)adapter).RowUpdating += SqlRowUpdatingHandler;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/UnquoteIdentifier/*'/>
        public override string UnquoteIdentifier(string quotedIdentifier)
        {

            ADP.CheckArgumentNull(quotedIdentifier, "quotedIdentifier");
            String unquotedIdentifier;
            string quoteSuffixLocal = QuoteSuffix;
            string quotePrefixLocal = QuotePrefix;
            ConsistentQuoteDelimiters(quotePrefixLocal, quoteSuffixLocal);
            // ignoring the return value becasue an unquoted source string is OK here
            ADP.RemoveStringQuotes(quotePrefixLocal, quoteSuffixLocal, quotedIdentifier, out unquotedIdentifier);
            return unquotedIdentifier;
        }
    }
}
