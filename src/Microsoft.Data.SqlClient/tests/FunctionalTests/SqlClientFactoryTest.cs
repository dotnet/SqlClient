// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Sql;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlClientFactoryTest
    {
        [Fact]
        public void InstanceTest()
        {
            SqlClientFactory instance = SqlClientFactory.Instance;
            Assert.NotNull(instance);
            Assert.Same(instance, SqlClientFactory.Instance);
        }

        public static readonly object[][] FactoryMethodTestData =
        {
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateCommand), typeof(SqlCommand), false },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateCommandBuilder), typeof(SqlCommandBuilder), false },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateConnection), typeof(SqlConnection), false },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateConnectionStringBuilder), typeof(SqlConnectionStringBuilder), false },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateDataAdapter), typeof(SqlDataAdapter), false },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateParameter), typeof(SqlParameter), false },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateDataSourceEnumerator), typeof(Microsoft.Data.Sql.SqlDataSourceEnumerator), true },
        };

        [Theory]
        [MemberData(
            nameof(FactoryMethodTestData),
            // xUnit can't consistently serialize the data for this test, so we
            // disable enumeration of the test data to avoid warnings on the
            // console.
            DisableDiscoveryEnumeration = true)]
        public void FactoryMethodTest(Func<object> factory, Type expectedType, bool singleton)
        {
            object value1 = factory();
            Assert.NotNull(value1);
            Assert.IsType(expectedType, value1);

            if (!singleton)
            {
                object value2 = factory();
                Assert.NotNull(value2);
                Assert.IsType(expectedType, value2);

                Assert.NotSame(value1, value2);
            }
        }

#if NETFRAMEWORK
        [Fact]
        public void FactoryGetService()
        {
            Type type = typeof(SqlClientFactory);
            MethodInfo method = type.GetMethod("System.IServiceProvider.GetService", BindingFlags.NonPublic | BindingFlags.Instance);
            object res = method.Invoke(SqlClientFactory.Instance, new object[] { null });
            Assert.Null(res);
        }
#endif
    }
}
