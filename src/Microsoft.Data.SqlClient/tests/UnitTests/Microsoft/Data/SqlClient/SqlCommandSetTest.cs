// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Provides unit tests for verifying the behavior of the SqlCommandSet class.
/// </summary>
public class SqlCommandSetTest
{
    /// <summary>
    /// Verifies that key properties throw an ObjectDisposedException after the SqlCommandSet has been disposed.
    /// </summary>
    /// <remarks>
    /// These properties are private, requiring reflection to access.
    /// </remarks>
    [Theory]
    [InlineData("BatchCommand")]
    [InlineData("CommandList")]
    public void GetDisposedProperty_Throws(string propertyName)
    {
        SqlCommandSet cmdSet = new();
        cmdSet.Dispose();

        ObjectDisposedException ex = GetProperty_Throws<ObjectDisposedException>(cmdSet, propertyName);
        Assert.Contains("disposed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that adding a SqlCommand with an empty CommandText to a SqlCommandSet throws an InvalidOperationException.
    /// </summary>
    [Fact]
    public void AppendCommandWithEmptyString_Throws()
    {
        SqlCommandSet cmdSet = new();
        using SqlCommand cmd = new("");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmdSet.Append(cmd));
        Assert.Contains("CommandText property has not been initialized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a set of invalid CommandType values.
    /// </summary>
    /// <see cref="AppendBadCommandType_Throws(CommandType)"/>
    /// <remarks>
    /// .NET Framework puts system enums in the Global Assembly Cache (GAC), and xUnit refuses to serialize enums that live there.
    /// We make these system enum values a method, then disable enumeration of the test data to avoid warnings on the console when running tests.
    /// </remarks>
    public static TheoryData<CommandType> CommandTypeData()
        => new(CommandType.TableDirect, (CommandType)5);

    /// <summary>
    /// Verifies that adding a SqlCommand with an invalid CommandType to a SqlCommandSet throws an ArgumentOutOfRangeException.
    /// </summary>
    [Theory]
    [MemberData(
        nameof(CommandTypeData)
#if NETFRAMEWORK
        , DisableDiscoveryEnumeration = true
#endif
        )]
    public void AppendBadCommandType_Throws(CommandType commandType)
    {
        SqlCommandSet cmdSet = new();
        using SqlCommand cmd = GenerateBadCommand(commandType);

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => cmdSet.Append(cmd));
        Assert.Contains("CommandType", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that adding a SqlCommand containing a SqlParameter with an invalid name to a SqlCommandSet throws an ArgumentException.
    /// </summary>
    [Fact]
    public void AppendBadParameterName_Throws()
    {
        SqlCommandSet cmdSet = new();
        using SqlCommand cmd = new("Test");
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.Add(new SqlParameter("Test1;=", "1"));

        ArgumentException ex = Assert.Throws<ArgumentException>(() => cmdSet.Append(cmd));
        Assert.Contains("not valid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a SqlParameter containing an array round-trips through a SqlCommandSet correctly.
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 1, 2, 3 })]
    [InlineData(new char[] { '1', '2', '3' })]
    public void AppendParameterArrayWithSize(object array)
    {
        SqlCommandSet cmdSet = new();
        using SqlCommand cmd = new("Test");
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Add(new SqlParameter("@array", array) { Size = 2 });
        cmdSet.Append(cmd);
        SqlParameter result = cmdSet.GetParameter(0, 0);
        Assert.NotNull(result);
        Assert.Equal("@array", result.ParameterName);
        Assert.Equal(2, result.Size);
    }

    /// <summary>
    /// Verifies that a SqlParameter containing a scalar value round-trips through a SqlCommandSet correctly.
    /// </summary>
    [Fact]
    public void GetParameter()
    {
        SqlCommandSet cmdSet = new();
        using SqlCommand cmd = new("Test");
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.Add(new SqlParameter("@text", "value"));
        cmdSet.Append(cmd);
        SqlParameter result = cmdSet.GetParameter(0, 0);
        Assert.NotNull(result);
        Assert.Equal("@text", result.ParameterName);
        Assert.Equal("value", (string)result.Value);
    }

    /// <summary>
    /// Verifies that SqlCommandSet.GetParameterCount returns the correct number of parameters for a command
    /// at the correct index.
    /// </summary>
    [Fact]
    public void GetParameterCount()
    {
        SqlCommandSet cmdSet = new();
        using SqlCommand cmd = new("Test");
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.Add(new SqlParameter("@abc", "1"));
        cmd.Parameters.Add(new SqlParameter("@test", "2"));
        cmdSet.Append(cmd);
        int index = 0;
        int count = cmdSet.GetParameterCount(index);
        Assert.Equal(2, count);
    }

    /// <summary>
    /// Verifies that SqlCommandSet.ValidateCommandBehavior throws an ArgumentOutOfRangeException when an invalid CommandBehavior is specified.
    /// </summary>
    [Fact]
    public void InvalidCommandBehaviorValidateCommandBehavior_Throws()
    {
        SqlCommandSet cmdSet = new();

        ArgumentOutOfRangeException ex = InvokeMethod_Throws<ArgumentOutOfRangeException>(cmdSet, "ValidateCommandBehavior", "ExecuteNonQuery", (CommandBehavior)64);
        Assert.Contains("CommandBehavior", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that SqlCommandSet.ValidateCommandBehavior throws an ArgumentOutOfRangeException when a valid but unsupported CommandBehavior is specified.
    /// </summary>
    [Fact]
    public void NotSupportedCommandBehaviorValidateCommandBehavior_Throws()
    {
        SqlCommandSet cmdSet = new();

        ArgumentOutOfRangeException ex = InvokeMethod_Throws<ArgumentOutOfRangeException>(cmdSet, "ValidateCommandBehavior", "ExecuteNonQuery", CommandBehavior.KeyInfo);
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #region private methods

    private static T GetProperty_Throws<T>(SqlCommandSet instance, string propertyName)
        where T : Exception
        => InvokeMethod_Throws<T>(instance,
            typeof(SqlCommandSet)
                .GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetGetMethod(true),
            []);

    private static T InvokeMethod_Throws<T>(SqlCommandSet instance, string methodName, params object[] values)
        where T : Exception
        => InvokeMethod_Throws<T>(instance,
            typeof(SqlCommandSet)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance),
            values);

    private static T InvokeMethod_Throws<T>(SqlCommandSet instance, MethodInfo methodInfo, params object[] values)
        where T : Exception
    {
        return Assert.Throws<T>(() =>
        {
            try
            {
                methodInfo.Invoke(instance, values);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        });
    }

    private static SqlCommand GenerateBadCommand(CommandType cType)
    {
        SqlCommand cmd = new("Test");
        // There's validation done on the CommandType property, but we need to create one that avoids the check for the test case.
        typeof(SqlCommand).GetField("_commandType", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cmd, cType);

        return cmd;
    }
    #endregion
}
