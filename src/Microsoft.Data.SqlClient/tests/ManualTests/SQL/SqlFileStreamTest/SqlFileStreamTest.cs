// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.IO;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class SqlFileStreamTest
    {
        private static bool IsFileStreamEnvironmentSet() => DataTestUtility.IsFileStreamSetup();
        private static bool AreConnectionStringsSetup() => DataTestUtility.AreConnStringsSetup();
        private static bool IsIntegratedSecurityEnvironmentSet() => DataTestUtility.IsIntegratedSecuritySetup();

        private static int[] s_insertedValues = { 11, 22 };
        private static string s_fileStreamDBName = null;

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalFact(nameof(IsFileStreamEnvironmentSet), nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void ReadFilestream()
        {
            try
            {
                string connString = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = SetupFileStreamDB(),
                    IntegratedSecurity = true
                }.ConnectionString;

                string tempTable = SetupTable(connString);
                int nRow = 0;
                byte[] retrievedValue;
                using SqlConnection connection = new(connString);
                connection.Open();
                SqlCommand command = new($"SELECT Photo.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT(),EmployeeId FROM {tempTable} ORDER BY EmployeeId", connection);
                try
                {
                    SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                    command.Transaction = transaction;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Get the pointer for the file.
                            string path = reader.GetString(0);
                            byte[] transactionContext = reader.GetSqlBytes(1).Buffer;

                            // Create the SqlFileStream  
                            using (Stream fileStream = new SqlFileStream(path, transactionContext, FileAccess.Read, FileOptions.SequentialScan, allocationSize: 0))
                            {
                                // Read the contents as bytes.
                                retrievedValue = new byte[fileStream.Length];
                                fileStream.Read(retrievedValue, 0, (int)(fileStream.Length));

                                // Reverse the byte array, if the system architecture is little-endian.
                                if (BitConverter.IsLittleEndian)
                                    Array.Reverse(retrievedValue);

                                // Compare inserted and retrieved values.
                                Assert.Equal(s_insertedValues[nRow], BitConverter.ToInt32(retrievedValue, 0));
                            }
                            nRow++;
                        }

                    }
                    transaction.Commit();
                }
                finally
                {
                    // Drop Table
                    ExecuteNonQueryCommand($"DROP TABLE {tempTable}", connString);
                }
            }
            finally
            {
                DropFileStreamDb(DataTestUtility.TCPConnectionString);
            }
        }

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalFact(nameof(IsFileStreamEnvironmentSet), nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void OverwriteFilestream()
        {
            try
            {
                string connString = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = SetupFileStreamDB(),
                    IntegratedSecurity = true
                }.ConnectionString;

                string tempTable = SetupTable(connString);
                byte[] insertedValue = BitConverter.GetBytes(3);

                // Reverse the byte array, if the system architecture is little-endian.
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(insertedValue);
                try
                {
                    using SqlConnection connection = new(connString);
                    connection.Open();
                    SqlCommand command = new($"SELECT TOP(1) Photo.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT(),EmployeeId FROM {tempTable} ORDER BY EmployeeId", connection);

                    SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                    command.Transaction = transaction;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Get the pointer for file   
                            string path = reader.GetString(0);
                            byte[] transactionContext = reader.GetSqlBytes(1).Buffer;

                            // Create the SqlFileStream  
                            using Stream fileStream = new SqlFileStream(path, transactionContext, FileAccess.Write, FileOptions.SequentialScan, allocationSize: 0);
                            // Overwrite the first row in the table
                            fileStream.Write((insertedValue), 0, 4);
                        }
                    }
                    transaction.Commit();

                    // Compare inserted and retrieved value
                    byte[] retrievedValue = RetrieveData(tempTable, connString, insertedValue.Length);
                    Assert.Equal(insertedValue, retrievedValue);
                }
                finally
                {
                    // Drop Table
                    ExecuteNonQueryCommand($"DROP TABLE {tempTable}", connString);
                }
            }
            finally
            {
                DropFileStreamDb(DataTestUtility.TCPConnectionString);
            }
        }

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalFact(nameof(IsFileStreamEnvironmentSet), nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void AppendFilestream()
        {
            try
            {
                string connString = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = SetupFileStreamDB(),
                    IntegratedSecurity = true
                }.ConnectionString;

                string tempTable = SetupTable(connString);

                byte[] insertedValue = BitConverter.GetBytes(s_insertedValues[0]);
                byte appendedByte = 0x04;
                insertedValue = AddByteToArray(insertedValue, appendedByte);

                // Reverse the byte array, if the system architecture is little-endian.
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(insertedValue);

                try
                {
                    using SqlConnection connection = new(connString);
                    connection.Open();
                    SqlCommand command = new($"SELECT TOP(1) Photo.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT(),EmployeeId FROM {tempTable} ORDER BY EmployeeId", connection);

                    SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                    command.Transaction = transaction;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Get the pointer for file  
                            string path = reader.GetString(0);
                            byte[] transactionContext = reader.GetSqlBytes(1).Buffer;

                            using Stream fileStream = new SqlFileStream(path, transactionContext, FileAccess.ReadWrite, FileOptions.SequentialScan, allocationSize: 0);
                            // Seek to the end of the file  
                            fileStream.Seek(0, SeekOrigin.End);

                            // Append a single byte   
                            fileStream.WriteByte(appendedByte);
                        }
                    }
                    transaction.Commit();

                    // Compare inserted and retrieved value
                    byte[] retrievedValue = RetrieveData(tempTable, connString, insertedValue.Length);
                    Assert.Equal(insertedValue, retrievedValue);

                }
                finally
                {
                    // Drop Table
                    ExecuteNonQueryCommand($"DROP TABLE {tempTable}", connString);
                }
            }
            finally
            {
                DropFileStreamDb(DataTestUtility.TCPConnectionString);
            }
        }

        #region Private helper methods

        private static string SetupFileStreamDB()
        {
            string fileStreamDir = DataTestUtility.FileStreamDirectory;
            try
            {
                if (fileStreamDir != null)
                {
                    if (!fileStreamDir.EndsWith("\\"))
                    {
                        fileStreamDir += "\\";
                    }

                    string dbName = DataTestUtility.GetShortName("FS", false);
                    string createDBQuery = @$"CREATE DATABASE [{dbName}]
                                         ON PRIMARY
                                          (NAME = PhotoLibrary_data,
                                           FILENAME = '{fileStreamDir}PhotoLibrary_data.mdf'),
                                         FILEGROUP FileStreamGroup CONTAINS FILESTREAM
                                          (NAME = PhotoLibrary_blobs,
                                           FILENAME = '{fileStreamDir}Photos')
                                         LOG ON
                                          (NAME = PhotoLibrary_log,
                                           FILENAME = '{fileStreamDir}PhotoLibrary_log.ldf')";
                    using SqlConnection con = new(new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { InitialCatalog = "master", IntegratedSecurity = true }.ConnectionString);
                    con.Open();
                    using SqlCommand cmd = con.CreateCommand();
                    cmd.CommandText = createDBQuery;
                    cmd.ExecuteNonQuery();
                    s_fileStreamDBName = dbName;
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine("File Stream database could not be setup. " + e.Message);
                fileStreamDir = null;
            }
            return s_fileStreamDBName;
        }

        private static void DropFileStreamDb(string connString)
        {
            try
            {
                using SqlConnection con = new(new SqlConnectionStringBuilder(connString) { InitialCatalog = "master" }.ConnectionString);
                con.Open();
                DataTestUtility.DropDatabase(con, s_fileStreamDBName);
                s_fileStreamDBName = null;
            }
            catch (SqlException e)
            {
                Console.WriteLine("File Stream database could not be dropped. " + e.Message);
            }
        }

        private static string SetupTable(string connString)
        {
            // Generate random table name
            string tempTable = DataTestUtility.GetLongName("fs");
            // Create table
            string createTable = $"CREATE TABLE {tempTable} (EmployeeId INT  NOT NULL  PRIMARY KEY, Photo VARBINARY(MAX) FILESTREAM  NULL, RowGuid UNIQUEIDENTIFIER NOT NULL ROWGUIDCOL UNIQUE DEFAULT NEWID() ) ";
            ExecuteNonQueryCommand(createTable, connString);

            // Insert data into created table
            for (int i = 0; i < s_insertedValues.Length; i++)
            {
                string prepTable = $"INSERT INTO {tempTable} VALUES ({i + 1}, {s_insertedValues[i]} , default)";
                ExecuteNonQueryCommand(prepTable, connString);
            }

            return tempTable;
        }

        private static void ExecuteNonQueryCommand(string cmdText, string connString)
        {
            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();
                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = cmdText;
                cmd.ExecuteNonQuery();
            }
        }

        private static byte[] RetrieveData(string tempTable, string connString, int len)
        {
            byte[] bArray = new byte[len];
            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();
                SqlCommand command = new($"SELECT TOP(1) Photo FROM {tempTable}", conn);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();
                    reader.GetBytes(0, 0, bArray, 0, len);
                }
            }
            return bArray;
        }

        public static byte[] AddByteToArray(byte[] oldArray, byte newByte)
        {
            byte[] newArray = new byte[oldArray.Length + 1];
            oldArray.CopyTo(newArray, 1);
            newArray[0] = newByte;
            return newArray;
        }
        #endregion
    }
}
