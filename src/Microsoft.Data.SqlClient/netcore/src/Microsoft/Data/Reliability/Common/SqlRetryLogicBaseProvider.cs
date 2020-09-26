// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Apply a retry logic on an operation.
    /// </summary>
    public abstract class SqlRetryLogicBaseProvider
    {
        /// <summary>
        /// This event raises exactly before time delay in retry the operation again. 
        /// </summary>
        public EventHandler<SqlRetryingEventArgs> Retrying { get; set; }

        /// <summary>
        /// Defined retry logic
        /// </summary>
        public SqlRetryLogicBase RetryLogic { get; protected set; }

        /// <summary>
        /// Executes a function with a TResult type.
        /// </summary>
        /// <typeparam name="TResult">The function return type</typeparam>
        /// <param name="function">The operaiton is likly be in the retry logic if transient condition happens</param>
        /// <returns>A TResult object or an exception</returns>
        public abstract TResult Execute<TResult>(Func<TResult> function);

        /// <summary>
        /// Executes a function with a generic Task and TResult type.
        /// </summary>
        /// <typeparam name="TResult">Inner function return type</typeparam>
        /// <param name="function">The operaiton is likly be in the retry logic if transient condition happens</param>
        /// <param name="cancellationToken">The cancellation instruction</param>
        /// <returns>A task representing TResult or an exception</returns>
        public abstract Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> function, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a function with a generic Task type.
        /// </summary>
        /// <param name="function">The operaiton is likly be in the retry logic if transient condition happens</param>
        /// <param name="cancellationToken">The cancellation instruction</param>
        /// <returns>A Task or an exception</returns>
        public abstract Task ExecuteAsync(Func<Task> function, CancellationToken cancellationToken = default);
    }
}
