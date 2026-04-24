// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System.Security;
using System.Security.Permissions;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient;

/// <summary>
/// Verifies that each public method on <see cref="SqlConnection"/> demands the correct
/// Code Access Security permissions on .NET Framework.
///
/// These tests override <see cref="SqlConnection.s_staticDemandPermissionSet"/> and
/// <see cref="SqlConnection.s_staticDemandCodeAccessPermission"/> to capture what was
/// demanded without needing a partial-trust AppDomain (which modern test runners do not
/// support). Because these fields are <c>[ThreadStatic]</c>, tests can run in parallel
/// without interfering with each other.
/// </summary>
public class SqlConnectionPermissionTests
{

    /// <summary>
    /// <see cref="SqlConnection.ClearAllPools"/> must demand an unrestricted
    /// <see cref="SqlClientPermission"/>, because it affects all pools regardless of connection
    /// string and cannot scope the demand to a specific target.
    /// </summary>
    [Fact]
    [Trait("Category", "nonnetcoreapptests")]
    public void ClearAllPools_DemandsUnrestrictedSqlClientPermission()
    {
        CodeAccessPermission demanded = null;
        SqlConnection.s_staticDemandCodeAccessPermission = (cap) => demanded = cap;
        try
        {
            SqlConnection.ClearAllPools();
        }
        finally
        {
            SqlConnection.s_staticDemandCodeAccessPermission = null;
        }

        Assert.NotNull(demanded);
        SqlClientPermission sqlPerm = Assert.IsType<SqlClientPermission>(demanded);
        Assert.Equal(PermissionState.Unrestricted, sqlPerm.PermissionState);
    }

    /// <summary>
    /// <see cref="SqlConnection.ClearPool"/> must demand a <see cref="PermissionSet"/> scoped
    /// to the connection's specific connection string, so only callers permitted to connect to
    /// that server can clear its pool.
    /// </summary>
    [Fact]
    [Trait("Category", "nonnetcoreapptests")]
    public void ClearPool_DemandsConnectionStringScopedPermission()
    {
        PermissionSet demanded = null;
        SqlConnection.s_staticDemandPermissionSet = (ps) => demanded = ps;
        try
        {
            using SqlConnection connection = new("Data Source=server;Integrated Security=true;");
            SqlConnection.ClearPool(connection);
        }
        finally
        {
            SqlConnection.s_staticDemandPermissionSet = null;
        }

        Assert.NotNull(demanded);
        Assert.NotNull(demanded.GetPermission(typeof(SqlClientPermission)));
    }

    /// <summary>
    /// <see cref="SqlConnection.EnlistDistributedTransaction"/> must demand both
    /// <see cref="SqlClientPermission"/> (execute) and
    /// <see cref="SecurityPermission"/>(<see cref="SecurityPermissionFlag.UnmanagedCode"/>),
    /// because it bridges to MSDTC via COM interop.
    /// </summary>
    [Fact]
    [Trait("Category", "nonnetcoreapptests")]
    public void EnlistDistributedTransaction_DemandsSqlClientAndUnmanagedCodePermissions()
    {
        PermissionSet demanded = null;
        SqlConnection.s_staticDemandPermissionSet = (ps) => demanded = ps;
        try
        {
            using SqlConnection connection = new("Data Source=server;Integrated Security=true;");
            // Downstream failure (connection not open) is expected.
            try { connection.EnlistDistributedTransaction(null); } catch { /* expected */ }
        }
        finally
        {
            SqlConnection.s_staticDemandPermissionSet = null;
        }

        Assert.NotNull(demanded);
        Assert.NotNull(demanded.GetPermission(typeof(SqlClientPermission)));
        SecurityPermission secPerm = (SecurityPermission)demanded.GetPermission(typeof(SecurityPermission));
        Assert.NotNull(secPerm);
        Assert.True(secPerm.Flags.HasFlag(SecurityPermissionFlag.UnmanagedCode));
    }

    /// <summary>
    /// <see cref="SqlConnection.EnlistTransaction"/> must demand
    /// <see cref="SqlConnection.ExecutePermission"/> (a blanket SQL-execute check). The
    /// connection-string-scoped permission was already validated at <see cref="SqlConnection.Open"/>
    /// time, so only execute permission is re-checked here.
    /// </summary>
    [Fact]
    [Trait("Category", "nonnetcoreapptests")]
    public void EnlistTransaction_DemandsExecutePermission()
    {
        CodeAccessPermission demanded = null;
        SqlConnection.s_staticDemandCodeAccessPermission = (cap) => demanded = cap;
        try
        {
            using SqlConnection connection = new("Data Source=server;Integrated Security=true;");
            // Permission demand fires before the connection-open check.
            try { connection.EnlistTransaction(null); } catch { /* expected */ }
        }
        finally
        {
            SqlConnection.s_staticDemandCodeAccessPermission = null;
        }

        Assert.NotNull(demanded);
        Assert.Same(SqlConnection.ExecutePermission, demanded);
    }

