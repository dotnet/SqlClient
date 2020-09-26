// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// provide retry information on each attemp
    /// </summary>
    public class SqlRetryingEventArgs : EventArgs
    {
        /// <summary>
        /// Contains information that is required for a retry.
        /// </summary>
        /// <param name="retryCount">The current retry attempt count.</param>
        /// <param name="delay">The delay that indicates how long the current thread will be suspended before the next iteration is invoked.</param>
        /// <param name="exceptions">The exceptions since the first retry that caused the retry conditions to occur.</param>
        public SqlRetryingEventArgs(int retryCount, TimeSpan delay, IList<Exception> exceptions)
        {
            RetryCount = retryCount;
            Delay = delay;
            Exceptions = exceptions;
        }

        /// <summary>
        /// Retry-attempt-number, after the fisrt exception occurrence.
        /// </summary>
        public int RetryCount { get; private set; }

        /// <summary>
        /// The current waiting time in millisecond.
        /// </summary>
        public TimeSpan Delay { get; private set; }

        /// <summary>
        /// If set to true retry will intruppted immidiately.
        /// </summary>
        public bool Cancel { get; set; } = false;

        /// <summary>
        /// The list of exceptions since the first rettry.
        /// </summary>
        public IList<Exception> Exceptions { get; private set; }
    }
}
