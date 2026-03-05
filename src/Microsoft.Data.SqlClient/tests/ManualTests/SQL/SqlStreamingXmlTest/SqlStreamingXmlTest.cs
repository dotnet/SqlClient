// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlStreamingXmlTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetChars_NonAsciiContent()
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // XML containing non-ASCII characters:
            //   - \u00E9 (e-acute) - 2 bytes in UTF-8
            //   - \u00F1 (n-tilde) - 2 bytes in UTF-8
            //   - \u00FC (u-umlaut) - 2 bytes in UTF-8
            string xml = "<r>caf\u00E9 se\u00F1or \u00FCber</r>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert(xml, N'{xml}')";

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                Assert.True(sqlDataReader.Read(), "Expected to read a row");

                (long length, string result) = ReadAllChars(sqlDataReader, expectedLength + 10);

                Assert.Equal(expectedLength, length);
                Assert.Equal(xml, result.Substring(0, (int)length));
                connection.Close();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetChars_NonAsciiContent_BulkRead()
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // Same non-ASCII XML but read in a single bulk GetChars call
            string xml = "<name>Jos\u00E9 Garc\u00EDa</name>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert(xml, N'{xml}')";

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                Assert.True(sqlDataReader.Read(), "Expected to read a row");

                char[] buffer = new char[expectedLength + 10];
                long charsRead = sqlDataReader.GetChars(0, 0, buffer, 0, buffer.Length);

                Assert.Equal(expectedLength, charsRead);
                string result = new string(buffer, 0, (int)charsRead);
                Assert.Equal(xml, result);
                connection.Close();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void GetChars_CjkContent()
        {
            SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            // CJK characters: 3 bytes each in UTF-8
            string xml = "<data>\u65E5\u672C\u8A9E\u30C6\u30B9\u30C8</data>";
            int expectedLength = xml.Length;
            string commandText = $"SELECT Convert(xml, N'{xml}')";

            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = commandText;

                SqlDataReader sqlDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                Assert.True(sqlDataReader.Read(), "Expected to read a row");

                (long length, string result) = ReadAllChars(sqlDataReader, expectedLength + 10);

                Assert.Equal(expectedLength, length);
                Assert.Equal(xml, result.Substring(0, (int)length));
                connection.Close();
            }
        }

        /// <summary>
        /// Read all chars one at a time using GetChars with SequentialAccess,
        /// replicating the pattern from issue #1877.
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