    /// <summary>
    /// <see cref="SqlConnection.GetSchema(string, string[])"/> must demand
    /// <see cref="SqlConnection.ExecutePermission"/>, treating schema retrieval the same as
    /// query execution against an already-open connection.
    /// </summary>
    [Fact]
    [Trait("Category", "nonnetcoreapptests")]
    public void GetSchema_DemandsExecutePermission()
    {
        CodeAccessPermission demanded = null;
        SqlConnection.s_staticDemandCodeAccessPermission = (cap) => demanded = cap;
        try
        {
            using SqlConnection connection = new("Data Source=server;Integrated Security=true;");
            // Permission demand fires before the connection-open check.
            try { connection.GetSchema("Tables"); } catch { /* expected */ }
        }
        finally
        {
            SqlConnection.s_staticDemandCodeAccessPermission = null;
        }

        Assert.NotNull(demanded);
        Assert.Same(SqlConnection.ExecutePermission, demanded);
    }

    /// <summary>
    /// <see cref="SqlConnection.ChangePassword(string, string)"/> must demand a
    /// <see cref="PermissionSet"/> scoped to the supplied connection string. There is no prior
    /// <see cref="SqlConnection.Open"/> call to have performed the demand, so it must be done here.
    /// </summary>
    [Fact]
    [Trait("Category", "nonnetcoreapptests")]
    public void ChangePassword_String_DemandsConnectionStringScopedPermission()
    {
        PermissionSet demanded = null;
        SqlConnection.s_staticDemandPermissionSet = (ps) => demanded = ps;
        try
        {
            // Permission demand fires before the actual connection attempt.
            try { SqlConnection.ChangePassword("Data Source=server;Integrated Security=true;", "newPass"); }
            catch { /* expected - no server */ }
        }
        finally
        {
            SqlConnection.s_staticDemandPermissionSet = null;
        }

        Assert.NotNull(demanded);
        Assert.NotNull(demanded.GetPermission(typeof(SqlClientPermission)));
    }

    /// <summary>
    /// <see cref="SqlConnection.ChangePassword(string, SqlCredential, SecureString)"/> must
    /// demand a <see cref="PermissionSet"/> scoped to the supplied connection string.
    /// </summary>
    [Fact]
    [Trait("Category", "nonnetcoreapptests")]
    public void ChangePassword_Credential_DemandsConnectionStringScopedPermission()
    {
        PermissionSet demanded = null;
        SqlConnection.s_staticDemandPermissionSet = (ps) => demanded = ps;
        try
        {
            SecureString securePassword = new();
            securePassword.AppendChar('x');
            securePassword.MakeReadOnly();
            SqlCredential credential = new("user", securePassword);

            // Permission demand fires before the actual connection attempt.
            try { SqlConnection.ChangePassword("Data Source=server;", credential, securePassword); }
            catch { /* expected - no server */ }
        }
        finally
        {
            SqlConnection.s_staticDemandPermissionSet = null;
        }

        Assert.NotNull(demanded);
        Assert.NotNull(demanded.GetPermission(typeof(SqlClientPermission)));
    }

    /// <summary>
    /// <see cref="SqlConnection.Open"/> must demand a <see cref="PermissionSet"/> scoped to the
    /// user's original connection string via <see cref="SqlConnection.PermissionDemand"/> before
    /// establishing the physical connection.
    /// </summary>
    [Fact]
    [Trait("Category", "nonnetcoreapptests")]
    public void Open_DemandsConnectionStringScopedPermission()
    {
        PermissionSet demanded = null;
        SqlConnection.s_staticDemandPermissionSet = (ps) => demanded = ps;
        try
        {
            using SqlConnection connection = new("Data Source=server;Integrated Security=true;");
            // PermissionDemand fires before the network connection attempt.
            try { connection.Open(); } catch { /* expected - no server */ }
        }
        finally
        {
            SqlConnection.s_staticDemandPermissionSet = null;
        }

        Assert.NotNull(demanded);
        Assert.NotNull(demanded.GetPermission(typeof(SqlClientPermission)));
    }
}

#endif
