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
///   server-side transaction is then reset out from under System.Transactions, corrupting the
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
    /// Exhaustively pins the predicate over all eight combinations of its three boolean inputs.
    ///
    /// The expectations encode two rules:
    /// <list type="number">
    ///   <item>An unpooled connection is never recycled, so nothing is ever preserved.</item>
    ///   <item>A pooled connection tied to a transaction in either way must be preserved.</item>
    /// </list>
    /// </summary>
    [Theory]
    // Not pooled: never preserve, regardless of any other state.
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    // Pooled, no transaction of any kind: nothing to preserve.
    [InlineData(true, false, false, false)]
    // Pooled connection enlisted in someone else's transaction (not the root). This is the
    // issue #2970 case.
    [InlineData(true, false, true, true)]
    // Pooled delegated transaction root with no EnlistedTransaction. This is the transient
    // half-state behind issue #4001: DetachCurrentTransactionIfEnded has already cleared
    // EnlistedTransaction while the delegated transaction still reports itself as active.
    [InlineData(true, true, false, true)]
    // Pooled connection that is both a delegated root and has an EnlistedTransaction. This is
    // the common state immediately after enlistment, since enlistment sets EnlistedTransaction
    // unconditionally.
    [InlineData(true, true, true, true)]
    public void ShouldPreserveTransactionOnReset_CoversBothTransactionOwnershipModes(
        bool isPooled,
        bool isTransactionRoot,
        bool hasEnlistedTransaction,
        bool expected)
    {
        // Act
        bool actual = SqlConnectionInternal.ShouldPreserveTransactionOnReset(
            isPooled: isPooled,
            isTransactionRoot: isTransactionRoot,
            hasEnlistedTransaction: hasEnlistedTransaction);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Regression guard for https://github.com/dotnet/SqlClient/issues/4001.
    ///
    /// A pooled delegated transaction root must preserve its transaction even though
    /// <c>EnlistedTransaction</c> has already been detached. An implementation that keys only off
    /// <c>EnlistedTransaction</c> returns <see langword="false"/> here and corrupts the pooled
    /// connection.
    /// </summary>
    [Fact]
    public void ShouldPreserveTransactionOnReset_DelegatedRootWithoutEnlistedTransaction_IsPreserved()
    {
        Assert.True(SqlConnectionInternal.ShouldPreserveTransactionOnReset(
            isPooled: true,
            isTransactionRoot: true,
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
            hasEnlistedTransaction: true));
    }

    /// <summary>
    /// An unpooled connection is destroyed rather than recycled, so there is no subsequent use to
    /// protect and the transaction must not be preserved.
    /// </summary>
    [Fact]
    public void ShouldPreserveTransactionOnReset_NotPooled_IsNotPreserved()
    {
        Assert.False(SqlConnectionInternal.ShouldPreserveTransactionOnReset(
            isPooled: false,
            isTransactionRoot: true,
            hasEnlistedTransaction: true));
    }
}
