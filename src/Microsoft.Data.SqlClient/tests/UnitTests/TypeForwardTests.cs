// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class TypeForwardTests
    {
        private static readonly Assembly s_sqlClientAssembly = typeof(SqlConnection).Assembly;

        [Theory]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationMethod", true)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationParameters", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationProvider", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationProviderException", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationToken", false)]
        public void AbstractionsType_CanBeLoadedFromSqlClientAssembly(string typeName, bool isEnum)
        {
            // Types moved to the Abstractions assembly must remain loadable via the
            // Microsoft.Data.SqlClient assembly for backward compatibility.
            Type? type = s_sqlClientAssembly.GetType(typeName, throwOnError: true);

            Assert.NotNull(type);
            Assert.Equal(isEnum, type.IsEnum);

            // Assert that the assembly containing the type is the Abstractions assembly.
            Assert.Equal("Microsoft.Data.SqlClient.Extensions.Abstractions", type.Assembly.GetName().Name);
        }
    }
}
