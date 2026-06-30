// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlExceptionTest
    {
        private const string badServer = "92B96911A0BD43E8ADA4451031F7E7CF";

        [Fact]
        public void SerializationTest()
        {
            SqlException e = CreateException();

            // Serialize the properties we want to validate round-trip through JSON.
            // SqlException cannot be directly serialized by System.Text.Json because
            // Exception.TargetSite (MethodBase) is not supported.
            string json = JsonSerializer.Serialize(new
            {
                e.Message,
                ClientConnectionId = e.ClientConnectionId.ToString(),
                e.Number,
                e.Class,
                e.State,
            });

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            Assert.Equal(e.Message, root.GetProperty("Message").GetString());
            Assert.Equal(e.ClientConnectionId.ToString(), root.GetProperty("ClientConnectionId").GetString());
            Assert.Equal(e.Number, root.GetProperty("Number").GetInt32());
            Assert.Equal(e.Class, root.GetProperty("Class").GetByte());
            Assert.Equal(e.State, root.GetProperty("State").GetByte());
        }

        private static SqlException CreateException()
        {
            var builder = new SqlConnectionStringBuilder()
            {
                DataSource = badServer,
                ConnectTimeout = 1,
                Pooling = false
            };

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                try
                {
                    connection.Open();
                }
                catch (SqlException ex)
                {
                    Assert.NotNull(ex.Errors);
                    Assert.Single(ex.Errors);

                    return ex;
                }
            }
            throw new InvalidOperationException("SqlException should have been returned.");
        }
    }
}
