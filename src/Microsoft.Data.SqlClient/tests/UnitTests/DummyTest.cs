// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using Xunit;

namespace UnitTests
{
    public class DummyTest
    {
        [Fact]
        public void TestSomethingInternal()
        {
            SqlConnection conn = new SqlConnection();
            Assert.False(conn.AsyncCommandInProgress);
        }
    }

}
