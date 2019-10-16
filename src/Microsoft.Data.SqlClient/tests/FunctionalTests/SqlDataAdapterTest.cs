// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Odbc;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlDataAdapterTest
    {
        [Fact]
        public void Constructor1()
        {
            SqlDataAdapter da = new SqlDataAdapter();
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.Null(da.SelectCommand);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void Constructor2()
        {
            SqlCommand cmd = new SqlCommand();
            SqlDataAdapter da = new SqlDataAdapter(cmd);
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.NotNull(da.SelectCommand);
            Assert.Same(cmd, da.SelectCommand);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void Constructor2_SelectCommand_Null()
        {
            SqlDataAdapter da = new SqlDataAdapter((SqlCommand)null);
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.Null(da.SelectCommand);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void Constructor3()
        {
            string selectCommandText = "SELECT * FROM Authors";
            SqlConnection selectConnection = new SqlConnection();

            SqlDataAdapter da = new SqlDataAdapter(selectCommandText,
                selectConnection);
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.NotNull(da.SelectCommand);
            Assert.Same(selectCommandText, da.SelectCommand.CommandText);
            Assert.Same(selectConnection, da.SelectCommand.Connection);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void Constructor3_SelectCommandText_Null()
        {
            SqlConnection selectConnection = new SqlConnection();

            SqlDataAdapter da = new SqlDataAdapter((string)null,
                selectConnection);
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.NotNull(da.SelectCommand);
            Assert.NotNull(da.SelectCommand.CommandText);
            Assert.Equal(string.Empty, da.SelectCommand.CommandText);
            Assert.Same(selectConnection, da.SelectCommand.Connection);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void Constructor3_SelectConnection_Null()
        {
            string selectCommandText = "SELECT * FROM Authors";

            SqlDataAdapter da = new SqlDataAdapter(selectCommandText,
                (SqlConnection)null);
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.NotNull(da.SelectCommand);
            Assert.Same(selectCommandText, da.SelectCommand.CommandText);
            Assert.Null(da.SelectCommand.Connection);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void Constructor4()
        {
            string selectCommandText = "SELECT * FROM Authors";
            string selectConnectionString = "server=SQLSRV;database=dotnet";

            SqlDataAdapter da = new SqlDataAdapter(selectCommandText,
                selectConnectionString);
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.NotNull(da.SelectCommand);
            Assert.Same(selectCommandText, da.SelectCommand.CommandText);
            Assert.NotNull(da.SelectCommand.Connection);
            Assert.Equal(selectConnectionString, da.SelectCommand.Connection.ConnectionString);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void Constructor4_SelectCommandText_Null()
        {
            string selectConnectionString = "server=SQLSRV;database=dotnet";

            SqlDataAdapter da = new SqlDataAdapter((string)null,
                selectConnectionString);
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.NotNull(da.SelectCommand);
            Assert.NotNull(da.SelectCommand.CommandText);
            Assert.Equal(string.Empty, da.SelectCommand.CommandText);
            Assert.NotNull(da.SelectCommand.Connection);
            Assert.Equal(selectConnectionString, da.SelectCommand.Connection.ConnectionString);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void Constructor4_SelectConnectionString_Null()
        {
            string selectCommandText = "SELECT * FROM Authors";

            SqlDataAdapter da = new SqlDataAdapter(selectCommandText,
                (string)null);
            Assert.True(da.AcceptChangesDuringFill);
            Assert.True(da.AcceptChangesDuringUpdate);
            Assert.Null(da.Container);
            Assert.False(da.ContinueUpdateOnError);
            Assert.Null(da.DeleteCommand);
            Assert.Equal(LoadOption.OverwriteChanges, da.FillLoadOption);
            Assert.Null(da.InsertCommand);
            Assert.Equal(MissingMappingAction.Passthrough, da.MissingMappingAction);
            Assert.Equal(MissingSchemaAction.Add, da.MissingSchemaAction);
            Assert.False(da.ReturnProviderSpecificTypes);
            Assert.NotNull(da.SelectCommand);
            Assert.Same(selectCommandText, da.SelectCommand.CommandText);
            Assert.NotNull(da.SelectCommand.Connection);
            Assert.Equal(string.Empty, da.SelectCommand.Connection.ConnectionString);
            Assert.Null(da.Site);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Equal(1, da.UpdateBatchSize);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void DeleteCommand()
        {
            SqlDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd1 = new SqlCommand();
            SqlCommand cmd2 = new SqlCommand();

            da.DeleteCommand = cmd1;
            Assert.Same(cmd1, da.DeleteCommand);
            da.DeleteCommand = cmd2;
            Assert.Same(cmd2, da.DeleteCommand);
            da.DeleteCommand = null;
            Assert.Null(da.DeleteCommand);
        }

        [Fact]
        public void Dispose()
        {
            SqlDataAdapter da = new SqlDataAdapter();
            da.DeleteCommand = new SqlCommand();
            da.InsertCommand = new SqlCommand();
            da.SelectCommand = new SqlCommand();
            da.UpdateCommand = new SqlCommand();
            da.Dispose();

            Assert.Null(da.DeleteCommand);
            Assert.Null(da.InsertCommand);
            Assert.Null(da.SelectCommand);
            Assert.NotNull(da.TableMappings);
            Assert.Empty(da.TableMappings);
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void InsertCommand()
        {
            SqlDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd1 = new SqlCommand();
            SqlCommand cmd2 = new SqlCommand();

            da.InsertCommand = cmd1;
            Assert.Same(cmd1, da.InsertCommand);
            da.InsertCommand = cmd2;
            Assert.Same(cmd2, da.InsertCommand);
            da.InsertCommand = null;
            Assert.Null(da.InsertCommand);
        }

        [Fact]
        public void SelectCommand()
        {
            SqlDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd1 = new SqlCommand();
            SqlCommand cmd2 = new SqlCommand();

            da.SelectCommand = cmd1;
            Assert.Same(cmd1, da.SelectCommand);
            da.SelectCommand = cmd2;
            Assert.Same(cmd2, da.SelectCommand);
            da.SelectCommand = null;
            Assert.Null(da.SelectCommand);
        }

        [Fact]
        public void UpdateBatchSize()
        {
            SqlDataAdapter da = new SqlDataAdapter();
            da.UpdateBatchSize = 0;
            Assert.Equal(0, da.UpdateBatchSize);
            da.UpdateBatchSize = int.MaxValue;
            Assert.Equal(int.MaxValue, da.UpdateBatchSize);
            da.UpdateBatchSize = 1;
            Assert.Equal(1, da.UpdateBatchSize);
        }

        [Fact]
        public void UpdateBatchSize_Negative()
        {
            SqlDataAdapter da = new SqlDataAdapter();
            try
            {
                da.UpdateBatchSize = -1;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.Equal(typeof(ArgumentOutOfRangeException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.NotNull(ex.ParamName);
                Assert.Equal("UpdateBatchSize", ex.ParamName);
            }
        }

        [Fact]
        public void UpdateCommand()
        {
            SqlDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd1 = new SqlCommand();
            SqlCommand cmd2 = new SqlCommand();

            da.UpdateCommand = cmd1;
            Assert.Same(cmd1, da.UpdateCommand);
            da.UpdateCommand = cmd2;
            Assert.Same(cmd2, da.UpdateCommand);
            da.UpdateCommand = null;
            Assert.Null(da.UpdateCommand);
        }

        [Fact]
        public void DeleteCommand_IDbDataAdapter()
        {
            IDbDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd1 = new SqlCommand();
            SqlCommand cmd2 = new SqlCommand();

            da.DeleteCommand = cmd1;
            Assert.Same(cmd1, da.DeleteCommand);
            da.DeleteCommand = cmd2;
            Assert.Same(cmd2, da.DeleteCommand);
            da.DeleteCommand = null;
            Assert.Null(da.DeleteCommand);

            try
            {
                da.DeleteCommand = new OdbcCommand();
            }
            catch (InvalidCastException ex)
            {
                Assert.Equal(typeof(InvalidCastException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }

        [Fact]
        public void InsertCommand_IDbDataAdapter()
        {
            IDbDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd1 = new SqlCommand();
            SqlCommand cmd2 = new SqlCommand();

            da.InsertCommand = cmd1;
            Assert.Same(cmd1, da.InsertCommand);
            da.InsertCommand = cmd2;
            Assert.Same(cmd2, da.InsertCommand);
            da.InsertCommand = null;
            Assert.Null(da.InsertCommand);

            try
            {
                da.InsertCommand = new OdbcCommand();
            }
            catch (InvalidCastException ex)
            {
                Assert.Equal(typeof(InvalidCastException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }

        [Fact]
        public void SelectCommand_IDbDataAdapter()
        {
            IDbDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd1 = new SqlCommand();
            SqlCommand cmd2 = new SqlCommand();

            da.SelectCommand = cmd1;
            Assert.Same(cmd1, da.SelectCommand);
            da.SelectCommand = cmd2;
            Assert.Same(cmd2, da.SelectCommand);
            da.SelectCommand = null;
            Assert.Null(da.SelectCommand);

            try
            {
                da.SelectCommand = new OdbcCommand();
            }
            catch (InvalidCastException ex)
            {
                Assert.Equal(typeof(InvalidCastException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }

        [Fact]
        public void UpdateCommand_IDbDataAdapter()
        {
            IDbDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd1 = new SqlCommand();
            SqlCommand cmd2 = new SqlCommand();

            da.UpdateCommand = cmd1;
            Assert.Same(cmd1, da.UpdateCommand);
            da.UpdateCommand = cmd2;
            Assert.Same(cmd2, da.UpdateCommand);
            da.UpdateCommand = null;
            Assert.Null(da.UpdateCommand);

            try
            {
                da.UpdateCommand = new OdbcCommand();
            }
            catch (InvalidCastException ex)
            {
                Assert.Equal(typeof(InvalidCastException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
            }
        }
    }
}
