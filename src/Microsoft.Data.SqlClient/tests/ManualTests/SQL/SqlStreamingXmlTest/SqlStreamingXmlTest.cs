// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlStreamingXmlTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void Linear_SingleNode()
        {
            // Use literal XML column constructed by replicating a string of 'B' characters to reach the desired size, and wrapping it in XML tags.
            const string commandTextBase = "SELECT Convert(xml, N'<foo>' + REPLICATE(CAST('' AS nvarchar(max)) +N'B', ({0} * 1024 * 1024) - 11) + N'</foo>')";

            TimeSpan time1 = TimedExecution(commandTextBase, 1);
            TimeSpan time5 = TimedExecution(commandTextBase, 5);

            // Compare linear time for 1MB vs 5MB. We expect the time to be at most 6 times higher for 5MB, which permits additional 20% for any noise in the measurements.
            Assert.True(time5.TotalMilliseconds <= (time1.TotalMilliseconds * 6), $"Execution time did not follow linear scale: 1MB={time1.TotalMilliseconds}ms vs. 5MB={time5.TotalMilliseconds}ms");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void Linear_MultipleNodes()
        {
            // Use literal XML column constructed by replicating a string of 'B' characters to reach 1MB, then replicating to the desired number of elements.
            const string commandTextBase = "SELECT Convert(xml, REPLICATE(N'<foo>' + REPLICATE(CAST('' AS nvarchar(max)) + N'B', (1024 * 1024) - 11) + N'</foo>', {0}))";

            TimeSpan time1 = TimedExecution(commandTextBase, 1);
            TimeSpan time5 = TimedExecution(commandTextBase, 5);

            // Compare linear time for 1MB vs 5MB. We expect the time to be at most 6 times higher for 5MB, which permits additional 20% for any noise in the measurements.
            Assert.True(time5.TotalMilliseconds <= (time1.TotalMilliseconds * 6), $"Execution time did not follow linear scale: 1x={time1.TotalMilliseconds}ms vs. 5x={time5.TotalMilliseconds}ms");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetChars_RequiresBuffer()
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            const string commandText = "SELECT Convert(xml, N'<foo>bar</foo>')";
            long charCount = 0;

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    charCount = sqlDataReader.GetChars(0, 0, null, 0, 1);
                }
                connection.Close();
            }

            //verify -1 is returned since buffer was not provided
            Assert.Equal(-1, charCount);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(true)]
        [InlineData(false)]
        public static void GetChars_SequentialDataIndex(bool backwards)
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            const string commandText = "SELECT Convert(xml, N'<foo>bar</foo>')";
            char[] buffer = new char[2];

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    sqlDataReader.GetChars(0, 0, buffer, 0, 2);
                    // Verify that providing the same or lower index than the previous call results in an exception.
                    // When backwards is true we test providing an index that is one less than the previous call,
                    // otherwise we test providing the same index as the previous call - both should not be allowed.
                    Assert.Throws<InvalidOperationException>(() => sqlDataReader.GetChars(0, backwards ? 0 : 1, buffer, 0, 2));
                }
                connection.Close();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetChars_PartialSingleElement()
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            const string commandText = "SELECT Convert(xml, N'<foo>_bar_baz</foo>')";
            long charCount = 0;
            char[] buffer = new char[3];

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    // Read just the 'bar' characters from the XML by specifying the offset, and the length of 3.
                    // The offset is 6 to skip the entire first element '<foo>' and the initial '_' part of text.
                    charCount = sqlDataReader.GetChars(0, 6, buffer, 0, 3);
                }
                connection.Close();
            }

            Assert.Equal(3, charCount);
            Assert.Equal("bar", new string(buffer));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(true)]
        [InlineData(false)]
        public static void GetChars_PartialAcrossElements(bool initialRead)
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            const string commandText = "SELECT Convert(xml, N'<foobar>baz</foobar>')";
            long charCount = 0;
            char[] buffer = new char[8];

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    if (initialRead)
                    {
                        // When initialRead is true, we verify continuation after a previous read,
                        // otherwise we just verify that we can read across XML elements in a single call.
                        char[] initialBuffer = new char[2];
                        sqlDataReader.GetChars(0, 0, initialBuffer, 0, 2);
                        Assert.Equal("<f", new string(initialBuffer));
                        // Verify skipping within the existing initial element.
                        sqlDataReader.GetChars(0, 3, initialBuffer, 0, 2);
                        Assert.Equal("ob", new string(initialBuffer));
                    }
                    // Read the 'r>baz</f' characters across XML elements by specifying the offset, and the length of 8.
                    // The offset is 6 to skip the '<fooba' characters.
                    charCount = sqlDataReader.GetChars(0, 6, buffer, 0, 8);
                }
                connection.Close();
            }

            Assert.Equal(8, charCount);
            Assert.Equal("r>baz</f", new string(buffer));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(true)]
        [InlineData(false)]
        public static void GetChars_ExcessiveLength(bool initialRead)
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = """<foo>_bar_baz</foo>""";
            int expectedSize = xml.Length;
            string commandText = $"SELECT Convert(xml, N'{xml}')";

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    if (initialRead)
                    {
                        // When initialRead is true, we verify continuation after a previous read,
                        // otherwise we just verify that we can read everything in a single call.
                        char[] initialBuffer = new char[2];
                        long initialLength = sqlDataReader.GetChars(0, 0, initialBuffer, 0, 2);
                        char[] remainingBuffer = new char[98];
                        long remainingLength = sqlDataReader.GetChars(0, 2, remainingBuffer, 0, 98);
                        string combined = new string(initialBuffer) + new string(remainingBuffer);

                        Assert.Equal(expectedSize, initialLength + remainingLength);
                        Assert.Equal(xml, combined.Substring(0, expectedSize));
                    }
                    else
                    {
                        // Try to read more characters than the actual XML to verify that the method returns only the actual number of characters.
                        (long length, string text) = ReadAllChars(sqlDataReader, 100);

                        Assert.Equal(expectedSize, length);
                        Assert.Equal(xml, text.Substring(0, expectedSize));
                    }
                }
                connection.Close();
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(true)]
        [InlineData(false)]
        public static void GetChars_ExcessiveDataIndex(bool initialRead)
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = """<foo>_bar_baz</foo>""";
            string commandText = $"SELECT Convert(xml, N'{xml}')";

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    if (initialRead)
                    {
                        // When initialRead is true, we verify continuation after a previous read,
                        // otherwise we just verify the large DataIndex in a single call.
                        char[] initialBuffer = new char[2];
                        long initialLength = sqlDataReader.GetChars(0, 0, initialBuffer, 0, 2);
                        Assert.Equal(2, initialLength);
                    }

                    // buffer will not be touched since the DataIndex is beyond the end of the XML, but a suitable buffer must still be provided.
                    char[] buffer = new char[100];
                    long length = sqlDataReader.GetChars(0, 100, buffer, 0, 2);
                    Assert.Equal(0, length);
                }
                connection.Close();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetChars_AsXDocument()
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Use a more complex XML column verify through XDocument.
            string xml = """<Person Id="1" Role="Admin"><Name>John</Name><Children /><PreservedWhitespace xml:space="preserve"> </PreservedWhitespace></Person>""";
            XDocument expect = XDocument.Parse(xml);
            int expectedSize = xml.Length;
            string commandText = $"SELECT Convert(xml, N'{xml}')";

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    (long length, string xmlString) = ReadAllChars(sqlDataReader, expectedSize);

                    Assert.Equal(expectedSize, length);
                    XDocument actual = XDocument.Parse(xmlString);
                    Assert.Equal((int)expect.Root.Attribute("Id"), (int)actual.Root.Attribute("Id"));
                    Assert.Equal((string)expect.Root.Attribute("Role"), (string)actual.Root.Attribute("Role"));
                    Assert.NotNull(expect.Root.Element("Name")?.Value);
                    Assert.Equal(expect.Root.Element("Name")!.Value, actual.Root.Element("Name")!.Value);
                    Assert.NotNull(expect.Root.Element("Children")?.HasElements);
                    Assert.Equal(expect.Root.Element("Children")!.HasElements, actual.Root.Element("Children")?.HasElements);
                    Assert.NotNull(expect.Root.Element("PreservedWhitespace")?.Value);
                    Assert.Equal(expect.Root.Element("PreservedWhitespace")!.Value, actual.Root.Element("PreservedWhitespace")!.Value);
                }
                connection.Close();
            }
        }

        private static TimeSpan TimedExecution(string commandTextBase, int scale)
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            Stopwatch stopwatch = new Stopwatch();
            int expectedSize = scale * 1024 * 1024;


            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = string.Format(CultureInfo.InvariantCulture, commandTextBase, scale);

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                if (sqlDataReader.Read())
                {
                    stopwatch.Start();
                    (long length, string _) = ReadAllChars(sqlDataReader, expectedSize);
                    stopwatch.Stop();
                    Assert.Equal(expectedSize, length);
                }
                connection.Close();
            }

            return stopwatch.Elapsed;
        }

        /// <summary>
        /// Replicate the reading approach used with issue #1877
        /// </summary>
        private static (long, string) ReadAllChars(SqlDataReader sqlDataReader, int expectedSize)
        {
            char[] text = new char[expectedSize];
            char[] buffer = new char[1];

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

            return (position, new string(text));
        }
    }
}
