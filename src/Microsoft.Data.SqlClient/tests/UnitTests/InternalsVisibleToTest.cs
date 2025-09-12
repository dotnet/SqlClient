using System;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Tests proving that the InternalsVisibleTo attribute works correctly.
    /// </summary>
    public class InternalsVisibleToTest
    {
        /// <summary>
        /// Creates an instance of an internal class. Verifies that this compiles.
        /// </summary>
        [Fact]
        public void TestInternalsVisible()
        {
            DbConnectionPoolAuthenticationContext context = new([1, 2, 3], DateTime.UtcNow.AddMinutes(5));
            Assert.NotNull(context);
            Assert.Equal([1, 2, 3], context.AccessToken);
        }
    }
}
