// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK
using System;
using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    /// <summary>
    /// Tests for Code Access Security (CAS) permission demands in SqlConnection.
    ///
    /// CAS is a .NET Framework security mechanism that restricts what code can do based on
    /// its origin and identity. SqlConnection.Open() calls DemandPermission(), which performs
    /// a stack walk to verify that all callers have been granted SqlClientPermission. If any
    /// caller lacks the permission, a SecurityException is thrown before any network I/O occurs.
    ///
    /// These tests use AppDomain sandboxing to create partial-trust environments with controlled
    /// permission sets, proving that the permission demand is enforced during Open().
    /// </summary>
    public class CasTest
    {
        /// <summary>
        /// Verify that calling Open() in a partial-trust sandbox without SqlClientPermission
        /// causes a SecurityException. Paired with the next test, this experimentally proves
        /// that SqlClientPermission is the specific permission being demanded: the only
        /// difference between the two sandboxes is SqlClientPermission being granted.
        /// </summary>
        [Fact]
        public void Open_WithoutSqlClientPermission_ThrowsSecurityException()
        {
            var permissions = new System.Security.PermissionSet(System.Security.Permissions.PermissionState.None);
            permissions.AddPermission(new System.Security.Permissions.SecurityPermission(
                System.Security.Permissions.SecurityPermissionFlag.Execution));

            var setup = new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
            };

            AppDomain sandbox = AppDomain.CreateDomain("PermissionTest_NoPerm", null, setup, permissions);
            try
            {
                Assert.Throws<System.Security.SecurityException>(() =>
                    sandbox.DoCallBack(OpenConnectionInSandbox));
            }
            finally
            {
                AppDomain.Unload(sandbox);
            }
        }

        /// <summary>
        /// Same sandbox as above, but with a narrow SqlClientPermission granted for the exact
        /// connection string used in OpenConnectionInSandbox. The SecurityException goes away,
        /// proving that the CAS demand is specifically for SqlClientPermission scoped to that
        /// connection string. (Open still fails with a non-security error because we can't connect.)
        /// </summary>
        [Fact]
        public void Open_WithSqlClientPermission_DoesNotThrowSecurityException()
        {
            var permissions = new System.Security.PermissionSet(System.Security.Permissions.PermissionState.None);
            permissions.AddPermission(new System.Security.Permissions.SecurityPermission(
                System.Security.Permissions.SecurityPermissionFlag.Execution));
            var sqlPerm = new SqlClientPermission(System.Security.Permissions.PermissionState.None);
            sqlPerm.Add("Server=will_not_resolve_12345;", "", KeyRestrictionBehavior.AllowOnly);
            permissions.AddPermission(sqlPerm);

            var setup = new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
            };

            AppDomain sandbox = AppDomain.CreateDomain("PermissionTest_WithPerm", null, setup, permissions);
            try
            {
                var ex = Record.Exception(() => sandbox.DoCallBack(OpenConnectionInSandbox));
                Assert.NotNull(ex);
                Assert.IsNotType<System.Security.SecurityException>(ex);
            }
            finally
            {
                AppDomain.Unload(sandbox);
            }
        }

        /// <summary>
        /// Grant a narrow SqlClientPermission for a different server than the one used in Open().
        /// This proves that the permission demand checks the connection string content, not just
        /// whether any SqlClientPermission is present.
        /// </summary>
        [Fact]
        public void Open_WithMismatchedSqlClientPermission_ThrowsSecurityException()
        {
            var permissions = new System.Security.PermissionSet(System.Security.Permissions.PermissionState.None);
            permissions.AddPermission(new System.Security.Permissions.SecurityPermission(
                System.Security.Permissions.SecurityPermissionFlag.Execution));
            var sqlPerm = new SqlClientPermission(System.Security.Permissions.PermissionState.None);
            sqlPerm.Add("Server=some_other_server;", "", KeyRestrictionBehavior.AllowOnly);
            permissions.AddPermission(sqlPerm);

            var setup = new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
            };

            AppDomain sandbox = AppDomain.CreateDomain("PermissionTest_Mismatch", null, setup, permissions);
            try
            {
                var ex = Record.Exception(() => sandbox.DoCallBack(OpenConnectionInSandbox));
                Assert.NotNull(ex);
                // The demand fails because the granted permission ("some_other_server") doesn't
                // cover the demanded permission ("will_not_resolve_12345"). This surfaces as
                // SecurityException or FileLoadException (the CLR encounters a circular assembly
                // load when constructing SecurityException with the custom permission type).
                Assert.True(
                    ex is System.Security.SecurityException || ex is System.IO.FileLoadException,
                    $"Expected security failure, got {ex.GetType().FullName}: {ex.Message}");
            }
            finally
            {
                AppDomain.Unload(sandbox);
            }
        }

        private static void OpenConnectionInSandbox()
        {
            using var connection = new SqlConnection("Server=will_not_resolve_12345;");
            connection.Open();
        }
    }
}
#endif
