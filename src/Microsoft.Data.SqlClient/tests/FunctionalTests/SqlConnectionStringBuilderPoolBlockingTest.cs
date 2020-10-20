// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public partial class SqlConnectionStringBuilderTest
    {

        [Theory]
        // PoolBlockingPeriod is not supported in .NET Standard
        [InlineData("PoolBlockingPeriod = Auto")]
        [InlineData("PoolBlockingperiod = NeverBlock")]
        public void ConnectionStringTestsNetStandard(string connectionString)
        {
            ExecuteConnectionStringTests(connectionString);
        }
    }
}
