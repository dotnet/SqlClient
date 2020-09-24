// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Reliability;

namespace Microsoft.Data.SqlClient
{
    /// 
    public interface ISqlRetryLogicProvider
    {
        /// 
        EventHandler<SqlRetryingEventArgs> Retrying { get; set; }

        /// 
        ISqlRetryLogic RetryLogic { get; }

        ///
        TResult Execute<TResult>(Func<TResult> function);

        /// 
        Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> function, CancellationToken cancellationToken = default);

        /// 
        Task ExecuteAsync<TResult>(Func<Task> function, CancellationToken cancellationToken = default);
    }
}
