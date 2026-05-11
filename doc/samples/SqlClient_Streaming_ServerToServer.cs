// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SqlClient_Streaming_ServerToServer
{
    class Program
    {
        private const string connectionString = @"Server=localhost;Database=Demo2;Integrated Security=true";

        static void Main(string[] args)
        {
            // For this example, we don't want to cancel
            // So we can pass in a "blank" cancellation token
            E2EStream(CancellationToken.None).Wait();

            Console.WriteLine("Done");
        }

        // Streaming from one SQL Server to Another One
        private static async Task E2EStream(CancellationToken cancellationToken)
        {
            using (SqlConnection readConn = new SqlConnection(connectionString))
            {
                using (SqlConnection writeConn = new SqlConnection(connectionString))
                {

                    // Note that we are using the same cancellation token for calls to both connections\commands
                    // Also we can start both the connection opening asynchronously, and then wait for both to complete
                    Task openReadConn = readConn.OpenAsync(cancellationToken);
                    Task openWriteConn = writeConn.OpenAsync(cancellationToken);
                    await Task.WhenAll(openReadConn, openWriteConn);

                    using (SqlCommand readCmd = new SqlCommand("SELECT [bindata] FROM [BinaryStreams]", readConn))
                    {
                        using (SqlCommand writeCmd = new SqlCommand("INSERT INTO [BinaryStreamsCopy] (bindata) VALUES (@bindata)", writeConn))
                        {

                            // Add an empty parameter to the write command which will be used for the streams we are copying
                            // Size is set to -1 to indicate "MAX"
                            SqlParameter streamParameter = writeCmd.Parameters.Add("@bindata", SqlDbType.Binary, -1);

                            // The reader needs to be executed with the SequentialAccess behavior to enable network streaming
                            // Otherwise ReadAsync will buffer the entire BLOB into memory which can cause scalability issues or even OutOfMemoryExceptions
                            using (SqlDataReader reader = await readCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
                            {
                                while (await reader.ReadAsync(cancellationToken))
                                {
                                    // Grab a stream to the binary data in the source database
                                    using (Stream dataStream = reader.GetStream(0))
                                    {

                                        // Set the parameter value to the stream source that was opened
                                        streamParameter.Value = dataStream;

                                        // Asynchronously send data from one database to another
                                        await writeCmd.ExecuteNonQueryAsync(cancellationToken);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
// </Snippet1>
