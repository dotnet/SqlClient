// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Xml;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.Json
{
    public class SqlJsonE2ETests
    {
        [Fact]
        public void SqlJsonRetrieval()
        {
            string commandText = "select * from dbo.jsonTab";
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "10.224.90.149,1433",
                UserID = "sa",
                Password = "Yukon900!Welcome",
                Encrypt = false,
                TrustServerCertificate = true,
                InitialCatalog = "JSONDbSaurabh"
            };
            string connectionString = builder.ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SqlString sqlString = reader.GetSqlString(0);
                            Assert.NotNull(sqlString.Value);
                            var sqlJson = reader.GetSqlJson(0);
                            Assert.NotNull(sqlJson);
                            string fieldval = reader.GetFieldValue<string>(0);
                            Assert.NotNull(fieldval);
                            JsonDocument jsondoc = reader.GetFieldValue<JsonDocument>(0);
                            Assert.NotNull(jsondoc);
                            Console.WriteLine(sqlString.Value);
                        }
                    }
                }
            }
        }

        [Fact]
        public void SqlJsonTest_Null()
        {
            //AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

            bool testSqlJson = true;
            SqlParameter param = null;
            string commandText = "insert into xmltable values (@xml)";
            string connectionString = null;
            if (testSqlJson)
            {
                string jsonPayload = "{\r\n    " +
                    "\"name\": \"John Doe\",\r\n    " +
                    "\"age\": 30,\r\n    " +
                    "\"isStudent\": false,\r\n    " +
                    "\"courses\": " +
                    "   [" +
                    "       \"Mathematics\"," +
                    "       \"Computer Science\"" +
                    " ], " +
                    " \"address\": {" +
                    "        \"street\": \"123 Main St\",\r\n        \"city\": \"Anytown\", " +
                    "        \"zipCode\": \"11112345\"\r\n    " +
                    "}" +
                    "}";
                JsonDocument document = JsonDocument.Parse(jsonPayload);
                SqlJson sqlJson = new(document);

                commandText = "Insert into jsonTab values (@jsonData)";
                param = new SqlParameter() { ParameterName = "@jsonData", Value = sqlJson };

                SqlConnectionStringBuilder builder = new()
                {
                    DataSource = "10.224.90.149,1433",
                    UserID = "sa",
                    Password = "Yukon900!Welcome",
                    Encrypt = false,
                    TrustServerCertificate = true,
                    InitialCatalog = "JSONDbSaurabh"
                };
                connectionString = builder.ConnectionString;
            }
            else
            {
                string xmlString = "<root></root>";

                _ = new SqlString(xmlString);

                TextReader textReader = new StringReader(xmlString);
                XmlReader reader = XmlReader.Create(textReader);

                SqlXml sqlXmlInstance = new SqlXml(reader);
                param = new SqlParameter() { ParameterName = "@xml", Value = sqlXmlInstance };

                SqlConnectionStringBuilder builder = new()
                {
                    DataSource = "localhost",
                    IntegratedSecurity = true,
                    Encrypt = false,
                    TrustServerCertificate = true,
                    InitialCatalog = "xmlstuff"
                };
                connectionString = builder.ConnectionString;
            }

            using SqlConnection conn = new(connectionString);
            conn.Open();
            using SqlCommand cmd = new(commandText, conn);
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Parameters.Add(param);
            cmd.ExecuteNonQuery();
        }
    }
}
