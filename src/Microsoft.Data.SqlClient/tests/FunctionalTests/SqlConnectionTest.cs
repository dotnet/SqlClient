// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Collections.Generic;
using Xunit;

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
            Assert.True(string.Compare (Environment.MachineName, cn.WorkstationId, true) == 0);

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
            Assert.True(string.Compare (Environment.MachineName, cn.WorkstationId, true) == 0);
        }

        [Fact]
        public void Constructor2_ConnectionString_Invalid()
        {
            try
            {
                new SqlConnection("InvalidConnectionString");
            }
            catch (ArgumentException ex)
            {
                // Format of the initialization string does
                // not conform to specification starting at
                // index 0
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }

            // invalid keyword
            try
            {
                new SqlConnection("invalidKeyword=10");
            }
            catch (ArgumentException ex)
            {
                // Keyword not supported: 'invalidkeyword'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'invalidkeyword'") != -1);
                Assert.Null(ex.ParamName);
            }

            // invalid packet size (< minimum)
            try
            {
                new SqlConnection("Packet Size=511");
           }
            catch (ArgumentException ex)
            {
                // Invalid 'Packet Size'.  The value must be an
                // integer >= 512 and <= 32768
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }

            // invalid packet size (> maximum)
            try
            {
                new SqlConnection("Packet Size=32769");
            }
            catch (ArgumentException ex)
            {
                // Invalid 'Packet Size'.  The value must be an
                // integer >= 512 and <= 32768
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }

            // negative connect timeout
            try
            {
                new SqlConnection("Connect Timeout=-1");
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'connect timeout'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }

            // negative max pool size
            try
            {
                new SqlConnection("Max Pool Size=-1");
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'max pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }

            // negative min pool size
            try
            {
                new SqlConnection("Min Pool Size=-1");
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'min pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }
        }

        [Fact]
        public void BeginTransaction_Connection_Closed()
        {
            SqlConnection cn = new SqlConnection();

            try
            {
                cn.BeginTransaction();
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.BeginTransaction((IsolationLevel)666);
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.BeginTransaction(IsolationLevel.Serializable);
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.BeginTransaction("trans");
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.BeginTransaction((IsolationLevel)666, "trans");
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.BeginTransaction(IsolationLevel.Serializable, "trans");
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }

        [Fact]
        public void ChangeDatabase_Connection_Closed()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "server=SQLSRV";

            try
            {
                cn.ChangeDatabase("database");
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }

        [Fact]
        public void ChangePassword_ConnectionString_Empty()
        {
            try
            {
                SqlConnection.ChangePassword(string.Empty, "dotnet");
            }
            catch (ArgumentNullException ex)
            {
                Assert.Equal(typeof(ArgumentNullException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.NotNull(ex.ParamName);
            }
        }

        [Fact]
        public void ChangePassword_ConnectionString_Null()
        {
            try
            {
                SqlConnection.ChangePassword((string)null, "dotnet");
            }
            catch (ArgumentNullException ex)
            {
                Assert.Equal(typeof(ArgumentNullException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.NotNull(ex.ParamName);
            }
        }

        [Fact]
        public void ChangePassword_NewPassword_Empty()
        {
            try
            {
                SqlConnection.ChangePassword("server=SQLSRV", string.Empty);
            }
            catch (ArgumentNullException ex)
            {
                Assert.Equal(typeof(ArgumentNullException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.NotNull(ex.ParamName);
                Assert.True(ex.ParamName.IndexOf("'newPassword'") != -1);
            }
        }

        [Fact]
        public void ChangePassword_NewPassword_ExceedMaxLength()
        {
            try
            {
                SqlConnection.ChangePassword("server=SQLSRV",
                    new string('d', 129));
            }
            catch (ArgumentException ex)
            {
                // The length of argument 'newPassword' exceeds
                // it's limit of '128'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'newPassword'") != -1);
                Assert.True(ex.Message.IndexOf("128") != -1);
                Assert.Null(ex.ParamName);
            }
        }

        [Fact]
        public void ChangePassword_NewPassword_Null()
        {
            try
            {
                SqlConnection.ChangePassword("server=SQLSRV", (string)null);
            }
            catch (ArgumentNullException ex)
            {
                Assert.Equal(typeof(ArgumentNullException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.NotNull(ex.ParamName);
                Assert.True(ex.ParamName.IndexOf("'newPassword'") != -1);
            }
        }

        [Fact]
        public void ClearPool_Connection_Null()
        {
            try
            {
                SqlConnection.ClearPool((SqlConnection)null);
            }
            catch (ArgumentNullException ex)
            {
                Assert.Equal(typeof(ArgumentNullException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Equal("connection", ex.ParamName);
            }
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

            try
            {
                cn.ConnectionString = "InvalidConnectionString";
            }
            catch (ArgumentException ex)
            {
                // Format of the initialization string does
                // not conform to specification starting at
                // index 0
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }

            // invalid keyword
            try
            {
                cn.ConnectionString = "invalidKeyword=10";
            }
            catch (ArgumentException ex)
            {
                // Keyword not supported: 'invalidkeyword'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'invalidkeyword'") != -1);
                Assert.Null(ex.ParamName);
            }
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

            try
            {
                cn.GetSchema();
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.GetSchema("Tables");
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.GetSchema((string)null);
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.GetSchema("Tables", new string[] { "master" });
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.GetSchema((string)null, new string[] { "master" });
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.GetSchema("Tables", null);
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            try
            {
                cn.GetSchema(null, null);
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
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
            try
            {
                cn.ConnectionString = "Connection timeout=-1";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'connect timeout'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'connect timeout'") != -1);
                Assert.Null(ex.ParamName);
            }

            // invalid number
            try
            {
                cn.ConnectionString = "connect Timeout=BB";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'connect timeout'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(FormatException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'connect timeout'") != -13);
                Assert.Null(ex.ParamName);

                // Input string was not in a correct format
                FormatException fe = (FormatException)ex.InnerException;
                Assert.Null(fe.InnerException);
                Assert.NotNull(fe.Message);
            }

            // overflow
            try
            {
                cn.ConnectionString = "timeout=2147483648";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'connect timeout'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'connect timeout'") != -1);
                Assert.Null(ex.ParamName);

                // Value was either too large or too small for an Int32
                OverflowException oe = (OverflowException)ex.InnerException;
                Assert.Null(oe.InnerException);
                Assert.NotNull(oe.Message);
            }
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
            try
            {
                cn.ConnectionString = "Command timeout=-1";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'connect timeout'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'command timeout'") != -1);
                Assert.Null(ex.ParamName);
            }

            // invalid number
            try
            {
                cn.ConnectionString = "command Timeout=BB";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'connect timeout'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(FormatException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'command timeout'") != -13);
                Assert.Null(ex.ParamName);

                // Input string was not in a correct format
                FormatException fe = (FormatException)ex.InnerException;
                Assert.Null(fe.InnerException);
                Assert.NotNull(fe.Message);
            }

            // overflow
            try
            {
                cn.ConnectionString = "command timeout=2147483648";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'command timeout'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'command timeout'") != -1);
                Assert.Null(ex.ParamName);

                // Value was either too large or too small for an Int32
                OverflowException oe = (OverflowException)ex.InnerException;
                Assert.Null(oe.InnerException);
                Assert.NotNull(oe.Message);
            }
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
            try
            {
                cn.ConnectionString = "Max Pool Size=-1";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'max pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'max pool size'") != -1);
                Assert.Null(ex.ParamName);
            }

            // invalid number
            try
            {
                cn.ConnectionString = "max Pool size=BB";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'max pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(FormatException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'max pool size'") != -1);
                Assert.Null(ex.ParamName);

                // Input string was not in a correct format
                FormatException fe = (FormatException)ex.InnerException;
                Assert.Null(fe.InnerException);
                Assert.NotNull(fe.Message);
            }

            // overflow
            try
            {
                cn.ConnectionString = "max pool size=2147483648";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'max pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'max pool size'") != -1);
                Assert.Null(ex.ParamName);

                // Value was either too large or too small for an Int32
                OverflowException oe = (OverflowException)ex.InnerException;
                Assert.Null(oe.InnerException);
                Assert.NotNull(oe.Message);
            }

            // less than minimum (1)
            try
            {
                cn.ConnectionString = "Min Pool Size=0;Max Pool Size=0";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'max pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'max pool size'") != -1);
                Assert.Null(ex.ParamName);
            }

            // less than min pool size
            try
            {
                cn.ConnectionString = "Min Pool Size=5;Max Pool Size=4";
            }
            catch (ArgumentException ex)
            {
                // Invalid min or max pool size values, min
                // pool size cannot be greater than the max
                // pool size
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.Null(ex.ParamName);
            }
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
            try
            {
                cn.ConnectionString = "Min Pool Size=-1";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'min pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'min pool size'") != -1);
                Assert.Null(ex.ParamName);
            }

            // invalid number
            try
            {
                cn.ConnectionString = "min Pool size=BB";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'min pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(FormatException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'min pool size'") != -1);
                Assert.Null(ex.ParamName);

                // Input string was not in a correct format
                FormatException fe = (FormatException)ex.InnerException;
                Assert.Null(fe.InnerException);
                Assert.NotNull(fe.Message);
            }

            // overflow
            try
            {
                cn.ConnectionString = "min pool size=2147483648";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'min pool size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'min pool size'") != -1);
                Assert.Null(ex.ParamName);

                // Value was either too large or too small for an Int32
                OverflowException oe = (OverflowException)ex.InnerException;
                Assert.Null(oe.InnerException);
                Assert.NotNull(oe.Message);
            }
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
            try
            {
                cn.ConnectionString = "MultipleActiveResultSets=1";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'multiple active result sets'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'multiple active result sets'") != -1);
                Assert.Null(ex.ParamName);
            }
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
            try
            {
                cn.ConnectionString = "Packet Size=511";
            }
            catch (ArgumentException ex)
            {
                // Invalid 'Packet Size'.  The value must be an
                // integer >= 512 and <= 32768
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'Packet Size'") != -1);
                Assert.Null(ex.ParamName);
            }

            // invalid packet size (> maximum)
            try
            {
                cn.ConnectionString = "packet SIze=32769";
            }
            catch (ArgumentException ex)
            {
                // Invalid 'Packet Size'.  The value must be an
                // integer >= 512 and <= 32768
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'Packet Size'") != -1);
                Assert.Null(ex.ParamName);
            }

            // overflow
            try
            {
                cn.ConnectionString = "packet SIze=2147483648";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'packet size'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.NotNull(ex.InnerException);
                Assert.Equal(typeof(OverflowException), ex.InnerException.GetType());
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'packet size'") != -1);
                Assert.Null(ex.ParamName);

                // Value was either too large or too small for an Int32
                OverflowException oe = (OverflowException)ex.InnerException;
                Assert.Null(oe.InnerException);
                Assert.NotNull(oe.Message);
            }
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
            try
            {
                cn.ConnectionString = "User Instance=1";
            }
            catch (ArgumentException ex)
            {
                // Invalid value for key 'user instance'
                Assert.Equal(typeof(ArgumentException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("'user instance'") != -1);
                Assert.Null(ex.ParamName);
            }
        }

        [Fact]
        public void ConnectionString_OtherKeywords()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "Application Name=test";
            cn.ConnectionString = "App=test";
            // see https://github.com/dotnet/SqlClient/issues/17
            //cn.ConnectionString = "Connection Reset=true";
            cn.ConnectionString = "Current Language=test";
            cn.ConnectionString = "Language=test";
            cn.ConnectionString = "Encrypt=false";
            cn.ConnectionString = "Encrypt=true";
            cn.ConnectionString = "Enlist=false";
            cn.ConnectionString = "Enlist=true";
            cn.ConnectionString = "Integrated Security=true";
            cn.ConnectionString = "Trusted_connection=true";
            cn.ConnectionString = "Max Pool Size=10";
            cn.ConnectionString = "Min Pool Size=10";
            cn.ConnectionString = "Pooling=true";
            cn.ConnectionString = "attachdbfilename=dunno";
            cn.ConnectionString = "extended properties=dunno";
            cn.ConnectionString = "initial file name=dunno";
        }

        [Fact]
        public void Open_ConnectionString_Empty()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = string.Empty;

            try
            {
                cn.Open();
            }
            catch (InvalidOperationException ex)
            {
                // The ConnectionString property has not been
                // initialized
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }

        [Fact]
        public void Open_ConnectionString_Null()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = null;

            try
            {
                cn.Open();
            }
            catch (InvalidOperationException ex)
            {
                // The ConnectionString property has not been
                // initialized
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }

        [Fact]
        public void Open_ConnectionString_Whitespace()
        {
            SqlConnection cn = new SqlConnection();
            cn.ConnectionString = "    ";

            try
            {
                cn.Open();
            }
            catch (InvalidOperationException ex)
            {
                // The ConnectionString property has not been
                // initialized
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }

        [Fact]
        public void ServerVersion_Connection_Closed()
        {
            SqlConnection cn = new SqlConnection();
            try
            {
                var version = cn.ServerVersion;
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }

            cn = new SqlConnection("server=SQLSRV; database=dotnet;");
            try
            {
                var version = cn.ServerVersion;
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation. The connection is closed
                Assert.Equal(typeof(InvalidOperationException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
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

            foreach(string key in s_retrieveInternalInfoKeys)
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
    }
}
