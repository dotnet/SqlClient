// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

    public  class JsonStreamTest
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
            _output.WriteLine("Generated JSON file "+filename);
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

        private void PrintJsonDataToFile(SqlConnection connection, string tableName)
        {
            DeleteFile(_outputFile);
            using (SqlCommand command = new SqlCommand($"SELECT [data] FROM [{tableName}]", connection))
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

        private async Task PrintJsonDataToFileAsync(SqlConnection connection, string tableName)
        {
            DeleteFile(_outputFile);
            using (SqlCommand command = new SqlCommand($"SELECT [data] FROM [{tableName}]", connection))
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

        private void StreamJsonFileToServer(SqlConnection connection, string tableName)
        {
            using (SqlCommand cmd = new SqlCommand($"INSERT INTO [{tableName}] (data) VALUES (@jsondata)", connection))
            {
                using (StreamReader jsonFile = File.OpenText(_jsonFile))
                {
                    cmd.Parameters.Add("@jsondata", Microsoft.Data.SqlDbTypeExtensions.Json, -1).Value = jsonFile;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private async Task StreamJsonFileToServerAsync(SqlConnection connection, string tableName)
        {
            using (SqlCommand cmd = new SqlCommand($"INSERT INTO [{tableName}] (data) VALUES (@jsondata)", connection))
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

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonStreaming()
        {
            GenerateJsonFile(1000, _jsonFile);
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                var tableName = DataTestUtility.GetLongName("jsonTab");
                DataTestUtility.CreateTable(connection, tableName, "(data json)");
                StreamJsonFileToServer(connection, tableName);
                PrintJsonDataToFile(connection, tableName);
                CompareJsonFiles();
                DeleteFile(_jsonFile);
                DeleteFile(_outputFile);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public async Task TestJsonStreamingAsync()
        {
            GenerateJsonFile(1000, _jsonFile);
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();
                var tableName = DataTestUtility.GetLongName("jsonTab");
                DataTestUtility.CreateTable(connection, tableName, "(data json)");
                await StreamJsonFileToServerAsync(connection, tableName);
                await PrintJsonDataToFileAsync(connection, tableName);
                CompareJsonFiles();
                DeleteFile(_jsonFile);
                DeleteFile(_outputFile);
            }
        }
    }
}

