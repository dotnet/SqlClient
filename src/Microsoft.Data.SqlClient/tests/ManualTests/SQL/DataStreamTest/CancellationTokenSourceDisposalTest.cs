// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Regression tests for the CancellationTokenSource cleanup work (PR dotnet/SqlClient#4009).
    /// Disposing a sequential <see cref="Stream"/>/<see cref="TextReader"/> now disposes its
    /// internal disposal CancellationTokenSource. These tests ensure that the subsequent
    /// (and unavoidable) call to SqlSequentialStream/SqlSequentialTextReader.SetClosed() made by
    /// the owning SqlDataReader does not throw ObjectDisposedException, and that repeated
    /// Close()/Dispose() calls remain safe.
    /// </summary>
    [Trait("Set", "2")]
    public class CancellationTokenSourceDisposalTest
    {
        public static IEnumerable<object[]> ConnectionStrings
        {
            get
            {
                foreach (string connectionString in DataTestUtility.GetConnectionStrings(withEnclave: false))
                {
                    yield return new object[] { connectionString };
                }
            }
        }

        // Enumeration is disabled to prevent generating empty test set when connection strings are not setup.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(ConnectionStrings), DisableDiscoveryEnumeration = true)]
        public static void SequentialStream_DisposedBeforeReaderClose_DoesNotThrow(string connectionString)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            using SqlCommand cmd = new SqlCommand(
                "SELECT CAST(REPLICATE(CAST('a' AS VARCHAR(MAX)), 8000) AS VARBINARY(MAX))",
                connection);
            using SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);

            Assert.True(reader.Read());

            Stream stream = reader.GetStream(0);

            // Disposing the stream disposes its internal disposal CancellationTokenSource.
            stream.Dispose();
            // Double-dispose of the stream must be safe.
            stream.Dispose();

            // The reader still references the stream and calls SetClosed() on it during Close().
            // Before the fix this called Cancel() on the disposed CancellationTokenSource and
            // threw ObjectDisposedException.
            reader.Close();
        }

        // Enumeration is disabled to prevent generating empty test set when connection strings are not setup.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(ConnectionStrings), DisableDiscoveryEnumeration = true)]
        public static void SequentialTextReader_DisposedBeforeReaderClose_DoesNotThrow(string connectionString)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            using SqlCommand cmd = new SqlCommand(
                "SELECT CAST(REPLICATE(CAST('a' AS NVARCHAR(MAX)), 4000) AS NVARCHAR(MAX))",
                connection);
            using SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);

            Assert.True(reader.Read());

            TextReader textReader = reader.GetTextReader(0);

            // Disposing the text reader disposes its internal disposal CancellationTokenSource.
            textReader.Dispose();
            // Double-dispose of the text reader must be safe.
            textReader.Dispose();

            // SetClosed() is invoked on the already-disposed text reader during Close();
            // it must not throw ObjectDisposedException.
            reader.Close();
        }

        // Enumeration is disabled to prevent generating empty test set when connection strings are not setup.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(ConnectionStrings), DisableDiscoveryEnumeration = true)]
        public static void SequentialStream_DisposedThenReaderAdvanced_DoesNotThrow(string connectionString)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            // Two result sets so we can advance the reader (which closes the active stream) after disposal.
            using SqlCommand cmd = new SqlCommand(
                "SELECT CAST(REPLICATE(CAST('a' AS VARCHAR(MAX)), 8000) AS VARBINARY(MAX)); SELECT 1",
                connection);
            using SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);

            Assert.True(reader.Read());

            Stream stream = reader.GetStream(0);
            stream.Dispose();

            // Advancing past the result set triggers CloseActiveSequentialStreamAndTextReader(),
            // which calls SetClosed() on the disposed stream. Must not throw.
            Assert.True(reader.NextResult());
            Assert.True(reader.Read());
        }

        // Enumeration is disabled to prevent generating empty test set when connection strings are not setup.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(ConnectionStrings), DisableDiscoveryEnumeration = true)]
        public static void DataReader_MultipleCloseAndDispose_DoesNotThrow(string connectionString)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            using SqlCommand cmd = new SqlCommand("SELECT 1", connection);
            SqlDataReader reader = cmd.ExecuteReader();

            Assert.True(reader.Read());

            // Close()/Dispose() dispose the close-cancellation CancellationTokenSource via an
            // Interlocked swap; repeated calls must be safe (the swapped-out source is null on
            // subsequent calls rather than a disposed instance).
            reader.Close();
            reader.Close();
            reader.Dispose();
            reader.Dispose();
        }
    }
}
