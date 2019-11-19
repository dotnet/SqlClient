// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;

namespace Microsoft.Data.SqlClient.Reliability
{
    /// <summary>
    /// Provides a generic version of the <see cref="SqlRetryPolicy"/> class.
    /// </summary>
    /// <typeparam name="T">The type that implements the <see cref="ITransientErrorDetectionStrategy"/> interface that is responsible for detecting transient conditions.</typeparam>
    public class SqlRetryPolicy<T> : SqlRetryPolicy where T : ITransientErrorDetectionStrategy, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryPolicy{T}"/> class with the specified number of retry attempts and parameters defining the progressive delay between retries.
        /// </summary>
        /// <param name="retryStrategy">The strategy to use for this retry policy.</param>
        public SqlRetryPolicy(SqlRetryStrategy retryStrategy)
            : base(new T(), retryStrategy)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryPolicy{T}"/> class with the specified number of retry attempts and the default fixed time interval between retries.
        /// </summary>
        /// <param name="retryCount">The number of retry attempts.</param>
        public SqlRetryPolicy(int retryCount)
            : base(new T(), retryCount)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryPolicy{T}"/> class with the specified number of retry attempts and a fixed time interval between retries.
        /// </summary>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="retryInterval">The interval between retries.</param>
        public SqlRetryPolicy(int retryCount, TimeSpan retryInterval)
            : base(new T(), retryCount, retryInterval)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryPolicy{T}"/> class with the specified number of retry attempts and backoff parameters for calculating the exponential delay between retries.
        /// </summary>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="minBackoff">The minimum backoff time.</param>
        /// <param name="maxBackoff">The maximum backoff time.</param>
        /// <param name="deltaBackoff">The time value that will be used to calculate a random delta in the exponential delay between retries.</param>
        public SqlRetryPolicy(int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
            : base(new T(), retryCount, minBackoff, maxBackoff, deltaBackoff)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryPolicy{T}"/> class with the specified number of retry attempts and parameters defining the progressive delay between retries.
        /// </summary>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="initialInterval">The initial interval that will apply for the first retry.</param>
        /// <param name="increment">The incremental time value that will be used to calculate the progressive delay between retries.</param>
        public SqlRetryPolicy(int retryCount, TimeSpan initialInterval, TimeSpan increment)
            : base(new T(), retryCount, initialInterval, increment)
        {
        }
    }
}
