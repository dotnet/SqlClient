// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using static Microsoft.Data.SqlClient.PerformanceTests.Constants;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class Table
    {
        /// <summary>
        /// Escaped Table Name. e.g. "[Table1]"
        /// </summary>
        public string Name;

        /// <summary>
        /// Unescaped Table Name. e.g. "Table1"
        /// </summary>
        public string UnescapedName
        {
            get => Name[1..^1];
        }

        public List<Column> Columns;
        private int _indexColumn = -1;

        private Table(string tableName)
        {
            Name = tableName;
            Columns = new List<Column>();
        }

        public static Table Build(string prefix)
            => new(DbUtils.GenerateEscapedTableName(prefix));

        public Table AddColumn(Column column)
        {
            if (!Columns.Contains(column))
            {
                Columns.Add(column);
            }
            return this;
        }

        public Table AddColumns(List<Column> _columns)
        {
            Columns = new(_columns);
            return this;
        }

        public Table SetIndexColumn(int i)
        {
            _indexColumn = i;
            return this;
        }

        public DataTable AsDataTable(long rowCount)
        {
            DataTable dataTable = new(Name);
            object[] row = new object[Columns.Count];
            for (int i = 0; i < Columns.Count; i++)
            {
                dataTable.Columns.Add(Columns[i].AsDataColumn());

                if (Columns[i].Type.Name == UniqueIdentifier)
                {
                    row[i] = Guid.Parse(Columns[i].Value.ToString());
                    dataTable.Columns[i].DataType = typeof(Guid);
                }
                else if (Columns[i].Type.Name == Bit)
                {
                    row[i] = bool.Parse(Columns[i].Value.ToString());
                    dataTable.Columns[i].DataType = typeof(bool);
                }
                else if (Columns[i].Type.Name == Constants.Char)
                {
                    row[i] = char.Parse(Columns[i].Value.ToString());
                    dataTable.Columns[i].DataType = typeof(char);
                }
                else if (Columns[i].Type.Name == TinyInt)
                {
                    row[i] = byte.Parse(Columns[i].Value.ToString());
                    dataTable.Columns[i].DataType = typeof(byte);
                }
                else if (Columns[i].Type.Name == DatetimeOffset)
                {
                    row[i] = DateTimeOffset.Parse(Columns[i].Value.ToString());
                    dataTable.Columns[i].DataType = typeof(DateTimeOffset);
                }
                else if (Columns[i].Type.Name == Binary || Columns[i].Type.Name == VarBinary)
                {
                    row[i] = Encoding.Default.GetBytes(Columns[i].Value.ToString());
                    dataTable.Columns[i].DataType = typeof(byte[]);
                }
                else
                {
                    row[i] = Columns[i].Value.ToString().Trim();
                }
            }
            for (int i = 0; i < rowCount; i++)
            {
                if (_indexColumn != -1)
                {
                    row[_indexColumn] = i + 1;
                }
                dataTable.Rows.Add(row);
            }
            return dataTable;
        }

        internal Table Clone() => new Table("[" + "Target_" + UnescapedName + "]")
                .AddColumns(Columns)
                .SetIndexColumn(_indexColumn);

        /// <summary>
        /// Creates table on database
        /// </summary>
        /// <param name="sqlConnection"></param>
        public Table CreateTable(SqlConnection sqlConnection)
        {
            DropTable(sqlConnection);
            string columnList = "";
            for (int i = 0; i < Columns.Count; i++)
            {
                columnList += Columns[i].QueryString;
                if (i != Columns.Count - 1)
                {
                    columnList += ",";
                }
            }
            string query = @$"CREATE TABLE {Name} ("
                + columnList + ")";
            DbUtils.ExecuteNonQuery(query, sqlConnection);
            return this;
        }

        public Table InsertBulkRows(long rowCount, SqlConnection sqlConnection)
        {
            DataTable dataTable = AsDataTable(rowCount);

            using SqlBulkCopy sqlBulkCopy = new(sqlConnection);
            sqlBulkCopy.DestinationTableName = Name;
            sqlBulkCopy.WriteToServer(dataTable);
            return this;
        }

        /// <summary>
        /// Drops table on database
        /// </summary>
        public void DropTable(SqlConnection sqlConnection)
        {
            string query = $"DROP TABLE IF EXISTS {Name}";
            DbUtils.ExecuteNonQuery(query, sqlConnection);
        }
    }
}
