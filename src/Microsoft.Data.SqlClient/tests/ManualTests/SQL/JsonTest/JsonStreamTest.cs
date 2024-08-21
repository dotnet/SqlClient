using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using Newtonsoft.Json.Linq;


namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class Record
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public  class JsonStreamTest
    {
        private readonly ITestOutputHelper _output;
        private static readonly string jsonFilename = "randomRecords.json";
        private static readonly string outputFilename = "serverRecords.json";

        public JsonStreamTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private void generateJsonFile(int noOfRecords, string filename)
        {
            deleteFile(filename);
            var random = new Random();
            var records = new List<Record>();
            int recordCount = noOfRecords;

            for (int i = 0; i < recordCount; i++)
            {
                records.Add(new Record
                {
                    Id = i + 1,
                    Name = "json" + random.Next(1, noOfRecords),
                });
            }

            string json = JsonConvert.SerializeObject(records, Formatting.Indented);
            File.WriteAllText(filename, json);
            Assert.True(File.Exists(filename));
            _output.WriteLine("Generated Json File "+filename);
        }

        private void compareJsonFiles()
        {
            using (var stream1 = File.OpenText(jsonFilename))
            using (var stream2 = File.OpenText(outputFilename))
            using (var reader1 = new JsonTextReader(stream1))
            using (var reader2 = new JsonTextReader(stream2))
            {
                var jToken1 = JToken.ReadFrom(reader1);
                var jToken2 = JToken.ReadFrom(reader2);
                Assert.True(JToken.DeepEquals(jToken1, jToken2));
            }
        }

        private void printJsonDataToFile(SqlConnection connection)
        {
            deleteFile(outputFilename);
            Console.OutputEncoding = Encoding.UTF8;
            using (SqlCommand command = new SqlCommand("SELECT [data] FROM [jsonTab]", connection))
            {
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    using (StreamWriter sw = new StreamWriter(outputFilename))
                    {
                        while (reader.Read())
                        {
                            char[] buffer = new char[4096];
                            int charsRead = 0;

                            using (TextReader data = reader.GetTextReader(0))
                            {
                                do
                                {
                                    charsRead = data.Read(buffer, 0, buffer.Length);
                                    sw.Write(buffer, 0, charsRead);

                                } while (charsRead > 0);
                            }
                            _output.WriteLine("Output written to " + outputFilename);
                        }
                    }
                }
            }
        }

        private void streamJsonFileToServer(SqlConnection connection)
        {
            using (SqlCommand cmd = new SqlCommand("INSERT INTO [jsonTab] (data) VALUES (@jsondata)", connection))
            {
                using (StreamReader jsonFile = File.OpenText(jsonFilename))
                {
                    cmd.Parameters.Add("@jsondata", Microsoft.Data.SqlDbTypeExtensions.Json, -1).Value = jsonFile;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void deleteFile(string filename)
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

        }
        

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsJsonSupported))]
        public void TestJsonStreaming()
        {
            generateJsonFile(10000, jsonFilename);

            string tableCreate = "CREATE TABLE jsonTab(data json)";
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                DataTestUtility.DropTable(connection, "jsonTab");
                using (SqlCommand command = connection.CreateCommand())
                {
                    //Create Table
                    command.CommandText = tableCreate;
                    command.ExecuteNonQuery();
                }
                streamJsonFileToServer(connection);
                printJsonDataToFile(connection);
                compareJsonFiles();
                deleteFile(jsonFilename);
                deleteFile(outputFilename);
            }
        }
    }
}

