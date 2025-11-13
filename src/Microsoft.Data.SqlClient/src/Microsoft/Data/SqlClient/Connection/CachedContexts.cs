// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

#nullable enable

namespace Microsoft.Data.SqlClient.Connection
{
    /// <summary>
    /// Provides cached asynchronous call contexts shared between objects in a connection's context.
    /// </summary>
    internal class CachedContexts
    {
        #region Fields

        /// <summary>
        /// Stores reusable context for ExecuteNonQueryAsync invocations.
        /// </summary>
        private SqlCommand.ExecuteNonQueryAsyncCallContext? _commandExecuteNonQueryAsyncContext;

        /// <summary>
        /// Stores reusable context for ExecuteReaderAsync invocations.
        /// </summary>
        private SqlCommand.ExecuteReaderAsyncCallContext? _commandExecuteReaderAsyncContext;

        /// <summary>
        /// Stores reusable context for ExecuteXmlReaderAsync invocations.
        /// </summary>
        private SqlCommand.ExecuteXmlReaderAsyncCallContext? _commandExecuteXmlReaderAsyncContext;

        /// <summary>
        /// Stores reusable context for IsDBNullAsync invocations.
        /// </summary>
        private SqlDataReader.IsDBNullAsyncCallContext? _dataReaderIsDbNullContext;

        /// <summary>
        /// Stores reusable context for ReadAsync invocations.
        /// </summary>
        private SqlDataReader.ReadAsyncCallContext? _dataReaderReadAsyncContext;

        #endregion

        #region Access Methods

        /// <summary>
        /// Removes and returns the cached ExecuteNonQueryAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlCommand.ExecuteNonQueryAsyncCallContext? ClearCommandExecuteNonQueryAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteNonQueryAsyncContext, null);

        /// <summary>
        /// Removes and returns the cached ExecuteReaderAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlCommand.ExecuteReaderAsyncCallContext? ClearCommandExecuteReaderAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteReaderAsyncContext, null);

        /// <summary>
        /// Removes and returns the cached ExecuteXmlReaderAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlCommand.ExecuteXmlReaderAsyncCallContext? ClearCommandExecuteXmlReaderAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteXmlReaderAsyncContext, null);

        /// <summary>
        /// Removes and returns the cached ReadAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlDataReader.ReadAsyncCallContext? ClearDataReaderReadAsyncContext() =>
            Interlocked.Exchange(ref _dataReaderReadAsyncContext, null);

        /// <summary>
        /// Removes and returns the cached IsDBNullAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlDataReader.IsDBNullAsyncCallContext? ClearDataReaderIsDbNullContext() =>
            Interlocked.Exchange(ref _dataReaderIsDbNullContext, null);

        /// <summary>
        /// Attempts to cache the provided ExecuteNonQueryAsync context.
        /// </summary>
        /// <param name="value">Context instance to store.</param>
        /// <returns>
        /// True when the context is cached; false if an existing value is preserved.
        /// </returns>
        internal bool TrySetCommandExecuteNonQueryAsyncContext(SqlCommand.ExecuteNonQueryAsyncCallContext value) =>
            TrySetContext(value, ref _commandExecuteNonQueryAsyncContext);

        /// <summary>
        /// Attempts to cache the provided ExecuteReaderAsync context.
        /// </summary>
        /// <param name="value">Context instance to store.</param>
        /// <returns>
        /// True when the context is cached; false if an existing value is preserved.
        /// </returns>
        internal bool TrySetCommandExecuteReaderAsyncContext(SqlCommand.ExecuteReaderAsyncCallContext value) =>
            TrySetContext(value, ref _commandExecuteReaderAsyncContext);

        /// <summary>
        /// Attempts to cache the provided ExecuteXmlReaderAsync context.
        /// </summary>
        /// <param name="value">Context instance to store.</param>
        /// <returns>
        /// True when the context is cached; false if an existing value is preserved.
        /// </returns>
        internal bool TrySetCommandExecuteXmlReaderAsyncContext(SqlCommand.ExecuteXmlReaderAsyncCallContext value) =>
            TrySetContext(value, ref _commandExecuteXmlReaderAsyncContext);

        /// <summary>
        /// Attempts to cache the provided ReadAsync context.
        /// </summary>
        /// <param name="value">Context instance to store.</param>
        /// <returns>
        /// True when the context is cached; false if an existing value is preserved.
        /// </returns>
        internal bool TrySetDataReaderReadAsyncContext(SqlDataReader.ReadAsyncCallContext value) =>
            TrySetContext(value, ref _dataReaderReadAsyncContext);

        /// <summary>
        /// Attempts to cache the provided IsDBNullAsync context.
        /// </summary>
        /// <param name="value">Context instance to store.</param>
        /// <returns>
        /// True when the context is cached; false if an existing value is preserved.
        /// </returns>
        internal bool TrySetDataReaderIsDbNullContext(SqlDataReader.IsDBNullAsyncCallContext value) =>
            TrySetContext(value, ref _dataReaderIsDbNullContext);

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
