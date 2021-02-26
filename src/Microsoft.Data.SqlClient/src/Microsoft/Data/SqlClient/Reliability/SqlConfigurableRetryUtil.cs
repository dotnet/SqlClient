// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Defines the exceptions are specific to the Configurable Retry Logic.
    /// </summary>
    internal static partial class SqlCRLUtil
    {
        internal static AggregateException RetryCancelledException(IList<Exception> innereExceptions, int currentAttemptNo)
        {
            var e = new AggregateException(StringsHelper.GetString(Strings.SqlRetryLogic_RetryCanceled, currentAttemptNo), innereExceptions);
            return e;
        }

        internal static AggregateException RetryExceededException(IList<Exception> innerExceptions, int maxAttempts)
        {
            var e = new AggregateException(StringsHelper.GetString(Strings.SqlRetryLogic_RetryExceeded, maxAttempts), innerExceptions);
            return e;
        }

        internal static ArgumentOutOfRangeException ArgumentOutOfRange(string paramName, int value, int minValue, int MaxValue)
        {
            var e = new ArgumentOutOfRangeException(paramName, StringsHelper.GetString(Strings.SqlRetryLogic_InvalidRange, value, minValue, MaxValue));
            return e;
        }

        internal static ArgumentOutOfRangeException ArgumentOutOfRange(string paramName, TimeSpan value, TimeSpan minValue, TimeSpan MaxValue)
        {
            var e = new ArgumentOutOfRangeException(paramName, StringsHelper.GetString(Strings.SqlRetryLogic_InvalidRange, value, minValue, MaxValue));
            return e;
        }

        internal static ArgumentNullException ArgumentNull(string paramName)
        {
            var e = new ArgumentNullException(paramName);
            return e;
        }
    }
}
