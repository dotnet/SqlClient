// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests;

public class SilentTraceListenerTests
{
    [Theory]
    [InlineData("Assertion message")]
    [InlineData("Value {0}")]
    [InlineData("")]
    public void WriteLine_Message_ForwardsUnformattedMessage(string message)
    {
        using var listener = new RecordingTraceListener();

        listener.WriteLine(message);

        Assert.Equal(message, listener.CapturedMessage);
    }

    private sealed class RecordingTraceListener : Utility.SilentTraceListener
    {
        public string CapturedMessage { get; private set; } = string.Empty;

        public override void Write(string message)
        {
            CapturedMessage = message;
        }
    }
}
