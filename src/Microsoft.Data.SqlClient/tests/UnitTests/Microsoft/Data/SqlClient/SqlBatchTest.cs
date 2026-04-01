// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Provides unit tests for verifying the behavior of the SqlBatch class.
/// </summary>
public class SqlBatchTest
{
    /// <summary>
    /// Verifies that SqlBatch.ValidateCommandBehavior throws an ArgumentOutOfRangeException when an invalid CommandBehavior is specified.
    /// </summary>
    [Fact]
    public void InvalidCommandBehaviorValidateCommandBehavior_Throws()
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => SqlBatch.ValidateCommandBehavior("ExecuteNonQuery", (CommandBehavior)64));
        Assert.Contains("CommandBehavior", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that SqlBatch.ValidateCommandBehavior throws an ArgumentOutOfRangeException when a valid but unsupported CommandBehavior is specified.
    /// </summary>
    [Fact]
    public void NotSupportedCommandBehaviorValidateCommandBehavior_Throws()
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => SqlBatch.ValidateCommandBehavior("ExecuteNonQuery", CommandBehavior.KeyInfo));
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

#endif
