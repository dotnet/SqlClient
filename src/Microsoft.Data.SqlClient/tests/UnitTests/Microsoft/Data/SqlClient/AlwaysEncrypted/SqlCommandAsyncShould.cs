// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Guards cleanup of the caller's key-store cancellation token when async command setup fails.
    /// </summary>
    public class SqlCommandAsyncShould
    {
        /// <summary>
        /// Missing or closed connections must fault execution without retaining the caller's token,
        /// including when setup fails before the normal cleanup continuation is registered.
        /// </summary>
        [Theory]
        [InlineData("Reader", false)]
        [InlineData("Reader", true)]
        [InlineData("NonQuery", false)]
        [InlineData("NonQuery", true)]
        [InlineData("XmlReader", false)]
        [InlineData("XmlReader", true)]
        public async Task FailedSetupClearsCancellationToken(string execution, bool hasConnection)
        {
            using SqlConnection connection = new();
            using SqlCommand command = new("SELECT 1", hasConnection ? connection : null);
            using CancellationTokenSource source = new();

            await Assert.ThrowsAsync<InvalidOperationException>(() => execution switch
            {
                "Reader" => (Task)command.ExecuteReaderAsync(source.Token),
                "NonQuery" => command.ExecuteNonQueryAsync(source.Token),
                "XmlReader" => command.ExecuteXmlReaderAsync(source.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(execution)),
            });

            FieldInfo tokenField = typeof(SqlCommand).GetField(
                "_asyncExecutionCancellationToken", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Execution cancellation token field not found.");
            Assert.Equal(CancellationToken.None, Assert.IsType<CancellationToken>(tokenField.GetValue(command)));
        }
    }
}
