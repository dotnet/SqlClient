// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlStreamingXmlTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void LinearSingleNode()
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Use a literal XML column of the specified size. The XML is constructed by replicating a string of 'B' characters to reach the desired size, and wrapping it in XML tags.
            const string commandTextBase = "SELECT Convert(xml, N'<foo>' + REPLICATE(CAST('' AS nvarchar(max)) +N'B', ({0} * 1024 * 1024) - 11) + N'</foo>')";

            TimeSpan time1 = TimedExecution(commandTextBase, 1);
            TimeSpan time5 = TimedExecution(commandTextBase, 5);

            // Compare linear time for 1MB vs 5MB. We expect the time to be at most 6 times higher for 5MB, which permits additional 20% for any noise in the measurements.
            Assert.True(time5.TotalMilliseconds <= (time1.TotalMilliseconds * 6), $"Execution time did not follow linear scale: 1MB={time1.TotalMilliseconds}ms vs. 5MB={time5.TotalMilliseconds}ms");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void LinearMultipleNodes()
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Use a literal XML column with the specified number of 1MB elements. The XML is constructed by replicating a string of 'B' characters to reach 1MB, then replicating to the desired number of elements.
            const string commandTextBase = "SELECT Convert(xml, REPLICATE(N'<foo>' + REPLICATE(CAST('' AS nvarchar(max)) + N'B', (1024 * 1024) - 11) + N'</foo>', {0}))";

            TimeSpan time1 = TimedExecution(commandTextBase, 1);
            TimeSpan time5 = TimedExecution(commandTextBase, 5);

            // Compare linear time for 1MB vs 5MB. We expect the time to be at most 6 times higher for 5MB, which permits additional 20% for any noise in the measurements.
            Assert.True(time5.TotalMilliseconds <= (time1.TotalMilliseconds * 6), $"Execution time did not follow linear scale: 1x={time1.TotalMilliseconds}ms vs. 5x={time5.TotalMilliseconds}ms");
        }

        private static TimeSpan TimedExecution(string commandTextBase, int scale)
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            var stopwatch = new Stopwatch();

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = string.Format(CultureInfo.InvariantCulture, commandTextBase, scale);

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    stopwatch.Start();
                    ReadAllChars(sqlDataReader, scale);
                    stopwatch.Stop();
                }
                connection.Close();
            }

            return stopwatch.Elapsed;
        }

        /// <summary>
        /// Replicate the reading approach used with issue #1877
        /// </summary>
        private static void ReadAllChars(SqlDataReader sqlDataReader, int expectedMB)
        {
            var expectedSize = expectedMB * 1024 * 1024;
            var text = new char[expectedSize];
            var buffer = new char[1];

            long position = 0;
            long numCharsRead;
            do
            {
                numCharsRead = sqlDataReader.GetChars(0, position, buffer, 0, 1);
                if (numCharsRead > 0)
                {
                    text[position] = buffer[0];
                    position += numCharsRead;
                }
            }
            while (numCharsRead > 0);

            Assert.Equal(expectedSize, position);
        }
    }
}
