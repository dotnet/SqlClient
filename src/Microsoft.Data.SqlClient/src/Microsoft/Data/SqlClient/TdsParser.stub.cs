// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: This is only a stub partial for clearing errors while merging other files.

using System;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using Microsoft.Data.Sql;

namespace Microsoft.Data.SqlClient
{
    internal partial class TdsParser
    {
        internal bool _asyncWrite = false;

        internal SqlInternalConnectionTds Connection { get; set; }
        
        internal SqlInternalTransaction CurrentTransaction { get; set; }
        
        internal TdsParserState State { get; set; }
        
        internal static SqlDecimal AdjustSqlDecimalScale(SqlDecimal sqlValue, int scale) =>
            SqlDecimal.Null;

        internal object EncryptColumnValue(
            object value,
            SqlMetaDataPriv metadata,
            string column,
            TdsParserStateObject stateObj,
            bool isDataFeed,
            bool isSqlType) =>
            null;

        internal TdsParserStateObject GetSession(object owner) =>
            null;
        
        internal void LoadColumnEncryptionKeys(
            _SqlMetaDataSet metadataCollection,
            SqlConnection connection,
            SqlCommand command = null)
        {
        }
        
        internal void Run(
            RunBehavior runBehavior,
            SqlCommand cmdHandler,
            SqlDataReader dataStream,
            BulkCopySimpleResultSet bulkCopyHandler,
            TdsParserStateObject stateObj)
        {
        }
        
        #if NETFRAMEWORK
        internal void RunReliably(
            RunBehavior runBehavior,
            SqlCommand cmdHandler,
            SqlDataReader dataStream,
            BulkCopySimpleResultSet bulkCopyHandler,
            TdsParserStateObject stateObj)
        {
        }
        #endif
        
        internal Task TdsExecuteSQLBatch(
            string text,
            int timeout,
            SqlNotificationRequest notificationRequest,
            TdsParserStateObject stateObj,
            bool sync,
            bool callerHasConnectionLock = false,
            byte[] enclavePackage = null) =>
            null;

        internal bool ShouldEncryptValuesForBulkCopy() =>
            false;

        internal Task WriteBulkCopyDone(TdsParserStateObject stateObj) =>
            Task.CompletedTask;

        internal void WriteBulkCopyMetaData(
            _SqlMetaDataSet metadataCollection,
            int count,
            TdsParserStateObject stateObj)
        {
        }

        internal Task WriteBulkCopyValue(
            object value,
            SqlMetaDataPriv metadata,
            TdsParserStateObject stateObj,
            bool isSqlType,
            bool isDataFeed,
            bool isNull) =>
            Task.CompletedTask;

        internal Task WriteSqlVariantDataRowValue(object value, TdsParserStateObject stateObje) =>
            Task.CompletedTask;

        internal void WriteSqlVariantDate(DateTime value, TdsParserStateObject stateObj)
        {
        }
        
        internal void WriteSqlVariantDateTime2(DateTime value, TdsParserStateObject stateObj)
        {
        }
    }
}
