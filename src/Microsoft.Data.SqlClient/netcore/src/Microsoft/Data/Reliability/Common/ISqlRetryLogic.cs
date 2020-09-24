// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Reliability
{
    /// 
    public interface ISqlRetryLogic
    {
        /// 
        int NumberOfTries { get; }

        /// 
        int Current { get; }

        /// 
        ISqlRetryIntervalEnumerator RetryIntervalEnumerator { get; }

        /// 
        Predicate<Exception> TransientPredicate { get; }

        /// 
        bool TryNextInterval(out TimeSpan intervalTime);

        ///
        void Reset();
    }
}
