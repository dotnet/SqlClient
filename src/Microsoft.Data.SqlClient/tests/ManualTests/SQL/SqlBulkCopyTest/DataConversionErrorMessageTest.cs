﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    internal enum ColumnsEnum
    {
        _int = 0,
        _varChar3 = 1
    }

    public class InitialDatabase : IDisposable
    {
        private string srcConstr { get; }

        public SqlConnection Connection { get; }

        public string TableName { get; }

        public InitialDatabase()
        {
            srcConstr = DataTestUtility.TCPConnectionString;

            Connection = new SqlConnection(srcConstr);
            TableName = $"SqlBulkCopyTest_CopyStringToIntTest_{Guid.NewGuid().ToString().Replace('-', '_')}";
            InitialTable(Connection, TableName);
        }

        #region database manupulation
        private string CreateTableCommand(SqlCommand command, string tableName)
        {
            using (command)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"IF(OBJECT_ID('{tableName}') IS NULL)");
                sb.AppendLine("BEGIN");
                sb.AppendLine($"\tCREATE TABLE {tableName}");
                sb.AppendLine($"\t(\t");
                sb.AppendLine($"\t\t{Enum.GetName(typeof(ColumnsEnum), ColumnsEnum._int)} int NULL");
                sb.AppendLine($"\t\t,{Enum.GetName(typeof(ColumnsEnum), ColumnsEnum._varChar3)} varchar(3) NULL");
                sb.AppendLine($"\t)");
                sb.AppendLine("END");

                return sb.ToString();
            }
        }

        private void InitialTable(SqlConnection sqlConnection, string targetTable)
        {
            sqlConnection.Open();

            using (var command = new SqlCommand())
            {
                command.Connection = sqlConnection;
                command.CommandText = CreateTableCommand(command, targetTable);
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
            }
        }

        private void DropTable(SqlConnection sqlConnection, string targetTable)
        {
            using (var command = new SqlCommand())
            {
                command.Connection = sqlConnection;
                command.CommandText = string.Format("DROP TABLE {0}", targetTable);
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
            }
        }
        #endregion

        public void Dispose()
        {
            DropTable(Connection, TableName);

            Connection.Close();
            Connection.Dispose();
        }
    }

    public class DataConversionErrorMessageTest : IClassFixture<InitialDatabase>
    {
        private readonly InitialDatabase _fixture;

        private enum SourceType
        {
            DataTable,
            DataRows
        }

        public DataConversionErrorMessageTest(InitialDatabase fixture)
        {
            _fixture = fixture;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void StringToIntDataTableTest()
        {
            StringToIntTest(_fixture.Connection, _fixture.TableName, SourceType.DataTable);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void StringToIntDataRowArrayTest()
        {
            StringToIntTest(_fixture.Connection, _fixture.TableName, SourceType.DataRows);
        }

        private void StringToIntTest(SqlConnection cnn, string targetTable, SourceType sourceType)
        {
            var value = "abcde";

            DataTable table = PrepareDataTable(targetTable, ColumnsEnum._varChar3, value);

            bool hitException = false;
            try
            {
                using (SqlBulkCopy bulkcopy = new SqlBulkCopy(cnn))
                {
                    bulkcopy.DestinationTableName = targetTable;
                    bulkcopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping((int)ColumnsEnum._varChar3, (int)ColumnsEnum._int));
                    switch (sourceType)
                    {
                        case SourceType.DataTable:
                            bulkcopy.WriteToServer(table);
                            break;
                        case SourceType.DataRows:
                            bulkcopy.WriteToServer(table.Select());
                            break;
                        default:
                            break;
                    }

                    bulkcopy.Close();
                }
            }
            catch (Exception ex)
            {
                object[] args =
                    new object[] { string.Format(" '{0}'", value), value.GetType().Name, "int", (int)ColumnsEnum._int, Enum.GetName(typeof(ColumnsEnum), ColumnsEnum._int), table.Rows.Count };
                string expectedErrorMsg = string.Format(SystemDataResourceManager.Instance.SQL_BulkLoadCannotConvertValue, args);
                Assert.True(ex.Message.Contains(expectedErrorMsg), "Unexpected error message: " + ex.Message);
                hitException = true;
            }
            Assert.True(hitException, "Did not get any exceptions!");
        }

        private DataTable PrepareDataTable(string tableName, ColumnsEnum selectedColumn, object value)
        {
            var table = new DataTable(tableName);

            table.Columns.Add(Enum.GetName(typeof(ColumnsEnum), ColumnsEnum._int), typeof(int));
            table.Columns.Add(Enum.GetName(typeof(ColumnsEnum), ColumnsEnum._varChar3), typeof(string));

            var row = table.NewRow();
            row[(int)selectedColumn] = value;

            table.Rows.Add(row);

            return table;
        }
    }
}
