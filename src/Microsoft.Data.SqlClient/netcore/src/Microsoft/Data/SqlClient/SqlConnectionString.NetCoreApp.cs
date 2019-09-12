// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlConnectionString : DbConnectionOptions
    {
        internal static partial class DEFAULT
        {
            internal const PoolBlockingPeriod PoolBlockingPeriod = DbConnectionStringDefaults.PoolBlockingPeriod;
        }

        private readonly PoolBlockingPeriod _poolBlockingPeriod;

        internal PoolBlockingPeriod PoolBlockingPeriod { get { return _poolBlockingPeriod; } }

        internal Microsoft.Data.SqlClient.PoolBlockingPeriod ConvertValueToPoolBlockingPeriod()
        {
            string value;
            if (!TryGetParsetableValue(KEY.PoolBlockingPeriod, out value))
            {
                return DEFAULT.PoolBlockingPeriod;
            }

            try
            {
                return DbConnectionStringBuilderUtil.ConvertToPoolBlockingPeriod(KEY.PoolBlockingPeriod, value);
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.PoolBlockingPeriod, e);
            }
        }
    }
}
