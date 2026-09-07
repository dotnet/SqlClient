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
    // An unpooled connection is destroyed rather than recycled, so there is no subsequent use to
    // protect and its transaction must not be preserved, regardless of any other state.
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    // Pooled, no transaction of any kind: nothing to preserve.
    [InlineData(true, false, false, false)]
    // Regression guard for #2970: an implementation keyed only on IsTransactionRoot returns false
    // for a pooled connection enlisted in a transaction it does not own.
    [InlineData(true, false, true, true)]
    // Regression guard for #4001: an implementation keyed only on EnlistedTransaction returns
    // false in this transient half-state, after the enlistment is detached but while the delegated
    // transaction still reports itself as active.
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

}
