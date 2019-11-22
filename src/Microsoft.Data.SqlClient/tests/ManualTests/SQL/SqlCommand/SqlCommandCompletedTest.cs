using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlCommandCompletedTest
    {
        private static readonly string s_connStr = (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { PacketSize = 512 }).ConnectionString;
        private static int completedHandlerExecuted = 0;

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void VerifyStatmentCompletedCalled()
        {
            string tableName = DataTestUtility.GetUniqueNameForSqlServer("stmt");

            using (var conn = new SqlConnection(s_connStr))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    cmd.StatementCompleted += StatementCompletedHandler;
                    conn.Open();

                    cmd.CommandText = $"CREATE TABLE {tableName} (c1 int)";
                    var res = cmd.ExecuteScalar();

                    cmd.CommandText = $"INSERT {tableName} VALUES(1)"; //DML (+1)
                    res = cmd.ExecuteScalar();

                    cmd.CommandText = $"Update {tableName} set c1=2"; //DML (+1)
                    res = cmd.ExecuteScalar();

                    cmd.CommandText = $"SELECT * from {tableName}"; //DQL (+1)
                    res = cmd.ExecuteScalar();

                    cmd.CommandText = $"DELETE FROM {tableName}"; //DML (+1)
                    res = cmd.ExecuteScalar();
                }
                finally
                {
                    cmd.CommandText = $"DROP TABLE {tableName}";
                    var res = cmd.ExecuteScalar();
                }
            }
            // DDL and DQL queries that return DoneRowCount are accounted here.
            Assert.True(completedHandlerExecuted == 4);
        }

        private static void StatementCompletedHandler(object sender, StatementCompletedEventArgs args)
        {
            // Increment on event pass through
            completedHandlerExecuted++;
        }
    }
}
