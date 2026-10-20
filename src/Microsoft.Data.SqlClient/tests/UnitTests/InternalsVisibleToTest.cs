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
        /// Ensures only UnitTests and ManualTests receive internal access, with their matching keys for signed builds.
        /// </summary>
        [Fact]
        public void FriendAssembliesMatchExpectedList()
        {
            var driverAssembly = typeof(SqlConnection).Assembly;
            var friends = driverAssembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(attribute => attribute.AssemblyName);

            string[] expectedFriends = { "UnitTests", "ManualTests" };
            if (driverAssembly.GetName().GetPublicKey() is { Length: > 0 })
            {
                byte[]? testPublicKey = typeof(InternalsVisibleToTest).Assembly.GetName().GetPublicKey();
                Assert.NotNull(testPublicKey);
                Assert.NotEmpty(testPublicKey);
                expectedFriends = new[]
                {
                    "UnitTests, PublicKey=" + BitConverter.ToString(testPublicKey).Replace("-", ""),
                    "ManualTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010047E9879A6FC243E13F6E33C67B00723CE2CEA1C00ECB8FF1DBF62F5BC60F1A96046E56243238ACE538A06EF320B31D9DAB7A45833F00CDE017418519B0891D36D9E019A88BD9BCA0857DED2BC34BD3270079DFCA6048900739E188A00A0FE8FFDB412EF0482439DC0B514A3D8E9F8D4719B007C82225A53E39BBB06BAB01E7CE"
                };
            }

            Assert.Equal(
                expectedFriends.OrderBy(name => name),
                friends.OrderBy(name => name),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
