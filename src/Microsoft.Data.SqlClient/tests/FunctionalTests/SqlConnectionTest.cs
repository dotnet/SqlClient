// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Collections.Generic;
using Xunit;
using System.Reflection;

namespace Microsoft.Data.SqlClient.Tests
{
    public partial class SqlConnectionTest
    {
        private static readonly string[] s_retrieveInternalInfoKeys =
        {
            "SQLDNSCachingSupportedState",
            "SQLDNSCachingSupportedStateBeforeRedirect"
        };

        [Fact]
        public void Constructor1()
        {
            SqlConnection cn = new SqlConnection();

            Assert.Equal(string.Empty, cn.ConnectionString);
            Assert.Equal(15, cn.ConnectionTimeout);

            Assert.Null(cn.Container);
            Assert.Equal(string.Empty, cn.Database);
            Assert.Equal(string.Empty, cn.DataSource);
            Assert.False(cn.FireInfoMessageEventOnUserErrors);
            Assert.Equal(8000, cn.PacketSize);
            Assert.Null(cn.Site);
            Assert.Equal(ConnectionState.Closed, cn.State);
            Assert.False(cn.StatisticsEnabled);
            Assert.True(string.Compare(Environment.MachineName, cn.WorkstationId, true) == 0);
        }

        [Fact]
        public void Constructor2()
        {
            string connectionString = "server=SQLSRV; database=dotNet;";

            SqlConnection cn = new SqlConnection(connectionString);
            Assert.Equal(connectionString, cn.ConnectionString);
            Assert.Equal(15, cn.ConnectionTimeout);
            Assert.Equal(30, cn.CommandTimeout);
            Assert.Null(cn.Container);
            Assert.Equal("dotNet", cn.Database);
            Assert.Equal("SQLSRV", cn.DataSource);
            Assert.False(cn.FireInfoMessageEventOnUserErrors);
            Assert.Equal(8000, cn.PacketSize);
            Assert.Null(cn.Site);
            Assert.Equal(ConnectionState.Closed, cn.State);
            Assert.False(cn.StatisticsEnabled);
            Assert.True(string.Compare(Environment.MachineName, cn.WorkstationId, true) == 0);

            cn = new SqlConnection((string)null);
            Assert.Equal(string.Empty, cn.ConnectionString);
            Assert.Equal(15, cn.ConnectionTimeout);
            Assert.Equal(30, cn.CommandTimeout);
            Assert.Null(cn.Container);
            Assert.Equal(string.Empty, cn.Database);
            Assert.Equal(string.Empty, cn.DataSource);
            Assert.False(cn.FireInfoMessageEventOnUserErrors);
            Assert.Equal(8000, cn.PacketSize);
            Assert.Null(cn.Site);
            Assert.Equal(ConnectionState.Closed, cn.State);
            Assert.False(cn.StatisticsEnabled);
            Assert.True(string.Compare(Environment.MachineName, cn.WorkstationId, true) == 0);
        }

        [Fact]
        public void Constructor2_ConnectionString_Invalid()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => new SqlConnection("InvalidConnectionString"));
            // Format of the initialization string does
            // not conform to specification starting at
            // index 0
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Null(ex.ParamName);

            // invalid keyword
            ex = Assert.Throws<ArgumentException>(() => new SqlConnection("invalidKeyword=10"));
            // Keyword not supported: 'invalidkeyword'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'invalidkeyword'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // invalid packet size (< minimum)
            ex = Assert.Throws<ArgumentException>(() => new SqlConnection("Packet Size=511"));
            // Invalid 'Packet Size'.  The value must be an
            // integer >= 512 and <= 32768
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Null(ex.ParamName);

            // invalid packet size (> maximum)
            ex = Assert.Throws<ArgumentException>(() => new SqlConnection("Packet Size=32769"));
            // Invalid 'Packet Size'.  The value must be an
            // integer >= 512 and <= 32768
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Null(ex.ParamName);

