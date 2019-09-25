using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlCommandCompletedTest
    {
        private static readonly string s_connStr = (new SqlConnectionStringBuilder(DataTestUtility.TcpConnStr) { PacketSize = 512 }).ConnectionString;
        private static bool completedHandlerExecuted = false;

        [CheckConnStrSetupFact]
        public static void VerifyStatmentCompletedCalled()
        {
            using (var conn = new SqlConnection(s_connStr))
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM sys.databases";
                    cmd.StatementCompleted += StatementCompletedHandler;
                    conn.Open();
                    var res = cmd.ExecuteScalar();
                }
            }
            Assert.True(completedHandlerExecuted);
        }

        private static void StatementCompletedHandler(object sender, StatementCompletedEventArgs args)
        {
            completedHandlerExecuted = true;
        }
    }
}
