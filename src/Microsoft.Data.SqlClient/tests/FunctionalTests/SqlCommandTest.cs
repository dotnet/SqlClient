// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sql;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public partial class SqlCommandTest
    {
        const string COMMAND_TEXT = "SELECT * FROM Authors";

        [Fact]
        public void Constructor1()
        {
            SqlCommand cmd = new SqlCommand();
            Assert.Equal(string.Empty, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Null(cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);
        }

        [Fact]
        public void Constructor2()
        {
            SqlCommand cmd = new SqlCommand(COMMAND_TEXT);
            Assert.Equal(COMMAND_TEXT, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Null(cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);

            cmd = new SqlCommand((string)null);
            Assert.Equal(string.Empty, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Null(cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);
        }

        [Fact]
        public void Constructor3()
        {
            SqlConnection conn = new SqlConnection();
            SqlCommand cmd;

            cmd = new SqlCommand(COMMAND_TEXT, conn);
            Assert.Equal(COMMAND_TEXT, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Same(conn, cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);

            cmd = new SqlCommand((string)null, conn);
            Assert.Equal(string.Empty, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Same(conn, cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);

            cmd = new SqlCommand(COMMAND_TEXT, (SqlConnection)null);
            Assert.Equal(COMMAND_TEXT, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Null(cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(30)]
        [InlineData(15)]
        public void Constructor3_CommandTimeout(int timeout)
        {
            SqlConnection conn = new SqlConnection($"Command Timeout = {timeout}");
            SqlCommand cmd;

            cmd = new SqlCommand(COMMAND_TEXT, conn);
            Assert.Equal(timeout, cmd.CommandTimeout);

            cmd.CommandTimeout = timeout + 10;
            Assert.Equal(timeout + 10, cmd.CommandTimeout);
        }

        [Fact]
        public void Constructor4()
        {
            SqlConnection conn = new SqlConnection();
            SqlCommand cmd;

            cmd = new SqlCommand(COMMAND_TEXT, conn, (SqlTransaction)null);
            Assert.Equal(COMMAND_TEXT, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Same(conn, cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);

            cmd = new SqlCommand((string)null, conn, (SqlTransaction)null);
            Assert.Equal(string.Empty, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Same(conn, cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);

            cmd = new SqlCommand(COMMAND_TEXT, (SqlConnection)null, (SqlTransaction)null);
            Assert.Equal(COMMAND_TEXT, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Null(cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);
        }

        [Fact]
        public void Clone()
        {
            SqlNotificationRequest notificationReq = new SqlNotificationRequest();

            SqlCommand cmd = new SqlCommand();
            cmd.CommandText = "sp_insert";
            cmd.CommandTimeout = 100;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.DesignTimeVisible = false;
            cmd.Notification = notificationReq;
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            cmd.Parameters.Add("@TestPar1", SqlDbType.Int);
            cmd.Parameters["@TestPar1"].Value = DBNull.Value;
            cmd.Parameters.AddWithValue("@BirthDate", DateTime.Now);
            cmd.UpdatedRowSource = UpdateRowSource.OutputParameters;

            SqlCommand clone = (((ICloneable)(cmd)).Clone()) as SqlCommand;
            Assert.Equal("sp_insert", clone.CommandText);
            Assert.Equal(100, clone.CommandTimeout);
            Assert.Equal(CommandType.StoredProcedure, clone.CommandType);
            Assert.Null(cmd.Connection);
            Assert.False(cmd.DesignTimeVisible);
            Assert.Same(notificationReq, cmd.Notification);
#if NETFRAMEWORK
            // see https://github.com/dotnet/SqlClient/issues/17
            Assert.True(cmd.NotificationAutoEnlist);
#endif
            Assert.Equal(2, clone.Parameters.Count);
            Assert.Equal(100, clone.CommandTimeout);
            clone.Parameters.AddWithValue("@test", DateTime.Now);
            clone.Parameters[0].ParameterName = "@ClonePar1";
            Assert.Equal(3, clone.Parameters.Count);
            Assert.Equal(2, cmd.Parameters.Count);
            Assert.Equal("@ClonePar1", clone.Parameters[0].ParameterName);
            Assert.Equal("@TestPar1", cmd.Parameters[0].ParameterName);
            Assert.Equal("@BirthDate", clone.Parameters[1].ParameterName);
            Assert.Equal("@BirthDate", cmd.Parameters[1].ParameterName);
            Assert.Null(clone.Transaction);
        }

        [Fact]
        public void CommandText()
        {
            SqlCommand cmd = new SqlCommand();
            cmd.CommandText = COMMAND_TEXT;
            Assert.Same(COMMAND_TEXT, cmd.CommandText);
            cmd.CommandText = null;
            Assert.Equal(string.Empty, cmd.CommandText);
            cmd.CommandText = COMMAND_TEXT;
            Assert.Same(COMMAND_TEXT, cmd.CommandText);
            cmd.CommandText = string.Empty;
            Assert.Equal(string.Empty, cmd.CommandText);
        }

        [Fact]
        public void CommandTimeout()
        {
            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 10;
            Assert.Equal(10, cmd.CommandTimeout);
            cmd.CommandTimeout = 25;
            Assert.Equal(25, cmd.CommandTimeout);
            cmd.CommandTimeout = 0;
            Assert.Equal(0, cmd.CommandTimeout);
        }

        [Fact]
        public void CommandTimeout_Value_Negative()
        {
            SqlCommand cmd = new SqlCommand();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => cmd.CommandTimeout = -1);
            // Invalid CommandTimeout value -1; the value must be >= 0
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Equal("CommandTimeout", ex.ParamName);
        }

        [Fact]
        public void CommandType_Value_Invalid()
        {
            SqlCommand cmd = new SqlCommand();

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => cmd.CommandType = (CommandType)(666));
            // The CommandType enumeration value, 666, is invalid
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("666", StringComparison.Ordinal) != -1);
            Assert.Equal("CommandType", ex.ParamName);
        }

        [Fact]
        public void Dispose()
        {
            string connectionString = "Initial Catalog=a;Server=b;User ID=c;"
                + "Password=d";
            SqlConnection connection = new SqlConnection(connectionString);
            SqlCommand command = connection.CreateCommand();
            command.Dispose();
            Assert.Equal(connectionString, connection.ConnectionString);
        }

        [Fact]
        public void ExecuteNonQuery_Connection_Closed()
        {
            string connectionString = "Initial Catalog=a;Server=b;User ID=c;"
                + "Password=d";
            SqlConnection cn = new SqlConnection(connectionString);

            SqlCommand cmd = new SqlCommand("delete from whatever", cn);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
            // ExecuteNonQuery requires an open and available
            // Connection. The connection's current state is
            // closed.
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("ExecuteNonQuery", StringComparison.Ordinal) != -1);
        }

        [Fact]
        public void ExecuteNonQuery_Connection_Null()
        {
            SqlCommand cmd = new SqlCommand("delete from whatever");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteNonQuery());
            // ExecuteNonQuery: Connection property has not
            // been initialized
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.StartsWith("ExecuteNonQuery:", ex.Message);
        }

        [Fact]
        public void ExecuteReader_Connection_Closed()
        {
            string connectionString = "Initial Catalog=a;Server=b;User ID=c;"
                + "Password=d";
            SqlConnection cn = new SqlConnection(connectionString);

            SqlCommand cmd = new SqlCommand("Select count(*) from whatever", cn);
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteReader());
            // ExecuteReader requires an open and available
            // Connection. The connection's current state is
            // closed.
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("ExecuteReader", StringComparison.Ordinal) != -1);
        }

        [Fact]
        public void ExecuteReader_Connection_Null()
        {
            SqlCommand cmd = new SqlCommand("select * from whatever");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteReader());
            // ExecuteReader: Connection property has not
            // been initialized
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.StartsWith("ExecuteReader:", ex.Message);
        }

        [Fact]
        public void ExecuteScalar_Connection_Closed()
        {
            string connectionString = "Initial Catalog=a;Server=b;User ID=c;"
                + "Password=d";
            SqlConnection cn = new SqlConnection(connectionString);

            SqlCommand cmd = new SqlCommand("Select count(*) from whatever", cn);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteScalar());
            // ExecuteScalar requires an open and available
            // Connection. The connection's current state is
            // closed.
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("ExecuteScalar", StringComparison.Ordinal) != -1);
        }

        [Fact] // bug #412584
        public void ExecuteScalar_Connection_Null()
        {
            SqlCommand cmd = new SqlCommand("select count(*) from whatever");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmd.ExecuteScalar());
            // ExecuteScalar: Connection property has not
            // been initialized
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.StartsWith("ExecuteScalar:", ex.Message);
        }

        [Fact]
        public void Prepare_Connection_Null()
        {
            SqlCommand cmd;

            // Text, with parameters
            cmd = new SqlCommand("select count(*) from whatever");
            cmd.Parameters.Add("@TestPar1", SqlDbType.Int);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmd.Prepare());
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.StartsWith("Prepare: Connection property has not been initialized.", ex.Message);
        }

        [Fact]
        public void Prepare_Connection_Closed()
        {
            string connectionString = "Initial Catalog=a;Server=b;User ID=c;"
                + "Password=d";
            SqlConnection cn = new SqlConnection(connectionString);

            SqlCommand cmd;

            // Text, without parameters
            cmd = new SqlCommand("select count(*) from whatever", cn);
            cmd.Prepare();

            // Text, with parameters
            cmd = new SqlCommand("select count(*) from whatever", cn);
            cmd.Parameters.Add("@TestPar1", SqlDbType.Int);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cmd.Prepare());
            // Prepare requires an open and available
            // Connection. The connection's current state
            // is Closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("Prepare", StringComparison.Ordinal) != -1);

            // Text, parameters cleared
            cmd = new SqlCommand("select count(*) from whatever", cn);
            cmd.Parameters.Add("@TestPar1", SqlDbType.Int);
            cmd.Parameters.Clear();
            cmd.Prepare();

            // StoredProcedure, without parameters
            cmd = new SqlCommand("FindCustomer", cn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Prepare();

            // StoredProcedure, with parameters
            cmd = new SqlCommand("FindCustomer", cn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@TestPar1", SqlDbType.Int);
            cmd.Prepare();

            // ensure connection was not implictly opened
            Assert.Equal(ConnectionState.Closed, cn.State);
        }

        [Fact]
        public void ResetCommandTimeout()
        {
            SqlCommand cmd = new SqlCommand();
            cmd.CommandTimeout = 50;
            Assert.Equal(50, cmd.CommandTimeout);
            cmd.ResetCommandTimeout();
            Assert.Equal(30, cmd.CommandTimeout);
        }

        [Fact]
        public void UpdatedRowSource()
        {
            SqlCommand cmd = new SqlCommand();
            cmd.UpdatedRowSource = UpdateRowSource.None;
            Assert.Equal(UpdateRowSource.None, cmd.UpdatedRowSource);
            cmd.UpdatedRowSource = UpdateRowSource.OutputParameters;
            Assert.Equal(UpdateRowSource.OutputParameters, cmd.UpdatedRowSource);
        }

        [Fact]
        public void UpdatedRowSource_Value_Invalid()
        {
            SqlCommand cmd = new SqlCommand();

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => cmd.UpdatedRowSource = (UpdateRowSource)666);
            // The UpdateRowSource enumeration value,666,
            // is invalid
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Equal("UpdateRowSource", ex.ParamName);
        }

        [Fact]
        public void ParameterCollectionTest()
        {
            using (var cmd = new SqlCommand())
            {
                cmd.Parameters.Add(new SqlParameter());
                cmd.Parameters.AddRange(new SqlParameter[] { });
                cmd.Parameters.Insert(0, new SqlParameter());
                cmd.Parameters.Insert(1, new SqlParameter());
                cmd.Parameters.RemoveAt(0);
                cmd.Parameters.Remove(cmd.Parameters[0]);
            }
        }

        #region fixing "Change SqlParameter to allow empty IEnumerable<SqlDataRecord>? #2971"
        public const string DbName = "TVPTestDb";
        public const string ServerConnection = @"Server=.;Integrated Security=true;Encrypt=false;";
        public static string DbConnection => ServerConnection + $"Initial Catalog={DbName};";
        private static class SqlDataRecordTestCases
        {
            public static IEnumerable<object[]> TestSqlDataRecordParameters => new List<object[]>
            {
                new object[] { null },
                new object[] { new List<SqlDataRecord>() } // empty list, and the issue it throw an Arrgument Error Exception
            };
        }

        private static void CreateDatabaseAndSchema()
        {
            string createDb = $@"
                                IF DB_ID('{DbName}') IS NULL
                                 CREATE DATABASE {DbName};";

            string createSchema = @"
                                    USE TVPTestDb;

                                        IF NOT EXISTS (SELECT * FROM sys.types WHERE is_table_type = 1 AND name = 'MySimpleTableType')
                                        BEGIN
                                        CREATE TYPE dbo.MySimpleTableType AS TABLE
                                        (
                                            Id INT,
                                            Name NVARCHAR(100)
                                        );
                                        END;

                                        IF OBJECT_ID('dbo.TargetTable', 'U') IS NULL
                                        BEGIN
                                        CREATE TABLE dbo.TargetTable
                                        (
                                            Id INT PRIMARY KEY,
                                            Name NVARCHAR(100)
                                        );
                                        END;

                                        IF OBJECT_ID('dbo.InsertFromTVP', 'P') IS NULL
                                        BEGIN
                                        EXEC('
                                            CREATE PROCEDURE dbo.InsertFromTVP
                                            @MyTVP dbo.MySimpleTableType READONLY
                                                AS
                                            BEGIN
                                            SET NOCOUNT ON;
                                            INSERT INTO dbo.TargetTable (Id, Name)
                                            SELECT Id, Name FROM @MyTVP;
                                            END
                                            ');
                                        END;";

            using var conn = new SqlConnection(ServerConnection);
            conn.Open();
            new SqlCommand(createDb, conn).ExecuteNonQuery();

            using var connDb = new SqlConnection(DbConnection);
            connDb.Open();
            new SqlCommand(createSchema, connDb).ExecuteNonQuery();
        }

        private static void DeleteDatabase()
        {
            string dropDb = $@"
                                IF DB_ID('{DbName}') IS NOT NULL
                                BEGIN
                                ALTER DATABASE {DbName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                DROP DATABASE {DbName};
                                END;";
            using var conn = new SqlConnection(ServerConnection);
            conn.Open();
            new SqlCommand(dropDb, conn).ExecuteNonQuery();
        }
        private int perpare_SqlRecord_tests(List<SqlDataRecord> records = null)
        {
            SqlConnection cn = new SqlConnection(DbConnection);

            SqlCommand cmd;
            // Text, with parameters
            cmd = new SqlCommand("dbo.InsertFromTVP", cn);
            cmd.CommandType = CommandType.StoredProcedure;

            var paramEmpty = cmd.Parameters.AddWithValue("@MyTVP", records);
            paramEmpty.SqlDbType = SqlDbType.Structured;
            paramEmpty.TypeName = "dbo.MySimpleTableType";
            cn.Open();
            return cmd.ExecuteNonQuery();

        }
        [Theory]
        [MemberData(nameof(SqlDataRecordTestCases.TestSqlDataRecordParameters), MemberType = typeof(SqlDataRecordTestCases))]
        public void SqlDataRecord_TABLE_deafult(List<SqlDataRecord> parameters)
        {
            CreateDatabaseAndSchema();
            int rowAffects = perpare_SqlRecord_tests(parameters);
            DeleteDatabase();
            Assert.Equal(-1, rowAffects);

        }
        #endregion


    }
}
