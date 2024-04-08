// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.DataCommon
{
    public class DateTimeOffsetList : SqlDataRecord
    {
        public DateTimeOffsetList(DateTimeOffset dateTimeOffset)
            : base(new SqlMetaData("dateTimeOffset", SqlDbType.DateTimeOffset, 0, 1))
        {
            this.SetValues(dateTimeOffset);
        }
    }
}
