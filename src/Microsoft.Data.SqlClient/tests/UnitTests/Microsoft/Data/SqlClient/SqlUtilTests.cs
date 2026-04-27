// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class SqlUtilTests
    {
        [Theory]
#pragma warning disable 0618
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryPassword)]
#pragma warning restore 0618
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryIntegrated)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryMSI)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryDefault)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)]
        public void CannotFindAuthProvider_ActiveDirectoryMethod_ContainsPackageGuidance(SqlAuthenticationMethod authMethod)
        {
            Exception exception = SQL.CannotFindAuthProvider(authMethod);

            Assert.IsType<ArgumentException>(exception);
            Assert.Contains(authMethod.ToString(), exception.Message);
            Assert.Contains("Microsoft.Data.SqlClient.Extensions.Azure", exception.Message);
        }

        [Theory]
        [InlineData(SqlAuthenticationMethod.NotSpecified)]
        [InlineData(SqlAuthenticationMethod.SqlPassword)]
        public void CannotFindAuthProvider_NonActiveDirectoryMethod_DoesNotContainPackageGuidance(SqlAuthenticationMethod authMethod)
        {
            Exception exception = SQL.CannotFindAuthProvider(authMethod);

            Assert.IsType<ArgumentException>(exception);
            Assert.Contains(authMethod.ToString(), exception.Message);
            Assert.DoesNotContain("Microsoft.Data.SqlClient.Extensions.Azure", exception.Message);
        }
    }
}
