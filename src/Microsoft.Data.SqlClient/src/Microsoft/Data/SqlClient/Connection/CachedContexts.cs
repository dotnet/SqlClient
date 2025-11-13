// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

#nullable enable

namespace Microsoft.Data.SqlClient.Connection
{
    internal class CachedContexts
    {
        #region Fields

        private SqlCommand.ExecuteNonQueryAsyncCallContext? _commandExecuteNonQueryAsyncContext;
        private SqlCommand.ExecuteReaderAsyncCallContext? _commandExecuteReaderAsyncContext;
        private SqlCommand.ExecuteXmlReaderAsyncCallContext? _commandExecuteXmlReaderAsyncContext;
        // private SqlDataReader.IsDBNullAsyncCallContext? _dataReaderIsDbNullContext;
        private SqlDataReader.ReadAsyncCallContext? _dataReaderReadAsyncContext;
        // private SqlDataReader.Snapshot? _dataReaderSnapshot;

        #endregion

        #region Access Methods

        internal SqlCommand.ExecuteNonQueryAsyncCallContext? ClearCommandExecuteNonQueryAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteNonQueryAsyncContext, null);

        internal SqlCommand.ExecuteReaderAsyncCallContext? ClearCommandExecuteReaderAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteReaderAsyncContext, null);

        internal SqlCommand.ExecuteXmlReaderAsyncCallContext? ClearCommandExecuteXmlReaderAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteXmlReaderAsyncContext, null);

        internal SqlDataReader.ReadAsyncCallContext? ClearDataReaderReadAsyncContext() =>
            Interlocked.Exchange(ref _dataReaderReadAsyncContext, null);

        internal bool TrySetCommandExecuteNonQueryAsyncContext(SqlCommand.ExecuteNonQueryAsyncCallContext value) =>
            TrySetContext(value, ref _commandExecuteNonQueryAsyncContext);

        internal bool TrySetCommandExecuteReaderAsyncContext(SqlCommand.ExecuteReaderAsyncCallContext value) =>
            TrySetContext(value, ref _commandExecuteReaderAsyncContext);

        internal bool TrySetCommandExecuteXmlReaderAsyncContext(SqlCommand.ExecuteXmlReaderAsyncCallContext value) =>
            TrySetContext(value, ref _commandExecuteXmlReaderAsyncContext);

        internal bool TrySetDataReaderReadAsyncContext(SqlDataReader.ReadAsyncCallContext value) =>
            TrySetContext(value, ref _dataReaderReadAsyncContext);

        #endregion

        private static bool TrySetContext<TContext>(TContext value, ref TContext? location)
            where TContext : class
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Interlocked.CompareExchange(ref location, value, null) is null;
        }
    }
}
