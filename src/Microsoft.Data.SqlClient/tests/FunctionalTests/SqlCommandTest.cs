// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.Data.Sql;
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

        #region Prepare()

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
        public void Prepare_ConnectionClosed_TextWithoutParams()
        {
            // Arrange
            using SqlConnection connection = GetNonConnectingConnection();
            using SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandType = CommandType.Text;
            command.CommandText = COMMAND_TEXT;

            // Act / Assert
            command.Prepare();
            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        [Fact]
        public void Prepare_ConnectionClosed_TextWithClearedParams()
        {
            // Arrange
            using SqlConnection connection = GetNonConnectingConnection();
            using SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandType = CommandType.Text;
            command.CommandText = COMMAND_TEXT;
            command.Parameters.Add("@TestPar1", SqlDbType.Int);
            command.Parameters.Clear();

            // Act / Assert
            command.Prepare();
            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        [Fact]
        public void Prepare_ConnectionClosed_TextWithParams()
        {
            // Arrange
            using SqlConnection connection = GetNonConnectingConnection();
            using SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandType = CommandType.Text;
            command.CommandText = COMMAND_TEXT;
            command.Parameters.Add("@TestPar1", SqlDbType.Int);

            // Act
            Action action = () => command.Prepare();

            // Assert
            var exception = Assert.Throws<InvalidOperationException>(action);
            Assert.Null(exception.InnerException);
            Assert.NotNull(exception.Message);
            Assert.Contains("Prepare", exception.Message);

            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        [Fact]
        public void Prepare_ConnectionClosed_SprocWithoutParams()
        {
            // Arrange
            using SqlConnection connection = GetNonConnectingConnection();
            using SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "FindCustomer";

            // Act / Assert
            command.Prepare();
            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        [Fact]
        public void Prepare_ConnectionClosed_SprocWithParams()
        {
            // Arrange
            using SqlConnection connection = GetNonConnectingConnection();
            using SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "FindCustomer";

            // Act / Assert
            command.Prepare();
            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        #endregion

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

        private static SqlConnection GetNonConnectingConnection() =>
            new SqlConnection("Initial Catalog=a;Server=b;User ID=c;Password=d");
    }
}
