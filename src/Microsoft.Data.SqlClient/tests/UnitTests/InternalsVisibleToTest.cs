using System;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class InternalsVisibleToTest
    {
        [Fact]
        public void TestInternalsVisible()
        {
            DbConnectionPoolAuthenticationContext context = new([1, 2, 3], DateTime.UtcNow.AddMinutes(5));
            Assert.Equal([1, 2, 3], context.AccessToken);
        }
    }
}
