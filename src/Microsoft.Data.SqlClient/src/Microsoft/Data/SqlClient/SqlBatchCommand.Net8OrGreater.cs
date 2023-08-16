// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public partial class SqlBatchCommand
    {
        public override DbParameter CreateParameter() => new SqlParameter();

        public override bool CanCreateParameter => true;
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
