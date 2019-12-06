using System;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Reliability;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace RetryLogicCore
{
    class Program
    {
        static async Task Main(string[] args)
        {
            SqlConnection cnn = null;
            SqlCommand cmd = null;

            while (true)
            {
                using (cnn = new SqlConnection("Server=tcp:localhost,11433;Initial Catalog=test;Integrated Security=SSPI;Connection Timeout=10;" +
                    "RetryStrategy=FixedInterval; RetryCount=3; RetryInterval=8; RetryIncrement=8;RetriableErrors=+208,10061+;"))
                {
                    cnn.RetryPolicy.Retrying += RetryPolicy_Retrying;
                    cnn.StateChange += Cnn_StateChange;

                    try
                    {
                        await cnn.OpenAsync();
                        //cnn.Open();

                        cmd = new SqlCommand("SELECT TOP 5 * FROM syscolumns sc1 CROSS JOIN syscolumns sc2;", cnn);

                        //SqlDataReader dr = await cmd.ExecuteReaderAsync();
                        SqlDataReader dr = cmd.ExecuteReader();

                        Console.WriteLine("[{0}] -- Begin Query Results \n", DateTime.Now.ToUniversalTime());
                        while (dr.Read())
                        {
                            Console.WriteLine(dr[0]);
                        }
                        dr.Close();
                        Console.WriteLine("\n[{0}] -- End Query Results \n", DateTime.Now.ToUniversalTime());

                        cmd = new SqlCommand("INSERT INTO mytablettt VALUES (1,'aaaaa')", cnn);
                        cmd.ExecuteNonQuery();
                        //await cmd.ExecuteNonQueryAsync();

                    }
                    catch (SqlException e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n[{0}] -- App Exception!!! -- SQL Error: {1} -- Exception Message: {2} \n\n", DateTime.Now.ToUniversalTime(), e.Number, e.Message);
                        Console.ForegroundColor = ConsoleColor.White;
                    }

                    System.Threading.Thread.Sleep(1000);
                }

            }
        }

        private static void RetryPolicy_Retrying(object sender, SqlRetryingEventArgs e)
        {
            SqlException esql = (SqlException)e.LastException;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[{0}] -- Retry #: {1} -- SQL Error: {2} -- Exception Message: {3} \n", DateTime.Now.ToUniversalTime(), e.CurrentRetryCount, esql.Number, esql.Message);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void Cnn_StateChange(object sender, StateChangeEventArgs e)
        {
            Console.WriteLine("[{0}] -- CurrentState: {0} -- OriginalState: {1} \n", DateTime.Now.ToUniversalTime(), e.CurrentState, e.OriginalState);
        }
    }
}
