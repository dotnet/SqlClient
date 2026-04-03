// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlStreamingXmlTest
    {
        /// <summary>
        /// Parameterize test data type scenarios using the value "xml".
        /// This ensures that GetChars method for XML only behaves consistently.
        /// </summary>
        public static TheoryData<string> TheoryData_DataType_XML_Only => new()
         {
            { "xml" },
        };

        /// <summary>
        /// Parameterize test data type scenarios using the value "nvarchar(max)".
        /// This ensures that GetChars method for nvarchar(max) behaves consistently.
        /// </summary>
        public static TheoryData<string> TheoryData_DataType_NVarChar_Only => new()
         {
            { "nvarchar(max)" },
        };

        /// <summary>
        /// Parameterize test data type scenarios using the values "xml" and "nvarchar(max)".
        /// This ensures that GetChars method for both the XML and nvarchar(max) behaves consistently.
        /// </summary>
        public static TheoryData<string> TheoryData_DataType => new()
         {
            { "xml" },
            { "nvarchar(max)" },
        };

        public static TheoryData<string, bool> TheoryData_DataType_Bool => new()
         {
            { "xml", true },
            { "xml", false },
            { "nvarchar(max)", true },
            { "nvarchar(max)", false },
        };

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_NonAsciiContent(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // XML containing non-ASCII characters:
            //   - \u00E9 (e-acute) - 2 bytes in UTF-8
            //   - \u00F1 (n-tilde) - 2 bytes in UTF-8
            //   - \u00FC (u-umlaut) - 2 bytes in UTF-8
            string xml = "<r>caf\u00E9 se\u00F1or \u00FCber</r>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_NonAsciiContent_BulkRead(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Same non-ASCII XML but read in a single bulk GetChars call
            string xml = "<name>Jos\u00E9 Garc\u00EDa</name>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            char[] buffer = new char[expectedLength + 10];
            long charsRead = sqlDataReader.GetChars(0, 0, buffer, 0, buffer.Length);

            Assert.Equal(expectedLength, charsRead);
            string result = new(buffer, 0, (int)charsRead);
            Assert.Equal(xml, result);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_CjkContent(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // CJK characters: 3 bytes each in UTF-8
            string xml = "<data>\u65E5\u672C\u8A9E\u30C6\u30B9\u30C8</data>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_SurrogatePairContent(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Surrogate Pair characters: 4 bytes each in UTF-8
            string xml = "<data>\U0001F600\U0001F525\U0001F680</data>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_SurrogatePair_ReadIndividually(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Surrogate Pair character: 4 bytes in UTF-8
            string xml = "<data>\U0001F600</data>";
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            // Find the surrogate pair location in the original string
            int highIndex = xml.IndexOf('\uD83D');
            Assert.True(highIndex >= 0);

            int lowIndex = highIndex + 1;

            char[] buffer = new char[1];

            // Read the high surrogate
            long read = sqlDataReader.GetChars(0, highIndex, buffer, 0, 1);
            Assert.Equal(1, read);
            Assert.True(char.IsHighSurrogate(buffer[0]));

            // Read the low surrogate
            read = sqlDataReader.GetChars(0, lowIndex, buffer, 0, 1);
            Assert.Equal(1, read);
            Assert.True(char.IsLowSurrogate(buffer[0]));

            // Reconstruct pair
            string reconstructed = new string(new[] { xml[highIndex], xml[lowIndex] });
            Assert.Equal("\U0001F600", reconstructed);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void Linear_SingleNode(string dataType)
        {
            // Use literal XML column constructed by replicating a string of 'B' characters to reach the desired size, and wrapping it in XML tags.
            string commandTextBase = "SELECT Convert({1}, N'<foo>' + REPLICATE(CAST('' AS nvarchar(max)) +N'B', ({0} * 1024 * 1024) - 11) + N'</foo>')";

            TimeSpan time1 = TimedExecution(commandTextBase, 1, dataType);
            TimeSpan time5 = TimedExecution(commandTextBase, 5, dataType);

            // Compare linear time for 1MB vs 5MB. We expect the time to be at most 6 times higher for 5MB, which permits additional 20% for any noise in the measurements.
            Assert.True(time5.TotalMilliseconds <= (time1.TotalMilliseconds * 6), $"Execution time did not follow linear scale: 1MB={time1.TotalMilliseconds}ms vs. 5MB={time5.TotalMilliseconds}ms");
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void Linear_MultipleNodes(string dataType)
        {
            // Use literal XML column constructed by replicating a string of 'B' characters to reach 1MB, then replicating to the desired number of elements.
            string commandTextBase = "SELECT Convert({1}, REPLICATE(N'<foo>' + REPLICATE(CAST('' AS nvarchar(max)) + N'B', (1024 * 1024) - 11) + N'</foo>', {0}))";

            TimeSpan time1 = TimedExecution(commandTextBase, 1, dataType);
            TimeSpan time5 = TimedExecution(commandTextBase, 5, dataType);

            // Compare linear time for 1MB vs 5MB. We expect the time to be at most 6 times higher for 5MB, which permits additional 20% for any noise in the measurements.
            Assert.True(time5.TotalMilliseconds <= (time1.TotalMilliseconds * 6), $"Execution time did not follow linear scale: 1x={time1.TotalMilliseconds}ms vs. 5x={time5.TotalMilliseconds}ms");
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_XML_Only)), ]
        [MemberData(nameof(TheoryData_DataType_NVarChar_Only), Skip = "Skip: The buffer is not required for nvarchar data type where it returns the length of the entire field.")]
        public static void GetChars_RequiresBuffer(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string commandText = $"SELECT Convert({dataType}, N'<foo>bar</foo>')";
            long charCount = 0;

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            charCount = sqlDataReader.GetChars(0, 0, null, 0, 1);

            //verify -1 is returned since buffer was not provided
            Assert.Equal(-1, charCount);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_Bool))]
        public static void GetChars_SequentialDataIndex(string dataType, bool overlapByOne)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string commandText = $"SELECT Convert({dataType}, N'<foo>bar</foo>')";
            char[] buffer = new char[2];

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            sqlDataReader.GetChars(0, 0, buffer, 0, 2);
            // Verify that providing the same or lower index than the previous call results in an exception.
            // When overlapByOne is true we test providing an index that is one less than the previous call,
            // otherwise we test providing the same index as the previous call - both should not be allowed.
            Assert.Throws<InvalidOperationException>(() => sqlDataReader.GetChars(0, overlapByOne ? 0 : 1, buffer, 0, 2));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_PartialSingleElement(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string commandText = $"SELECT Convert({dataType}, N'<foo>_bar_baz</foo>')";
            long charCount = 0;
            char[] buffer = new char[3];

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            // Read just the 'bar' characters from the XML by specifying the offset, and the length of 3.
            // The offset is 6 to skip the entire first element '<foo>' and the initial '_' part of text.
            charCount = sqlDataReader.GetChars(0, 6, buffer, 0, 3);

            Assert.Equal(3, charCount);
            Assert.Equal("bar", new string(buffer));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_Bool))]
        public static void GetChars_PartialAcrossElements(string dataType, bool initialRead)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string commandText = $"SELECT Convert({dataType}, N'<foobar>baz</foobar>')";
            long charCount = 0;
            char[] buffer = new char[8];

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

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

            Assert.Equal(8, charCount);
            Assert.Equal("r>baz</f", new string(buffer));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_Bool))]
        public static void GetChars_ExcessiveLength(string dataType, bool initialRead)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = """<foo>_bar_baz</foo>""";
            int expectedSize = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

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

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_Bool))]
        public static void GetChars_ExcessiveDataIndex(string dataType, bool initialRead)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = """<foo>_bar_baz</foo>""";
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

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

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_XML_Only))]
        [MemberData(nameof(TheoryData_DataType_NVarChar_Only), Skip = "Skip: nvarchar transformation encodes space as '&#x20;', which breaks expected comparison")]
        public static void GetChars_AsXDocument(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Use a more complex XML column verify through XDocument.
            string xml = """<Person Id="1" Role="Admin"><Name>John</Name><Children /><PreservedWhitespace xml:space="preserve"> </PreservedWhitespace></Person>""";
            XDocument expect = XDocument.Parse(xml);
            int expectedSize = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

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

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_ProcessingInstructionOnly(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<?pi something?>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_ZeroLength(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string commandText = $"SELECT Convert({dataType}, N'<foo>bar</foo>')";
            long charCount = 0;

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            // While not used, cannot pass an empty buffer to GetChars, so provide a buffer of size 1 but request 0 characters to read.
            char[] buffer = new char[1];
            charCount = sqlDataReader.GetChars(0, 0, buffer, 0, 0);

            //verify 0 is returned since nothing was requested
            Assert.Equal(0, charCount);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_CommentAndProcessingInstructionMixed(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<root><!-- comment --><?pi test?></root>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_XML_Only))]
        [MemberData(nameof(TheoryData_DataType_NVarChar_Only), Skip = "Skip: nvarchar transformation does not include a space prior to the self-closing '/>', which breaks expected comparison")]
        public static void GetChars_EmptyElementWithAttributes(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Use an empty element with various attributes, including empty attribute value, normal attribute value, and attributes with escaped characters to verify that all are preserved correctly.
            string xml = "<empty attrEmpty=\"\" attrVal=\"val\" attrEscapedAmp=\"&amp;\" attrEscapedLt=\"&lt;\" attrEscapedGt=\"&gt;\" attrEscapedQuot=\"&quot;\" attrEscapedMixed=\"&amp;&lt;&gt;&quot; abc &gt;&quot;&lt;&amp;\" />";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_XML_Only))]
        [MemberData(nameof(TheoryData_DataType_NVarChar_Only), Skip = "Skip: nvarchar transformation does not include a space prior to the self-closing '/>', which breaks expected comparison")]
        public static void GetChars_EmptyElementWithAttribute_Apos(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // &apos; is normalized by SQL Server and converts to simply '
            string xml = "<empty attrEscaped=\"&apos;\" />";
            string expected = "<empty attrEscaped=\"'\" />";
            int expectedLength = expected.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(expected, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_ElementWithNamespacePrefix(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<ns:tag xmlns:ns=\"urn:test\">content</ns:tag>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_MixedContent(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<root>text<child>inner</child>more</root>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_XML_Only))]
        [MemberData(nameof(TheoryData_DataType_NVarChar_Only), Skip = "Skip: nvarchar transformation encodes embedded as '&lt;encoded&gt;', which breaks expected comparison")]
        public static void GetChars_CDATASection(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<data><![CDATA[some <encoded> content]]></data>";
            string expected = "<data>some <encoded> content</data>";
            int expectedLength = expected.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(expected, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_XML_Only))]
        [MemberData(nameof(TheoryData_DataType_NVarChar_Only), Skip = "Skip: nvarchar transformation encodes final space as '&#x20;', which breaks expected comparison")]
        public static void GetChars_WhitespaceAndSignificantWhitespace(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<root xml:space=\"preserve\">  \t\n  </root>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_XML_Only))]
        [MemberData(nameof(TheoryData_DataType_NVarChar_Only), Skip = "Skip: nvarchar transformation encodes embedded '<>' as '&lt;&gt;', which breaks expected comparison")]
        public static void GetChars_EntityReferences_Normalized(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<data>&lt;&gt;&amp;&quot;&apos;</data>";
            const string expected = "<>&\"'";
            int expectedLength = expected.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read());

            char[] buffer = new char[expectedLength];
            // Use 6 for dataIndex to skip "<data>"
            long charsRead = sqlDataReader.GetChars(0, 6, buffer, 0, buffer.Length);

            Assert.Equal(expectedLength, charsRead);
            string text = new(buffer, 0, (int)charsRead);
            Assert.Equal(expected, text);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_ProcessingInstructions(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<data><?pi instruction?></data>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType_XML_Only))]
        [MemberData(nameof(TheoryData_DataType_NVarChar_Only), Skip = "Skip: nvarchar transformation does not include a space prior to the self-closing '/>', which breaks expected comparison")]
        public static void GetChars_XmlDeclaration_Normalized(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<?xml version=\"1.0\"?><data />";
            string expected = "<data />";
            int expectedLength = expected.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(expected, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_CommentNode(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<root><!-- comment --></root>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_MultipleComments(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<root><!-- first --><!-- second --></root>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_CommentWithSpecialChars(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<root><!-- & < > \" ' --></root>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_EntityReferencesInsideComment(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<root><!-- &lt; &gt; &amp; --></root>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            (long length, string result) = ReadAllChars(sqlDataReader, expectedLength);

            Assert.Equal(expectedLength, length);
            Assert.Equal(xml, result.Substring(0, (int)length));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_SingleCharReadsVsBulk(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml = "<root>ABCDEFGHIJKLMNOPQRSTUVWXYZ</root>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam", SqlDbType.Xml) { Value = xml });

            // ---- single char reads ----
            using (SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                Assert.True(sqlDataReader.Read(), "Expected to read a row");

                long position = 0;
                string singleReadResult = string.Empty;
                char[] buffer = new char[1];
                position = 0;

                while (true)
                {
                    long read = sqlDataReader.GetChars(0, position, buffer, 0, 1);
                    if (read == 0)
                    {
                        break;
                    }

                    singleReadResult += buffer[0];
                    position += read;
                }

                Assert.Equal(expectedLength, position);
                Assert.Equal(xml, singleReadResult);
            }

            // Reuse the same command to verify that bulk read returns the same result, and that the two approaches can be used interchangeably.
            // ---- bulk read ----
            using (SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                Assert.True(sqlDataReader.Read(), "Expected to read a row");

                char[] buffer = new char[expectedLength];
                long bulkRead = sqlDataReader.GetChars(0, 0, buffer, 0, buffer.Length);
                string bulkResult = new(buffer, 0, (int)bulkRead);

                Assert.Equal(expectedLength, bulkRead);
                Assert.Equal(xml, bulkResult);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [MemberData(nameof(TheoryData_DataType))]
        public static void GetChars_TwoXmlColumns(string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            string xml1 = "<root>ABCDEFGHIJKLMNOPQRSTUVWXYZ</root>";
            int expectedLength1 = xml1.Length;
            string xml2 = "<root>0123456789</root>";
            int expectedLength2 = xml2.Length;
            string commandText = $"SELECT Convert({dataType}, @xmlParam1),  Convert({dataType}, @xmlParam2)";

            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = commandText;
            command.Parameters.Add(new SqlParameter("@xmlParam1", SqlDbType.Xml) { Value = xml1 });
            command.Parameters.Add(new SqlParameter("@xmlParam2", SqlDbType.Xml) { Value = xml2 });

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            // Bulk read the first column
            char[] buffer1 = new char[expectedLength1];
            long column1Count = sqlDataReader.GetChars(0, 0, buffer1, 0, buffer1.Length);
            string column1 = new(buffer1, 0, (int)column1Count);

            Assert.Equal(expectedLength1, column1Count);
            Assert.Equal(xml1, column1);

            // Bulk read the second column
            char[] buffer2 = new char[expectedLength2];
            // Change the column index to 1 to read from the second column, and verify that we get the expected result for the second column.
            long column2Count = sqlDataReader.GetChars(1, 0, buffer2, 0, buffer2.Length);
            string column2 = new(buffer2, 0, (int)column2Count);

            Assert.Equal(expectedLength2, column2Count);
            Assert.Equal(xml2, column2);
        }

        private static TimeSpan TimedExecution(string commandTextBase, int scale, string dataType)
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            Stopwatch stopwatch = new();
            int expectedSize = scale * 1024 * 1024;


            using SqlCommand command = connection.CreateCommand();
            connection.Open();
            command.CommandText = string.Format(CultureInfo.InvariantCulture, commandTextBase, scale, dataType);

            using SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            Assert.True(sqlDataReader.Read(), "Expected to read a row");

            stopwatch.Start();
            (long length, string _) = ReadAllChars(sqlDataReader, expectedSize);
            stopwatch.Stop();
            Assert.Equal(expectedSize, length);

            return stopwatch.Elapsed;
        }

        /// <summary>
        /// Replicate the reading approach used with issue #1877
        /// </summary>
        private static (long, string) ReadAllChars(SqlDataReader sqlDataReader, long expectedSize)
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
            while (numCharsRead > 0 && position < expectedSize);

            return (position, new string(text));
        }
    }
}
