// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Verifies internal access and framework-specific friend assembly metadata.
    /// </summary>
    public class InternalsVisibleToTest
    {
        /// <summary>
        /// Ensures the unit test assembly can use the driver's internal types.
        /// </summary>
        [Fact]
        public void TestInternalsVisible()
        {
            DbConnectionPoolAuthenticationContext context = new([1, 2, 3], DateTime.UtcNow.AddMinutes(5));
            Assert.NotNull(context);
            Assert.Equal([1, 2, 3], context.AccessToken);
        }

        /// <summary>
        /// Preserves the exact DataSetExtensions friend identity on .NET Framework only.
        /// </summary>
        [Fact]
        public void DataSetExtensionsFriendAssemblyMatchesTargetFramework()
        {
            var friends = typeof(SqlConnection).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
                .Where(attribute => new AssemblyName(attribute.AssemblyName).Name == "System.Data.DataSetExtensions");

#if NETFRAMEWORK
            Assert.Equal("System.Data.DataSetExtensions, PublicKey=00000000000000000400000000000000",
                Assert.Single(friends).AssemblyName);
#else
            Assert.Empty(friends);
#endif
        }
    }
}
