// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/SqlRetryLogicBaseProvider/*' />
    public abstract class SqlRetryLogicBaseProvider
    {
        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/Retrying/*' />
        public EventHandler<SqlRetryingEventArgs> Retrying { get; set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/RetryLogic/*' />
        public SqlRetryLogicBase RetryLogic { get; protected set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/Execute/*' />
        public abstract TResult Execute<TResult>(object sender, Func<TResult> function);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/ExecuteAsync1/*' />
        public abstract Task<TResult> ExecuteAsync<TResult>(object sender, Func<Task<TResult>> function, CancellationToken cancellationToken = default);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicBaseProvider.xml' path='docs/members[@name="SqlRetryLogicBaseProvider"]/ExecuteAsync2/*' />
        public abstract Task ExecuteAsync(object sender, Func<Task> function, CancellationToken cancellationToken = default);
    }
}
