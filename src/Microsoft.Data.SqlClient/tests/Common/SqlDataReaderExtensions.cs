// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.Tests.Common
{
    /// <summary>
    /// Extensions on the <see cref="SqlDataReader"/> class.
    /// </summary>
    public static class SqlDataReaderExtensions
    {
        /// <summary>
        /// Reads all result sets in the provided <paramref name="dataReader"/> and discards them.
        /// </summary>
        /// <param name="dataReader">Reader to flush results from.</param>
        /// <param name="flushResults">
        /// If <c>true</c>, the records in each result set will be flushed, too. If <c>false</c>
        /// only <see cref="SqlDataReader.NextResult"/> will be the only method called.
        /// </param>
        public static void FlushAllResults(this SqlDataReader dataReader, bool flushResults = true)
        {
            do
            {
                if (flushResults)
                {
                    dataReader.FlushResultSet();
                }
            } while (dataReader.NextResult());
        }

        /// <summary>
        /// Reads all result sets in the provided <paramref name="dataReader"/> and discards them.
        /// </summary>
        /// <param name="dataReader">Reader to flush results from.</param>
        /// <param name="flushResults">
        /// If <c>true</c>, the records in each result set will be flushed, too. If <c>false</c>
        /// only <see cref="SqlDataReader.NextResultAsync()"/> will be the only method called.
        /// </param>
        public static Task FlushAllResultsAsync(this SqlDataReader dataReader, bool flushResults = true) =>
            FlushAllResultsAsync(dataReader, CancellationToken.None, flushResults);

        /// <summary>
        /// Reads all result sets in the provided <paramref name="dataReader"/> and discards them.
        /// </summary>
        /// <param name="dataReader">Reader to flush results from.</param>
        /// <param name="cancellationToken">Token to use for premature cancellation of the task.</param>
        /// <param name="flushResults">
        /// If <c>true</c>, the records in each result set will be flushed, too. If <c>false</c>
        /// only <see cref="SqlDataReader.NextResultAsync()"/> will be the only method called.
        /// </param>
        public static async Task FlushAllResultsAsync(
            this SqlDataReader dataReader,
            CancellationToken cancellationToken,
            bool flushResults = true)
        {
            do
            {
                if (flushResults)
                {
                    await dataReader.FlushResultSetAsync(cancellationToken).ConfigureAwait(false);
                }
            } while (await dataReader.NextResultAsync(cancellationToken).ConfigureAwait(false));
        }

        /// <summary>
        /// Reads all results in the current result set of the provided <paramref name="dataReader"/>
        /// and discards them.
        /// </summary>
        /// <param name="dataReader">Reader to flush results from.</param>
        public static void FlushResultSet(this SqlDataReader dataReader)
        {
            while (dataReader.Read())
            {
                // Discard results.
            }
        }

        /// <summary>
        /// Reads all results in the current result set of the provided <paramref name="dataReader"/>
        /// and discards them.
        /// </summary>
        /// <param name="dataReader">Reader to flush results from.</param>
        public static Task FlushResultSetAsync(this SqlDataReader dataReader) =>
            FlushResultSetAsync(dataReader, CancellationToken.None);

        /// <summary>
        /// Reads all results in the current result set of the provided <paramref name="dataReader"/>
        /// and discards them.
        /// </summary>
        /// <param name="dataReader">Reader to flush results from.</param>
        /// <param name="cancellationToken">Token to use for premature cancellation of the task.</param>
        public static async Task FlushResultSetAsync(this SqlDataReader dataReader, CancellationToken cancellationToken)
        {
            while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // Discard results.
            }
        }
    }
}
