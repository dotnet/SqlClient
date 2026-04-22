namespace SqlJsonExample;

// JSONConsoleApp: End-to-end demo of working with
// SQL Server JSON datatype via Microsoft.Data.SqlClient
//
// Highlights:
// - Creates a demo table with a JSON column
// - Inserts JSON via string and via SqlJson
// - Inserts JSON from a file
// - Reads JSON back with GetString and GetSqlJson
// - Demonstrates a stored procedure with a JSON OUTPUT parameter
//
// Requirements:
// - SQL Server 2025 and above.
// - Microsoft.Data.SqlClient (6.1.2 and above)
//<Snippet1>
using System.Data;
using Microsoft.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using System.Text.Json;
using System;
using System.IO;

class Program
{
    // NOTE: For production, prefer Encrypt=True and TrustServerCertificate=False with a valid cert.
    private const string connString = @"Server=tcp:localhost;Database=Demo2;Integrated Security=True;Encrypt=true;TrustServerCertificate=true;";
    private const string tableName = "jsonTab";
    private const string createBody = "(Id INT IDENTITY(1,1) PRIMARY KEY, Data json NULL)";

    static void Main(string[] args)
    {
        CreateTable(connString, tableName, createBody);

        InsertJSONWithStr(connString);
        InsertJSONWithSqlJson(connString);
        InsertJSONFile(connString);
        ReadJSONWithGetString(connString);
        ReadJSONWithSqlJson(connString);
        ReadJsonWithSPParam(connString);
    }

    #region Helper Methods to Create/Drop Table and stored procedures
    public static void CreateSP(string connString, string spName, string spBody)
    {
        DropSP(connString, spName);
        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            string spCreate = "CREATE PROCEDURE " + spName + spBody;
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = spCreate;
                command.ExecuteNonQuery();
            }
        }
    }

    public static void DropSP(string connString, string spName)
    {
        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (OBJECT_ID('{0}') IS NOT NULL) \n DROP PROCEDURE {0}", spName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    private static void CreateTable(string connString, string tableName, string createBody)
    {
        DropTable(connString, tableName);
        string tableCreate = "CREATE TABLE [" + tableName + "]" + createBody;
        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = tableCreate;
                command.ExecuteNonQuery();
            }
        }
    }

    private static void DropTable(string connString, string tableName)
    {
        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (OBJECT_ID('{0}') IS NOT NULL) \n DROP TABLE {0}", tableName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    #endregion

    private static void ReadJsonWithSPParam(string connString)
    {
        Console.WriteLine("Reading JSON with Stored Procedure and Output Parameter");

        // Create stored procedure for reading JSON value
        string storedProcName = "GetJsonData";
        string tableName = "jsonTab";
        string createSP = $@"
                @id int,
                @jsonData json OUTPUT
                AS
                BEGIN
                    SELECT @jsonData = (SELECT Data FROM {tableName} WHERE Id = @id)
                END;";
        CreateSP(connString, storedProcName, createSP);

        using (SqlConnection connection = new SqlConnection(connString))
        {
            connection.Open();
            string procName = "GetJsonData";

            // Execute Stored Procedure
            using (SqlCommand spCommand = new SqlCommand(procName, connection))
            {
                spCommand.CommandType = CommandType.StoredProcedure;
                spCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Direction = ParameterDirection.Input, Value = 1 });

                //Use SqlDbType.Json depending on the dotnet version or or SqlDbTypeExtensions.Json 
                SqlParameter outputParam = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Direction = ParameterDirection.Output };
                spCommand.Parameters.Add(outputParam);
                spCommand.ExecuteNonQuery();
                Console.WriteLine($"JSON Data from SP: {outputParam.Value}");
            }
        }
    }

    private static void ReadJSONWithSqlJson(string connString)
    {
        Console.WriteLine("Reading JSON into SqlJson");
        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            using (SqlCommand command = new SqlCommand("SELECT Data FROM jsonTab", sqlConnection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            //Read JSON with GetString
                            var jsonData = reader.GetSqlJson(0);
                            Console.WriteLine($"JSON Data: {jsonData}");
                        }
                        else
                        {
                            Console.WriteLine("JSON Data: NULL");
                        }
                    }
                }
            }
        }
    }

    private static void ReadJSONWithGetString(string connString)
    {
        Console.WriteLine("Reading JSON with GetString");
        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            using (SqlCommand command = new SqlCommand("SELECT Data FROM jsonTab", sqlConnection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            //Read JSON with GetString
                            string jsonData = reader.GetString(0);
                            Console.WriteLine($"JSON Data: {jsonData}");
                        }
                        else
                        {
                            Console.WriteLine("JSON Data: NULL");
                        }
                    }
                }
            }
        }
    }

    private static void InsertJSONFile(string connString)
    {
        Console.WriteLine("Inserting JSON File");
        var dummyData = new
        {
            name = "Keith Johnson",
            age = 36,
            city = "Boston"
        };

        string jsonString = JsonSerializer.Serialize(dummyData, new JsonSerializerOptions { WriteIndented = true });
        string jsonFile = "dummyData.json";
        if (File.Exists(jsonFile))
        {
            File.Delete(jsonFile);
        }
        File.WriteAllText(jsonFile, jsonString);

        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            using (SqlCommand command = new SqlCommand("INSERT INTO jsonTab (Data) VALUES (@jsonData)", sqlConnection))
            {
                SqlParameter jsonParam = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
                using (StreamReader jsonFileReader = File.OpenText(jsonFile))
                {
                    jsonParam.Value = jsonFileReader;
                    command.Parameters.Add(jsonParam);
                    command.ExecuteNonQuery();
                }
            }
        }
        if (File.Exists(jsonFile))
        {
            File.Delete(jsonFile);
        }
    }

    private static void InsertJSONWithSqlJson(string connString)
    {
        Console.WriteLine("Inserting JSON with SqlJson");
        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            using (SqlCommand command = new SqlCommand("INSERT INTO jsonTab (Data) VALUES (@jsonData)", sqlConnection))
            {
                //Insert non-null value with SqlJson
                string jsonData = @"{ ""name"": ""Jane Smith"", ""age"": 25, ""city"": ""Los Angeles"" }";
                SqlJson sqlJsonObj = new SqlJson(jsonData);
                SqlParameter jsonParam = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = sqlJsonObj };
                command.Parameters.Add(jsonParam);
                command.ExecuteNonQuery();
                command.Parameters.Clear();

                //Insert null value with SqlJson
                sqlJsonObj = new SqlJson();
                jsonParam.Value = sqlJsonObj;
                command.Parameters.Add(jsonParam);
                command.ExecuteNonQuery();
            }
        }
    }

    private static void InsertJSONWithStr(string connString)
    {
        Console.WriteLine("Inserting JSON with string");
        string insertQuery = "INSERT INTO jsonTab (Data) VALUES (@jsonData)";

        using (SqlConnection sqlConnection = new SqlConnection(connString))
        {
            sqlConnection.Open();
            using (SqlCommand command = new SqlCommand(insertQuery, sqlConnection))
            {
                //Insert non-null value
                string jsonData = @"{ ""name"": ""John Doe"", ""age"": 30, ""city"": ""New York"" }";
                command.Parameters.AddWithValue("@jsonData", jsonData);
                command.ExecuteNonQuery();
                command.Parameters.Clear();

                //Insert null value
                command.Parameters.AddWithValue("@jsonData", DBNull.Value);
                command.ExecuteNonQuery();
            }
        }
    }
}
//</Snippet1>
