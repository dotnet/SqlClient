using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Xunit.Abstractions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.JsonTest
{
    public class JsonBulkCopyTest
    {
        private readonly ITestOutputHelper _output;
        private static readonly string _jsonFile = "randomRecords.json";
        private static readonly string _outputFile = "serverRecords.json";
        private static readonly bool _isTestEnabled = DataTestUtility.IsJsonSupported;
        
        public JsonBulkCopyTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private void PopulateData(int noOfRecords)
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                DataTestUtility.CreateTable(connection, "jsonTab", "(data json)");
                DataTestUtility.CreateTable(connection, "jsonTabCopy", "(data json)");
                GenerateJsonFile(50000, _jsonFile);
                StreamJsonFileToServer(connection);
            }
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
                    //Inclusion of 𩸽 and क is intentional to include 4byte and 3 byte UTF8character
                    Name = "𩸽jsonक" + random.Next(1, noOfRecords),
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
            using (SqlCommand command = new SqlCommand("SELECT [data] FROM [jsonTabCopy]", connection))
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

        private void BulkCopyData(CommandBehavior cb, bool enableStraming)
        {
            using (SqlConnection sourceConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                sourceConnection.Open();
                SqlCommand commandRowCount = new SqlCommand("SELECT COUNT(*) FROM " + "dbo.jsonTabCopy;", sourceConnection);
                long countStart = System.Convert.ToInt32(commandRowCount.ExecuteScalar());
                _output.WriteLine("Starting row count = {0}", countStart);
                SqlCommand commandSourceData = new SqlCommand("SELECT data FROM dbo.jsonTab;", sourceConnection);
                SqlDataReader reader = commandSourceData.ExecuteReader(cb);
                using (SqlConnection destinationConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
                {
                    destinationConnection.Open();
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection))
                    {
                        bulkCopy.EnableStreaming = enableStraming;
                        bulkCopy.DestinationTableName = "dbo.jsonTabCopy";
                        try
                        {
                            bulkCopy.WriteToServer(reader);
                        }
                        catch (Exception ex)
                        {
                            Assert.Fail(ex.Message);
                        }
                        finally
                        {
                            reader.Close();
                        }
                    }
                    long countEnd = System.Convert.ToInt32(commandRowCount.ExecuteScalar());
                    _output.WriteLine("Ending row count = {0}", countEnd);
                    _output.WriteLine("{0} rows were added.", countEnd - countStart);
                }
            }
        }

        private async Task BulkCopyDataAsync(CommandBehavior cb, bool enableStraming)
        {
            using (SqlConnection sourceConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await sourceConnection.OpenAsync();
                SqlCommand commandRowCount = new SqlCommand("SELECT COUNT(*) FROM " + "dbo.jsonTabCopy;", sourceConnection);
                long countStart = System.Convert.ToInt32(await commandRowCount.ExecuteScalarAsync());
                _output.WriteLine("Starting row count = {0}", countStart);
                SqlCommand commandSourceData = new SqlCommand("SELECT data FROM dbo.jsonTab;", sourceConnection);
                SqlDataReader reader = await commandSourceData.ExecuteReaderAsync(cb);
                using (SqlConnection destinationConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
                {
                    await destinationConnection.OpenAsync();
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection))
                    {
                        bulkCopy.EnableStreaming = enableStraming;
                        bulkCopy.DestinationTableName = "dbo.jsonTabCopy";
                        try
                        {
                            await bulkCopy.WriteToServerAsync(reader);
                        }
                        catch (Exception ex)
                        {
                            Assert.Fail(ex.Message);
                        }
                        finally
                        {
                            reader.Close();
                        }
                    }
                    long countEnd = System.Convert.ToInt32(await commandRowCount.ExecuteScalarAsync());
                    _output.WriteLine("Ending row count = {0}", countEnd);
                    _output.WriteLine("{0} rows were added.", countEnd - countStart);
                }
            }
        }

        [Theory]
        [InlineData(CommandBehavior.Default, false)]
        [InlineData(CommandBehavior.Default, true)]
        [InlineData(CommandBehavior.SequentialAccess, false)]
        [InlineData(CommandBehavior.SequentialAccess, true)]
        public void TestJsonBulkCopy(CommandBehavior cb, bool enableStraming)
        {
            if (!_isTestEnabled)
            {
                return;
            }

            PopulateData(10000);
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                BulkCopyData(cb, enableStraming);
                connection.Open();
                PrintJsonDataToFile(connection);
                CompareJsonFiles();
                DeleteFile(_jsonFile);
                DeleteFile(_outputFile);
            }
        }

        [Theory]
        [InlineData(CommandBehavior.Default, false)]
        [InlineData(CommandBehavior.Default, true)]
        [InlineData(CommandBehavior.SequentialAccess, false)]
        [InlineData(CommandBehavior.SequentialAccess, true)]
        public async Task TestJsonBulkCopyAsync(CommandBehavior cb, bool enableStraming)
        {
            if (!_isTestEnabled)
            {
                return;
            }
            PopulateData(10000);
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await BulkCopyDataAsync(cb, enableStraming);
                await connection.OpenAsync();
                await PrintJsonDataToFileAsync(connection);
                CompareJsonFiles();
                DeleteFile(_jsonFile);
                DeleteFile(_outputFile);
            }
        }
    }
}
