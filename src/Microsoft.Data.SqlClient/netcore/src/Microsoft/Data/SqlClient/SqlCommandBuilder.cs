// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/CatalogLocation/*'/>
        /// <devnote>SqlServer only supports CatalogLocation.Start</devnote>
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
                    throw ADP.SingleValuedProperty(nameof(CatalogLocation), nameof(CatalogLocation.Start));
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/CatalogSeparator/*'/>
        /// <devnote>SqlServer only supports '.'</devnote>
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
                    throw ADP.SingleValuedProperty(nameof(CatalogSeparator), ".");
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/DataAdapter/*'/>
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuotePrefix/*'/>
        /// <devnote>SqlServer only supports '.'</devnote>
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
                    throw ADP.DoubleValuedProperty(nameof(QuotePrefix), "[", "\"");
                }
                base.QuotePrefix = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuoteSuffix/*'/>
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
                    throw ADP.DoubleValuedProperty(nameof(QuoteSuffix), "]", "\"");
                }
                base.QuoteSuffix = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/SchemaSeparator/*'/>
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
                    throw ADP.SingleValuedProperty(nameof(SchemaSeparator), ".");
                }
            }
        }

        private void SqlRowUpdatingHandler(object sender, SqlRowUpdatingEventArgs ruevent)
        {
            base.RowUpdatingHandler(ruevent);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetInsertCommand2/*'/>
        new public SqlCommand GetInsertCommand()
            => (SqlCommand)base.GetInsertCommand();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetInsertCommand3/*'/>
        new public SqlCommand GetInsertCommand(bool useColumnsForParameterNames)
            => (SqlCommand)base.GetInsertCommand(useColumnsForParameterNames);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetUpdateCommand2/*'/>
        new public SqlCommand GetUpdateCommand()
            => (SqlCommand)base.GetUpdateCommand();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetUpdateCommand3/*'/>
        new public SqlCommand GetUpdateCommand(bool useColumnsForParameterNames)
            => (SqlCommand)base.GetUpdateCommand(useColumnsForParameterNames);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetDeleteCommand2/*'/>
        new public SqlCommand GetDeleteCommand()
            => (SqlCommand)base.GetDeleteCommand();

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetDeleteCommand3/*'/>
        new public SqlCommand GetDeleteCommand(bool useColumnsForParameterNames)
            => (SqlCommand)base.GetDeleteCommand(useColumnsForParameterNames);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/ApplyParameterInfo/*'/>
        protected override void ApplyParameterInfo(DbParameter parameter, DataRow datarow, StatementType statementType, bool whereClause)
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
                p.UdtTypeName = string.Empty;
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
        protected override string GetParameterName(int parameterOrdinal)
            => ("@p" + parameterOrdinal.ToString(CultureInfo.InvariantCulture));

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetParameterName2/*'/>
        protected override string GetParameterName(string parameterName)
            => ("@" + parameterName);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetParameterPlaceholder/*'/>
        protected override string GetParameterPlaceholder(int parameterOrdinal)
            => ("@p" + parameterOrdinal.ToString(CultureInfo.InvariantCulture));

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
        public static void DeriveParameters(SqlCommand command)
        {
            if (null == command)
            {
                throw ADP.ArgumentNull(nameof(command));
            }

            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                command.DeriveParameters();
            }
            catch (OutOfMemoryException e)
            {
                command?.Connection?.Abort(e);
                throw;
            }
            catch (StackOverflowException e)
            {
                command?.Connection?.Abort(e);
                throw;
            }
            catch (ThreadAbortException e)
            {
                command?.Connection?.Abort(e);
                throw;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/GetSchemaTable/*'/>
        protected override DataTable GetSchemaTable(DbCommand srcCommand)
        {
            SqlCommand sqlCommand = srcCommand as SqlCommand;
            SqlNotificationRequest notificationRequest = sqlCommand.Notification;

            sqlCommand.Notification = null;

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
            }

        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/InitializeCommand/*'/>
        protected override DbCommand InitializeCommand(DbCommand command)
            => (SqlCommand)base.InitializeCommand(command);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/QuoteIdentifier/*'/>
        public override string QuoteIdentifier(string unquotedIdentifier)
        {
            ADP.CheckArgumentNull(unquotedIdentifier, nameof(unquotedIdentifier));
            string quoteSuffixLocal = QuoteSuffix;
            string quotePrefixLocal = QuotePrefix;
            ConsistentQuoteDelimiters(quotePrefixLocal, quoteSuffixLocal);
            return ADP.BuildQuotedString(quotePrefixLocal, quoteSuffixLocal, unquotedIdentifier);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommandBuilder.xml' path='docs/members[@name="SqlCommandBuilder"]/SetRowUpdatingHandler/*'/>
        protected override void SetRowUpdatingHandler(DbDataAdapter adapter)
        {
            Debug.Assert(adapter is SqlDataAdapter, "Adapter is not a SqlDataAdapter.");
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
            ADP.CheckArgumentNull(quotedIdentifier, nameof(quotedIdentifier));
            string unquotedIdentifier;
            string quoteSuffixLocal = QuoteSuffix;
            string quotePrefixLocal = QuotePrefix;
            ConsistentQuoteDelimiters(quotePrefixLocal, quoteSuffixLocal);
            // ignoring the return value because an unquoted source string is OK here
            ADP.RemoveStringQuotes(quotePrefixLocal, quoteSuffixLocal, quotedIdentifier, out unquotedIdentifier);
            return unquotedIdentifier;
        }
    }
}
