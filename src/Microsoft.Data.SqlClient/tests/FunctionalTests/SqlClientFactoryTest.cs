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
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateCommand), typeof(SqlCommand) },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateCommandBuilder), typeof(SqlCommandBuilder) },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateConnection), typeof(SqlConnection) },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateConnectionStringBuilder), typeof(SqlConnectionStringBuilder) },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateDataAdapter), typeof(SqlDataAdapter) },
            new object[] { new Func<object>(SqlClientFactory.Instance.CreateParameter), typeof(SqlParameter) },
        };

        [Theory]
        [MemberData(nameof(FactoryMethodTestData))]
        public void FactoryMethodTest(Func<object> factory, Type expectedType)
        {
            object value1 = factory();
            Assert.NotNull(value1);
            Assert.IsType(expectedType, value1);

            object value2 = factory();
            Assert.NotNull(value2);
            Assert.IsType(expectedType, value2);

            Assert.NotSame(value1, value2);
        }

#if NETFRAMEWORK
        [Fact]
        public void FactoryCreateDataSourceEnumerator()
        {
            // Unable to cover the in the FactoryMethodTest because the SqlDataSourceEnumerator is a singleton so, it's always the same.
            object instance = SqlClientFactory.Instance.CreateDataSourceEnumerator();
            // SqlDataSourceEnumerator is not available for .NET core 3.1 and above, so the type check is only for .NET Framework.
            Assert.IsType<SqlDataSourceEnumerator>(instance);
            Assert.NotNull(instance);
        }

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
