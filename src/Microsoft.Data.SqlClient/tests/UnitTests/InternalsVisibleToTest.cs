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
        /// Ensures only the expected test assemblies receive internal access, sharing one key for signed builds.
        /// </summary>
        [Fact]
        public void FriendAssembliesMatchExpectedList()
        {
            var driverAssembly = typeof(SqlConnection).Assembly;
            var friends = driverAssembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(attribute => attribute.AssemblyName);

            string[] expectedFriends = { "UnitTests", "ManualTests", "Microsoft.Data.SqlClient.TestCommon" };
            if (driverAssembly.GetName().GetPublicKey() is { Length: > 0 })
            {
                byte[]? testPublicKey = typeof(InternalsVisibleToTest).Assembly.GetName().GetPublicKey();
                Assert.NotNull(testPublicKey);
                Assert.NotEmpty(testPublicKey);
                string publicKey = BitConverter.ToString(testPublicKey).Replace("-", "");
                expectedFriends = expectedFriends.Select(name => name + ", PublicKey=" + publicKey).ToArray();
            }

            Assert.Equal(
                expectedFriends.OrderBy(name => name),
                friends.OrderBy(name => name),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
