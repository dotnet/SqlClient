// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Tests.Common;
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
        /// Compares assembly names for equality, ignoring case.
        /// </summary>
        private sealed class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public bool Equals(AssemblyName? x, AssemblyName? y)
            {
                return ReferenceEquals(x, y) ||
                        (x is not null &&
                        y is not null &&
                        StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name));
            }

            public int GetHashCode(AssemblyName obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty);
            }
        }

        /// <summary>
        /// Ensures only the expected test assemblies receive internal access with the expected signing keys.
        /// </summary>
        [Fact]
        public void FriendAssembliesMatchExpectedList()
        {
            // The SqlClient assembly.
            var sqlClientAssembly = typeof(SqlConnection).Assembly;

            // Is the SqlClient assembly signed?
            bool isSigned = sqlClientAssembly.GetName().GetPublicKey() is { Length: > 0 };

            // The IVT friend assemblies we expect to find listed on the SqlClient assembly.
            HashSet<AssemblyName> expectedFriends = new(new AssemblyNameComparer());

            var testCommonAssembly = typeof(LocalAppContextSwitchesHelper).Assembly;
            Assert.Equal("Microsoft.Data.SqlClient.TestCommon", testCommonAssembly.GetName().Name);
            expectedFriends.Add(testCommonAssembly.GetName());

            var unitTestAssembly = typeof(InternalsVisibleToTest).Assembly;
            Assert.Equal("UnitTests", unitTestAssembly.GetName().Name);
            expectedFriends.Add(unitTestAssembly.GetName());

            AssemblyName manualTestAssembly = unitTestAssembly.GetName();
            manualTestAssembly.Name = "ManualTests";
            expectedFriends.Add(manualTestAssembly);

            // Inspect the list of friend assemblies declared on the SqlClient assembly.
            foreach (var actualFriend in
                     sqlClientAssembly.GetCustomAttributes<InternalsVisibleToAttribute>())
            {
                AssemblyName actualAssembly = new(actualFriend.AssemblyName);
                // net462 has no API to find and retrieve a HashSet entry, so we use Assert.Single
                // with a predicate to locate it.
                AssemblyName expectedAssembly = Assert.Single(
                    expectedFriends,
                    candidate => expectedFriends.Comparer.Equals(candidate, actualAssembly));
                expectedFriends.Remove(actualAssembly);

                if (isSigned)
                {
                    byte[]? publicKey = expectedAssembly.GetPublicKey();
                    Assert.NotNull(publicKey);
                    Assert.NotEmpty(publicKey);
                    Assert.Equal(publicKey, actualAssembly.GetPublicKey());
                }
            }

            Assert.Empty(expectedFriends);
        }
    }
}
