using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
        private static readonly string _generatedJsonFile = DataTestUtility.GetShortName("randomRecords");
        private static readonly string _outputFile = DataTestUtility.GetShortName("serverResults");
        private static readonly string _sourceTableName = DataTestUtility.GetShortName("jsonBulkCopySrcTable", true);
        private static readonly string _destinationTableName = DataTestUtility.GetShortName("jsonBulkCopyDestTable", true);

        public JsonBulkCopyTest(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> JsonBulkCopyTestData()
        {
            yield return new object[] { CommandBehavior.Default, false, 30, 10 };
            yield return new object[] { CommandBehavior.Default, true, 30, 10 };
            yield return new object[] { CommandBehavior.SequentialAccess, false, 30, 10 };
            yield return new object[] { CommandBehavior.SequentialAccess, true, 30, 10 };
        }

        private void PopulateData(int noOfRecords, int rows)
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                DataTestUtility.CreateTable(connection, _sourceTableName, "(data json)");
                DataTestUtility.CreateTable(connection, _destinationTableName, "(data json)");
                GenerateJsonFile(noOfRecords, _generatedJsonFile);
                while (rows-- > 0)
                {
                    StreamJsonFileToServer(connection);
                }
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
            using (var stream1 = File.OpenText(_generatedJsonFile))
            using (var stream2 = File.OpenText(_outputFile))
            using (var reader1 = new JsonTextReader(stream1))
            using (var reader2 = new JsonTextReader(stream2))
            {
                var jToken1 = JToken.ReadFrom(reader1);
                var jToken2 = JToken.ReadFrom(reader2);
                Assert.True(JToken.DeepEquals(jToken1, jToken2));
            }
        }

        private void PrintJsonDataToFileAndCompare(SqlConnection connection)
        {
            try
            {
                DeleteFile(_outputFile);
                using (SqlCommand command = new SqlCommand("SELECT [data] FROM " + _destinationTableName, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                    {
                        while (reader.Read())
                        {
                            char[] buffer = new char[4096];
                            int charsRead = 0;

                            using (TextReader data = reader.GetTextReader(0))
                            {
                                using (StreamWriter sw = new StreamWriter(_outputFile))
                                {
                                    do
                                    {
                                        charsRead = data.Read(buffer, 0, buffer.Length);
                                        sw.Write(buffer, 0, charsRead);

                                    } while (charsRead > 0);
                                }
                            }
                            CompareJsonFiles();
                        }
                    }
                }
            }
            finally
            {
                DeleteFile(_outputFile);
            }

        }

        private async Task PrintJsonDataToFileAndCompareAsync(SqlConnection connection)
        {
            try
            {
                DeleteFile(_outputFile);
                using (SqlCommand command = new SqlCommand("SELECT [data] FROM " + _destinationTableName, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        while (await reader.ReadAsync())
                        {
                            char[] buffer = new char[4096];
                            int charsRead = 0;

                            using (TextReader data = reader.GetTextReader(0))
                            {
                                using (StreamWriter sw = new StreamWriter(_outputFile))
                                {
                                    do
                                    {
                                        charsRead = await data.ReadAsync(buffer, 0, buffer.Length);
                                        await sw.WriteAsync(buffer, 0, charsRead);

                                    } while (charsRead > 0);
                                }
                            }
                            CompareJsonFiles();
                        }
                    }
                }
            }
            finally
            {
                DeleteFile(_outputFile);
            }
        }

        private void StreamJsonFileToServer(SqlConnection connection)
        {
            using (SqlCommand cmd = new SqlCommand("INSERT INTO " + _sourceTableName + " (data) VALUES (@jsondata)", connection))
            {
                using (StreamReader jsonFile = File.OpenText(_generatedJsonFile))
                {
                    cmd.Parameters.Add("@jsondata", Microsoft.Data.SqlDbTypeExtensions.Json, -1).Value = jsonFile;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private async Task StreamJsonFileToServerAsync(SqlConnection connection)
        {
            using (SqlCommand cmd = new SqlCommand("INSERT INTO " + _sourceTableName + " (data) VALUES (@jsondata)", connection))
            {
                using (StreamReader jsonFile = File.OpenText(_generatedJsonFile))
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

        private void BulkCopyData(CommandBehavior cb, bool enableStraming, int expectedTransferCount)
        {
            using (SqlConnection sourceConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                sourceConnection.Open();
                SqlCommand commandRowCount = new SqlCommand("SELECT COUNT(*) FROM " + _destinationTableName, sourceConnection);
                long countStart = System.Convert.ToInt32(commandRowCount.ExecuteScalar());
                _output.WriteLine("Starting row count = {0}", countStart);
                SqlCommand commandSourceData = new SqlCommand("SELECT data FROM " + _sourceTableName, sourceConnection);
                SqlDataReader reader = commandSourceData.ExecuteReader(cb);
                using (SqlConnection destinationConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
                {
                    destinationConnection.Open();
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection))
                    {
                        bulkCopy.EnableStreaming = enableStraming;
                        bulkCopy.DestinationTableName = _destinationTableName;
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
                    Assert.Equal(expectedTransferCount, countEnd - countStart);
                }
            }
        }

        private async Task BulkCopyDataAsync(CommandBehavior cb, bool enableStraming, int expectedTransferCount)
        {
            using (SqlConnection sourceConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await sourceConnection.OpenAsync();
                SqlCommand commandRowCount = new SqlCommand("SELECT COUNT(*) FROM " + _destinationTableName, sourceConnection);
                long countStart = System.Convert.ToInt32(await commandRowCount.ExecuteScalarAsync());
                _output.WriteLine("Starting row count = {0}", countStart);
                SqlCommand commandSourceData = new SqlCommand("SELECT data FROM " + _sourceTableName, sourceConnection);
                SqlDataReader reader = await commandSourceData.ExecuteReaderAsync(cb);
                using (SqlConnection destinationConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
                {
                    await destinationConnection.OpenAsync();
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection))
                    {
                        bulkCopy.EnableStreaming = enableStraming;
                        bulkCopy.DestinationTableName = _destinationTableName;
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
                    Assert.Equal(expectedTransferCount, countEnd - countStart);
                }
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        [MemberData(
            nameof(JsonBulkCopyTestData)
#if NETFRAMEWORK
            // .NET Framework puts system enums in something called the Global
            // Assembly Cache (GAC), and xUnit refuses to serialize enums that
            // live there.  So for .NET Framework, we disable enumeration of the
            // test data to avoid warnings on the console when running tests.
            , DisableDiscoveryEnumeration = true
#endif
        )]
        public void TestJsonBulkCopy(CommandBehavior cb, bool enableStraming, int jsonArrayElements, int rows)
        {
            PopulateData(jsonArrayElements, rows);
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                BulkCopyData(cb, enableStraming, rows);
                connection.Open();
                PrintJsonDataToFileAndCompare(connection);
                DeleteFile(_generatedJsonFile);
                DeleteFile(_outputFile);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        [MemberData(
            nameof(JsonBulkCopyTestData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
        )]
        public async Task TestJsonBulkCopyAsync(CommandBehavior cb, bool enableStraming, int jsonArrayElements, int rows)
        {
            PopulateData(jsonArrayElements, rows);
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await BulkCopyDataAsync(cb, enableStraming, rows);
                await connection.OpenAsync();
                await PrintJsonDataToFileAndCompareAsync(connection);
                DeleteFile(_generatedJsonFile);
                DeleteFile(_outputFile);
            }
        }
    }
}
