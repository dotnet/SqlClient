// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using SysES = System.EnterpriseServices;
using SysTx = System.Transactions;

namespace Microsoft.Data.Common
{
    internal static partial class ADP
    {
        internal static Task<T> CreatedTaskWithException<T>(Exception ex)
        {
            TaskCompletionSource<T> completion = new();
            completion.SetException(ex);
            return completion.Task;
        }

        internal static Task<T> CreatedTaskWithCancellation<T>()
        {
            TaskCompletionSource<T> completion = new();
            completion.SetCanceled();
            return completion.Task;
        }

        internal static void TraceExceptionForCapture(Exception e)
        {
            Debug.Assert(ADP.IsCatchableExceptionType(e), "Invalid exception type, should have been re-thrown!");
            TraceException("<comm.ADP.TraceException|ERR|CATCH> '{0}'", e);
        }

        //
        // COM+ exceptions
        //
        internal static ArgumentException Argument(string error, string parameter, Exception inner)
        {
            ArgumentException e = new(error, parameter, inner);
            TraceExceptionAsReturnValue(e);
            return e;
        }
        internal static ConfigurationException Configuration(string message)
        {
            ConfigurationException e = new ConfigurationErrorsException(message);
            TraceExceptionAsReturnValue(e);
            return e;
        }
        internal static ConfigurationException Configuration(string message, XmlNode node)
        {
            ConfigurationException e = new ConfigurationErrorsException(message, node);
            TraceExceptionAsReturnValue(e);
            return e;
        }
        internal static DataException Data(string message)
        {
            DataException e = new(message);
            TraceExceptionAsReturnValue(e);
            return e;
        }


