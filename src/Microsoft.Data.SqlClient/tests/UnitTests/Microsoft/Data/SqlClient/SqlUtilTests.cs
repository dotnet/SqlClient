// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>Verifies authentication error types and actionable provider guidance.</summary>
    public class SqlUtilTests
    {
        /// <summary>
        /// Missing Entra providers distinguish publish-time removal from supported trimmed JIT discovery.
        /// </summary>
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
            // Act
            Exception exception = SQL.CannotFindAuthProvider(authMethod, internalConnection: null);

            // Assert
            var sqlException = Assert.IsType<SqlException>(exception);
            Assert.Single(sqlException.Errors);
            Assert.Null(sqlException.InnerException);
            Assert.Contains(authMethod.ToString(), exception.Message);
            Assert.Contains("Microsoft.Data.SqlClient.Extensions.Azure", exception.Message);
            Assert.Contains("unavailable when disabled at publish time", exception.Message);
            Assert.Contains("Trimmed JIT applications require preserved reflection contracts",
                exception.Message);
            Assert.Contains("NativeAOT applications should disable reflective bootstrap paths",
                exception.Message);
            Assert.DoesNotContain("unavailable in trimmed", exception.Message);
        }

        [Theory]
        [InlineData(SqlAuthenticationMethod.NotSpecified)]
        [InlineData(SqlAuthenticationMethod.SqlPassword)]
        public void CannotFindAuthProvider_NonActiveDirectoryMethod_DoesNotContainPackageGuidance(SqlAuthenticationMethod authMethod)
        {
            Exception exception = SQL.CannotFindAuthProvider(authMethod, internalConnection: null);

            var sqlException = Assert.IsType<SqlException>(exception);
            Assert.Single(sqlException.Errors);
            Assert.Null(sqlException.InnerException);
            Assert.Contains(authMethod.ToString(), exception.Message);
            Assert.DoesNotContain("Microsoft.Data.SqlClient.Extensions.Azure", exception.Message);
        }
    }
}
