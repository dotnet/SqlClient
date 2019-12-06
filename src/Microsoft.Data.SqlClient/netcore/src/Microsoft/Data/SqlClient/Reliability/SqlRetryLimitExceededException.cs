using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using Microsoft.Data.SqlClient.Reliability.Properties;

namespace Microsoft.Data.SqlClient.Reliability
{
    /// <summary>
    /// The special type of exception that provides managed exit from a retry loop. The user code can use this
    /// exception to notify the retry policy that no further retry attempts are required.
    /// </summary>
    [Obsolete("You should use cancellation tokens or other means of stoping the retry loop.")]
    public sealed class SqlRetryLimitExceededException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryLimitExceededException"/> class with a default error message.
        /// </summary>
        public SqlRetryLimitExceededException()
            : this(Resources.RetryLimitExceeded)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryLimitExceededException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SqlRetryLimitExceededException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryLimitExceededException"/> class with a reference to the inner exception
        /// that is the cause of this exception.
        /// </summary>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public SqlRetryLimitExceededException(Exception innerException)
            : base(innerException != null ? innerException.Message : Resources.RetryLimitExceeded, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRetryLimitExceededException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public SqlRetryLimitExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
