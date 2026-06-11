// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace SqlClient_Streaming_FromServer
{
    class Program
    {
        private const string connectionString = @"Server=localhost;Database=Demo;Integrated Security=true";

        static void Main(string[] args)
        {
            CopyBinaryValueToFile().Wait();
            PrintTextValues().Wait();
            PrintXmlValues().Wait();
            PrintXmlValuesViaNVarChar().Wait();

            Console.WriteLine("Done");
        }

        // Application retrieving a large BLOB from SQL Server in .NET 4.5 using the new asynchronous capability
        private static async Task CopyBinaryValueToFile()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand("SELECT [bindata] FROM [Streams] WHERE [id]=@id", connection))
                {
                    command.Parameters.AddWithValue("id", 1);

                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming
                    // Otherwise ReadAsync will buffer the entire BLOB into memory which can cause scalability issues or even OutOfMemoryExceptions
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        if (await reader.ReadAsync())
                        {
                            if (!(await reader.IsDBNullAsync(0)))
                            {
                                using (FileStream file = new FileStream("binarydata.bin", FileMode.Create, FileAccess.Write))
                                {
                                    using (Stream data = reader.GetStream(0))
                                    {

                                        // Asynchronously copy the stream from the server to the file we just created
                                        await data.CopyToAsync(file);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Application transferring a large Text File from SQL Server
        private static async Task PrintTextValues()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand("SELECT [id], [textdata] FROM [Streams]", connection))
                {

                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming
                    // Otherwise ReadAsync will buffer the entire text document into memory which can cause scalability issues or even OutOfMemoryExceptions
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        while (await reader.ReadAsync())
                        {
                            Console.Write("{0}: ", reader.GetInt32(0));

                            if (await reader.IsDBNullAsync(1))
                            {
                                Console.Write("(NULL)");
                            }
                            else
                            {
                                char[] buffer = new char[4096];
                                int charsRead = 0;
                                using (TextReader data = reader.GetTextReader(1))
                                {
                                    do
                                    {
                                        // Grab each chunk of text and write it to the console
                                        // If you are writing to a TextWriter you should use WriteAsync or WriteLineAsync
                                        charsRead = await data.ReadAsync(buffer, 0, buffer.Length);
                                        Console.Write(buffer, 0, charsRead);
                                    } while (charsRead > 0);
                                }
                            }

                            Console.WriteLine();
                        }
                    }
                }
            }
        }

        // Application transferring a large Xml Document from SQL Server
        private static async Task PrintXmlValues()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand("SELECT [id], [xmldata] FROM [Streams]", connection))
                {

                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming
                    // Otherwise ReadAsync will buffer the entire Xml Document into memory which can cause scalability issues or even OutOfMemoryExceptions
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        while (await reader.ReadAsync())
                        {
                            Console.WriteLine("{0}: ", reader.GetInt32(0));

                            if (await reader.IsDBNullAsync(1))
                            {
                                Console.WriteLine("\t(NULL)");
                            }
                            else
                            {
                                using (XmlReader xmlReader = reader.GetXmlReader(1))
                                {
                                    int depth = 1;
                                    // NOTE: The XmlReader returned by GetXmlReader does NOT support async operations
                                    // See the example below (PrintXmlValuesViaNVarChar) for how to get an XmlReader with asynchronous capabilities
                                    while (xmlReader.Read())
                                    {
                                        switch (xmlReader.NodeType)
                                        {
                                            case XmlNodeType.Element:
                                                Console.WriteLine("{0}<{1}>", new string('\t', depth), xmlReader.Name);
                                                depth++;
                                                break;
                                            case XmlNodeType.Text:
                                                Console.WriteLine("{0}{1}", new string('\t', depth), xmlReader.Value);
                                                break;
                                            case XmlNodeType.EndElement:
                                                depth--;
                                                Console.WriteLine("{0}</{1}>", new string('\t', depth), xmlReader.Name);
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Application transferring a large Xml Document from SQL Server
        // This goes via NVarChar and TextReader to enable asynchronous reading
        private static async Task PrintXmlValuesViaNVarChar()
        {
            XmlReaderSettings xmlSettings = new XmlReaderSettings()
            {
                // Async must be explicitly enabled in the XmlReaderSettings otherwise the XmlReader will throw exceptions when async methods are called
                Async = true,
                // Since we will immediately wrap the TextReader we are creating in an XmlReader, we will permit the XmlReader to take care of closing\disposing it
                CloseInput = true,
                // If the Xml you are reading is not a valid document (as per <https://docs.microsoft.com/previous-versions/dotnet/netframework-4.0/6bts1x50(v=vs.100)>) you will need to set the conformance level to Fragment
                ConformanceLevel = ConformanceLevel.Fragment
            };

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Cast the XML into NVarChar to enable GetTextReader - trying to use GetTextReader on an XML type will throw an exception
                using (SqlCommand command = new SqlCommand("SELECT [id], CAST([xmldata] AS NVARCHAR(MAX)) FROM [Streams]", connection))
                {

                    // The reader needs to be executed with the SequentialAccess behavior to enable network streaming
                    // Otherwise ReadAsync will buffer the entire Xml Document into memory which can cause scalability issues or even OutOfMemoryExceptions
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        while (await reader.ReadAsync())
                        {
                            Console.WriteLine("{0}:", reader.GetInt32(0));

                            if (await reader.IsDBNullAsync(1))
                            {
                                Console.WriteLine("\t(NULL)");
                            }
                            else
                            {
                                // Grab the row as a TextReader, then create an XmlReader on top of it
                                // We are not keeping a reference to the TextReader since the XmlReader is created with the "CloseInput" setting (so it will close the TextReader when needed)
                                using (XmlReader xmlReader = XmlReader.Create(reader.GetTextReader(1), xmlSettings))
                                {
                                    int depth = 1;
                                    // The XmlReader above now supports asynchronous operations, so we can use ReadAsync here
                                    while (await xmlReader.ReadAsync())
                                    {
                                        switch (xmlReader.NodeType)
                                        {
                                            case XmlNodeType.Element:
                                                Console.WriteLine("{0}<{1}>", new string('\t', depth), xmlReader.Name);
                                                depth++;
                                                break;
                                            case XmlNodeType.Text:
                                                // Depending on what your data looks like, you should either use Value or GetValueAsync
                                                // Value has less overhead (since it doesn't create a Task), but it may also block if additional data is required
                                                Console.WriteLine("{0}{1}", new string('\t', depth), await xmlReader.GetValueAsync());
                                                break;
                                            case XmlNodeType.EndElement:
                                                depth--;
                                                Console.WriteLine("{0}</{1}>", new string('\t', depth), xmlReader.Name);
                                                break;
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
}
// </Snippet1>
