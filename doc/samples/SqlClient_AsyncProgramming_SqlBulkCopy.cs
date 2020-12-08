using System;
// <Snippet1>
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SqlBulkCopyAsyncCodeSample
{
    class Program
    {
        static string selectStatement = "SELECT * FROM [pubs].[dbo].[titles]";
        static string createDestTableStatement =
            @"CREATE TABLE {0} (
            [title_id] [varchar](6) NOT NULL,
            [title] [varchar](80) NOT NULL,
            [type] [char](12) NOT NULL,
            [pub_id] [char](4) NULL,
            [price] [money] NULL,
            [advance] [money] NULL,
            [royalty] [int] NULL,
            [ytd_sales] [int] NULL,
            [notes] [varchar](200) NULL,
            [pubdate] [datetime] NOT NULL)";

        // Replace the connection string if needed, for instance to connect to SQL Express: @"Server=(local)\SQLEXPRESS;Database=Demo;Integrated Security=true"
        // static string connectionString = @"Server=(localdb)\V11.0;Database=Demo";
        static string connectionString = @"Server=(local);Database=Demo;Integrated Security=true";

        // static string marsConnectionString = @"Server=(localdb)\V11.0;Database=Demo;MultipleActiveResultSets=true;";
        static string marsConnectionString = @"Server=(local);Database=Demo;MultipleActiveResultSets=true;Integrated Security=true";

        // Replace the Server name with your actual sql azure server name and User ID/Password
        static string azureConnectionString = @"Server=SqlAzure;User ID=<myUserID>;Password=<myPassword>;Database=Demo";

        static void Main(string[] args)
        {
            SynchronousSqlBulkCopy();
            AsyncSqlBulkCopy().Wait();
            MixSyncAsyncSqlBulkCopy().Wait();
            AsyncSqlBulkCopyNotifyAfter().Wait();
            AsyncSqlBulkCopyDataRows().Wait();
            AsyncSqlBulkCopySqlServerToSqlAzure().Wait();
            AsyncSqlBulkCopyCancel().Wait();
            AsyncSqlBulkCopyMARS().Wait();
        }

        // 3.1.1 Synchronous bulk copy in .NET 4.5
        private static void SynchronousSqlBulkCopy()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                DataTable dt = new DataTable();
                using (SqlCommand cmd = new SqlCommand(selectStatement, conn))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(dt);

                    string temptable = "[#" + Guid.NewGuid().ToString("N") + "]";
                    cmd.CommandText = string.Format(createDestTableStatement, temptable);
                    cmd.ExecuteNonQuery();

                    using (SqlBulkCopy bcp = new SqlBulkCopy(conn))
                    {
                        bcp.DestinationTableName = temptable;
                        bcp.WriteToServer(dt);
                    }
                }
            }

        }

        // 3.1.2 Asynchronous bulk copy in .NET 4.5
        private static async Task AsyncSqlBulkCopy()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                DataTable dt = new DataTable();
                using (SqlCommand cmd = new SqlCommand(selectStatement, conn))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(dt);

                    string temptable = "[#" + Guid.NewGuid().ToString("N") + "]";
                    cmd.CommandText = string.Format(createDestTableStatement, temptable);
                    await cmd.ExecuteNonQueryAsync();

                    using (SqlBulkCopy bcp = new SqlBulkCopy(conn))
                    {
                        bcp.DestinationTableName = temptable;
                        await bcp.WriteToServerAsync(dt);
                    }
                }
            }
        }

        // 3.2 Add new Async.NET capabilities in an existing application (Mixing synchronous and asynchronous calls)
        private static async Task MixSyncAsyncSqlBulkCopy()
        {
            using (SqlConnection conn1 = new SqlConnection(connectionString))
            {
                conn1.Open();
                using (SqlCommand cmd = new SqlCommand(selectStatement, conn1))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        using (SqlConnection conn2 = new SqlConnection(connectionString))
                        {
                            await conn2.OpenAsync();
                            string temptable = "[#" + Guid.NewGuid().ToString("N") + "]";
                            SqlCommand createCmd = new SqlCommand(string.Format(createDestTableStatement, temptable), conn2);
                            await createCmd.ExecuteNonQueryAsync();
                            using (SqlBulkCopy bcp = new SqlBulkCopy(conn2))
                            {
                                bcp.DestinationTableName = temptable;
                                await bcp.WriteToServerAsync(reader);
                            }
                        }
                    }
                }
            }
        }

        // 3.3 Using the NotifyAfter property
        private static async Task AsyncSqlBulkCopyNotifyAfter()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                DataTable dt = new DataTable();
                using (SqlCommand cmd = new SqlCommand(selectStatement, conn))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(dt);

                    string temptable = "[#" + Guid.NewGuid().ToString("N") + "]";
                    cmd.CommandText = string.Format(createDestTableStatement, temptable);
                    await cmd.ExecuteNonQueryAsync();

                    using (SqlBulkCopy bcp = new SqlBulkCopy(conn))
                    {
                        bcp.DestinationTableName = temptable;
                        bcp.NotifyAfter = 5;
                        bcp.SqlRowsCopied += new SqlRowsCopiedEventHandler(OnSqlRowsCopied);
                        await bcp.WriteToServerAsync(dt);
                    }
                }
            }
        }

        private static void OnSqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
        {
            Console.WriteLine("Copied {0} so far...", e.RowsCopied);
        }

        // 3.4 Using the new SqlBulkCopy Async.NET capabilities with DataRow[]
        private static async Task AsyncSqlBulkCopyDataRows()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                DataTable dt = new DataTable();
                using (SqlCommand cmd = new SqlCommand(selectStatement, conn))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(dt);
                    DataRow[] rows = dt.Select();

                    string temptable = "[#" + Guid.NewGuid().ToString("N") + "]";
                    cmd.CommandText = string.Format(createDestTableStatement, temptable);
                    await cmd.ExecuteNonQueryAsync();

                    using (SqlBulkCopy bcp = new SqlBulkCopy(conn))
                    {
                        bcp.DestinationTableName = temptable;
                        await bcp.WriteToServerAsync(rows);
                    }
                }
            }
        }

        // 3.5 Copying data from SQL Server to SQL Azure in .NET 4.5
        private static async Task AsyncSqlBulkCopySqlServerToSqlAzure()
        {
            using (SqlConnection srcConn = new SqlConnection(connectionString))
            using (SqlConnection destConn = new SqlConnection(azureConnectionString))
            {
                await srcConn.OpenAsync();
                await destConn.OpenAsync();
                using (SqlCommand srcCmd = new SqlCommand(selectStatement, srcConn))
                {
                    using (SqlDataReader reader = await srcCmd.ExecuteReaderAsync())
                    {
                        string temptable = "[#" + Guid.NewGuid().ToString("N") + "]";
                        using (SqlCommand destCmd = new SqlCommand(string.Format(createDestTableStatement, temptable), destConn))
                        {
                            await destCmd.ExecuteNonQueryAsync();
                            using (SqlBulkCopy bcp = new SqlBulkCopy(destConn))
                            {
                                bcp.DestinationTableName = temptable;
                                await bcp.WriteToServerAsync(reader);
                            }
                        }
                    }
                }
            }
        }

        // 3.6 Cancelling an Asynchronous Operation to SQL Azure
        private static async Task AsyncSqlBulkCopyCancel()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            using (SqlConnection srcConn = new SqlConnection(connectionString))
            using (SqlConnection destConn = new SqlConnection(azureConnectionString))
            {
                await srcConn.OpenAsync(cts.Token);
                await destConn.OpenAsync(cts.Token);
                using (SqlCommand srcCmd = new SqlCommand(selectStatement, srcConn))
                {
                    using (SqlDataReader reader = await srcCmd.ExecuteReaderAsync(cts.Token))
                    {
                        string temptable = "[#" + Guid.NewGuid().ToString("N") + "]";
                        using (SqlCommand destCmd = new SqlCommand(string.Format(createDestTableStatement, temptable), destConn))
                        {
                            await destCmd.ExecuteNonQueryAsync(cts.Token);
                            using (SqlBulkCopy bcp = new SqlBulkCopy(destConn))
                            {
                                bcp.DestinationTableName = temptable;
                                await bcp.WriteToServerAsync(reader, cts.Token);
                                //Cancel Async SqlBulCopy Operation after 200 ms
                                cts.CancelAfter(200);
                            }
                        }
                    }
                }
            }
        }

        // 3.7 Using Async.Net and MARS
        private static async Task AsyncSqlBulkCopyMARS()
        {
            using (SqlConnection marsConn = new SqlConnection(marsConnectionString))
            {
                await marsConn.OpenAsync();

                SqlCommand titlesCmd = new SqlCommand("SELECT * FROM [pubs].[dbo].[titles]", marsConn);
                SqlCommand authorsCmd = new SqlCommand("SELECT * FROM [pubs].[dbo].[authors]", marsConn);
                //With MARS we can have multiple active results sets on the same connection
                using (SqlDataReader titlesReader = await titlesCmd.ExecuteReaderAsync())
                using (SqlDataReader authorsReader = await authorsCmd.ExecuteReaderAsync())
                {
                    await authorsReader.ReadAsync();

                    string temptable = "[#" + Guid.NewGuid().ToString("N") + "]";
                    using (SqlConnection destConn = new SqlConnection(connectionString))
                    {
                        await destConn.OpenAsync();
                        using (SqlCommand destCmd = new SqlCommand(string.Format(createDestTableStatement, temptable), destConn))
                        {
                            await destCmd.ExecuteNonQueryAsync();
                            using (SqlBulkCopy bcp = new SqlBulkCopy(destConn))
                            {
                                bcp.DestinationTableName = temptable;
                                await bcp.WriteToServerAsync(titlesReader);
                            }
                        }
                    }
                }
            }
        }
    }
}
// </Snippet1>
