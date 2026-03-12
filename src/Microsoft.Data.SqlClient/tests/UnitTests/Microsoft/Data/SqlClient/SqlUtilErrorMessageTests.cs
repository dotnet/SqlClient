// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class SqlUtilErrorMessageTests
    {
        [Fact]
        public void CannotFindAuthProvider_ReturnsGenericMessage()
        {
            Exception exception = global::Microsoft.Data.SqlClient.SQL.CannotFindAuthProvider("SomeCustomAuth");

            Assert.IsType<ArgumentException>(exception);
            Assert.Contains("SomeCustomAuth", exception.Message);
            Assert.DoesNotContain("Microsoft.Data.SqlClient.Extensions.Azure", exception.Message);
        }

        [Theory]
        [InlineData("ActiveDirectoryPassword")]
        [InlineData("ActiveDirectoryIntegrated")]
        [InlineData("ActiveDirectoryInteractive")]
        [InlineData("ActiveDirectoryServicePrincipal")]
        [InlineData("ActiveDirectoryDeviceCodeFlow")]
        [InlineData("ActiveDirectoryManagedIdentity")]
        [InlineData("ActiveDirectoryMSI")]
        [InlineData("ActiveDirectoryDefault")]
        [InlineData("ActiveDirectoryWorkloadIdentity")]
        public void CannotFindActiveDirectoryAuthProvider_ContainsPackageGuidance(string authMethod)
        {
            Exception exception = global::Microsoft.Data.SqlClient.SQL.CannotFindActiveDirectoryAuthProvider(authMethod);

            Assert.IsType<ArgumentException>(exception);
            Assert.Contains(authMethod, exception.Message);
            Assert.Contains("Microsoft.Data.SqlClient.Extensions.Azure", exception.Message);
        }
    }
}
