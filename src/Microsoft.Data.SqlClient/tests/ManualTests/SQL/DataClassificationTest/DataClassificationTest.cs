// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Data;
using Microsoft.Data.SqlClient.DataClassification;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class DataClassificationTest
    {
        private static string s_tableName;

        // Synapse: Azure Synapse does not support RANK
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsSupportedDataClassification))]
        public static void TestDataClassificationResultSetRank()
        {
            s_tableName = DataTestUtility.GetUniqueNameForSqlServer("DC");
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
            {
                try
                {
                    sqlConnection.Open();
                    Assert.True(DataTestUtility.IsSupportedDataClassification());
                    CreateTable(sqlCommand);
                    AddSensitivity(sqlCommand, rankEnabled: true);
                    InsertData(sqlCommand);
                    RunTestsForServer(sqlCommand, rankEnabled: true);
                }
                finally
                {
                    DataTestUtility.DropTable(sqlConnection, s_tableName);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSupportedDataClassification))]
        public static void TestDataClassificationResultSet()
        {
            s_tableName = DataTestUtility.GetUniqueNameForSqlServer("DC");
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            using (SqlCommand sqlCommand = sqlConnection.CreateCommand())
            {
                try
                {
                    sqlConnection.Open();
                    Assert.True(DataTestUtility.IsSupportedDataClassification());
                    CreateTable(sqlCommand);
                    AddSensitivity(sqlCommand);
                    InsertData(sqlCommand);
                    RunTestsForServer(sqlCommand);
                }
                finally
                {
                    DataTestUtility.DropTable(sqlConnection, s_tableName);
                }
            }
        }

        private static void RunTestsForServer(SqlCommand sqlCommand, bool rankEnabled = false)
        {
            sqlCommand.CommandText = "SELECT * FROM " + s_tableName;
            using (SqlDataReader reader = sqlCommand.ExecuteReader())
            {
                VerifySensitivityClassification(reader, rankEnabled);
            }
        }

        private static void VerifySensitivityClassification(SqlDataReader reader, bool rankEnabled = false)
        {
            if (null != reader.SensitivityClassification)
            {
                for (int columnPos = 0; columnPos < reader.SensitivityClassification.ColumnSensitivities.Count;
                        columnPos++)
                {
                    foreach (SensitivityProperty sp in reader.SensitivityClassification.ColumnSensitivities[columnPos].SensitivityProperties)
                    {
                        ReadOnlyCollection<InformationType> infoTypes = reader.SensitivityClassification.InformationTypes;
                        Assert.Equal(3, infoTypes.Count);
                        for (int i = 0; i < infoTypes.Count; i++)
                        {
                            VerifyInfoType(infoTypes[i], i + 1);
                        }

                        ReadOnlyCollection<Label> labels = reader.SensitivityClassification.Labels;
                        Assert.Single(labels);
                        VerifyLabel(labels[0]);

                        if (columnPos == 1 || columnPos == 2 || columnPos == 6 || columnPos == 7)
                        {
                            VerifyLabel(sp.Label);
                            VerifyInfoType(sp.InformationType, columnPos);
                        }
                        if (rankEnabled)
                        {
                            if (columnPos == 1 || columnPos == 2)
                            {
                                Assert.Equal(SensitivityRank.LOW, sp.SensitivityRank);
                            }
                            else if (columnPos == 6 || columnPos == 7)
                            {
                                Assert.Equal(SensitivityRank.MEDIUM, sp.SensitivityRank);
                            }
                        }
                        else
                        {
                            Assert.True(reader.SensitivityClassification.SensitivityRank == SensitivityRank.NOT_DEFINED);
                        }
                    }
                }
                Assert.Equal(reader.SensitivityClassification.SensitivityRank, rankEnabled ? SensitivityRank.MEDIUM : SensitivityRank.NOT_DEFINED);
            }
        }

        private static void VerifyLabel(Label label)
        {
            Assert.True(label != null);
            Assert.Equal("L1", label.Id);
            Assert.Equal("PII", label.Name);
        }

        private static void VerifyInfoType(InformationType informationType, int i)
        {
            Assert.True(informationType != null);
            Assert.Equal(i == 1 ? "COMPANY" : (i == 2 ? "NAME" : "CONTACT"), informationType.Id);
            Assert.Equal(i == 1 ? "Company Name" : (i == 2 ? "Person Name" : "Contact Information"), informationType.Name);
        }

        private static void CreateTable(SqlCommand sqlCommand)
        {
            sqlCommand.CommandText = "CREATE TABLE " + s_tableName + " ("
                + "[Id] [int] IDENTITY(1,1) NOT NULL,"
                + "[CompanyName] [nvarchar](40) NOT NULL,"
                + "[ContactName] [nvarchar](50) NULL,"
                + "[ContactTitle] [nvarchar](40) NULL,"
                + "[City] [nvarchar](40) NULL,"
                + "[CountryName] [nvarchar](40) NULL,"
                + "[Phone] [nvarchar](30) MASKED WITH (FUNCTION = 'default()') NULL,"
                + "[Fax] [nvarchar](30) MASKED WITH (FUNCTION = 'default()') NULL)";
            sqlCommand.ExecuteNonQuery();
        }

        private static void AddSensitivity(SqlCommand sqlCommand, bool rankEnabled = false)
        {
            if (rankEnabled)
            {
                sqlCommand.CommandText = "ADD SENSITIVITY CLASSIFICATION TO " + s_tableName
                        + ".CompanyName WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Company Name', INFORMATION_TYPE_ID='COMPANY', RANK=LOW)";
                sqlCommand.ExecuteNonQuery();

                sqlCommand.CommandText = "ADD SENSITIVITY CLASSIFICATION TO " + s_tableName
                        + ".ContactName WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Person Name', INFORMATION_TYPE_ID='NAME', RANK=LOW)";
                sqlCommand.ExecuteNonQuery();

                sqlCommand.CommandText = "ADD SENSITIVITY CLASSIFICATION TO " + s_tableName
                        + ".Phone WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Contact Information', INFORMATION_TYPE_ID='CONTACT', RANK=MEDIUM)";
                sqlCommand.ExecuteNonQuery();

                sqlCommand.CommandText = "ADD SENSITIVITY CLASSIFICATION TO " + s_tableName
                        + ".Fax WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Contact Information', INFORMATION_TYPE_ID='CONTACT', RANK=MEDIUM)";
                sqlCommand.ExecuteNonQuery();
            }
            else
            {
                sqlCommand.CommandText = "ADD SENSITIVITY CLASSIFICATION TO " + s_tableName
                        + ".CompanyName WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Company Name', INFORMATION_TYPE_ID='COMPANY')";
                sqlCommand.ExecuteNonQuery();

                sqlCommand.CommandText = "ADD SENSITIVITY CLASSIFICATION TO " + s_tableName
                        + ".ContactName WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Person Name', INFORMATION_TYPE_ID='NAME')";
                sqlCommand.ExecuteNonQuery();

                sqlCommand.CommandText = "ADD SENSITIVITY CLASSIFICATION TO " + s_tableName
                        + ".Phone WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Contact Information', INFORMATION_TYPE_ID='CONTACT')";
                sqlCommand.ExecuteNonQuery();

                sqlCommand.CommandText = "ADD SENSITIVITY CLASSIFICATION TO " + s_tableName
                        + ".Fax WITH (LABEL='PII', LABEL_ID='L1', INFORMATION_TYPE='Contact Information', INFORMATION_TYPE_ID='CONTACT')";
                sqlCommand.ExecuteNonQuery();
            }
        }

        private static void InsertData(SqlCommand sqlCommand)
        {
            // INSERT ROWS OF DATA
            sqlCommand.CommandText = "INSERT INTO " + s_tableName + " VALUES (@companyName, @contactName, @contactTitle, @city, @country, @phone, @fax)";

            sqlCommand.Parameters.AddWithValue("@companyName", "Exotic Liquids");
            sqlCommand.Parameters.AddWithValue("@contactName", "Charlotte Cooper");
            sqlCommand.Parameters.AddWithValue("@contactTitle", "");
            sqlCommand.Parameters.AddWithValue("city", "London");
            sqlCommand.Parameters.AddWithValue("@country", "UK");
            sqlCommand.Parameters.AddWithValue("@phone", "(171) 555-2222");
            sqlCommand.Parameters.AddWithValue("@fax", "(171) 554-2222");
            sqlCommand.ExecuteNonQuery();

            sqlCommand.Parameters.Clear();
            sqlCommand.Parameters.AddWithValue("@companyName", "New Orleans");
            sqlCommand.Parameters.AddWithValue("@contactName", "Cajun Delights");
            sqlCommand.Parameters.AddWithValue("@contactTitle", "");
            sqlCommand.Parameters.AddWithValue("city", "New Orleans");
            sqlCommand.Parameters.AddWithValue("@country", "USA");
            sqlCommand.Parameters.AddWithValue("@phone", "(100) 555-4822");
            sqlCommand.Parameters.AddWithValue("@fax", "(100) 223-3243");
            sqlCommand.ExecuteNonQuery();

            sqlCommand.Parameters.Clear();
            sqlCommand.Parameters.AddWithValue("@companyName", "Grandma Kelly's Homestead");
            sqlCommand.Parameters.AddWithValue("@contactName", "Regina Murphy");
            sqlCommand.Parameters.AddWithValue("@contactTitle", "");
            sqlCommand.Parameters.AddWithValue("@city", "Ann Arbor");
            sqlCommand.Parameters.AddWithValue("@country", "USA");
            sqlCommand.Parameters.AddWithValue("@phone", "(313) 555-5735");
            sqlCommand.Parameters.AddWithValue("@fax", "(313) 555-3349");
            sqlCommand.ExecuteNonQuery();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSupportedDataClassification))]
        public static void TestDataClassificationBulkCopy()
        {
            var data = new DataTable("Company");
            data.Columns.Add("CompanyId", typeof(Guid));
            data.Columns.Add("CompanyName", typeof(string));
            data.Columns.Add("Email", typeof(string));
            data.Columns.Add("CompanyType", typeof(int));

            data.Rows.Add(Guid.NewGuid(), "Company 1", "sample1@contoso.com", 1);
            data.Rows.Add(Guid.NewGuid(), "Company 2", "sample2@contoso.com", 1);
            data.Rows.Add(Guid.NewGuid(), "Company 3", "sample3@contoso.com", 1);

            var tableName = DataTestUtility.GetUniqueNameForSqlServer("DC");

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                try
                {
                    // Setup Table
                    using (SqlCommand sqlCommand = connection.CreateCommand())
                    {
                        sqlCommand.CommandText = $"CREATE TABLE {tableName} (" +
                            $" [CompanyId] [uniqueidentifier] NOT NULL," +
                            $" [CompanyName][nvarchar](255) NOT NULL," +
                            $" [Email] [nvarchar](50) NULL," +
                            $" [CompanyType] [int] not null)";
                        sqlCommand.ExecuteNonQuery();
                        sqlCommand.CommandText = $"ADD SENSITIVITY CLASSIFICATION TO {tableName}.CompanyName WITH (label = 'Confidential', label_id = 'c185460f-4e20-4b89-9876-ae95f07ba087', information_type = 'Contact Info', information_type_id = '5c503e21-22c6-81fa-620b-f369b8ec38d1');";
                        sqlCommand.CommandText = $"ADD SENSITIVITY CLASSIFICATION TO {tableName}.Email WITH (label = 'Confidential', label_id = 'c185460f-4e20-4b89-9876-ae95f07ba087', information_type = 'Contact Info', information_type_id = '5c503e21-22c6-81fa-620b-f369b8ec38d1', rank = HIGH);";
                        sqlCommand.ExecuteNonQuery();
                    }

                    // Perform Bulk Insert
                    using (var bulk = new SqlBulkCopy(connection))
                    {
                        bulk.DestinationTableName = tableName;
                        bulk.ColumnMappings.Add("CompanyId", "CompanyId");
                        bulk.ColumnMappings.Add("CompanyName", "CompanyName");
                        bulk.ColumnMappings.Add("Email", "Email");
                        bulk.ColumnMappings.Add("CompanyType", "CompanyType");
                        bulk.WriteToServer(data);
                    }
                }
                finally
                {
                    DataTestUtility.DropTable(connection, tableName);
                }
            }
        }
    }
}
