// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/SqlRetryingEventArgs/*' />
    public sealed class SqlRetryingEventArgs : EventArgs
    {
        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/ctor/*' />
        public SqlRetryingEventArgs(int retryCount, TimeSpan delay, IList<Exception> exceptions)
        {
            RetryCount = retryCount;
            Delay = delay;
            Exceptions = exceptions;
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/RetryCount/*' />
        public int RetryCount { get; private set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/Delay/*' />
        public TimeSpan Delay { get; private set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/Cancel/*' />
        public bool Cancel { get; set; } = false;

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryingEventArgs.xml' path='docs/members[@name="SqlRetryingEventArgs"]/Exceptions/*' />
        public IList<Exception> Exceptions { get; private set; }
    }
}
