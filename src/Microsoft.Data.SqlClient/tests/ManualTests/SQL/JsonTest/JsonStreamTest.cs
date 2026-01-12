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
    public class JsonRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class JsonStreamTest
    {
        private readonly ITestOutputHelper _output;
        private static readonly string _jsonFile = DataTestUtility.GetShortName("randomRecords") + ".json";
        private static readonly string _outputFile = DataTestUtility.GetShortName("serverRecords") + ".json";

        public JsonStreamTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private void GenerateJsonFile(int noOfRecords, string filename)
        {
            DeleteFile(filename);
            var random = new Random();
            var records = new List<JsonRecord>();
            int recordCount = noOfRecords;

            for (int i = 0; i < recordCount; i++)
            {
                records.Add(new JsonRecord
                {
                    Id = i + 1,
                    Name = "𩸽json" + random.Next(1, noOfRecords),
                });
            }

            string json = JsonConvert.SerializeObject(records, Formatting.Indented);
            File.WriteAllText(filename, json);
            Assert.True(File.Exists(filename));
            _output.WriteLine("Generated JSON file " + filename);
        }

        private void CompareJsonFiles()
        {
            using (var stream1 = File.OpenText(_jsonFile))
            using (var stream2 = File.OpenText(_outputFile))
            using (var reader1 = new JsonTextReader(stream1))
            using (var reader2 = new JsonTextReader(stream2))
            {
                var jToken1 = JToken.ReadFrom(reader1);
                var jToken2 = JToken.ReadFrom(reader2);
                Assert.True(JToken.DeepEquals(jToken1, jToken2));
            }
        }

        private void PrintJsonDataToFile(SqlConnection connection)
        {
            DeleteFile(_outputFile);
            using (SqlCommand command = new SqlCommand("SELECT [data] FROM [jsonTab]", connection))
            {
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    using (StreamWriter sw = new StreamWriter(_outputFile))
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
                            _output.WriteLine("Output written to " + _outputFile);
                        }
                    }
                }
            }
        }

        private async Task PrintJsonDataToFileAsync(SqlConnection connection)
        {
            DeleteFile(_outputFile);
            using (SqlCommand command = new SqlCommand("SELECT [data] FROM [jsonTab]", connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                {
                    using (StreamWriter sw = new StreamWriter(_outputFile))
                    {
                        while (await reader.ReadAsync())
                        {
                            char[] buffer = new char[4096];
                            int charsRead = 0;

                            using (TextReader data = reader.GetTextReader(0))
                            {
                                do
                                {
                                    charsRead = await data.ReadAsync(buffer, 0, buffer.Length);
                                    await sw.WriteAsync(buffer, 0, charsRead);

                                } while (charsRead > 0);
                            }
                            _output.WriteLine("Output written to file " + _outputFile);
                        }
                    }
                }
            }
        }

        private void StreamJsonFileToServer(SqlConnection connection)
        {
            using (SqlCommand cmd = new SqlCommand("INSERT INTO [jsonTab] (data) VALUES (@jsondata)", connection))
            {
                using (StreamReader jsonFile = File.OpenText(_jsonFile))
                {
                    cmd.Parameters.Add("@jsondata", Microsoft.Data.SqlDbTypeExtensions.Json, -1).Value = jsonFile;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private async Task StreamJsonFileToServerAsync(SqlConnection connection)
        {
            using (SqlCommand cmd = new SqlCommand("INSERT INTO [jsonTab] (data) VALUES (@jsondata)", connection))
            {
                using (StreamReader jsonFile = File.OpenText(_jsonFile))
                {
                    cmd.Parameters.Add("@jsondata", Microsoft.Data.SqlDbTypeExtensions.Json, -1).Value = jsonFile;
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private void DeleteFile(string filename)
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonStreaming()
        {
            GenerateJsonFile(1000, _jsonFile);
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                DataTestUtility.CreateTable(connection, "jsonTab", "(data json)");
                StreamJsonFileToServer(connection);
                PrintJsonDataToFile(connection);
                CompareJsonFiles();
                DeleteFile(_jsonFile);
                DeleteFile(_outputFile);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public async Task TestJsonStreamingAsync()
        {
            GenerateJsonFile(1000, _jsonFile);
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();
                DataTestUtility.CreateTable(connection, "jsonTab", "(data json)");
                await StreamJsonFileToServerAsync(connection);
                await PrintJsonDataToFileAsync(connection);
                CompareJsonFiles();
                DeleteFile(_jsonFile);
                DeleteFile(_outputFile);
            }
        }
    }
}
