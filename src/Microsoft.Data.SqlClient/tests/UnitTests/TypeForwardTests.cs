// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>Verifies relocated public types remain resolvable through the driver assembly.</summary>
    public class TypeForwardTests
    {
        private static readonly Assembly s_sqlClientAssembly = typeof(SqlConnection).Assembly;

        /// <summary>Type forwards preserve resolution through the original assembly name.</summary>
        [Theory]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationMethod", true)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationInitializer", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationParameters", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationProvider", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationProviderException", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationToken", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection", false)]
        [InlineData("Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection", false)]
        public void AbstractionsType_CanBeLoadedFromSqlClientAssembly(string typeName, bool isEnum)
        {
            // Types moved to the Abstractions assembly must remain loadable via the
            // Microsoft.Data.SqlClient assembly for backward compatibility.
            Type? type = s_sqlClientAssembly.GetType(typeName, throwOnError: true);

            Assert.NotNull(type);
            Assert.Equal(isEnum, type.IsEnum);
            Assert.True(type.IsPublic);
            Assert.Same(type, Type.GetType(typeName + ", Microsoft.Data.SqlClient", throwOnError: true));

            // Assert that the assembly containing the type is the Abstractions assembly.
            Assert.Equal("Microsoft.Data.SqlClient.Extensions.Abstractions", type.Assembly.GetName().Name);
        }
    }
}
