// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class Utf8SupportTest
    {
        // Synapse: 'CONNECTIONPROPERTY' is not a recognized built-in function name.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsUTF8Supported), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void CheckSupportUtf8ConnectionProperty()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = "SELECT CONNECTIONPROPERTY('SUPPORT_UTF8')";
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Assert.Equal(1, reader.GetInt32(0));
                    }
                }
            }
        }


        // skip creating database on Azure
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsUTF8Supported))]
        public static void UTF8databaseTest()
        {
            const string letters = @"!\#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u007f€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f\u0090‘’“”•–—˜™š›œ\u009džŸ ¡¢£¤¥¦§¨©ª«¬­®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõö÷øùúûüýþÿ";
            string dbName = DataTestUtility.GetLongName("UTF8databaseTest", false);
            string tblName = "Table1";

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.InitialCatalog = "master";

            using SqlConnection cn = new(builder.ConnectionString);
            cn.Open();

            try
            {
                PrepareDatabaseUTF8(cn, dbName, tblName, letters);

                builder.InitialCatalog = dbName;
                using SqlConnection cnnTest = new(builder.ConnectionString);
                // creating a databse is a time consumer action and could be retried.
                SqlRetryLogicOption retryOption = new() { NumberOfTries = 3, DeltaTime = TimeSpan.FromMilliseconds(200) };
                cnnTest.RetryLogicProvider = SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(retryOption);
                cnnTest.Open();

                using SqlCommand cmd = cnnTest.CreateCommand();
                cmd.CommandText = $"SELECT * FROM {tblName}";

                using SqlDataReader reader = cmd.ExecuteReader();

                Assert.True(reader.Read(), "The test table should have a row!");
                object[] data = new object[1];
                reader.GetSqlValues(data);
                Assert.Equal(letters, data[0].ToString());
                reader.Close();
                cnnTest.Close();
            }
            finally
            {
                DataTestUtility.DropDatabase(cn, dbName);
            }
        }

        private static void PrepareDatabaseUTF8(SqlConnection cnn, string dbName, string tblName, string letters)
        {
            StringBuilder sb = new();

            using SqlCommand cmd = cnn.CreateCommand();

            cmd.CommandText = $"CREATE DATABASE [{dbName}] COLLATE Latin1_General_100_CI_AS_SC_UTF8;";
            cmd.ExecuteNonQuery();

            sb.AppendLine($"CREATE TABLE [{dbName}].dbo.[{tblName}] (col VARCHAR(7633) COLLATE Latin1_General_100_CI_AS_SC);");
            sb.AppendLine($"INSERT INTO [{dbName}].dbo.[{tblName}] VALUES (@letters);");

            cmd.Parameters.Add(new SqlParameter("letters", letters));
            cmd.CommandText = sb.ToString();
            cmd.ExecuteNonQuery();
        }
    }
}
