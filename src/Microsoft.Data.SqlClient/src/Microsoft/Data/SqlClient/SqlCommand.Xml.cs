// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlCommand
    {
        #region Public/Internal Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReader/*'/>
        public XmlReader ExecuteXmlReader()
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif
            
            // Reset _pendingCancel upon entry into any Execute - used to synchronize state
            // between entry into Execute* API and the thread obtaining the stateObject.
            _pendingCancel = false;
            
            #if NET
            using var diagnosticScope = s_diagnosticListener.CreateCommandScope(this, _transaction);
            #endif

            using var eventScope = TryEventScope.Create($"SqlCommand.ExecuteXmlReader | API | Object Id {ObjectID}");
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.ExecuteXmlReader | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            SqlStatistics statistics = null;
            bool success = false;
            int? sqlExceptionNumber = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                WriteBeginExecuteEvent();

                // Use the reader to consume metadata
                SqlDataReader reader = IsProviderRetriable
                    ? RunExecuteReaderWithRetry(CommandBehavior.SequentialAccess, RunBehavior.ReturnImmediately, returnStream: true)
                    : RunExecuteReader(CommandBehavior.SequentialAccess, RunBehavior.ReturnImmediately, returnStream: true);
                success = true;

                return CompleteXmlReader(reader, isAsync: false);
            }
            catch (Exception ex)
            {
                #if NET
                diagnosticScope.SetException(ex);
                #endif

                if (ex is SqlException sqlException)
                {
                    sqlExceptionNumber = sqlException.Number;
                }

                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
                WriteEndExecuteEvent(success, sqlExceptionNumber, synchronous: true);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReaderAsync[@name="default"]/*'/>
        public Task<XmlReader> ExecuteXmlReaderAsync() =>
            ExecuteXmlReaderAsync(CancellationToken.None);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReaderAsync[@name="CancellationToken"]/*'/>
        public Task<XmlReader> ExecuteXmlReaderAsync(CancellationToken cancellationToken) =>
            IsProviderRetriable
                ? InternalExecuteXmlReaderWithRetryAsync(cancellationToken)
                : InternalExecuteXmlReaderAsync(cancellationToken);
        
        #endregion
        
        #region Private Methods

        private static XmlReader CompleteXmlReader(SqlDataReader dataReader, bool isAsync)
        {
            XmlReader xmlReader = null;

            SmiExtendedMetaData[] metaData = dataReader.GetInternalSmiMetaData();
            bool isXmlCapable = metaData?.Length == 1 &&
                                metaData[0].SqlDbType is SqlDbType.NVarChar or SqlDbType.NText or SqlDbType.Xml;

            if (isXmlCapable)
            {
                try
                {
                    SqlStream sqlStream = new SqlStream(
                        dataReader,
                        addByteOrderMark: true,
                        processAllRows: metaData[0].SqlDbType is not SqlDbType.Xml);
                    xmlReader = sqlStream.ToXmlReader(isAsync);
                }
                catch (Exception e)
                {
                    if (ADP.IsCatchableExceptionType(e))
                    {
                        dataReader.Close();
                    }

                    throw;
                }
            }

            if (xmlReader is null)
            {
                dataReader.Close();
                throw SQL.NonXmlResult();
            }

            return xmlReader;
        }

        #endregion
    }
}
