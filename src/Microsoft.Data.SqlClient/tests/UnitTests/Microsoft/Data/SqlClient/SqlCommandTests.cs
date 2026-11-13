// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.Data.SqlClient.Parser.Tokens;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Verifies preparation response handling in SqlCommand.</summary>
public class SqlCommandTests
{
    /// <summary>A returned handle commits the capacities belonging to that preparation.</summary>
    [Fact]
    public void OnReturnValue_PrepareHandle_CommitsPreparedCapacity()
    {
        // Arrange
        using SqlCommand command = new();
        int[] pendingCapacities = { 8000 };
        typeof(SqlCommand).GetField("_pendingParameterByteCapacities",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(command, pendingCapacities);
        typeof(SqlCommand).GetField("_inPrepare",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(command, true);
        TdsReturnValueToken token = new();
        token.Value.Int32 = 123;

        // Act
        command.OnReturnValue(token, null!);

        // Assert
        Assert.False(command.InPrepare);
        Assert.Same(pendingCapacities, typeof(SqlCommand).GetField(
            "_preparedParameterByteCapacities", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(command));
        Assert.Equal(123, typeof(SqlCommand).GetField("_prepareHandle",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(command));
    }

    /// <summary>
    /// A null handle leaves no committed preparation capacity after a failed prepare.
    /// </summary>
    [Fact]
    public void OnReturnValue_NullPrepareHandle_DoesNotCommitPreparedCapacity()
    {
        // Arrange
        using SqlCommand command = new();
        typeof(SqlCommand).GetField("_pendingParameterByteCapacities",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(command, new[] { 8000 });
        typeof(SqlCommand).GetField("_inPrepare",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(command, true);
        TdsReturnValueToken token = new();
        token.Value.SetToNullOfType(SqlBuffer.StorageType.Int32);

        // Act
        command.OnReturnValue(token, null!);

        // Assert
        Assert.False(command.InPrepare);
        Assert.Null(typeof(SqlCommand).GetField("_preparedParameterByteCapacities",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(command));
    }
}
