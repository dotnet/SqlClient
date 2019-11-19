// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;

namespace Microsoft.Data.SqlClient.Reliability
{

    /// <summary>
    /// Contains information that is required for the <see cref="SqlRetryPolicy.Retrying"/> event.
    /// </summary>
    public class SqlRetryingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryingEventArgs"/> class.
        /// </summary>
        /// <param name="currentRetryCount">The current retry attempt count.</param>
        /// <param name="delay">The delay that indicates how long the current thread will be suspended before the next iteration is invoked.</param>
        /// <param name="lastException">The exception that caused the retry conditions to occur.</param>
        public SqlRetryingEventArgs(int currentRetryCount, TimeSpan delay, Exception lastException)
        {
            this.CurrentRetryCount = currentRetryCount;
            this.Delay = delay;
            this.LastException = lastException;
        }

        /// <summary>
        /// Gets the current retry count.
        /// </summary>
        public int CurrentRetryCount { get; private set; }

        /// <summary>
        /// Gets the delay that indicates how long the current thread will be suspended before the next iteration is invoked.
        /// </summary>
        public TimeSpan Delay { get; private set; }

        /// <summary>
        /// Gets the exception that caused the retry conditions to occur.
        /// </summary>
        public Exception LastException { get; private set; }
    }
}