            // negative connect timeout
            ex = Assert.Throws<ArgumentException>(() => new SqlConnection("Connect Timeout=-1"));
            // Invalid value for key 'connect timeout'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Null(ex.ParamName);

            // negative max pool size
            ex = Assert.Throws<ArgumentException>(() => new SqlConnection("Max Pool Size=-1"));
            // Invalid value for key 'max pool size'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Null(ex.ParamName);

            // negative min pool size
            ex = Assert.Throws<ArgumentException>(() => new SqlConnection("Min Pool Size=-1"));
            // Invalid value for key 'min pool size'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Null(ex.ParamName);
        }

        [Fact]
        public void BeginTransaction_Connection_Closed()
        {
            SqlConnection cn = new SqlConnection();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cn.BeginTransaction());
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.BeginTransaction((IsolationLevel)666));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.BeginTransaction(IsolationLevel.Serializable));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.BeginTransaction("trans"));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.BeginTransaction((IsolationLevel)666, "trans"));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.BeginTransaction(IsolationLevel.Serializable, "trans"));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
        }

        [Fact]
        public void ChangeDatabase_Connection_Closed()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "server=SQLSRV";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cn.ChangeDatabase("database"));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
        }

        [Fact]
        public void ChangePassword_ConnectionString_Empty()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => SqlConnection.ChangePassword(string.Empty, "dotnet"));
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.NotNull(ex.ParamName);
        }

        [Fact]
        public void ChangePassword_ConnectionString_Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => SqlConnection.ChangePassword(null, "dotnet"));
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.NotNull(ex.ParamName);
        }

        [Fact]
        public void ChangePassword_NewPassword_Empty()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => SqlConnection.ChangePassword("server=SQLSRV", string.Empty));
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.NotNull(ex.ParamName);
            Assert.True(ex.ParamName.IndexOf("'newPassword'") != -1);
        }

        [Fact]
        public void ChangePassword_NewPassword_ExceedMaxLength()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => SqlConnection.ChangePassword("server=SQLSRV", new string('d', 129)));
            // The length of argument 'newPassword' exceeds
            // its limit of '128'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'newPassword'") != -1);
            Assert.True(ex.Message.IndexOf("128") != -1);
            Assert.Null(ex.ParamName);
        }

        [Fact]
        public void ChangePassword_NewPassword_Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => SqlConnection.ChangePassword("server=SQLSRV", null));
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.NotNull(ex.ParamName);
            Assert.True(ex.ParamName.IndexOf("'newPassword'") != -1);
        }

        [Fact]
        public void ClearPool_Connection_Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => SqlConnection.ClearPool(null));
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Equal("connection", ex.ParamName);
        }

        [Fact]
        public void ConnectionString()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "server=SQLSRV";
            Assert.Equal("server=SQLSRV", cn.ConnectionString);
            cn.ConnectionString = null;
            Assert.Equal(string.Empty, cn.ConnectionString);
            cn.ConnectionString = "server=SQLSRV";
            Assert.Equal("server=SQLSRV", cn.ConnectionString);
            cn.ConnectionString = string.Empty;
            Assert.Equal(string.Empty, cn.ConnectionString);
        }

        [Fact]
        public void ConnectionString_Value_Invalid()
        {
            SqlConnection cn = new SqlConnection();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "InvalidConnectionString");
            // Format of the initialization string does
            // not conform to specification starting at
            // index 0
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Null(ex.ParamName);

            // invalid keyword
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "invalidKeyword=10");
            // Keyword not supported: 'invalidkeyword'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'invalidkeyword'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);
        }

        [Fact]
        public void CreateCommand()
        {
            SqlConnection cn = new SqlConnection();
            SqlCommand cmd = cn.CreateCommand();
            Assert.NotNull(cmd);
            Assert.Equal(string.Empty, cmd.CommandText);
            Assert.Equal(30, cmd.CommandTimeout);
            Assert.Equal(CommandType.Text, cmd.CommandType);
            Assert.Same(cn, cmd.Connection);
            Assert.Null(cmd.Container);
            Assert.True(cmd.DesignTimeVisible);
            Assert.Null(cmd.Notification);
            // see https://github.com/dotnet/SqlClient/issues/17
            // Assert.True(cmd.NotificationAutoEnlist);
            Assert.NotNull(cmd.Parameters);
            Assert.Equal(0, cmd.Parameters.Count);
            Assert.Null(cmd.Site);
            Assert.Null(cmd.Transaction);
            Assert.Equal(UpdateRowSource.Both, cmd.UpdatedRowSource);
        }

        [Fact]
        public void Dispose()
        {
            SqlConnection cn = new SqlConnection("Server=SQLSRV;Database=master;Timeout=25;Packet Size=512;Workstation ID=DUMMY");
            cn.Dispose();

            Assert.Equal(string.Empty, cn.ConnectionString);
            Assert.Equal(15, cn.ConnectionTimeout);
            Assert.Equal(string.Empty, cn.Database);
            Assert.Equal(string.Empty, cn.DataSource);
            Assert.Equal(8000, cn.PacketSize);
            Assert.True(string.Compare(Environment.MachineName, cn.WorkstationId, true) == 0);
            Assert.Equal(ConnectionState.Closed, cn.State);
            cn.Dispose();

            cn = new SqlConnection();
            cn.Dispose();
        }

        [Fact]
        public void GetSchema_Connection_Closed()
        {
            SqlConnection cn = new SqlConnection();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cn.GetSchema());
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.GetSchema("Tables"));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.GetSchema(null));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.GetSchema("Tables", new string[] { "master" }));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.GetSchema(null, new string[] { "master" }));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.GetSchema("Tables", null));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            ex = Assert.Throws<InvalidOperationException>(() => cn.GetSchema(null, null));
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
        }

        [Theory]
        [InlineData("Authentication = ActiveDirectoryIntegrated;Password = ''")]
        [InlineData("Authentication = ActiveDirectoryIntegrated;PWD = ''")]
        [InlineData("Authentication = ActiveDirectoryIntegrated;User Id='';PWD = ''")]
        [InlineData("Authentication = ActiveDirectoryIntegrated;User Id='';Password = ''")]
        [InlineData("Authentication = ActiveDirectoryIntegrated;UID='';PWD = ''")]
        [InlineData("Authentication = ActiveDirectoryIntegrated;UID='';Password = ''")]
        public void ConnectionString_ActiveDirectoryIntegrated_Password(string connectionString)
        {
            SqlConnection cn = new SqlConnection();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = connectionString);
            // Invalid value for key 'user instance'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'pwd'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);
        }

        [Theory]
        [InlineData(@"AttachDbFileName=C:\test\attach.mdf", @"AttachDbFileName=C:\test\attach.mdf")]
        [InlineData(@"AttachDbFileName=C:\test\attach.mdf;", @"AttachDbFileName=C:\test\attach.mdf;")]
        public void ConnectionString_AttachDbFileName_Plain(string value, string expected)
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = value;
            Assert.Equal(expected, cn.ConnectionString);
        }

        [Theory]
        [PlatformSpecific(TestPlatforms.Windows)]
        [InlineData(@"Data Source=.;AttachDbFileName=|DataDirectory|\attach.mdf",
                    @"Data Source=.;AttachDbFileName=|DataDirectory|\attach.mdf",
                    @"C:\test\")]
        [InlineData(@"Data Source=.;AttachDbFileName=|DataDirectory|\attach.mdf",
                    @"Data Source=.;AttachDbFileName=|DataDirectory|\attach.mdf",
                    @"C:\test")]
        [InlineData(@"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf",
                    @"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf",
                    @"C:\test")]
        [InlineData(@"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf",
                    @"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf",
                    @"C:\test\")]
        [InlineData(@"Data Source=.;AttachDbFileName=C:\test\attach.mdf;AttachDbFileName=|DataDirectory|attach.mdf",
                    @"Data Source=.;AttachDbFileName=C:\test\attach.mdf;AttachDbFileName=|DataDirectory|attach.mdf",
                    null)]
        public void ConnectionString_AttachDbFileName_DataDirectory(string value, string expected, string dataDirectory)
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);

            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = value;
            Assert.Equal(expected, cn.ConnectionString);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Linux)]
        public void ConnectionString_AttachDbFileName_DataDirectory_NoLinuxRootFolder()
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", @"C:\test\");

            SqlConnection cn = new SqlConnection();
            Assert.Throws<ArgumentException>(() => cn.ConnectionString = @"Data Source=.;AttachDbFileName=|DataDirectory|\attach.mdf");
        }

        [Theory]
        [InlineData(@"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf", @"..\test\")]
        [InlineData(@"Data Source=(local);AttachDbFileName=|DataDirectory|attach.mdf", @"c:\temp\..\test")]
        [InlineData(@"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf", @"c:\temp\..\test\")]
        [InlineData(@"Data Source=Random12344321;AttachDbFileName=|DataDirectory|attach.mdf", @"C:\\test\\")]
        [InlineData(@"Data Source=local;AttachDbFileName=|DataDirectory|attach.mdf", @"C:\\test\\")]
        [InlineData(@"Data Source=..;AttachDbFileName=|DataDirectory|attach.mdf", @"C:\\test\\")]
        public void ConnectionString_AttachDbFileName_DataDirectory_Fails(string value, string dataDirectory)
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);

            SqlConnection cn = new SqlConnection();
            Assert.Throws<ArgumentException>(() => cn.ConnectionString = value);
        }

        [Theory]
        [InlineData(@"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf", 1)]
        [InlineData(@"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf", 1.5)]
        public void ConnectionString_AttachDbFileName_DataDirectory_Throws(string value, object dataDirectory)
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);

            SqlConnection cn = new SqlConnection();
            Assert.Throws<InvalidOperationException>(() => cn.ConnectionString = value);
        }

        [Fact]
        public void ConnectionString_AttachDbFileName_DataDirectory_Long_Throws()
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", @"C:\test\" + new string('x', 261));

            SqlConnection cn = new SqlConnection();
            var exception = Record.Exception(() => cn.ConnectionString = @"Data Source=.;AttachDbFileName=|DataDirectory|attach.mdf");
            Assert.NotNull(exception);
        }

        [Fact]
        public void ConnectionString_AttachDbFileName_Long_Throws()
        {
            SqlConnection cn = new SqlConnection();
            Assert.Throws<ArgumentException>(() => cn.ConnectionString = @"Data Source=.;AttachDbFileName=C:\test\" + new string('x', 261));
        }

        [Fact]
        public void ConnectionString_ConnectTimeout()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "Connection Timeout=45";
            Assert.Equal(45, cn.ConnectionTimeout);
            cn.ConnectionString = "Connect Timeout=40";
            Assert.Equal(40, cn.ConnectionTimeout);
            cn.ConnectionString = "Timeout=";
            Assert.Equal(15, cn.ConnectionTimeout);
            cn.ConnectionString = "Timeout=2147483647";
            Assert.Equal(int.MaxValue, cn.ConnectionTimeout);
            cn.ConnectionString = "Timeout=0";
            Assert.Equal(0, cn.ConnectionTimeout);
        }

        [Fact]
        public void ConnectionString_ConnectTimeout_Invalid()
        {
            SqlConnection cn = new SqlConnection();

            // negative number
            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "Connection timeout=-1");
            // Invalid value for key 'connect timeout'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'connect timeout'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // invalid number
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "connect Timeout=BB");
            // Invalid value for key 'connect timeout'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(FormatException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'connect timeout'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Input string was not in a correct format
            FormatException fe = (FormatException)ex.InnerException;
            Assert.Null(fe.InnerException);
            Assert.NotNull(fe.Message);

            // overflow
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "timeout=2147483648");
            // Invalid value for key 'connect timeout'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'connect timeout'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Value was either too large or too small for an Int32
            OverflowException oe = (OverflowException)ex.InnerException;
            Assert.Null(oe.InnerException);
            Assert.NotNull(oe.Message);
        }

        [Fact]
        public void ConnectionString_CommandTimeout()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "Command Timeout=45";
            Assert.Equal(45, cn.CommandTimeout);
            cn.ConnectionString = "Command Timeout=40";
            Assert.Equal(40, cn.CommandTimeout);
            cn.ConnectionString = "command timeout=";
            Assert.Equal(30, cn.CommandTimeout);
            cn.ConnectionString = "Command Timeout=2147483647";
            Assert.Equal(int.MaxValue, cn.CommandTimeout);
            cn.ConnectionString = "Command Timeout=0";
            Assert.Equal(0, cn.CommandTimeout);
        }

        [Fact]
        public void ConnectionString_CommandTimeout_Invalid()
        {
            SqlConnection cn = new SqlConnection();

            // negative number
            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "Command timeout=-1");
            // Invalid value for key 'connect timeout'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'command timeout'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // invalid number
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "command Timeout=BB");
            // Invalid value for key 'connect timeout'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(FormatException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'command timeout'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Input string was not in a correct format
            FormatException fe = (FormatException)ex.InnerException;
            Assert.Null(fe.InnerException);
            Assert.NotNull(fe.Message);

            // overflow
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "command timeout=2147483648");
            // Invalid value for key 'command timeout'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'command timeout'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Value was either too large or too small for an Int32
            OverflowException oe = (OverflowException)ex.InnerException;
            Assert.Null(oe.InnerException);
            Assert.NotNull(oe.Message);
        }

        [Fact]
        public void ConnectionString_Database_Synonyms()
        {
            SqlConnection cn = null;

            cn = new SqlConnection();
            cn.ConnectionString = "Initial Catalog=db";
            Assert.Equal("db", cn.Database);

            cn = new SqlConnection();
            cn.ConnectionString = "Database=db";
            Assert.Equal("db", cn.Database);
        }

        [Fact]
        public void ConnectionString_MARS_Synonyms()
        {
            SqlConnection cn = null;
            SqlConnectionStringBuilder builder = null;

            cn = new SqlConnection();
            cn.ConnectionString = "MultipleActiveResultSets=true";
            builder = new SqlConnectionStringBuilder(cn.ConnectionString);
            Assert.True(true == builder.MultipleActiveResultSets);

            cn = new SqlConnection();
            cn.ConnectionString = "Multiple Active Result Sets=true";
            builder = new SqlConnectionStringBuilder(cn.ConnectionString);
            Assert.True(true == builder.MultipleActiveResultSets);
        }

        [Fact]
        public void ConnectionString_DataSource_Synonyms()
        {
            SqlConnection cn = null;

            cn = new SqlConnection();
            cn.ConnectionString = "Data Source=server";
            Assert.Equal("server", cn.DataSource);

            cn = new SqlConnection();
            cn.ConnectionString = "addr=server";
            Assert.Equal("server", cn.DataSource);

            cn = new SqlConnection();
            cn.ConnectionString = "address=server";
            Assert.Equal("server", cn.DataSource);

            cn = new SqlConnection();
            cn.ConnectionString = "network address=server";
            Assert.Equal("server", cn.DataSource);

            cn = new SqlConnection();
            cn.ConnectionString = "server=server";
            Assert.Equal("server", cn.DataSource);
        }

        [Fact]
        public void ConnectionString_MaxPoolSize()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "Max Pool Size=2147483647";
            cn.ConnectionString = "Max Pool Size=1";
            cn.ConnectionString = "Max Pool Size=500";
        }

        [Fact]
        public void ConnectionString_MaxPoolSize_Invalid()
        {
            SqlConnection cn = new SqlConnection();

            // negative number
            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "Max Pool Size=-1");
            // Invalid value for key 'max pool size'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'max pool size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // invalid number
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "max Pool size=BB");
            // Invalid value for key 'max pool size'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(FormatException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'max pool size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Input string was not in a correct format
            FormatException fe = (FormatException)ex.InnerException;
            Assert.Null(fe.InnerException);
            Assert.NotNull(fe.Message);

            // overflow
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "max pool size=2147483648");
            // Invalid value for key 'max pool size'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'max pool size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Value was either too large or too small for an Int32
            OverflowException oe = (OverflowException)ex.InnerException;
            Assert.Null(oe.InnerException);
            Assert.NotNull(oe.Message);

            // less than minimum (1)
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "Min Pool Size=0;Max Pool Size=0");
            // Invalid value for key 'max pool size'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'max pool size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // less than min pool size
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "Min Pool Size=5;Max Pool Size=4");
            // Invalid min or max pool size values, min
            // pool size cannot be greater than the max
            // pool size
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Null(ex.ParamName);
        }

        [Fact]
        public void ConnectionString_MinPoolSize()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "min Pool size=0";
            cn.ConnectionString = "Min Pool size=100";
            cn.ConnectionString = "Min Pool Size=2147483647;Max Pool Size=2147483647";
        }

        [Fact]
        public void ConnectionString_MinPoolSize_Invalid()
        {
            SqlConnection cn = new SqlConnection();

            // negative number
            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "Min Pool Size=-1");
            // Invalid value for key 'min pool size'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'min pool size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // invalid number
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "min Pool size=BB");
            // Invalid value for key 'min pool size'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(FormatException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'min pool size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Input string was not in a correct format
            FormatException fe = (FormatException)ex.InnerException;
            Assert.Null(fe.InnerException);
            Assert.NotNull(fe.Message);

            // overflow
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "min pool size=2147483648");
            // Invalid value for key 'min pool size'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'min pool size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Value was either too large or too small for an Int32
            OverflowException oe = (OverflowException)ex.InnerException;
            Assert.Null(oe.InnerException);
            Assert.NotNull(oe.Message);
        }

        [Fact]
        public void ConnectionString_MultipleActiveResultSets()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "MultipleActiveResultSets=true";
        }

        [Fact]
        public void ConnectionString_MultipleActiveResultSets_Invalid()
        {
            SqlConnection cn = new SqlConnection();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "MultipleActiveResultSets=1");
            // Invalid value for key 'multiple active result sets'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'multiple active result sets'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);
        }

        [Fact]
        public void ConnectionString_PacketSize()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "Packet Size=1024";
            Assert.Equal(1024, cn.PacketSize);
            cn.ConnectionString = "packet SizE=533";
            Assert.Equal(533, cn.PacketSize);
            cn.ConnectionString = "packet SizE=512";
            Assert.Equal(512, cn.PacketSize);
            cn.ConnectionString = "packet SizE=32768";
            Assert.Equal(32768, cn.PacketSize);
            cn.ConnectionString = "packet Size=";
            Assert.Equal(8000, cn.PacketSize);
        }

        [Fact]
        public void ConnectionString_PacketSize_Invalid()
        {
            SqlConnection cn = new SqlConnection();

            // invalid packet size (< minimum)
            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "Packet Size=511");
            // Invalid 'Packet Size'.  The value must be an
            // integer >= 512 and <= 32768
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'Packet Size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // invalid packet size (> maximum)
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "packet SIze=32769");
            // Invalid 'Packet Size'.  The value must be an
            // integer >= 512 and <= 32768
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'Packet Size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // overflow
            ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "packet SIze=2147483648");
            // Invalid value for key 'packet size'
            Assert.NotNull(ex.InnerException);
            Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'packet size'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);

            // Value was either too large or too small for an Int32
            OverflowException oe = (OverflowException)ex.InnerException;
            Assert.Null(oe.InnerException);
            Assert.NotNull(oe.Message);
        }

        [Fact]
        public void ConnectionString_Password_Synonyms()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "Password=scrambled";
            cn.ConnectionString = "Pwd=scrambled";
        }

        [Fact]
        public void ConnectionString_PersistSecurityInfo_Synonyms()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "Persist Security Info=true";
            cn.ConnectionString = "PersistSecurityInfo=true";
        }

        [Fact]
        public void ConnectionString_UserID_Synonyms()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "User Id=test";
            cn.ConnectionString = "User=test";
            cn.ConnectionString = "Uid=test";
        }

        [Fact]
        public void ConnectionString_UserInstance()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "User Instance=true";
        }

        [Fact]
        public void ConnectionString_UserInstance_Invalid()
        {
            SqlConnection cn = new SqlConnection();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = "User Instance=1");
            // Invalid value for key 'user instance'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.IndexOf("'user instance'", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.Null(ex.ParamName);
        }

        [Theory]
        [InlineData("Application Name=test")]
        [InlineData("App=test")]
        // [InlineData("Connection Reset=true")] // see https://github.com/dotnet/SqlClient/issues/17
        [InlineData("Current Language=test")]
        [InlineData("Language=test")]
        [InlineData("Encrypt=false")]
        [InlineData("Encrypt=true")]
        [InlineData("Encrypt=yes")]
        [InlineData("Encrypt=no")]
        [InlineData("Encrypt=strict")]
        [InlineData("Encrypt=mandatory")]
        [InlineData("Encrypt=optional")]
        [InlineData("Host Name In Certificate=tds.test.com")]
        [InlineData("HostNameInCertificate=tds.test.com")]
        [InlineData("Server Certificate=c:\\test.cer")]
        [InlineData("ServerCertificate=c:\\test.cer")]
        [InlineData("Enlist=false")]
        [InlineData("Enlist=true")]
        [InlineData("Integrated Security=true")]
        [InlineData("Trusted_connection=true")]
        [InlineData("Max Pool Size=10")]
        [InlineData("Min Pool Size=10")]
        [InlineData("Pooling=true")]
        [InlineData("attachdbfilename=dunno")]
        [InlineData("extended properties=dunno")]
        [InlineData("initial file name=dunno")]
        public void ConnectionString_OtherKeywords(string connectionString)
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = connectionString;
        }

        [Fact]
        public void Open_ConnectionString_Empty()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = string.Empty;

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cn.Open());
            // The ConnectionString property has not been
            // initialized
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
        }

        [Fact]
        public void Open_ConnectionString_Null()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = null;

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cn.Open());
            // The ConnectionString property has not been
            // initialized
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
        }

        [Fact]
        public void Open_ConnectionString_Whitespace()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "    ";

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => cn.Open());
            // The ConnectionString property has not been
            // initialized
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
        }

        [Fact]
        public void ServerVersion_Connection_Closed()
        {
            SqlConnection cn = new SqlConnection();
            string version;

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => version = cn.ServerVersion);
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);

            cn = new SqlConnection("server=SQLSRV; database=dotnet;");

            ex = Assert.Throws<InvalidOperationException>(() => version = cn.ServerVersion);
            // Invalid operation. The connection is closed
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
        }

        [Fact]
        public void RetrieveInternalInfo_Success()
        {
            SqlConnection cn = new SqlConnection();
            IDictionary<string, object> d = cn.RetrieveInternalInfo();

            Assert.NotNull(d);
        }

        [Fact]
        public void RetrieveInternalInfo_ExpectedKeysInDictionary_Success()
        {
            SqlConnection cn = new SqlConnection();
            IDictionary<string, object> d = cn.RetrieveInternalInfo();

            Assert.NotEmpty(d);
            Assert.Equal(s_retrieveInternalInfoKeys.Length, d.Count);

            Assert.NotEmpty(d.Keys);
            Assert.Equal(s_retrieveInternalInfoKeys.Length, d.Keys.Count);

            Assert.NotEmpty(d.Values);
            Assert.Equal(s_retrieveInternalInfoKeys.Length, d.Values.Count);

            foreach (string key in s_retrieveInternalInfoKeys)
            {
                Assert.True(d.ContainsKey(key));

                d.TryGetValue(key, out object value);
                Assert.NotNull(value);
                Assert.IsType<string>(value);
            }
        }

        [Fact]
        public void RetrieveInternalInfo_UnexpectedKeysInDictionary_Success()
        {
            SqlConnection cn = new SqlConnection();
            IDictionary<string, object> d = cn.RetrieveInternalInfo();
            Assert.False(d.ContainsKey("Foo"));
        }

        [Fact]
        public void ConnectionString_IPAddressPreference()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "IPAddressPreference=IPv4First";
            cn.ConnectionString = "IPAddressPreference=IPV4FIRST";
            cn.ConnectionString = "IPAddressPreference=ipv4first";
            cn.ConnectionString = "IPAddressPreference=iPv4FirSt";
            cn.ConnectionString = "IPAddressPreference=IPv6First";
            cn.ConnectionString = "IPAddressPreference=IPV6FIRST";
            cn.ConnectionString = "IPAddressPreference=ipv6first";
            cn.ConnectionString = "IPAddressPreference=iPv6FirST";
            cn.ConnectionString = "IPAddressPreference=UsePlatformDefault";
            cn.ConnectionString = "IPAddressPreference=USEPLATFORMDEFAULT";
            cn.ConnectionString = "IPAddressPreference=useplatformdefault";
            cn.ConnectionString = "IPAddressPreference=usePlAtFormdeFault";
        }

        [Theory]
        [InlineData("IPAddressPreference=-1")]
        [InlineData("IPAddressPreference=0")]
        [InlineData("IPAddressPreference=!@#")]
        [InlineData("IPAddressPreference=ABC")]
        [InlineData("IPAddressPreference=ipv6")]
        public void ConnectionString_IPAddressPreference_Invalid(string value)
        {
            SqlConnection cn = new SqlConnection();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => cn.ConnectionString = value);
            // Invalid value for key 'ip address preference'
            Assert.Null(ex.InnerException);
            Assert.NotNull(ex.Message);
            Assert.Contains("'ip address preference'", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(ex.ParamName);
        }

        [Theory]
        [InlineData("Server SPN = server1")]
        [InlineData("ServerSPN = server2")]
        [InlineData("Failover Partner SPN = server3")]
        [InlineData("FailoverPartnerSPN = server4")]
        public void ConnectionString_ServerSPN_FailoverPartnerSPN(string value)
        {
            SqlConnection _ = new(value);
        }

        [Fact]
        public void ConnectionRetryForNonAzureEndpoints()
        {
            SqlConnection cn = new SqlConnection("Data Source = someserver");
            FieldInfo field = typeof(SqlConnection).GetField("_connectRetryCount", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field.GetValue(cn));
            Assert.Equal(1, (int)field.GetValue(cn));
        }

        [Fact]
        public void ConnectionRetryForAzureDbEndpoints()
        {
            SqlConnection cn = new SqlConnection("Data Source = someserver.database.windows.net");
            FieldInfo field = typeof(SqlConnection).GetField("_connectRetryCount", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field.GetValue(cn));
            Assert.Equal(2, (int)field.GetValue(cn));
        }

        [Theory]
        [InlineData("myserver-ondemand.sql.azuresynapse.net")]
        [InlineData("someserver-ondemand.database.windows.net")]
        public void ConnectionRetryForAzureOnDemandEndpoints(string serverName)
        {
            SqlConnection cn = new SqlConnection($"Data Source = {serverName}");
            FieldInfo field = typeof(SqlConnection).GetField("_connectRetryCount", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field.GetValue(cn));
            Assert.Equal(5, (int)field.GetValue(cn));
        }
    }
}
