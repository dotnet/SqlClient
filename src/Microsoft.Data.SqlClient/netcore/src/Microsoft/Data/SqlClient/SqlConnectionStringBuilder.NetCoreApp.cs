// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlConnectionStringBuilder : DbConnectionStringBuilder
    {
        private PoolBlockingPeriod _poolBlockingPeriod = DbConnectionStringDefaults.PoolBlockingPeriod;

        private void SetPoolBlockingPeriodValue(PoolBlockingPeriod value)
        {
            Debug.Assert(DbConnectionStringBuilderUtil.IsValidPoolBlockingPeriodValue(value), "Invalid value for PoolBlockingPeriod");
            base[DbConnectionStringKeywords.PoolBlockingPeriod] = DbConnectionStringBuilderUtil.PoolBlockingPeriodToString(value);
        }

        private static PoolBlockingPeriod ConvertToPoolBlockingPeriod(string keyword, object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToPoolBlockingPeriod(keyword, value);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PoolBlockingPeriod/*' />
        public PoolBlockingPeriod PoolBlockingPeriod
        {
            get { return _poolBlockingPeriod; }
            set
            {
                if (!DbConnectionStringBuilderUtil.IsValidPoolBlockingPeriodValue(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(PoolBlockingPeriod), (int)value);
                }

                SetPoolBlockingPeriodValue(value);
                _poolBlockingPeriod = value;
            }
        }
    }
}