        internal static NotImplementedException NotImplemented(string error)
        {
            NotImplementedException e = new(error);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static PlatformNotSupportedException PropertyNotSupported(string property)
        {
            PlatformNotSupportedException e = new(StringsHelper.GetString(Strings.ADP_PropertyNotSupported, property));
            TraceExceptionAsReturnValue(e);
            return e;
        }


        internal static InvalidOperationException DataAdapter(string error, Exception inner)
        {
            return InvalidOperation(error, inner);
        }

        internal static ArgumentException IncorrectAsyncResult()
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_IncorrectAsyncResult), "AsyncResult");
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentException MultipleReturnValue()
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_MultipleReturnValue));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        //
        // Helper Functions
        //
        internal static void CheckArgumentLength(string value, string parameterName)
        {
            CheckArgumentNull(value, parameterName);
            if (0 == value.Length)
            {
                throw Argument(StringsHelper.GetString(Strings.ADP_EmptyString, parameterName)); // MDAC 94859
            }
        }
        internal static void CheckArgumentLength(Array value, string parameterName)
        {
            CheckArgumentNull(value, parameterName);
            if (0 == value.Length)
            {
                throw Argument(StringsHelper.GetString(Strings.ADP_EmptyArray, parameterName));
            }
        }

        // Invalid Enumeration

        internal static ArgumentOutOfRangeException InvalidAcceptRejectRule(AcceptRejectRule value)
        {
#if DEBUG
            switch (value)
            {
                case AcceptRejectRule.None:
                case AcceptRejectRule.Cascade:
                    Debug.Assert(false, "valid AcceptRejectRule " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(AcceptRejectRule), (int)value);
        }
        // DbCommandBuilder.CatalogLocation
        internal static ArgumentOutOfRangeException InvalidCatalogLocation(CatalogLocation value)
        {
#if DEBUG
            switch (value)
            {
                case CatalogLocation.Start:
                case CatalogLocation.End:
                    Debug.Assert(false, "valid CatalogLocation " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(CatalogLocation), (int)value);
        }


        // IDbCommand.CommandType

        internal static ArgumentOutOfRangeException InvalidConflictOptions(ConflictOption value)
        {
#if DEBUG
            switch (value)
            {
                case ConflictOption.CompareAllSearchableValues:
                case ConflictOption.CompareRowVersion:
                case ConflictOption.OverwriteChanges:
                    Debug.Assert(false, "valid ConflictOption " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(ConflictOption), (int)value);
        }

        // IDataAdapter.Update
        internal static ArgumentOutOfRangeException InvalidDataRowState(DataRowState value)
        {
#if DEBUG
            switch (value)
            {
                case DataRowState.Detached:
                case DataRowState.Unchanged:
                case DataRowState.Added:
                case DataRowState.Deleted:
                case DataRowState.Modified:
                    Debug.Assert(false, "valid DataRowState " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(DataRowState), (int)value);
        }

        // IDbConnection.BeginTransaction, OleDbTransaction.Begin
        internal static ArgumentOutOfRangeException InvalidIsolationLevel(IsolationLevel value)
        {
#if DEBUG
            switch (value)
            {
                case IsolationLevel.Unspecified:
                case IsolationLevel.Chaos:
                case IsolationLevel.ReadUncommitted:
                case IsolationLevel.ReadCommitted:
                case IsolationLevel.RepeatableRead:
                case IsolationLevel.Serializable:
                case IsolationLevel.Snapshot:
                    Debug.Assert(false, "valid IsolationLevel " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(IsolationLevel), (int)value);
        }

        // DBDataPermissionAttribute.KeyRestrictionBehavior
        internal static ArgumentOutOfRangeException InvalidKeyRestrictionBehavior(KeyRestrictionBehavior value)
        {
#if DEBUG
            switch (value)
            {
                case KeyRestrictionBehavior.PreventUsage:
                case KeyRestrictionBehavior.AllowOnly:
                    Debug.Assert(false, "valid KeyRestrictionBehavior " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(KeyRestrictionBehavior), (int)value);
        }

        // IDataAdapter.FillLoadOption
        internal static ArgumentOutOfRangeException InvalidLoadOption(LoadOption value)
        {
#if DEBUG
            switch (value)
            {
                case LoadOption.OverwriteChanges:
                case LoadOption.PreserveChanges:
                case LoadOption.Upsert:
                    Debug.Assert(false, "valid LoadOption " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(LoadOption), (int)value);
        }

        // IDataAdapter.MissingMappingAction
        internal static ArgumentOutOfRangeException InvalidMissingMappingAction(MissingMappingAction value)
        {
#if DEBUG
            switch (value)
            {
                case MissingMappingAction.Passthrough:
                case MissingMappingAction.Ignore:
                case MissingMappingAction.Error:
                    Debug.Assert(false, "valid MissingMappingAction " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(MissingMappingAction), (int)value);
        }

        // IDataAdapter.MissingSchemaAction
        internal static ArgumentOutOfRangeException InvalidMissingSchemaAction(MissingSchemaAction value)
        {
#if DEBUG
            switch (value)
            {
                case MissingSchemaAction.Add:
                case MissingSchemaAction.Ignore:
                case MissingSchemaAction.Error:
                case MissingSchemaAction.AddWithKey:
                    Debug.Assert(false, "valid MissingSchemaAction " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(MissingSchemaAction), (int)value);
        }

        // IDataParameter.Direction
        internal static ArgumentOutOfRangeException InvalidParameterDirection(ParameterDirection value)
        {
#if DEBUG
            switch (value)
            {
                case ParameterDirection.Input:
                case ParameterDirection.Output:
                case ParameterDirection.InputOutput:
                case ParameterDirection.ReturnValue:
                    Debug.Assert(false, "valid ParameterDirection " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(ParameterDirection), (int)value);
        }

        internal static ArgumentOutOfRangeException InvalidPermissionState(PermissionState value)
        {
#if DEBUG
            switch (value)
            {
                case PermissionState.Unrestricted:
                case PermissionState.None:
                    Debug.Assert(false, "valid PermissionState " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(PermissionState), (int)value);
        }

        internal static ArgumentOutOfRangeException InvalidRule(Rule value)
        {
#if DEBUG
            switch (value)
            {
                case Rule.None:
                case Rule.Cascade:
                case Rule.SetNull:
                case Rule.SetDefault:
                    Debug.Assert(false, "valid Rule " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(Rule), (int)value);
        }

        // IDataAdapter.FillSchema
        internal static ArgumentOutOfRangeException InvalidSchemaType(SchemaType value)
        {
#if DEBUG
            switch (value)
            {
                case SchemaType.Source:
                case SchemaType.Mapped:
                    Debug.Assert(false, "valid SchemaType " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(SchemaType), (int)value);
        }

        // RowUpdatingEventArgs.StatementType
        internal static ArgumentOutOfRangeException InvalidStatementType(StatementType value)
        {
#if DEBUG
            switch (value)
            {
                case StatementType.Select:
                case StatementType.Insert:
                case StatementType.Update:
                case StatementType.Delete:
                case StatementType.Batch:
                    Debug.Assert(false, "valid StatementType " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(StatementType), (int)value);
        }

        // RowUpdatingEventArgs.UpdateStatus
        internal static ArgumentOutOfRangeException InvalidUpdateStatus(UpdateStatus value)
        {
#if DEBUG
            switch (value)
            {
                case UpdateStatus.Continue:
                case UpdateStatus.ErrorsOccurred:
                case UpdateStatus.SkipAllRemainingRows:
                case UpdateStatus.SkipCurrentRow:
                    Debug.Assert(false, "valid UpdateStatus " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(UpdateStatus), (int)value);
        }

        internal static ArgumentOutOfRangeException NotSupportedStatementType(StatementType value, string method)
        {
            return NotSupportedEnumerationValue(typeof(StatementType), value.ToString(), method);
        }

        //
        // DbProviderFactories
        //
        internal static ArgumentException ConfigProviderNotFound()
        {
            return Argument(StringsHelper.GetString(Strings.ConfigProviderNotFound));
        }
        internal static InvalidOperationException ConfigProviderInvalid()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ConfigProviderInvalid));
        }
        internal static ConfigurationException ConfigProviderNotInstalled()
        {
            return Configuration(StringsHelper.GetString(Strings.ConfigProviderNotInstalled));
        }
        internal static ConfigurationException ConfigProviderMissing()
        {
            return Configuration(StringsHelper.GetString(Strings.ConfigProviderMissing));
        }

        //
        // DbProviderConfigurationHandler
        //
        internal static ConfigurationException ConfigBaseNoChildNodes(XmlNode node)
        { // Strings.Config_base_no_child_nodes
            return Configuration(StringsHelper.GetString(Strings.ConfigBaseNoChildNodes), node);
        }
        internal static ConfigurationException ConfigBaseElementsOnly(XmlNode node)
        { // Strings.Config_base_elements_only
            return Configuration(StringsHelper.GetString(Strings.ConfigBaseElementsOnly), node);
        }
        internal static ConfigurationException ConfigUnrecognizedAttributes(XmlNode node)
        { // Strings.Config_base_unrecognized_attribute
            return Configuration(StringsHelper.GetString(Strings.ConfigUnrecognizedAttributes, node.Attributes[0].Name), node);
        }
        internal static ConfigurationException ConfigUnrecognizedElement(XmlNode node)
        { // Strings.Config_base_unrecognized_element
            return Configuration(StringsHelper.GetString(Strings.ConfigUnrecognizedElement), node);
        }
        internal static ConfigurationException ConfigSectionsUnique(string sectionName)
        { // Strings.Strings.ConfigSectionsUnique
            return Configuration(StringsHelper.GetString(Strings.ConfigSectionsUnique, sectionName));
        }
        internal static ConfigurationException ConfigRequiredAttributeMissing(string name, XmlNode node)
        { // Strings.Config_base_required_attribute_missing
            return Configuration(StringsHelper.GetString(Strings.ConfigRequiredAttributeMissing, name), node);
        }
        internal static ConfigurationException ConfigRequiredAttributeEmpty(string name, XmlNode node)
        { // Strings.Config_base_required_attribute_empty
            return Configuration(StringsHelper.GetString(Strings.ConfigRequiredAttributeEmpty, name), node);
        }

        //
        // DbConnectionOptions, DataAccess
        //
        /*
        internal static ArgumentException EmptyKeyValue(string keyword) { // MDAC 80715
            return Argument(ResHelper.GetString(Strings.ADP_EmptyKeyValue, keyword));
        }
        */
        internal static ArgumentException UdlFileError(Exception inner)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_UdlFileError), inner);
        }
        internal static ArgumentException InvalidUDL()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidUDL));
        }
        internal static ArgumentException InvalidKeyname(string parameterName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidKey), parameterName);
        }
        internal static ArgumentException InvalidValue(string parameterName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidValue), parameterName);
        }

        internal static InvalidOperationException InvalidMixedUsageOfSecureCredentialAndContextConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfSecureCredentialAndContextConnection));
        }

        internal static ArgumentException InvalidMixedArgumentOfSecureCredentialAndContextConnection()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfSecureCredentialAndContextConnection));
        }

        internal static InvalidOperationException InvalidMixedUsageOfAccessTokenAndContextConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndContextConnection));
        }

        internal static Exception InvalidMixedUsageOfAccessTokenAndCredential()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndCredential));
        }

        //
        // DbConnection
        //
        internal static ConfigurationException ConfigUnableToLoadXmlMetaDataFile(string settingName)
        {
            return Configuration(StringsHelper.GetString(Strings.OleDb_ConfigUnableToLoadXmlMetaDataFile, settingName));
        }

        internal static ConfigurationException ConfigWrongNumberOfValues(string settingName)
        {
            return Configuration(StringsHelper.GetString(Strings.OleDb_ConfigWrongNumberOfValues, settingName));
        }

        //
        // DBDataPermission, DataAccess, Odbc
        //
        internal static Exception InvalidXMLBadVersion()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidXMLBadVersion));
        }
        internal static Exception NotAPermissionElement()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_NotAPermissionElement));
        }
        internal static Exception PermissionTypeMismatch()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_PermissionTypeMismatch));
        }

        internal static Exception WrongType(Type got, Type expected)
        {
            return Argument(StringsHelper.GetString(Strings.SQL_WrongType, got.ToString(), expected.ToString()));
        }

        internal static Exception OdbcNoTypesFromProvider()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_OdbcNoTypesFromProvider));
        }

        //
        // Generic Data Provider Collection
        //

        internal static Exception CollectionUniqueValue(Type itemType, string propertyName, string propertyValue)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_CollectionUniqueValue, itemType.Name, propertyName, propertyValue));
        }

        // IDbDataAdapter.Fill(Schema)
        internal static InvalidOperationException MissingSelectCommand(string method)
        {
            return Provider(StringsHelper.GetString(Strings.ADP_MissingSelectCommand, method));
        }

        //
        // AdapterMappingException
        //
        static private InvalidOperationException DataMapping(string error)
        {
            return InvalidOperation(error);
        }

        // DataColumnMapping.GetDataColumnBySchemaAction
        internal static InvalidOperationException ColumnSchemaExpression(string srcColumn, string cacheColumn)
        {
            return DataMapping(StringsHelper.GetString(Strings.ADP_ColumnSchemaExpression, srcColumn, cacheColumn));
        }

        // DataColumnMapping.GetDataColumnBySchemaAction
        internal static InvalidOperationException ColumnSchemaMismatch(string srcColumn, Type srcType, DataColumn column)
        {
            return DataMapping(StringsHelper.GetString(Strings.ADP_ColumnSchemaMismatch, srcColumn, srcType.Name, column.ColumnName, column.DataType.Name));
        }

        // DataColumnMapping.GetDataColumnBySchemaAction
        internal static InvalidOperationException ColumnSchemaMissing(string cacheColumn, string tableName, string srcColumn)
        {
            if (ADP.IsEmpty(tableName))
            {
                return InvalidOperation(StringsHelper.GetString(Strings.ADP_ColumnSchemaMissing1, cacheColumn, tableName, srcColumn));
            }
            return DataMapping(StringsHelper.GetString(Strings.ADP_ColumnSchemaMissing2, cacheColumn, tableName, srcColumn));
        }

        // DataColumnMappingCollection.GetColumnMappingBySchemaAction
        internal static InvalidOperationException MissingColumnMapping(string srcColumn)
        {
            return DataMapping(StringsHelper.GetString(Strings.ADP_MissingColumnMapping, srcColumn));
        }

        // DataTableMapping.GetDataTableBySchemaAction
        internal static InvalidOperationException MissingTableSchema(string cacheTable, string srcTable)
        {
            return DataMapping(StringsHelper.GetString(Strings.ADP_MissingTableSchema, cacheTable, srcTable));
        }

        // DataTableMappingCollection.GetTableMappingBySchemaAction
        internal static InvalidOperationException MissingTableMapping(string srcTable)
        {
            return DataMapping(StringsHelper.GetString(Strings.ADP_MissingTableMapping, srcTable));
        }

        // DbDataAdapter.Update
        internal static InvalidOperationException MissingTableMappingDestination(string dstTable)
        {
            return DataMapping(StringsHelper.GetString(Strings.ADP_MissingTableMappingDestination, dstTable));
        }

        //
        // DataColumnMappingCollection, DataAccess
        //
        internal static Exception InvalidSourceColumn(string parameter)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidSourceColumn), parameter);
        }
        internal static Exception ColumnsAddNullAttempt(string parameter)
        {
            return CollectionNullValue(parameter, typeof(DataColumnMappingCollection), typeof(DataColumnMapping));
        }
        internal static Exception ColumnsDataSetColumn(string cacheColumn)
        {
            return CollectionIndexString(typeof(DataColumnMapping), ADP.DataSetColumn, cacheColumn, typeof(DataColumnMappingCollection));
        }
        internal static Exception ColumnsIndexInt32(int index, IColumnMappingCollection collection)
        {
            return CollectionIndexInt32(index, collection.GetType(), collection.Count);
        }
        internal static Exception ColumnsIndexSource(string srcColumn)
        {
            return CollectionIndexString(typeof(DataColumnMapping), ADP.SourceColumn, srcColumn, typeof(DataColumnMappingCollection));
        }
        internal static Exception ColumnsIsNotParent(ICollection collection)
        {
            return ParametersIsNotParent(typeof(DataColumnMapping), collection);
        }
        internal static Exception ColumnsIsParent(ICollection collection)
        {
            return ParametersIsParent(typeof(DataColumnMapping), collection);
        }
        internal static Exception ColumnsUniqueSourceColumn(string srcColumn)
        {
            return CollectionUniqueValue(typeof(DataColumnMapping), ADP.SourceColumn, srcColumn);
        }
        internal static Exception NotADataColumnMapping(object value)
        {
            return CollectionInvalidType(typeof(DataColumnMappingCollection), typeof(DataColumnMapping), value);
        }

        //
        // DataTableMappingCollection, DataAccess
        //
        internal static Exception InvalidSourceTable(string parameter)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidSourceTable), parameter);
        }
        internal static Exception TablesAddNullAttempt(string parameter)
        {
            return CollectionNullValue(parameter, typeof(DataTableMappingCollection), typeof(DataTableMapping));
        }
        internal static Exception TablesDataSetTable(string cacheTable)
        {
            return CollectionIndexString(typeof(DataTableMapping), ADP.DataSetTable, cacheTable, typeof(DataTableMappingCollection));
        }
        internal static Exception TablesIndexInt32(int index, ITableMappingCollection collection)
        {
            return CollectionIndexInt32(index, collection.GetType(), collection.Count);
        }
        internal static Exception TablesIsNotParent(ICollection collection)
        {
            return ParametersIsNotParent(typeof(DataTableMapping), collection);
        }
        internal static Exception TablesIsParent(ICollection collection)
        {
            return ParametersIsParent(typeof(DataTableMapping), collection);
        }
        internal static Exception TablesSourceIndex(string srcTable)
        {
            return CollectionIndexString(typeof(DataTableMapping), ADP.SourceTable, srcTable, typeof(DataTableMappingCollection));
        }
        internal static Exception TablesUniqueSourceTable(string srcTable)
        {
            return CollectionUniqueValue(typeof(DataTableMapping), ADP.SourceTable, srcTable);
        }
        internal static Exception NotADataTableMapping(object value)
        {
            return CollectionInvalidType(typeof(DataTableMappingCollection), typeof(DataTableMapping), value);
        }

        //
        // IDbCommand
        //

        internal static InvalidOperationException CommandAsyncOperationCompleted()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.SQL_AsyncOperationCompleted));
        }

        internal static InvalidOperationException UpdateConnectionRequired(StatementType statementType, bool isRowUpdatingCommand)
        {
            string resource;
            if (isRowUpdatingCommand)
            {
                resource = Strings.ADP_ConnectionRequired_Clone;
            }
            else
            {
                switch (statementType)
                {
                    case StatementType.Insert:
                        resource = Strings.ADP_ConnectionRequired_Insert;
                        break;
                    case StatementType.Update:
                        resource = Strings.ADP_ConnectionRequired_Update;
                        break;
                    case StatementType.Delete:
                        resource = Strings.ADP_ConnectionRequired_Delete;
                        break;
                    case StatementType.Batch:
                        resource = Strings.ADP_ConnectionRequired_Batch;
                        goto default;
#if DEBUG
                    case StatementType.Select:
                        Debug.Assert(false, "shouldn't be here");
                        goto default;
#endif
                    default:
                        throw ADP.InvalidStatementType(statementType);
                }
            }
            return InvalidOperation(StringsHelper.GetString(resource));
        }

        internal static InvalidOperationException ConnectionRequired_Res(string method)
        {
            string resource = "ADP_ConnectionRequired_" + method;
#if DEBUG
            switch (resource)
            {
                case StringsHelper.ResourceNames.ADP_ConnectionRequired_Fill:
                case StringsHelper.ResourceNames.ADP_ConnectionRequired_FillPage:
                case StringsHelper.ResourceNames.ADP_ConnectionRequired_FillSchema:
                case StringsHelper.ResourceNames.ADP_ConnectionRequired_Update:
                case StringsHelper.ResourceNames.ADP_ConnecitonRequired_UpdateRows:
                    break;
                default:
                    Debug.Assert(false, "missing resource string: " + resource);
                    break;
            }
#endif
            return InvalidOperation(StringsHelper.GetString(resource));
        }
        internal static InvalidOperationException UpdateOpenConnectionRequired(StatementType statementType, bool isRowUpdatingCommand, ConnectionState state)
        {
            string resource;
            if (isRowUpdatingCommand)
            {
                resource = Strings.ADP_OpenConnectionRequired_Clone;
            }
            else
            {
                switch (statementType)
                {
                    case StatementType.Insert:
                        resource = Strings.ADP_OpenConnectionRequired_Insert;
                        break;
                    case StatementType.Update:
                        resource = Strings.ADP_OpenConnectionRequired_Update;
                        break;
                    case StatementType.Delete:
                        resource = Strings.ADP_OpenConnectionRequired_Delete;
                        break;
#if DEBUG
                    case StatementType.Select:
                        Debug.Assert(false, "shouldn't be here");
                        goto default;
                    case StatementType.Batch:
                        Debug.Assert(false, "isRowUpdatingCommand should have been true");
                        goto default;
#endif
                    default:
                        throw ADP.InvalidStatementType(statementType);
                }
            }
            return InvalidOperation(StringsHelper.GetString(resource, ADP.ConnectionStateMsg(state)));
        }

        internal static Exception TransactionCompleted()
        {
            return DataAdapter(StringsHelper.GetString(Strings.ADP_TransactionCompleted));
        }

        //
        // DbDataReader
        //
        internal static Exception NumericToDecimalOverflow()
        {
            return InvalidCast(StringsHelper.GetString(Strings.ADP_NumericToDecimalOverflow));
        }

        //
        // Stream, SqlTypes, SqlClient
        //

        internal static Exception ExceedsMaxDataLength(long specifiedLength, long maxLength)
        {
            return IndexOutOfRange(StringsHelper.GetString(Strings.SQL_ExceedsMaxDataLength, specifiedLength.ToString(CultureInfo.InvariantCulture), maxLength.ToString(CultureInfo.InvariantCulture)));
        }

        //
        // SqlMetaData, SqlTypes, SqlClient
        //
        internal static Exception InvalidImplicitConversion(Type fromtype, string totype)
        {
            return InvalidCast(StringsHelper.GetString(Strings.ADP_InvalidImplicitConversion, fromtype.Name, totype));
        }

        internal static Exception NotRowType()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_NotRowType));
        }

        //
        // DbDataAdapter
        //
        internal static ArgumentException UnwantedStatementType(StatementType statementType)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_UnwantedStatementType, statementType.ToString()));
        }
        //
        // DbDataAdapter.FillSchema
        //
        internal static Exception FillSchemaRequiresSourceTableName(string parameter)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_FillSchemaRequiresSourceTableName), parameter);
        }

        //
        // DbDataAdapter.Fill
        //
        internal static Exception InvalidMaxRecords(string parameter, int max)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidMaxRecords, max.ToString(CultureInfo.InvariantCulture)), parameter);
        }
        internal static Exception InvalidStartRecord(string parameter, int start)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidStartRecord, start.ToString(CultureInfo.InvariantCulture)), parameter);
        }
        internal static Exception FillRequires(string parameter)
        {
            return ArgumentNull(parameter);
        }
        internal static Exception FillRequiresSourceTableName(string parameter)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_FillRequiresSourceTableName), parameter);
        }
        internal static Exception FillChapterAutoIncrement()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_FillChapterAutoIncrement));
        }
        internal static InvalidOperationException MissingDataReaderFieldType(int index)
        {
            return DataAdapter(StringsHelper.GetString(Strings.ADP_MissingDataReaderFieldType, index));
        }
        internal static InvalidOperationException OnlyOneTableForStartRecordOrMaxRecords()
        {
            return DataAdapter(StringsHelper.GetString(Strings.ADP_OnlyOneTableForStartRecordOrMaxRecords));
        }
        //
        // DbDataAdapter.Update
        //
        internal static ArgumentNullException UpdateRequiresNonNullDataSet(string parameter)
        {
            return ArgumentNull(parameter);
        }
        internal static InvalidOperationException UpdateRequiresSourceTable(string defaultSrcTableName)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_UpdateRequiresSourceTable, defaultSrcTableName));
        }
        internal static InvalidOperationException UpdateRequiresSourceTableName(string srcTable)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_UpdateRequiresSourceTableName, srcTable)); // MDAC 70448
        }
        internal static ArgumentNullException UpdateRequiresDataTable(string parameter)
        {
            return ArgumentNull(parameter);
        }

        internal static Exception UpdateConcurrencyViolation(StatementType statementType, int affected, int expected, DataRow[] dataRows)
        {
            string resource;
            switch (statementType)
            {
                case StatementType.Update:
                    resource = Strings.ADP_UpdateConcurrencyViolation_Update;
                    break;
                case StatementType.Delete:
                    resource = Strings.ADP_UpdateConcurrencyViolation_Delete;
                    break;
                case StatementType.Batch:
                    resource = Strings.ADP_UpdateConcurrencyViolation_Batch;
                    break;
#if DEBUG
                case StatementType.Select:
                case StatementType.Insert:
                    Debug.Assert(false, "should be here");
                    goto default;
#endif
                default:
                    throw ADP.InvalidStatementType(statementType);
            }
            DBConcurrencyException exception = new(StringsHelper.GetString(resource, affected.ToString(CultureInfo.InvariantCulture), expected.ToString(CultureInfo.InvariantCulture)), null, dataRows);
            TraceExceptionAsReturnValue(exception);
            return exception;
        }

        internal static InvalidOperationException UpdateRequiresCommand(StatementType statementType, bool isRowUpdatingCommand)
        {
            string resource;
            if (isRowUpdatingCommand)
            {
                resource = Strings.ADP_UpdateRequiresCommandClone;
            }
            else
            {
                switch (statementType)
                {
                    case StatementType.Select:
                        resource = Strings.ADP_UpdateRequiresCommandSelect;
                        break;
                    case StatementType.Insert:
                        resource = Strings.ADP_UpdateRequiresCommandInsert;
                        break;
                    case StatementType.Update:
                        resource = Strings.ADP_UpdateRequiresCommandUpdate;
                        break;
                    case StatementType.Delete:
                        resource = Strings.ADP_UpdateRequiresCommandDelete;
                        break;
#if DEBUG
                    case StatementType.Batch:
                        Debug.Assert(false, "isRowUpdatingCommand should have been true");
                        goto default;
#endif
                    default:
                        throw ADP.InvalidStatementType(statementType);
                }
            }
            return InvalidOperation(StringsHelper.GetString(resource));
        }
        internal static ArgumentException UpdateMismatchRowTable(int i)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_UpdateMismatchRowTable, i.ToString(CultureInfo.InvariantCulture)));
        }
        internal static DataException RowUpdatedErrors()
        {
            return Data(StringsHelper.GetString(Strings.ADP_RowUpdatedErrors));
        }
        internal static DataException RowUpdatingErrors()
        {
            return Data(StringsHelper.GetString(Strings.ADP_RowUpdatingErrors));
        }
        internal static InvalidOperationException ResultsNotAllowedDuringBatch()
        {
            return DataAdapter(StringsHelper.GetString(Strings.ADP_ResultsNotAllowedDuringBatch));
        }

        //
        // : IDbCommand
        //
        internal static Exception InvalidCommandTimeout(int value)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidCommandTimeout, value.ToString(CultureInfo.InvariantCulture)), ADP.CommandTimeout);
        }

        //
        // : ConnectionUtil
        //
        internal static Exception ConnectionIsDisabled(Exception InnerException)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_ConnectionIsDisabled), InnerException);
        }

        internal static Exception DelegatedTransactionPresent()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DelegatedTransactionPresent));
        }

        internal static Exception DatabaseNameTooLong()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_DatabaseNameTooLong));
        }

        internal static Exception InternalError(InternalErrorCode internalError, Exception innerException)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_InternalProviderError, (int)internalError), innerException);
        }
        internal static Exception InvalidConnectTimeoutValue()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidConnectTimeoutValue));
        }

        //
        // : DbDataReader
        //
        internal static Exception DataReaderNoData()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DataReaderNoData));
        }

        //
        // : DbDataAdapter
        //
        internal static InvalidOperationException DynamicSQLJoinUnsupported()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DynamicSQLJoinUnsupported));
        }
        internal static InvalidOperationException DynamicSQLNoTableInfo()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DynamicSQLNoTableInfo));
        }
        internal static InvalidOperationException DynamicSQLNoKeyInfoDelete()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DynamicSQLNoKeyInfoDelete));
        }
        internal static InvalidOperationException DynamicSQLNoKeyInfoUpdate()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DynamicSQLNoKeyInfoUpdate));
        }
        internal static InvalidOperationException DynamicSQLNoKeyInfoRowVersionDelete()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DynamicSQLNoKeyInfoRowVersionDelete));
        }
        internal static InvalidOperationException DynamicSQLNoKeyInfoRowVersionUpdate()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DynamicSQLNoKeyInfoRowVersionUpdate));
        }
        internal static InvalidOperationException DynamicSQLNestedQuote(string name, string quote)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DynamicSQLNestedQuote, name, quote));
        }
        internal static InvalidOperationException NoQuoteChange()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_NoQuoteChange));
        }
        internal static InvalidOperationException ComputerNameEx(int lastError)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_ComputerNameEx, lastError));
        }
        internal static InvalidOperationException MissingSourceCommand()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_MissingSourceCommand));
        }
        internal static InvalidOperationException MissingSourceCommandConnection()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_MissingSourceCommandConnection));
        }

        //
        // : IDbTransaction
        //
        internal static Exception DbRecordReadOnly(string methodname)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_DbRecordReadOnly, methodname));
        }

        internal static Exception OffsetOutOfRangeException()
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_OffsetOutOfRangeException));
        }

        //
        // : DbMetaDataFactory
        //
        internal static ArgumentException InvalidRestrictionValue(string collectionName, string restrictionName, string restrictionValue)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.MDF_InvalidRestrictionValue, collectionName, restrictionName, restrictionValue));
        }

        //
        // : CommandBuilder
        //

        internal static InvalidOperationException InvalidDateTimeDigits(string dataTypeName)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidDateTimeDigits, dataTypeName));
        }

        internal static Exception InvalidFormatValue()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidFormatValue));
        }

        internal static InvalidOperationException InvalidMaximumScale(string dataTypeName)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMaximumScale, dataTypeName));
        }

        internal static Exception LiteralValueIsInvalid(string dataTypeName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_LiteralValueIsInvalid, dataTypeName));
        }

        internal static Exception EvenLengthLiteralValue(string argumentName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_EvenLengthLiteralValue), argumentName);
        }

        internal static Exception HexDigitLiteralValue(string argumentName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_HexDigitLiteralValue), argumentName);
        }

        internal static InvalidOperationException QuotePrefixNotSet(string method)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_QuotePrefixNotSet, method));
        }

        internal static InvalidOperationException UnableToCreateBooleanLiteral()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_UnableToCreateBooleanLiteral));
        }

        internal static Exception UnsupportedNativeDataTypeOleDb(string dataTypeName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_UnsupportedNativeDataTypeOleDb, dataTypeName));
        }

        // Sql Result Set and other generic message
        internal static Exception InvalidArgumentValue(string methodName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidArgumentValue, methodName));
        }

        // global constant strings
        internal const string Append = "Append";
        internal const string BeginExecuteNonQuery = "BeginExecuteNonQuery";
        internal const string BeginExecuteReader = "BeginExecuteReader";
        internal const string BeginTransaction = "BeginTransaction";
        internal const string BeginExecuteXmlReader = "BeginExecuteXmlReader";
        internal const string ChangeDatabase = "ChangeDatabase";
        internal const string Cancel = "Cancel";
        internal const string Clone = "Clone";
        internal const string CommitTransaction = "CommitTransaction";
        internal const string CommandTimeout = "CommandTimeout";
        internal const string ConnectionString = "ConnectionString";
        internal const string DataSetColumn = "DataSetColumn";
        internal const string DataSetTable = "DataSetTable";
        internal const string Delete = "Delete";
        internal const string DeleteCommand = "DeleteCommand";
        internal const string DeriveParameters = "DeriveParameters";
        internal const string EndExecuteNonQuery = "EndExecuteNonQuery";
        internal const string EndExecuteReader = "EndExecuteReader";
        internal const string EndExecuteXmlReader = "EndExecuteXmlReader";
        internal const string ExecuteReader = "ExecuteReader";
        internal const string ExecuteRow = "ExecuteRow";
        internal const string ExecuteNonQuery = "ExecuteNonQuery";
        internal const string ExecuteScalar = "ExecuteScalar";
        internal const string ExecuteSqlScalar = "ExecuteSqlScalar";
        internal const string ExecuteXmlReader = "ExecuteXmlReader";
        internal const string Fill = "Fill";
        internal const string FillPage = "FillPage";
        internal const string FillSchema = "FillSchema";
        internal const string GetBytes = "GetBytes";
        internal const string GetChars = "GetChars";
        internal const string GetOleDbSchemaTable = "GetOleDbSchemaTable";
        internal const string GetProperties = "GetProperties";
        internal const string GetSchema = "GetSchema";
        internal const string GetSchemaTable = "GetSchemaTable";
        internal const string GetServerTransactionLevel = "GetServerTransactionLevel";
        internal const string Insert = "Insert";
        internal const string Open = "Open";
        internal const string ParameterBuffer = "buffer";
        internal const string ParameterCount = "count";
        internal const string ParameterDestinationType = "destinationType";
        internal const string ParameterIndex = "index";
        internal const string ParameterOffset = "offset";
        internal const string ParameterService = "Service";
        internal const string ParameterTimeout = "Timeout";
        internal const string ParameterUserData = "UserData";
        internal const string Prepare = "Prepare";
        internal const string QuoteIdentifier = "QuoteIdentifier";
        internal const string Read = "Read";
        internal const string ReadAsync = "ReadAsync";
        internal const string Remove = "Remove";
        internal const string RollbackTransaction = "RollbackTransaction";
        internal const string SaveTransaction = "SaveTransaction";
        internal const string SetProperties = "SetProperties";
        internal const string SourceColumn = "SourceColumn";
        internal const string SourceVersion = "SourceVersion";
        internal const string SourceTable = "SourceTable";
        internal const string UnquoteIdentifier = "UnquoteIdentifier";
        internal const string Update = "Update";
        internal const string UpdateCommand = "UpdateCommand";
        internal const string UpdateRows = "UpdateRows";

        internal const int DecimalMaxPrecision = 29;
        internal const int DecimalMaxPrecision28 = 28;  // there are some cases in Odbc where we need that ...
        internal const float FailoverTimeoutStepForTnir = 0.125F; // Fraction of timeout to use in case of Transparent Network IP resolution.
        internal const int MinimumTimeoutForTnirMs = 500; // The first login attempt in  Transparent network IP Resolution 

        internal static readonly IntPtr s_ptrZero = new(0); // IntPtr.Zero
        internal static readonly int s_ptrSize = IntPtr.Size;
        internal static readonly IntPtr s_invalidPtr = new(-1); // use for INVALID_HANDLE
        internal static readonly IntPtr s_recordsUnaffected = new(-1);

        internal static readonly HandleRef s_nullHandleRef = new(null, IntPtr.Zero);

        internal static readonly bool s_isWindowsNT = (PlatformID.Win32NT == Environment.OSVersion.Platform);
        internal static readonly bool s_isPlatformNT5 = (ADP.s_isWindowsNT && (Environment.OSVersion.Version.Major >= 5));

        internal static SysTx.IDtcTransaction GetOletxTransaction(SysTx.Transaction transaction)
        {
            SysTx.IDtcTransaction oleTxTransaction = null;

            if (null != transaction)
            {
                oleTxTransaction = SysTx.TransactionInterop.GetDtcTransaction(transaction);
            }
            return oleTxTransaction;
        }

        [System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static bool IsSysTxEqualSysEsTransaction()
        {
            // This Method won't JIT inproc (ES isn't available), so we code it
            // separately and call it behind an if statement.
            bool result = (!SysES.ContextUtil.IsInTransaction && null == SysTx.Transaction.Current)
                       || (SysES.ContextUtil.IsInTransaction && SysTx.Transaction.Current == (SysTx.Transaction)SysES.ContextUtil.SystemTransaction);
            return result;
        }

        internal static bool NeedManualEnlistment()
        {
            // We need to force a manual enlistment of transactions for ODBC and
            // OLEDB whenever the current SysTx transaction != the SysTx transaction
            // on the EnterpriseServices ContextUtil, or when ES.ContextUtil is
            // not available and there is a non-null current SysTx transaction.
            if (s_isWindowsNT)
            { // we can reference SysTx just not use it on Win9X, we can't ever reference SysES on Win9X
                bool isEnterpriseServicesOK = !InOutOfProcHelper.InProc;
                if ((isEnterpriseServicesOK && !IsSysTxEqualSysEsTransaction())
                 || (!isEnterpriseServicesOK && null != SysTx.Transaction.Current))
                {
                    return true;
                }
            }
            return false;
        }

        private const string HexDigits = "0123456789abcdef";

        internal static byte[] ByteArrayFromString(string hexString, string dataTypeName)
        {
            if ((hexString.Length & 0x1) != 0)
            {
                throw ADP.LiteralValueIsInvalid(dataTypeName);
            }
            char[] c = hexString.ToCharArray();
            byte[] b = new byte[hexString.Length / 2];

            CultureInfo invariant = CultureInfo.InvariantCulture;
            for (int i = 0; i < hexString.Length; i += 2)
            {
                int h = HexDigits.IndexOf(char.ToLower(c[i], invariant));
                int l = HexDigits.IndexOf(char.ToLower(c[i + 1], invariant));

                if (h < 0 || l < 0)
                {
                    throw ADP.LiteralValueIsInvalid(dataTypeName);
                }
                b[i / 2] = (byte)((h << 4) | l);
            }
            return b;
        }

        internal static void EscapeSpecialCharacters(string unescapedString, StringBuilder escapedString)
        {

            // note special characters list is from character escapes
            // in the MSDN regular expression language elements documentation
            // added ] since escaping it seems necessary
            const string specialCharacters = ".$^{[(|)*+?\\]";

            foreach (char currentChar in unescapedString)
            {
                if (specialCharacters.IndexOf(currentChar) >= 0)
                {
                    escapedString.Append("\\");
                }
                escapedString.Append(currentChar);
            }
            return;
        }




        internal static string FixUpDecimalSeparator(string numericString,
                                                     bool formatLiteral,
                                                     string decimalSeparator,
                                                     char[] exponentSymbols)
        {
            string returnString;
            // don't replace the decimal separator if the string is in exponent format
            if (numericString.IndexOfAny(exponentSymbols) == -1)
            {

                // if the user has set a decimal separator use it, if not use the current culture's value
                if (ADP.IsEmpty(decimalSeparator) == true)
                {
                    decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                }
                if (formatLiteral == true)
                {
                    returnString = numericString.Replace(".", decimalSeparator);
                }
                else
                {
                    returnString = numericString.Replace(decimalSeparator, ".");
                }
            }
            else
            {
                returnString = numericString;
            }
            return returnString;
        }

        [FileIOPermission(SecurityAction.Assert, AllFiles = FileIOPermissionAccess.PathDiscovery)]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static string GetFullPath(string filename)
        { // MDAC 77686
            return Path.GetFullPath(filename);
        }

        // TODO: cache machine name and listen to longhorn event to reset it
        internal static string GetComputerNameDnsFullyQualified()
        {
            const int ComputerNameDnsFullyQualified = 3; // winbase.h, enum COMPUTER_NAME_FORMAT
            const int ERROR_MORE_DATA = 234; // winerror.h

            string value;
            if (s_isPlatformNT5)
            {
                int length = 0; // length parameter must be zero if buffer is null
                // query for the required length
                // VSTFDEVDIV 479551 - ensure that GetComputerNameEx does not fail with unexpected values and that the length is positive
                int getComputerNameExError = 0;
                if (0 == SafeNativeMethods.GetComputerNameEx(ComputerNameDnsFullyQualified, null, ref length))
                {
                    getComputerNameExError = Marshal.GetLastWin32Error();
                }
                if ((getComputerNameExError != 0 && getComputerNameExError != ERROR_MORE_DATA) || length <= 0)
                {
                    throw ADP.ComputerNameEx(getComputerNameExError);
                }

                StringBuilder buffer = new(length);
                length = buffer.Capacity;
                if (0 == SafeNativeMethods.GetComputerNameEx(ComputerNameDnsFullyQualified, buffer, ref length))
                {
                    throw ADP.ComputerNameEx(Marshal.GetLastWin32Error());
                }

                // Note: In Longhorn you'll be able to rename a machine without
                // rebooting.  Therefore, don't cache this machine name.
                value = buffer.ToString();
            }
            else
            {
                value = ADP.MachineName();
            }
            return value;
        }


        // SxS: the file is opened in FileShare.Read mode allowing several threads/apps to read it simultaneously
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static Stream GetFileStream(string filename)
        {
            (new FileIOPermission(FileIOPermissionAccess.Read, filename)).Assert();
            try
            {
                return new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            finally
            {
                FileIOPermission.RevertAssert();
            }
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static FileVersionInfo GetVersionInfo(string filename)
        {
            (new FileIOPermission(FileIOPermissionAccess.Read, filename)).Assert(); // MDAC 62038
            try
            {
                return FileVersionInfo.GetVersionInfo(filename); // MDAC 60411
            }
            finally
            {
                FileIOPermission.RevertAssert();
            }
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static Stream GetXmlStreamFromValues(string[] values, string errorString)
        {
            if (values.Length != 1)
            {
                throw ADP.ConfigWrongNumberOfValues(errorString);
            }
            return ADP.GetXmlStream(values[0], errorString);
        }

        // SxS (VSDD 545786): metadata files are opened from <.NetRuntimeFolder>\CONFIG\<metadatafilename.xml>
        // this operation is safe in SxS because the file is opened in read-only mode and each NDP runtime accesses its own copy of the metadata
        // under the runtime folder.
        // This method returns stream to open file, so its ResourceExposure value is ResourceScope.Machine.
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static Stream GetXmlStream(string value, string errorString)
        {
            Stream XmlStream;
            const string config = "config\\";
            // get location of config directory
            string rootPath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            if (rootPath == null)
            {
                throw ADP.ConfigUnableToLoadXmlMetaDataFile(errorString);
            }
            StringBuilder tempstring = new(rootPath.Length + config.Length + value.Length);
            tempstring.Append(rootPath);
            tempstring.Append(config);
            tempstring.Append(value);
            string fullPath = tempstring.ToString();

            // don't allow relative paths
            if (ADP.GetFullPath(fullPath) != fullPath)
            {
                throw ADP.ConfigUnableToLoadXmlMetaDataFile(errorString);
            }

            try
            {
                XmlStream = ADP.GetFileStream(fullPath);
            }
            catch (Exception e)
            {
                // UNDONE - should not be catching all exceptions!!!
                if (!ADP.IsCatchableExceptionType(e))
                {
                    throw;
                }
                throw ADP.ConfigUnableToLoadXmlMetaDataFile(errorString);
            }

            return XmlStream;

        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static object ClassesRootRegistryValue(string subkey, string queryvalue)
        { // MDAC 77697
            (new RegistryPermission(RegistryPermissionAccess.Read, "HKEY_CLASSES_ROOT\\" + subkey)).Assert(); // MDAC 62028
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(subkey, false))
                {
                    return key?.GetValue(queryvalue);
                }
            }
            catch (SecurityException e)
            {
                // Even though we assert permission - it's possible there are
                // ACL's on registry that cause SecurityException to be thrown.
                ADP.TraceExceptionWithoutRethrow(e);
                return null;
            }
            finally
            {
                RegistryPermission.RevertAssert();
            }
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static object LocalMachineRegistryValue(string subkey, string queryvalue)
        { // MDAC 77697
            (new RegistryPermission(RegistryPermissionAccess.Read, "HKEY_LOCAL_MACHINE\\" + subkey)).Assert(); // MDAC 62028
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subkey, false))
                {
                    return key?.GetValue(queryvalue);
                }
            }
            catch (SecurityException e)
            {
                // Even though we assert permission - it's possible there are
                // ACL's on registry that cause SecurityException to be thrown.
                ADP.TraceExceptionWithoutRethrow(e);
                return null;
            }
            finally
            {
                RegistryPermission.RevertAssert();
            }
        }

        //// SxS: although this method uses registry, it does not expose anything out
        //[ResourceExposure(ResourceScope.None)]
        //[ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        //internal static void CheckVersionMDAC(bool ifodbcelseoledb) {
        //    int major, minor, build;
        //    string version;

        //    try {
        //        version = (string)ADP.LocalMachineRegistryValue("Software\\Microsoft\\DataAccess", "FullInstallVer");
        //        if (ADP.IsEmpty(version)) {
        //            string filename = (string)ADP.ClassesRootRegistryValue(Microsoft.Data.OleDb.ODB.DataLinks_CLSID, ADP.StrEmpty);
        //            FileVersionInfo versionInfo = ADP.GetVersionInfo(filename); // MDAC 60411
        //            major = versionInfo.FileMajorPart;
        //            minor = versionInfo.FileMinorPart;
        //            build = versionInfo.FileBuildPart;
        //            version = versionInfo.FileVersion;
        //        }
        //        else {
        //            string[] parts = version.Split('.');
        //            major = int.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture);
        //            minor = int.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture);
        //            build = int.Parse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture);
        //            int.Parse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture);
        //        }
        //    }
        //    catch(Exception e) {
        //        // UNDONE - should not be catching all exceptions!!!
        //        if (!ADP.IsCatchableExceptionType(e)) {
        //            throw;
        //        }

        //        throw Microsoft.Data.OleDb.ODB.MDACNotAvailable(e);
        //    }

        //    // disallow any MDAC version before MDAC 2.6 rtm
        //    // include MDAC 2.51 that ships with Win2k
        //    if ((major < 2) || ((major == 2) && ((minor < 60) || ((minor == 60) && (build < 6526))))) { // MDAC 66628
        //        if (ifodbcelseoledb) {
        //            throw ADP.DataAdapter(ResHelper.GetString(Strings.Odbc_MDACWrongVersion, version));
        //        }
        //        else {
        //            throw ADP.DataAdapter(ResHelper.GetString(Strings.OleDb_MDACWrongVersion, version));
        //        }
        //    }
        //}

        internal static DataRow[] SelectAdapterRows(DataTable dataTable, bool sorted)
        {
            const DataRowState rowStates = DataRowState.Added | DataRowState.Deleted | DataRowState.Modified;

            // equivalent to but faster than 'return dataTable.Select("", "", rowStates);'
            int countAdded = 0, countDeleted = 0, countModifed = 0;
            DataRowCollection rowCollection = dataTable.Rows;
            foreach (DataRow dataRow in rowCollection)
            {
                switch (dataRow.RowState)
                {
                    case DataRowState.Added:
                        countAdded++;
                        break;
                    case DataRowState.Deleted:
                        countDeleted++;
                        break;
                    case DataRowState.Modified:
                        countModifed++;
                        break;
                    default:
                        Debug.Assert(0 == (rowStates & dataRow.RowState), "flagged RowState");
                        break;
                }
            }
            DataRow[] dataRows = new DataRow[countAdded + countDeleted + countModifed];
            if (sorted)
            {
                countModifed = countAdded + countDeleted;
                countDeleted = countAdded;
                countAdded = 0;

                foreach (DataRow dataRow in rowCollection)
                {
                    switch (dataRow.RowState)
                    {
                        case DataRowState.Added:
                            dataRows[countAdded++] = dataRow;
                            break;
                        case DataRowState.Deleted:
                            dataRows[countDeleted++] = dataRow;
                            break;
                        case DataRowState.Modified:
                            dataRows[countModifed++] = dataRow;
                            break;
                        default:
                            Debug.Assert(0 == (rowStates & dataRow.RowState), "flagged RowState");
                            break;
                    }
                }
            }
            else
            {
                int index = 0;
                foreach (DataRow dataRow in rowCollection)
                {
                    if (0 != (dataRow.RowState & rowStates))
                    {
                        dataRows[index++] = dataRow;
                        if (index == dataRows.Length)
                        {
                            break;
                        }
                    }
                }
            }
            return dataRows;
        }

        internal static int StringLength(string inputString)
        {
            return ((null != inputString) ? inputString.Length : 0);
        }

        // { "a", "a", "a" } -> { "a", "a1", "a2" }
        // { "a", "a", "a1" } -> { "a", "a2", "a1" }
        // { "a", "A", "a" } -> { "a", "A1", "a2" }
        // { "a", "A", "a1" } -> { "a", "A2", "a1" } // MDAC 66718
        internal static void BuildSchemaTableInfoTableNames(string[] columnNameArray)
        {
            Dictionary<string, int> hash = new(columnNameArray.Length);

            int startIndex = columnNameArray.Length; // lowest non-unique index
            for (int i = columnNameArray.Length - 1; 0 <= i; --i)
            {
                string columnName = columnNameArray[i];
                if ((null != columnName) && (0 < columnName.Length))
                {
                    columnName = columnName.ToLower(CultureInfo.InvariantCulture);
                    if (hash.TryGetValue(columnName, out int index))
                    {
                        startIndex = Math.Min(startIndex, index);
                    }
                    hash[columnName] = i;
                }
                else
                {
                    columnNameArray[i] = ADP.s_strEmpty; // MDAC 66681
                    startIndex = i;
                }
            }
            int uniqueIndex = 1;
            for (int i = startIndex; i < columnNameArray.Length; ++i)
            {
                string columnName = columnNameArray[i];
                if (0 == columnName.Length)
                { // generate a unique name
                    columnNameArray[i] = "Column";
                    uniqueIndex = GenerateUniqueName(hash, ref columnNameArray[i], i, uniqueIndex);
                }
                else
                {
                    columnName = columnName.ToLower(CultureInfo.InvariantCulture);
                    if (i != hash[columnName])
                    {
                        GenerateUniqueName(hash, ref columnNameArray[i], i, 1); // MDAC 66718
                    }
                }
            }
        }

        static private int GenerateUniqueName(Dictionary<string, int> hash, ref string columnName, int index, int uniqueIndex)
        {
            for (; ; ++uniqueIndex)
            {
                string uniqueName = columnName + uniqueIndex.ToString(CultureInfo.InvariantCulture);
                string lowerName = uniqueName.ToLower(CultureInfo.InvariantCulture); // MDAC 66978
                if (!hash.ContainsKey(lowerName))
                {

                    columnName = uniqueName;
                    hash.Add(lowerName, index);
                    break;
                }
            }
            return uniqueIndex;
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static IntPtr IntPtrOffset(IntPtr pbase, int offset)
        {
            if (4 == ADP.s_ptrSize)
            {
                return (IntPtr)checked(pbase.ToInt32() + offset);
            }
            Debug.Assert(8 == ADP.s_ptrSize, "8 != IntPtr.Size"); // MDAC 73747
            return (IntPtr)checked(pbase.ToInt64() + offset);
        }

        internal static int IntPtrToInt32(IntPtr value)
        {
            if (4 == ADP.s_ptrSize)
            {
                return (int)value;
            }
            else
            {
                long lval = (long)value;
                lval = Math.Min((long)int.MaxValue, lval);
                lval = Math.Max((long)int.MinValue, lval);
                return (int)lval;
            }
        }

        // TODO: are those names appropriate for common code?
        internal static int SrcCompare(string strA, string strB)
        { // this is null safe
            return ((strA == strB) ? 0 : 1);
        }

        internal static bool IsEmpty(string str) => string.IsNullOrEmpty(str);
    }
}
