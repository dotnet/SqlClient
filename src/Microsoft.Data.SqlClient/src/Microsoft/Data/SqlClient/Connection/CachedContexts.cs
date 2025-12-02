// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

#nullable enable

namespace Microsoft.Data.SqlClient.Connection
{
    /// <summary>
    /// Provides thread-safe caching and sharing of asynchronous call contexts between objects
    /// within a single SQL connection context.
    /// </summary>
    /// <remarks>
    /// This internal class manages reusable context objects for various asynchronous operations
    /// such as ExecuteNonQueryAsync, ExecuteReaderAsync, etc, performed on a connection, enabling
    /// efficient reuse and reducing allocations.
    ///
    /// Thread safety is ensured via interlocked operations, allowing concurrent access and
    /// updates without explicit locking. All accessors and mutators are designed to be safe for
    /// use by multiple threads.
    ///
    /// Intended for internal use by connection management infrastructure.
    /// </remarks>
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

        /// <summary>
        /// Stores a data reader snapshot.
        /// </summary>
        private SqlDataReader.Snapshot? _dataReaderSnapshot;

        #endregion

        #region Access Methods

        /// <summary>
        /// Removes and returns the cached ExecuteNonQueryAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlCommand.ExecuteNonQueryAsyncCallContext? TakeCommandExecuteNonQueryAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteNonQueryAsyncContext, null);

        /// <summary>
        /// Removes and returns the cached ExecuteReaderAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlCommand.ExecuteReaderAsyncCallContext? TakeCommandExecuteReaderAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteReaderAsyncContext, null);

        /// <summary>
        /// Removes and returns the cached ExecuteXmlReaderAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlCommand.ExecuteXmlReaderAsyncCallContext? TakeCommandExecuteXmlReaderAsyncContext() =>
            Interlocked.Exchange(ref _commandExecuteXmlReaderAsyncContext, null);

        /// <summary>
        /// Removes and returns the cached ReadAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlDataReader.ReadAsyncCallContext? TakeDataReaderReadAsyncContext() =>
            Interlocked.Exchange(ref _dataReaderReadAsyncContext, null);

        /// <summary>
        /// Removes and returns the cached IsDBNullAsync context.
        /// </summary>
        /// <returns>The previously cached context or null when empty.</returns>
        internal SqlDataReader.IsDBNullAsyncCallContext? TakeDataReaderIsDbNullContext() =>
            Interlocked.Exchange(ref _dataReaderIsDbNullContext, null);

        /// <summary>
        /// Removes and returns the cached data reader snapshot.
        /// </summary>
        /// <returns>The previously cached snapshot or null when empty.</returns>
        internal SqlDataReader.Snapshot? TakeDataReaderSnapshot() =>
            Interlocked.Exchange(ref _dataReaderSnapshot, null);

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

        /// <summary>
        /// Attempts to cache the provided data reader snapshot context.
        /// </summary>
        /// <param name="value">Context instance to store.</param>
        /// <returns>
        /// True when the snapshot is cached; false if an existing snapshot is preserved.
        /// </returns>
        internal bool TrySetDataReaderSnapshot(SqlDataReader.Snapshot value) =>
            TrySetContext(value, ref _dataReaderSnapshot);

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
