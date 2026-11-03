// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Microsoft.Data.ProviderBase;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies manual-test friend access and diagnostic reads without SQL Server configuration.
    /// </summary>
    [Trait("Set", "3")]
    public class InternalsVisibleToTest
    {
        /// <summary>
        /// Guards the direct internal access used by connection diagnostics at compile time and runtime.
        /// </summary>
        [Fact]
        public void ConnectionDiagnostics_CanAccessDriverInternals()
        {
            using SqlConnection connection = new();
            DbConnectionInternal inner = connection.InnerConnection;

            Assert.Equal(ConnectionState.Closed, inner.State);
            Assert.Null(inner.Pool);
            Assert.False(inner.IsTransactionRoot);
        }
    }
}
