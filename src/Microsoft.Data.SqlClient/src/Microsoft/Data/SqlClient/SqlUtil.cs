// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

// This file will use to merge code from NetFx and NetCore SqlUtil.cs in future.
namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Defines the exceptions are specific to the Configurable Retry Logic.
    /// </summary>
    internal static class SqlReliabilityUtil
    {
        internal static AggregateException ConfigurableRetryFail(IList<Exception> exceptions, SqlRetryLogicBase retryLogic, bool canceled)
            => canceled ? new AggregateException(StringsHelper.GetString(Strings.SqlRetryLogic_RetryCanceled, retryLogic.Current), exceptions)
                : new AggregateException(StringsHelper.GetString(Strings.SqlRetryLogic_RetryExceeded, retryLogic.NumberOfTries), exceptions);

        internal static ArgumentOutOfRangeException ArgumentOutOfRange(string paramName, int value, int minValue, int MaxValue)
            => new ArgumentOutOfRangeException(paramName, StringsHelper.GetString(Strings.SqlRetryLogic_InvalidRange, value, minValue, MaxValue));

        internal static ArgumentOutOfRangeException ArgumentOutOfRange(string paramName, TimeSpan value, TimeSpan minValue, TimeSpan MaxValue)
            => new ArgumentOutOfRangeException(paramName, StringsHelper.GetString(Strings.SqlRetryLogic_InvalidRange, value, minValue, MaxValue));

        internal static ArgumentNullException ArgumentNull(string paramName)
            => new ArgumentNullException(paramName);
    }
}
