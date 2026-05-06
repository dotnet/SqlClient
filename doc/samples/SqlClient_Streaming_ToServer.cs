// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SqlClient_Streaming_ToServer
{
    class Program
    {
        private const string connectionString = @"Server=localhost;Database=Demo2;Integrated Security=true";

        static void Main(string[] args)
        {
            CreateDemoFiles();

            StreamBLOBToServer().Wait();
            StreamTextToServer().Wait();

            // Create a CancellationTokenSource that will be cancelled after 100ms
            // Typically this token source will be cancelled by a user request (e.g. a Cancel button)
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(100);
            try
            {
                CancelBLOBStream(tokenSource.Token).Wait();
            }
            catch (AggregateException ex)
            {
                // Cancelling an async operation will throw an exception
                // Since we are using the Task's Wait method, this exception will be wrapped in an AggregateException
                // If you were using the 'await' keyword, the compiler would take care of unwrapping the AggregateException
                // Depending on when the cancellation occurs, you can either get an error from SQL Server or from .Net
                if ((ex.InnerException is SqlException) || (ex.InnerException is TaskCanceledException))
                {
                    // This is an expected exception
                    Console.WriteLine("Got expected exception: {0}", ex.InnerException.Message);
                }
                else
                {
                    // Did not expect this exception - re-throw it
                    throw;
                }
            }

            Console.WriteLine("Done");
        }

        // This is used to generate the files which are used by the other sample methods
        private static void CreateDemoFiles()
        {
            Random rand = new Random();
            byte[] data = new byte[1024];
            rand.NextBytes(data);

            using (FileStream file = File.Open("binarydata.bin", FileMode.Create))
            {
                file.Write(data, 0, data.Length);
            }

            using (StreamWriter writer = new StreamWriter(File.Open("textdata.txt", FileMode.Create)))
            {
                writer.Write(Convert.ToBase64String(data));
            }
        }

        // Application transferring a large BLOB to SQL Server
        private static async Task StreamBLOBToServer()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("INSERT INTO [BinaryStreams] (bindata) VALUES (@bindata)", conn))
                {
                    using (FileStream file = File.Open("binarydata.bin", FileMode.Open))
                    {

                        // Add a parameter which uses the FileStream we just opened
                        // Size is set to -1 to indicate "MAX"
                        cmd.Parameters.Add("@bindata", SqlDbType.Binary, -1).Value = file;

                        // Send the data to the server asynchronously
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // Application transferring a large Text File to SQL Server
        private static async Task StreamTextToServer()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("INSERT INTO [TextStreams] (textdata) VALUES (@textdata)", conn))
                {
                    using (StreamReader file = File.OpenText("textdata.txt"))
                    {

                        // Add a parameter which uses the StreamReader we just opened
                        // Size is set to -1 to indicate "MAX"
                        cmd.Parameters.Add("@textdata", SqlDbType.NVarChar, -1).Value = file;

                        // Send the data to the server asynchronously
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // Cancelling the transfer of a large BLOB
        private static async Task CancelBLOBStream(CancellationToken cancellationToken)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                // We can cancel not only sending the data to the server, but also opening the connection
                await conn.OpenAsync(cancellationToken);

                // Artificially delay the command by 100ms
                using (SqlCommand cmd = new SqlCommand("WAITFOR DELAY '00:00:00:100';INSERT INTO [BinaryStreams] (bindata) VALUES (@bindata)", conn))
                {
                    using (FileStream file = File.Open("binarydata.bin", FileMode.Open))
                    {

                        // Add a parameter which uses the FileStream we just opened
                        // Size is set to -1 to indicate "MAX"
                        cmd.Parameters.Add("@bindata", SqlDbType.Binary, -1).Value = file;

                        // Send the data to the server asynchronously
                        // Pass the cancellation token such that the command will be cancelled if needed
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }
        }
    }
}
// </Snippet1>
