// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class JsonTest
    {
        private readonly ITestOutputHelper _output;

        public JsonTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsJsonSupported))]
        public void TestJsonWrite()
        {
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            
            string jsonDataString = "[\r\n    {\r\n        \"name\": \"Dave\",\r\n        \"skills\": [ \"Python\" ]\r\n    },\r\n    {\r\n        \"name\": \"Ron\",\r\n        \"surname\": \"Peter\"\r\n    }\r\n]";

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();

                string query = "INSERT INTO dbo.JsonTable VALUES (@jsonData)";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Add the parameter and set its value
                    var parameter = new SqlParameter("@jsonData", jsonDataString);
                    parameter.SqlDbType = SqlDbTypeExtensions.Json;
                    command.Parameters.Add(parameter);

                    for (int i = 0; i < 10; i++)
                    {
                        int rowsAffected = command.ExecuteNonQuery();
                        _output.WriteLine($"Rows affected: {rowsAffected}");
                    }
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsJsonSupported))]
        public void TestJsonRead()
        {
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            
            SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();
            SqlCommand command = connection.CreateCommand();
            string commandText = "SELECT * FROM dbo.JsonTable";
            command.CommandText = commandText;
            SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string jsonData = reader.GetString(0);
                _output.WriteLine(jsonData);
                Assert.NotNull(jsonData);
            }
            System.Collections.ObjectModel.ReadOnlyCollection<DbColumn> schema = reader.GetColumnSchema();
            foreach (DbColumn column in schema)
            {
                _output.WriteLine("Column Name is " + column.ColumnName);
                _output.WriteLine("Column DataType is " + column?.DataType.ToString());
                _output.WriteLine("Column DataTypeName is " + column.DataTypeName);
            }
            reader.Close();
            connection.Close();
        }
    }
}
