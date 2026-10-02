// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class DbUtils
    {
        private static readonly Random s_random = new();
        const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*()0123456789";

        public static string RandomString(int length)
            => new(Enumerable.Repeat(Chars, length)
              .Select(s => s[s_random.Next(s.Length)]).ToArray());

        public static string GenerateEscapedTableName(string prefix)
            => new StringBuilder("[").Append(prefix)
                .Append('_').Append(Environment.MachineName)
                .Append('_').Append(RandomString(10))
                .Append("]").ToString();

        /// <summary>Executes fixture SQL using the specified timeout.</summary>
        /// <param name="query">Setup or cleanup SQL.</param>
        /// <param name="sqlConnection">Open fixture connection.</param>
        /// <param name="commandTimeoutSeconds">SQL command timeout in seconds.</param>
        public static void ExecuteNonQuery(string query, SqlConnection sqlConnection, int commandTimeoutSeconds = 30)
        {
            using SqlCommand sqlCommand = new(query, sqlConnection) { CommandTimeout = commandTimeoutSeconds };
            sqlCommand.ExecuteNonQuery();
        }
    }
}
