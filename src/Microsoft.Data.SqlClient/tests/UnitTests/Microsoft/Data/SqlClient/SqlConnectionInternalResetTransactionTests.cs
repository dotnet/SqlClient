// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Connection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies <see cref="SqlConnectionInternal.ShouldPreserveTransactionOnReset"/>, which decides
/// whether a pooled connection is reset with <c>ST_RESET_CONNECTION_PRESERVE_TRANSACTION</c>
/// rather than a plain <c>ST_RESET_CONNECTION</c>.
///
/// Getting this predicate wrong has caused two separate regressions, in opposite directions:
/// <list type="bullet">
///   <item>
///   Testing only <c>IsTransactionRoot</c> misses connections that merely enlisted in someone
///   else's transaction, so they are reset and subsequently run in auto-commit mode
///   (https://github.com/dotnet/SqlClient/issues/2970).
///   </item>
///   <item>
///   Testing only <c>EnlistedTransaction</c> misses delegated transaction roots, whose
///   server side transaction is then reset out from under System.Transactions, corrupting the
///   connection as it is recycled through the pool
///   (https://github.com/dotnet/SqlClient/issues/4001).
///   </item>
/// </list>
///
/// Both conditions must therefore be honored. Asserting against the extracted predicate covers
/// every combination deterministically, including state combinations that are transient and
/// racy to stage against a live server.
/// </summary>
public class SqlConnectionInternalResetTransactionTests
{
    /// <summary>
    /// Exhaustively pins the predicate over all sixteen combinations of its four boolean inputs.
    ///
    /// The expectations encode three rules:
    /// <list type="number">
    ///   <item>An unpooled connection is never recycled, so nothing is ever preserved.</item>
    ///   <item>A delegated transaction root must be preserved, but only on SQL Server 2008 or
    ///   newer; older servers cannot carry a delegated transaction across a reset.</item>
    ///   <item>An enlisted transaction must always be preserved, independent of server version
    ///   and independent of whether this connection is also the root.</item>
    /// </list>
    /// </summary>
    [Theory]
    // Not pooled: never preserve, regardless of any other state. All eight combinations of the
    // remaining inputs are enumerated so that rule 1 is pinned unconditionally.
    [InlineData(false, false, false, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, true, true, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, true, true, false, false)]
    [InlineData(false, true, true, true, false)]
    // Pooled, no transaction of any kind: nothing to preserve.
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, false, false)]
    // Pooled delegated transaction root with no EnlistedTransaction. This is the transient
    // half-state behind issue #4001: DetachCurrentTransactionIfEnded has already cleared
    // EnlistedTransaction while the delegated transaction still reports itself as active.
    // Preserving requires a 2008+ server.
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, false, false, false)]
    // Pooled connection enlisted in someone else's transaction (not the root). This is the
    // issue #2970 case, and it must be preserved even on pre-2008 servers.
    [InlineData(true, false, true, true, true)]
    [InlineData(true, false, false, true, true)]
    // Pooled connection that is both a delegated root and has an EnlistedTransaction. This is
    // the common state immediately after enlistment, since enlistment sets EnlistedTransaction
    // unconditionally.
    [InlineData(true, true, true, true, true)]
    [InlineData(true, true, false, true, true)]
    public void ShouldPreserveTransactionOnReset_CoversBothTransactionOwnershipModes(
        bool isPooled,
        bool isTransactionRoot,
        bool is2008OrNewer,
        bool hasEnlistedTransaction,
        bool expected)
    {
        // Act
        bool actual = SqlConnectionInternal.ShouldPreserveTransactionOnReset(
            isPooled: isPooled,
            isTransactionRoot: isTransactionRoot,
            is2008OrNewer: is2008OrNewer,
            hasEnlistedTransaction: hasEnlistedTransaction);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Regression guard for https://github.com/dotnet/SqlClient/issues/4001.
    ///
    /// A pooled delegated transaction root on a modern server must preserve its transaction even
    /// though <c>EnlistedTransaction</c> has already been detached. An implementation that keys
    /// only off <c>EnlistedTransaction</c> returns <see langword="false"/> here and corrupts the
    /// pooled connection.
    /// </summary>
    [Fact]
    public void ShouldPreserveTransactionOnReset_DelegatedRootWithoutEnlistedTransaction_IsPreserved()
    {
        Assert.True(SqlConnectionInternal.ShouldPreserveTransactionOnReset(
            isPooled: true,
            isTransactionRoot: true,
            is2008OrNewer: true,
            hasEnlistedTransaction: false));
    }

    /// <summary>
    /// Regression guard for https://github.com/dotnet/SqlClient/issues/2970.
    ///
    /// A pooled connection that enlisted in a transaction it does not own must preserve that
    /// transaction. An implementation that keys only off <c>IsTransactionRoot</c> returns
    /// <see langword="false"/> here, and the connection silently continues in auto-commit mode.
    /// </summary>
    [Fact]
    public void ShouldPreserveTransactionOnReset_EnlistedNonRoot_IsPreserved()
    {
        Assert.True(SqlConnectionInternal.ShouldPreserveTransactionOnReset(
            isPooled: true,
            isTransactionRoot: false,
            is2008OrNewer: true,
            hasEnlistedTransaction: true));
    }

    /// <summary>
    /// A delegated transaction root cannot be preserved on a pre-2008 server, so such a
    /// connection must be reset normally rather than recycled with a transaction attached.
    /// This guard existed before https://github.com/dotnet/SqlClient/pull/3019 and is retained.
    /// </summary>
    [Fact]
    public void ShouldPreserveTransactionOnReset_DelegatedRootOnLegacyServer_IsNotPreserved()
    {
        Assert.False(SqlConnectionInternal.ShouldPreserveTransactionOnReset(
            isPooled: true,
            isTransactionRoot: true,
            is2008OrNewer: false,
            hasEnlistedTransaction: false));
    }
}
