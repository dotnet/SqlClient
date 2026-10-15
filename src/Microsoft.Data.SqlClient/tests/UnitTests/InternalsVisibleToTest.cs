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
    /// Verifies internal access and the complete set of friend assembly grants.
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
        /// Ensures only UnitTests receives internal access, with its public key for signed builds.
        /// </summary>
        [Fact]
        public void FriendAssembliesMatchExpectedList()
        {
            var driverAssembly = typeof(SqlConnection).Assembly;
            var friends = driverAssembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(attribute => attribute.AssemblyName);

            string expectedFriend = "UnitTests";
            if (driverAssembly.GetName().GetPublicKey() is { Length: > 0 })
            {
                byte[]? testPublicKey = typeof(InternalsVisibleToTest).Assembly.GetName().GetPublicKey();
                Assert.NotNull(testPublicKey);
                Assert.NotEmpty(testPublicKey);
                expectedFriend += ", PublicKey=" + BitConverter.ToString(testPublicKey).Replace("-", "");
            }

            Assert.Equal(expectedFriend, Assert.Single(friends), ignoreCase: true);
        }
    }
}
