// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Transactions;

namespace Microsoft.Data.ProviderBase
{
    // DO NOT USE THIS FILE IN ANY PROJECT!
    // This is a temporary stub to enable migrating DbConnectionInternal to the common project.
    internal class DbConnectionPool
    {
        internal TimeSpan LoadBalanceTimeout => throw new NotImplementedException("STUB");

        #if NETFRAMEWORK
        internal DbConnectionPoolCounters PerformanceCounters => throw new NotImplementedException("STUB");
        #endif

        internal bool UseLoadBalancing => throw new NotImplementedException("STUB");

        internal void PutObject(DbConnectionInternal obj, object owningObject) =>
            throw new NotImplementedException("STUB");

        internal void PutObjectFromTransactedPool(DbConnectionInternal obj) =>
            throw new NotImplementedException("STUB");

        internal void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject) =>
            throw new NotImplementedException("STUB");
    }
}
