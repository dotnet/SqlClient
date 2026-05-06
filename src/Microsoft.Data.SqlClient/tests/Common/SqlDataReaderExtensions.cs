// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        public static void FlushAllResults(this SqlDataReader dataReader)
        {
            do
            {
                dataReader.FlushResultSet();
            } while (dataReader.NextResult());
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
    }
}
