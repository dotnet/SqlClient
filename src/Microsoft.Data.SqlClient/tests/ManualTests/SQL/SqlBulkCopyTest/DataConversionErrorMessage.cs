// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class DataConversionErrorMessage
    {
        private enum ColumnsEnum
        {
            _int = 0,
            _varChar3 = 1
        }

        public static void StringToIntTest(string dstConstr, string targetTable)
        {
            var value = "abcde";

            using (var sqlConnection = new SqlConnection(dstConstr))
            {
                sqlConnection.Open();

                InitialTable(sqlConnection, targetTable);

                var table = PrepareDataTable(targetTable, ColumnsEnum._varChar3, value);

                bool hitException = false;
                try
                {
                    using (SqlBulkCopy bulkcopy = new SqlBulkCopy(sqlConnection))
                    {
                        bulkcopy.DestinationTableName = targetTable;
                        bulkcopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping((int)ColumnsEnum._varChar3, (int)ColumnsEnum._int));

                        bulkcopy.WriteToServer(table);
                        bulkcopy.Close();
                    }
                }
                catch (Exception ex)
                {
                    object[] args = new object[] { string.Format("'{0}'", value), value.GetType().Name, "int", (int)ColumnsEnum._int, Enum.GetName(typeof(ColumnsEnum), ColumnsEnum._int) };
                    string expectedErrorMsg = string.Format(SystemDataResourceManager.Instance.SQL_BulkLoadCannotConvertValue, args);
                    Assert.True(ex.Message.Contains(expectedErrorMsg), "Unexpected error message: " + ex.Message);
                    hitException = true;
                }
                finally
                {
                    DropTable(sqlConnection, targetTable);
                }
                Assert.True(hitException, "Did not get any exceptions!");
            }
        }

        #region table manupulations
        private static string CreateTableCommand(SqlCommand command, string tableName)
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

        private static DataTable PrepareDataTable(string tableName, ColumnsEnum selectedColumn, object value)
        {
            var table = new DataTable(tableName);

            table.Columns.Add(Enum.GetName(typeof(ColumnsEnum), ColumnsEnum._int), typeof(int));
            table.Columns.Add(Enum.GetName(typeof(ColumnsEnum), ColumnsEnum._varChar3), typeof(string));

            var row = table.NewRow();
            row[(int)selectedColumn] = value;

            table.Rows.Add(row);

            return table;
        }

        private static void InitialTable(SqlConnection sqlConnection, string targetTable)
        {
            using (var command = new SqlCommand())
            {
                command.Connection = sqlConnection;
                command.CommandText = CreateTableCommand(command, targetTable);
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
            }
        }

        private static void DropTable(SqlConnection sqlConnection, string targetTable)
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
    }
}
