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
        /// Ensures only UnitTests and TestCommon receive internal access, with their public key for signed builds.
        /// </summary>
        [Fact]
        public void FriendAssembliesMatchExpectedList()
        {
            var driverAssembly = typeof(SqlConnection).Assembly;
            var friends = driverAssembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(attribute => attribute.AssemblyName);

            string publicKeySuffix = string.Empty;
            if (driverAssembly.GetName().GetPublicKey() is { Length: > 0 })
            {
                byte[]? testPublicKey = typeof(InternalsVisibleToTest).Assembly.GetName().GetPublicKey();
                Assert.NotNull(testPublicKey);
                Assert.NotEmpty(testPublicKey);
                publicKeySuffix = ", PublicKey=" + BitConverter.ToString(testPublicKey).Replace("-", "");
            }

            string[] expectedFriends =
            [
                "UnitTests" + publicKeySuffix,
                "Microsoft.Data.SqlClient.TestCommon" + publicKeySuffix
            ];

            Assert.Equal(
                expectedFriends.OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
                friends.OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
