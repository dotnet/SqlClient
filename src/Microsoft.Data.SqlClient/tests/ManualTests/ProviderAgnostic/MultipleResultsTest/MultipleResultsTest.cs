// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class MultipleResultsTest
    {
        private const string ResultSet1_Message = "0";
        private const string ResultSet2_Message = "1";
        private const string ResultSet2_Error = "Error 1";

        private const string ResultSet3_Message = "3";
        private const string ResultSet4_Message = "4";
        private const string ResultSet4_Error = "Error 2";

        private const string ResultSet5_Message = "5";
        private const string ResultSet6_Message = "6";
        private const string ResultSet6_Error = "Error 3";

        private const string ResultSet7_Message = "7";
        private const string ResultSet8_Message = "8";
        private const string ResultSet8_Error = "Error 4";

        private const string ResultSet9_Message = "9";
        private const string ResultSet10_Message = "10";
        private const string ResultSet10_Error = "Error 5";

        private const string ResultSet11_Message = "11";

        private readonly static string s_sqlStatement =
            $"PRINT N'{ResultSet1_Message}'; SELECT num = 1, str = 'ABC';\n" +
            $"PRINT N'{ResultSet2_Message}'; RAISERROR('{ResultSet2_Error}', 15, 1);\n" +
            $"PRINT N'{ResultSet3_Message}'; SELECT num = 2, str = 'ABC';\n" +
            $"PRINT N'{ResultSet4_Message}'; RAISERROR('{ResultSet4_Error}', 15, 1);\n" +
            $"PRINT N'{ResultSet5_Message}'; SELECT num = 3, str = 'ABC';\n" +
            $"PRINT N'{ResultSet6_Message}'; RAISERROR('{ResultSet6_Error}', 15, 1);\n" +
            $"PRINT N'{ResultSet7_Message}'; SELECT num = 4, str = 'ABC';\n" +
            $"PRINT N'{ResultSet8_Message}'; RAISERROR('{ResultSet8_Error}', 15, 1);\n" +
            $"PRINT N'{ResultSet9_Message}'; SELECT num = 5, str = 'ABC';\n" +
            $"PRINT N'{ResultSet10_Message}'; RAISERROR('{ResultSet10_Error}', 15, 1);\n" +
            $"PRINT N'{ResultSet11_Message}';";

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ExecuteNonQuery()
        {
            using SqlConnection connection = new SqlConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString);
            using SqlCommand command = connection.CreateCommand();
            ConcurrentQueue<string> messages = new ConcurrentQueue<string>();

            connection.InfoMessage += (object sender, SqlInfoMessageEventArgs args) =>
                messages.Enqueue(args.Message);

            connection.Open();

            command.CommandText = s_sqlStatement;

            // ExecuteNonQuery will drain every result set, info message and exception, collating these into a single exception.
            Func<object> testCode = () => command.ExecuteNonQuery();
            SqlException exNonQuery = Assert.Throws<SqlException>(testCode);

            string expectedInfoMessages = string.Join(Environment.NewLine,
                ResultSet2_Error, ResultSet4_Error, ResultSet6_Error, ResultSet8_Error, ResultSet10_Error,
                ResultSet1_Message, ResultSet2_Message, ResultSet3_Message, ResultSet4_Message, ResultSet5_Message,
                ResultSet6_Message, ResultSet7_Message, ResultSet8_Message, ResultSet9_Message, ResultSet10_Message,
                ResultSet11_Message);

            Assert.Equal(expectedInfoMessages, exNonQuery.Message);
            Assert.Empty(messages);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ExecuteScalar()
        {
            using SqlConnection connection = new SqlConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString);
            using SqlCommand command = connection.CreateCommand();
            ConcurrentQueue<string> messages = new ConcurrentQueue<string>();

            connection.InfoMessage += (object sender, SqlInfoMessageEventArgs args) =>
                messages.Enqueue(args.Message);

            connection.Open();

            command.CommandText = s_sqlStatement;

            // ExecuteScalar now drains all result sets to ensure errors are not silently ignored (GH #3736 fix).
            // Since the SQL statement contains RAISERRORs after the first result set, an exception is thrown.
            SqlException ex = Assert.Throws<SqlException>(() => command.ExecuteScalar());
            Assert.Contains("Error 1", ex.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ExecuteReader()
        {
            using SqlConnection connection = new SqlConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString);
            using SqlCommand command = connection.CreateCommand();
            ConcurrentQueue<string> messages = new ConcurrentQueue<string>();

            connection.InfoMessage += (object sender, SqlInfoMessageEventArgs args) =>
                messages.Enqueue(args.Message);

            connection.Open();

            command.CommandText = s_sqlStatement;

            // SqlDataReader will drain every result set it's told to, throwing exceptions and printing messages as it goes.
            using SqlDataReader reader = command.ExecuteReader();

            // Result set 1: a result set and a message of '0'.
            // Result set 2: a message of '1' and an exception.
            AdvanceReader(reader, messages, ResultSet1_Message, ResultSet2_Message, ResultSet2_Error, finalBlock: false);

            // Result set 3: a result set and a message of '3'.
            // Result set 4: a message of '4' and an exception.
            AdvanceReader(reader, messages, ResultSet3_Message, ResultSet4_Message, ResultSet4_Error, finalBlock: false);

            // Result set 5: a result set and a message of '5'.
            // Result set 6: a message of '6' and an exception.
            AdvanceReader(reader, messages, ResultSet5_Message, ResultSet6_Message, ResultSet6_Error, finalBlock: false);

            // Result set 7: a result set and a message of '7'.
            // Result set 8: a message of '8' and an exception.
            AdvanceReader(reader, messages, ResultSet7_Message, ResultSet8_Message, ResultSet8_Error, finalBlock: false);

            // Result set 9: a result set and a message of '9'.
            // Result set 10: a message of '10' and an exception.
            AdvanceReader(reader, messages, ResultSet9_Message, ResultSet10_Message, ResultSet10_Error, finalBlock: true);

            // One message following the final result set
            Assert.True(messages.TryDequeue(out string lastMessage));
            Assert.Empty(messages);
            Assert.Equal(ResultSet11_Message, lastMessage);
        }

        private static void AdvanceReader(SqlDataReader reader, ConcurrentQueue<string> messageBuffer, string resultSet1Message, string resultSet2Message, string resultSet2ExceptionMessage, bool finalBlock)
        {
            bool moreResults = true;

            // This is a pair of result sets:
            // Result set 1: an info message of something and a result set
            // Result set 2: an info message of something and an exception
            Assert.True(messageBuffer.TryDequeue(out string lastMessage));
            Assert.Empty(messageBuffer);
            Assert.Equal(resultSet1Message, lastMessage);

            SqlException exReader = Assert.Throws<SqlException>(() => moreResults = reader.NextResult());
            Assert.Equal(resultSet2ExceptionMessage, exReader.Message);

            Assert.True(messageBuffer.TryDequeue(out lastMessage));
            Assert.Empty(messageBuffer);
            Assert.Equal(resultSet2Message, lastMessage);

            moreResults = reader.NextResult();
            Assert.Equal(finalBlock, !moreResults);
        }
    }
}
