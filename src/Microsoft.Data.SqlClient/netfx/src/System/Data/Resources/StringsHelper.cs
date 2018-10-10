//------------------------------------------------------------------------------
// <copyright file="ResHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace Microsoft.Data
{
    internal class StringsHelper : Strings
    {
        internal class ResourceNames
        {
            internal const string ADP_ConnectionRequired_Fill = "ADP_ConnectionRequired_Fill";
            internal const string ADP_ConnectionRequired_FillPage = "ADP_ConnectionRequired_FillPage";
            internal const string ADP_ConnectionRequired_FillSchema = "ADP_ConnectionRequired_FillSchema";
            internal const string ADP_ConnectionRequired_Update = "ADP_ConnectionRequired_Update";
            internal const string ADP_ConnecitonRequired_UpdateRows = "ADP_ConnecitonRequired_UpdateRows";
            internal const string collectionChangedEventDescr = "collectionChangedEventDescr";
            internal const string ConstraintNameDescr = "ConstraintNameDescr";
            internal const string ConstraintTableDescr = "ConstraintTableDescr";
            internal const string DataAdapter_AcceptChangesDuringFill = "DataAdapter_AcceptChangesDuringFill";
            internal const string DataAdapter_AcceptChangesDuringUpdate = "DataAdapter_AcceptChangesDuringUpdate";
            internal const string DataAdapter_ContinueUpdateOnError = "DataAdapter_ContinueUpdateOnError";
            internal const string DataAdapter_FillError = "DataAdapter_FillError";
            internal const string DataAdapter_FillLoadOption = "DataAdapter_FillLoadOption";
            internal const string DataAdapter_MissingMappingAction = "DataAdapter_MissingMappingAction";
            internal const string DataAdapter_MissingSchemaAction = "DataAdapter_MissingSchemaAction";
            internal const string DataAdapter_ReturnProviderSpecificTypes = "DataAdapter_ReturnProviderSpecificTypes";
            internal const string DataAdapter_TableMappings = "DataAdapter_TableMappings";
            internal const string DataCategory_Action = "DataCategory_Action";
            internal const string DataCategory_Advanced = "DataCategory_Advanced";
            internal const string DataCategory_Behavior = "DataCategory_Behavior";
            internal const string DataCategory_ConnectionResilency = "DataCategory_ConnectionResilency";
            internal const string DataCategory_Context = "DataCategory_Context";
            internal const string DataCategory_Data = "DataCategory_Data";
            internal const string DataCategory_Fill = "DataCategory_Fill";
            internal const string DataCategory_InfoMessage = "DataCategory_InfoMessage";
            internal const string DataCategory_Initialization = "DataCategory_Initialization";
            internal const string DataCategory_Mapping = "DataCategory_Mapping";
            internal const string DataCategory_NamedConnectionString = "DataCategory_NamedConnectionString";
            internal const string DataCategory_Notification = "DataCategory_Notification";
            internal const string DataCategory_Pooling = "DataCategory_Pooling";
            internal const string DataCategory_Replication = "DataCategory_Replication";
            internal const string DataCategory_Schema = "DataCategory_Schema";
            internal const string DataCategory_Security = "DataCategory_Security";
            internal const string DataCategory_Source = "DataCategory_Source";
            internal const string DataCategory_StateChange = "DataCategory_StateChange";
            internal const string DataCategory_StatementCompleted = "DataCategory_StatementCompleted";
            internal const string DataCategory_Update = "DataCategory_Update";
            internal const string DataCategory_Xml = "DataCategory_Xml";
            internal const string DataColumnAllowNullDescr = "DataColumnAllowNullDescr";
            internal const string DataColumnAutoIncrementDescr = "DataColumnAutoIncrementDescr";
            internal const string DataColumnAutoIncrementSeedDescr = "DataColumnAutoIncrementSeedDescr";
            internal const string DataColumnAutoIncrementStepDescr = "DataColumnAutoIncrementStepDescr";
            internal const string DataColumnCaptionDescr = "DataColumnCaptionDescr";
            internal const string DataColumnColumnNameDescr = "DataColumnColumnNameDescr";
            internal const string DataColumnDataTableDescr = "DataColumnDataTableDescr";
            internal const string DataColumnDataTypeDescr = "DataColumnDataTypeDescr";
            internal const string DataColumnDateTimeModeDescr = "DataColumnDateTimeModeDescr";
            internal const string DataColumnDefaultValueDescr = "DataColumnDefaultValueDescr";
            internal const string DataColumnExpressionDescr = "DataColumnExpressionDescr";
            internal const string DataColumnMapping_DataSetColumn = "DataColumnMapping_DataSetColumn";
            internal const string DataColumnMapping_SourceColumn = "DataColumnMapping_SourceColumn";
            internal const string DataColumnMappingDescr = "DataColumnMappingDescr";
            internal const string DataColumnMappings_Count = "DataColumnMappings_Count";
            internal const string DataColumnMappings_Item = "DataColumnMappings_Item";
            internal const string DataColumnMaxLengthDescr = "DataColumnMaxLengthDescr";
            internal const string DataColumnNamespaceDescr = "DataColumnNamespaceDescr";
            internal const string DataColumnOrdinalDescr = "DataColumnOrdinalDescr";
            internal const string DataColumnPrefixDescr = "DataColumnPrefixDescr";
            internal const string DataColumnReadOnlyDescr = "DataColumnReadOnlyDescr";
            internal const string DataColumnUniqueDescr = "DataColumnUniqueDescr";
            internal const string DataRelationChildColumnsDescr = "DataRelationChildColumnsDescr";
            internal const string DataRelationNested = "DataRelationNested";
            internal const string DataRelationParentColumnsDescr = "DataRelationParentColumnsDescr";
            internal const string DataRelationRelationNameDescr = "DataRelationRelationNameDescr";
            internal const string DataSetCaseSensitiveDescr = "DataSetCaseSensitiveDescr";
            internal const string DataSetDataSetNameDescr = "DataSetDataSetNameDescr";
            internal const string DataSetDefaultViewDescr = "DataSetDefaultViewDescr";
            internal const string DataSetDescr = "DataSetDescr";
            internal const string DataSetEnforceConstraintsDescr = "DataSetEnforceConstraintsDescr";
            internal const string DataSetHasErrorsDescr = "DataSetHasErrorsDescr";
            internal const string DataSetInitializedDescr = "DataSetInitializedDescr";
            internal const string DataSetLocaleDescr = "DataSetLocaleDescr";
            internal const string DataSetMergeFailedDescr = "DataSetMergeFailedDescr";
            internal const string DataSetNamespaceDescr = "DataSetNamespaceDescr";
            internal const string DataSetPrefixDescr = "DataSetPrefixDescr";
            internal const string DataSetRelationsDescr = "DataSetRelationsDescr";
            internal const string DataSetTablesDescr = "DataSetTablesDescr";
            internal const string DataTableCaseSensitiveDescr = "DataTableCaseSensitiveDescr";
            internal const string DataTableChildRelationsDescr = "DataTableChildRelationsDescr";
            internal const string DataTableColumnChangedDescr = "DataTableColumnChangedDescr";
            internal const string DataTableColumnChangingDescr = "DataTableColumnChangingDescr";
            internal const string DataTableColumnsDescr = "DataTableColumnsDescr";
            internal const string DataTableConstraintsDescr = "DataTableConstraintsDescr";
            internal const string DataTableDataSetDescr = "DataTableDataSetDescr";
            internal const string DataTableDefaultViewDescr = "DataTableDefaultViewDescr";
            internal const string DataTableDisplayExpressionDescr = "DataTableDisplayExpressionDescr";
            internal const string DataTableHasErrorsDescr = "DataTableHasErrorsDescr";
            internal const string DataTableLocaleDescr = "DataTableLocaleDescr";
            internal const string DataTableMapping_ColumnMappings = "DataTableMapping_ColumnMappings";
            internal const string DataTableMapping_DataSetTable = "DataTableMapping_DataSetTable";
            internal const string DataTableMapping_SourceTable = "DataTableMapping_SourceTable";
            internal const string DataTableMappings_Count = "DataTableMappings_Count";
            internal const string DataTableMappings_Item = "DataTableMappings_Item";
            internal const string DataTableMinimumCapacityDescr = "DataTableMinimumCapacityDescr";
            internal const string DataTableNamespaceDescr = "DataTableNamespaceDescr";
            internal const string DataTableParentRelationsDescr = "DataTableParentRelationsDescr";
            internal const string DataTablePrefixDescr = "DataTablePrefixDescr";
            internal const string DataTablePrimaryKeyDescr = "DataTablePrimaryKeyDescr";
            internal const string DataTableRowChangedDescr = "DataTableRowChangedDescr";
            internal const string DataTableRowChangingDescr = "DataTableRowChangingDescr";
            internal const string DataTableRowDeletedDescr = "DataTableRowDeletedDescr";
            internal const string DataTableRowDeletingDescr = "DataTableRowDeletingDescr";
            internal const string DataTableRowsClearedDescr = "DataTableRowsClearedDescr";
            internal const string DataTableRowsClearingDescr = "DataTableRowsClearingDescr";
            internal const string DataTableRowsDescr = "DataTableRowsDescr";
            internal const string DataTableRowsNewRowDescr = "DataTableRowsNewRowDescr";
            internal const string DataTableSerializeHierarchy = "DataTableSerializeHierarchy";
            internal const string DataTableTableNameDescr = "DataTableTableNameDescr";
            internal const string DataViewAllowDeleteDescr = "DataViewAllowDeleteDescr";
            internal const string DataViewAllowEditDescr = "DataViewAllowEditDescr";
            internal const string DataViewAllowNewDescr = "DataViewAllowNewDescr";
            internal const string DataViewApplyDefaultSortDescr = "DataViewApplyDefaultSortDescr";
            internal const string DataViewCountDescr = "DataViewCountDescr";
            internal const string DataViewDataViewManagerDescr = "DataViewDataViewManagerDescr";
            internal const string DataViewIsOpenDescr = "DataViewIsOpenDescr";
            internal const string DataViewListChangedDescr = "DataViewListChangedDescr";
            internal const string DataViewManagerDataSetDescr = "DataViewManagerDataSetDescr";
            internal const string DataViewManagerTableSettingsDescr = "DataViewManagerTableSettingsDescr";
            internal const string DataViewRowFilterDescr = "DataViewRowFilterDescr";
            internal const string DataViewRowStateFilterDescr = "DataViewRowStateFilterDescr";
            internal const string DataViewSortDescr = "DataViewSortDescr";
            internal const string DataViewTableDescr = "DataViewTableDescr";
            internal const string DbCommand_CommandText = "DbCommand_CommandText";
            internal const string DbCommand_CommandTimeout = "DbCommand_CommandTimeout";
            internal const string DbCommand_CommandType = "DbCommand_CommandType";
            internal const string DbCommand_Connection = "DbCommand_Connection";
            internal const string DbCommand_Parameters = "DbCommand_Parameters";
            internal const string DbCommand_StatementCompleted = "DbCommand_StatementCompleted";
            internal const string DbCommand_Transaction = "DbCommand_Transaction";
            internal const string DbCommand_UpdatedRowSource = "DbCommand_UpdatedRowSource";
            internal const string DbCommandBuilder_CatalogLocation = "DbCommandBuilder_CatalogLocation";
            internal const string DbCommandBuilder_CatalogSeparator = "DbCommandBuilder_CatalogSeparator";
            internal const string DbCommandBuilder_ConflictOption = "DbCommandBuilder_ConflictOption";
            internal const string DbCommandBuilder_DataAdapter = "DbCommandBuilder_DataAdapter";
            internal const string DbCommandBuilder_QuotePrefix = "DbCommandBuilder_QuotePrefix";
            internal const string DbCommandBuilder_QuoteSuffix = "DbCommandBuilder_QuoteSuffix";
            internal const string DbCommandBuilder_SchemaSeparator = "DbCommandBuilder_SchemaSeparator";
            internal const string DbCommandBuilder_SetAllValues = "DbCommandBuilder_SetAllValues";
            internal const string DbConnection_InfoMessage = "DbConnection_InfoMessage";
            internal const string DbConnection_State = "DbConnection_State";
            internal const string DbConnection_StateChange = "DbConnection_StateChange";
            internal const string DbConnectionString_ApplicationIntent = "DbConnectionString_ApplicationIntent";
            internal const string DbConnectionString_ApplicationName = "DbConnectionString_ApplicationName";
            internal const string DbConnectionString_AsynchronousProcessing = "DbConnectionString_AsynchronousProcessing";
            internal const string DbConnectionString_AttachDBFilename = "DbConnectionString_AttachDBFilename";
            internal const string DbConnectionString_Authentication = "DbConnectionString_Authentication";
            internal const string DbConnectionString_Certificate = "DbConnectionString_Certificate";
            internal const string DbConnectionString_ConnectionReset = "DbConnectionString_ConnectionReset";
            internal const string DbConnectionString_ConnectionString = "DbConnectionString_ConnectionString";
            internal const string DbConnectionString_ConnectRetryCount = "DbConnectionString_ConnectRetryCount";
            internal const string DbConnectionString_ConnectRetryInterval = "DbConnectionString_ConnectRetryInterval";
            internal const string DbConnectionString_ConnectTimeout = "DbConnectionString_ConnectTimeout";
            internal const string DbConnectionString_ContextConnection = "DbConnectionString_ContextConnection";
            internal const string DbConnectionString_CurrentLanguage = "DbConnectionString_CurrentLanguage";
            internal const string DbConnectionString_DataSource = "DbConnectionString_DataSource";
            internal const string DbConnectionString_Driver = "DbConnectionString_Driver";
            internal const string DbConnectionString_DSN = "DbConnectionString_DSN";
            internal const string DbConnectionString_Encrypt = "DbConnectionString_Encrypt";
            internal const string DbConnectionString_Enlist = "DbConnectionString_Enlist";
            internal const string DbConnectionString_FailoverPartner = "DbConnectionString_FailoverPartner";
            internal const string DbConnectionString_FileName = "DbConnectionString_FileName";
            internal const string DbConnectionString_InitialCatalog = "DbConnectionString_InitialCatalog";
            internal const string DbConnectionString_IntegratedSecurity = "DbConnectionString_IntegratedSecurity";
            internal const string DbConnectionString_LoadBalanceTimeout = "DbConnectionString_LoadBalanceTimeout";
            internal const string DbConnectionString_MaxPoolSize = "DbConnectionString_MaxPoolSize";
            internal const string DbConnectionString_MinPoolSize = "DbConnectionString_MinPoolSize";
            internal const string DbConnectionString_MultipleActiveResultSets = "DbConnectionString_MultipleActiveResultSets";
            internal const string DbConnectionString_MultiSubnetFailover = "DbConnectionString_MultiSubnetFailover";
            internal const string DbConnectionString_NamedConnection = "DbConnectionString_NamedConnection";
            internal const string DbConnectionString_NetworkLibrary = "DbConnectionString_NetworkLibrary";
            internal const string DbConnectionString_OleDbServices = "DbConnectionString_OleDbServices";
            internal const string DbConnectionString_PacketSize = "DbConnectionString_PacketSize";
            internal const string DbConnectionString_Password = "DbConnectionString_Password";
            internal const string DbConnectionString_PersistSecurityInfo = "DbConnectionString_PersistSecurityInfo";
            internal const string DbConnectionString_PoolBlockingPeriod = "DbConnectionString_PoolBlockingPeriod";
            internal const string DbConnectionString_Pooling = "DbConnectionString_Pooling";
            internal const string DbConnectionString_Provider = "DbConnectionString_Provider";
            internal const string DbConnectionString_Replication = "DbConnectionString_Replication";
            internal const string DbConnectionString_TransactionBinding = "DbConnectionString_TransactionBinding";
            internal const string DbConnectionString_TransparentNetworkIPResolution = "DbConnectionString_TransparentNetworkIPResolution";
            internal const string DbConnectionString_TrustServerCertificate = "DbConnectionString_TrustServerCertificate";
            internal const string DbConnectionString_TypeSystemVersion = "DbConnectionString_TypeSystemVersion";
            internal const string DbConnectionString_UserID = "DbConnectionString_UserID";
            internal const string DbConnectionString_UserInstance = "DbConnectionString_UserInstance";
            internal const string DbConnectionString_WorkstationID = "DbConnectionString_WorkstationID";
            internal const string DbDataAdapter_DeleteCommand = "DbDataAdapter_DeleteCommand";
            internal const string DbDataAdapter_InsertCommand = "DbDataAdapter_InsertCommand";
            internal const string DbDataAdapter_RowUpdated = "DbDataAdapter_RowUpdated";
            internal const string DbDataAdapter_RowUpdating = "DbDataAdapter_RowUpdating";
            internal const string DbDataAdapter_SelectCommand = "DbDataAdapter_SelectCommand";
            internal const string DbDataAdapter_UpdateBatchSize = "DbDataAdapter_UpdateBatchSize";
            internal const string DbDataAdapter_UpdateCommand = "DbDataAdapter_UpdateCommand";
            internal const string DbDataParameter_Precision = "DbDataParameter_Precision";
            internal const string DbDataParameter_Scale = "DbDataParameter_Scale";
            internal const string DbParameter_DbType = "DbParameter_DbType";
            internal const string DbParameter_Direction = "DbParameter_Direction";
            internal const string DbParameter_Offset = "DbParameter_Offset";
            internal const string DbParameter_ParameterName = "DbParameter_ParameterName";
            internal const string DbParameter_Size = "DbParameter_Size";
            internal const string DbParameter_SourceColumn = "DbParameter_SourceColumn";
            internal const string DbParameter_SourceColumnNullMapping = "DbParameter_SourceColumnNullMapping";
            internal const string DbParameter_SourceVersion = "DbParameter_SourceVersion";
            internal const string DbParameter_Value = "DbParameter_Value";
            internal const string ExtendedPropertiesDescr = "ExtendedPropertiesDescr";
            internal const string ForeignKeyConstraintAcceptRejectRuleDescr = "ForeignKeyConstraintAcceptRejectRuleDescr";
            internal const string ForeignKeyConstraintChildColumnsDescr = "ForeignKeyConstraintChildColumnsDescr";
            internal const string ForeignKeyConstraintDeleteRuleDescr = "ForeignKeyConstraintDeleteRuleDescr";
            internal const string ForeignKeyConstraintParentColumnsDescr = "ForeignKeyConstraintParentColumnsDescr";
            internal const string ForeignKeyConstraintUpdateRuleDescr = "ForeignKeyConstraintUpdateRuleDescr";
            internal const string ForeignKeyRelatedTableDescr = "ForeignKeyRelatedTableDescr";
            internal const string KeyConstraintColumnsDescr = "KeyConstraintColumnsDescr";
            internal const string KeyConstraintIsPrimaryKeyDescr = "KeyConstraintIsPrimaryKeyDescr";
            internal const string OdbcCommandBuilder_DataAdapter = "OdbcCommandBuilder_DataAdapter";
            internal const string OdbcConnection_ConnectionString = "OdbcConnection_ConnectionString";
            internal const string OdbcConnection_ConnectionTimeout = "OdbcConnection_ConnectionTimeout";
            internal const string OdbcConnection_Database = "OdbcConnection_Database";
            internal const string OdbcConnection_DataSource = "OdbcConnection_DataSource";
            internal const string OdbcConnection_Driver = "OdbcConnection_Driver";
            internal const string OdbcConnection_ServerVersion = "OdbcConnection_ServerVersion";
            internal const string OdbcParameter_OdbcType = "OdbcParameter_OdbcType";
            internal const string OleDbCommandBuilder_DataAdapter = "OleDbCommandBuilder_DataAdapter";
            internal const string OleDbConnection_ConnectionString = "OleDbConnection_ConnectionString";
            internal const string OleDbConnection_ConnectionTimeout = "OleDbConnection_ConnectionTimeout";
            internal const string OleDbConnection_Database = "OleDbConnection_Database";
            internal const string OleDbConnection_DataSource = "OleDbConnection_DataSource";
            internal const string OleDbConnection_Provider = "OleDbConnection_Provider";
            internal const string OleDbConnection_ServerVersion = "OleDbConnection_ServerVersion";
            internal const string OleDbParameter_OleDbType = "OleDbParameter_OleDbType";
            internal const string SqlCommand_Notification = "SqlCommand_Notification";
            internal const string SqlCommand_NotificationAutoEnlist = "SqlCommand_NotificationAutoEnlist";
            internal const string SqlCommandBuilder_DataAdapter = "SqlCommandBuilder_DataAdapter";
            internal const string SqlConnection_AccessToken = "SqlConnection_AccessToken";
            internal const string SqlConnection_ClientConnectionId = "SqlConnection_ClientConnectionId";
            internal const string SqlConnection_ConnectionString = "SqlConnection_ConnectionString";
            internal const string SqlConnection_ConnectionTimeout = "SqlConnection_ConnectionTimeout";
            internal const string SqlConnection_Credential = "SqlConnection_Credential";
            internal const string SqlConnection_Database = "SqlConnection_Database";
            internal const string SqlConnection_DataSource = "SqlConnection_DataSource";
            internal const string SqlConnection_PacketSize = "SqlConnection_PacketSize";
            internal const string SqlConnection_ServerVersion = "SqlConnection_ServerVersion";
            internal const string SqlConnection_StatisticsEnabled = "SqlConnection_StatisticsEnabled";
            internal const string SqlConnection_WorkstationId = "SqlConnection_WorkstationId";
            internal const string SqlDependency_AddCommandDependency = "SqlDependency_AddCommandDependency";
            internal const string SqlDependency_HasChanges = "SqlDependency_HasChanges";
            internal const string SqlDependency_Id = "SqlDependency_Id";
            internal const string SqlDependency_OnChange = "SqlDependency_OnChange";
            internal const string SqlParameter_ParameterName = "SqlParameter_ParameterName";
            internal const string SqlParameter_SqlDbType = "SqlParameter_SqlDbType";
            internal const string SqlParameter_XmlSchemaCollectionDatabase = "SqlParameter_XmlSchemaCollectionDatabase";
            internal const string SqlParameter_XmlSchemaCollectionName = "SqlParameter_XmlSchemaCollectionName";
            internal const string SqlParameter_XmlSchemaCollectionOwningSchema = "SqlParameter_XmlSchemaCollectionOwningSchema";
            internal const string TCE_DbConnectionString_ColumnEncryptionSetting = "TCE_DbConnectionString_ColumnEncryptionSetting";
            internal const string TCE_DbConnectionString_EnclaveAttestationUrl = "TCE_DbConnectionString_EnclaveAttestationUrl";
            internal const string TCE_SqlCommand_ColumnEncryptionSetting = "TCE_SqlCommand_ColumnEncryptionSetting";
            internal const string TCE_SqlConnection_ColumnEncryptionKeyCacheTtl = "TCE_SqlConnection_ColumnEncryptionKeyCacheTtl";
            internal const string TCE_SqlConnection_ColumnEncryptionQueryMetadataCacheEnabled = "TCE_SqlConnection_ColumnEncryptionQueryMetadataCacheEnabled";
            internal const string TCE_SqlConnection_TrustedColumnMasterKeyPaths = "TCE_SqlConnection_TrustedColumnMasterKeyPaths";
            internal const string TCE_SqlParameter_ForceColumnEncryption = "TCE_SqlParameter_ForceColumnEncryption";
        }

        private static CultureInfo CultureHelper
        {
            get { return null/*use ResourceManager default, CultureInfo.CurrentUICulture*/; }
        }

        public static string GetString(string res, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    String value = args[i] as String;
                    if (value != null && value.Length > 1024)
                    {
                        args[i] = value.Substring(0, 1024 - 3) + "...";
                    }
                }
                return String.Format(CultureInfo.CurrentCulture, res, args);
            }
            else
            {
                return res;
            }
        }
    }
}
