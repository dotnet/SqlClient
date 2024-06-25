// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.JSON
{
    public class JsonTest
    {
        private readonly ITestOutputHelper _output;

        private readonly string PWD = Environment.GetEnvironmentVariable("SQL_PASSWORD") != null ? Environment.GetEnvironmentVariable("SQL_PASSWORD") : "";
        public JsonTest(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public void TestJsonWrite()
        {
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = "tcp:10.224.90.151";
            builder.UserID = "sa";
            builder.Password = PWD;
            builder.InitialCatalog = "TestJson";
            builder.TrustServerCertificate = true;
            builder.Encrypt = false;
            string jsonData = "{\"key\":\"value\"}"; // Example JSON data

            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();

                string query = "INSERT INTO JsonTable (jsonCol) VALUES (@jsonData)";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Add the parameter and set its value
                    var parameter = new SqlParameter("@jsonData", jsonData);
                    parameter.SqlDbType = (SqlDbType)35;
                    command.Parameters.Add(parameter);

                    for (int i = 0; i < 1000; i++)
                    {
                        int rowsAffected = command.ExecuteNonQuery();
                        Console.WriteLine($"Rows affected: {rowsAffected}");
                    }
                    // Execute the command
                    
                }
            }
        }

            [Fact]
        public void TestJsonRead()
        {
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = "tcp:10.224.90.151";
            builder.UserID = "sa";
            builder.Password = PWD;
            builder.InitialCatalog = "TestJson";
            builder.TrustServerCertificate = true;
            builder.Encrypt = false;
            SqlConnection connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            SqlCommand command = connection.CreateCommand();
            string commandText = "SELECT * FROM JsonTable";
            command.CommandText = commandText;
            SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string json = reader.GetString(0);
                _output.WriteLine(json);
                Assert.NotNull(json);
            }
            System.Collections.ObjectModel.ReadOnlyCollection<DbColumn> schema = reader.GetColumnSchema();
            foreach (DbColumn column in schema)
            {
                _output.WriteLine(column.ColumnName);
                _output.WriteLine(column?.DataType.ToString());
                _output.WriteLine(column.DataTypeName);
            }
            reader.Close();
            connection.Close();
        }
    }
}
