using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Helper methods used in the Test methods to reduce code bloat.
    /// </summary>
    /// <remarks>
    /// Related to issue:
    /// https://github.com/dotnet/SqlClient/issues/28
    /// </remarks>
    public static class T3Helpers
    {
        public static string ConnectionString => CreateConnectionString();

        public static string CreateConnectionString(Action<SqlConnectionStringBuilder> builder = null)
        {
            var csb = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                MultipleActiveResultSets = false,
                Pooling = false,
            };

            builder?.Invoke(csb);
            return csb.ToString();
        }

        public static void DropTable(SqlConnection connection, string name)
        {
            ExecuteNonQuery(connection, $"drop table if exists {name};");
        }

        public static int InsertTable(SqlConnection connection, string name, Action<SqlCommand> callback = null)
        {
            return ExecuteNonQuery(connection, $"insert into {name} default values;", callback);
        }

        public static int InsertTable(SqlConnection connection, string name, long value, Action<SqlCommand> callback = null)
        {
            return ExecuteNonQuery(connection, $"insert into {name} ([Value]) values ({value.ToString(CultureInfo.InvariantCulture)});", callback);
        }

        public static void CreateTable(SqlConnection connection, string name, bool value)
        {
            DropTable(connection, name);
            ExecuteNonQuery(connection, $"create table {name}"
                + "("
                + "[ID] bigint identity(1,1) not null"
                + (value ? ", [Value] bigint not null" : string.Empty)
                + ", primary key clustered ([ID] asc)"
                + ");");
        }

        public static int ExecuteNonQuery(SqlConnection connection, string query, Action<SqlCommand> callback = null)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = query;
                callback?.Invoke(command);
                return command.ExecuteNonQuery();
            }
        }

        public static object ExecuteScalar(SqlConnection connection, string query, Action<SqlCommand> callback = null)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = query;
                callback?.Invoke(command);
                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Shorthand to calling: 'select count(*) from {name}';
        /// </summary>
        public static int CountTable(SqlConnection connection, string name, Action<SqlCommand> callback = null)
        {
            return (int)ExecuteScalar(connection, $"select count(*) from {name};", callback);
        }

        /// <summary>
        /// Shorthand to calling: 'truncate table {name}';
        /// </summary>
        public static void PurgeTable(SqlConnection connection, string name, Action<SqlCommand> callback = null)
        {
            ExecuteNonQuery(connection, $"truncate table {name};", callback);
        }

        /// <summary>
        /// Shorthand to calling: 'begin transaction';
        /// </summary>
        public static void BeginTransaction(SqlConnection connection, Action<SqlCommand> callback = null)
        {
            ExecuteNonQuery(connection, $"begin transaction", callback);
        }

        /// <summary>
        /// Shorthand to calling: 'commit transaction';
        /// </summary>
        public static void CommitTransaction(SqlConnection connection, Action<SqlCommand> callback = null)
        {
            ExecuteNonQuery(connection, $"commit transaction;", callback);
        }

        /// <summary>
        /// Shorthand to calling: 'rollback transaction';
        /// </summary>
        public static void RollbackTransaction(SqlConnection connection, Action<SqlCommand> callback = null)
        {
            ExecuteNonQuery(connection, $"rollback transaction;", callback);
        }

        public static DataTable BulkValues(string table, int count = 100, Action<DataTable> callback = null)
        {
            DataTable dt = new DataTable(table);
            DataColumn col = dt.Columns.Add("Value", typeof(long));
            col.AllowDBNull = false;
            for (int it = 0; it < count; ++it)
            {
                dt.Rows.Add(it);
            }
            callback?.Invoke(dt);
            return dt;
        }

        public static void BulkPrepare(SqlBulkCopy bulkCopy, DataTable dt, int batch = 0)
        {
            bulkCopy.DestinationTableName = dt.TableName;
            bulkCopy.BatchSize = batch;
            foreach (DataColumn col in dt.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }
        }
    }
}
