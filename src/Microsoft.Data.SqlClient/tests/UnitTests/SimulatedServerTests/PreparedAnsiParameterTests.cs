// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Parser.Tokens;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    [Collection(SimulatedServerTestCollection.Name)]
    public class PreparedAnsiParameterTests
    {
        [Theory]
        [InlineData(4000, false, true, true)]
        [InlineData(4000, true, false, false)]
        [InlineData(8000, false, true, false)]
        [InlineData(-1, false, true, false)]
        public void PreparedDeclaration_ValueChanges_RequiresOnlyWiderByteCapacity(
            int size, bool initialMultibyte, bool nextMultibyte, bool expectReprepare)
        {
            using TdsServer server = new();
            server.Start();
            using SqlConnection connection = new(new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false
            }.ConnectionString);
            connection.Open();
            TdsParser parser = connection.Parser;
            typeof(TdsParser).GetField("_defaultEncoding",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parser, Encoding.UTF8);
            object state = typeof(TdsParser).GetField("_physicalStateObj",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(parser)!;
            using SqlCommand command = new("SELECT @p", connection);
            SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.VarChar, size);
            parameter.Value = new string(initialMultibyte ? '\u00e9' : 'a', 4000);
            command.Parameters.Add("@integer", SqlDbType.Int).Value = 1;
            command.Parameters.Add("@legacy", SqlDbType.Text, 10).Value = "legacy";
            command.Parameters.Add("@return", SqlDbType.VarChar, 10).Direction =
                ParameterDirection.ReturnValue;
            command.Prepare();
            typeof(SqlCommand).GetField("_stateObj",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(command, state);

            _SqlRPC rpc = (_SqlRPC)typeof(SqlCommand).GetMethod("BuildPrepExec",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(command,
                    new object[] { CommandBehavior.Default })!;
            string declaration = Assert.IsType<string>(rpc.systemParams[1].Value);
            Assert.Contains(size == -1 ? "@p varchar(max)" :
                $"@p varchar({Math.Max(size, initialMultibyte ? 8000 : 4000)})", declaration);

            typeof(SqlCommand).GetField("_inPrepare",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(command, true);
            TdsReturnValueToken handle = new();
            handle.Value.Int32 = 123;
            command.OnReturnValue(handle, (TdsParserStateObject)state);
            parameter.Value = new string(nextMultibyte ? '\u00e9' : 'a', 4000);

            bool requiresReprepare = (bool)typeof(SqlCommand)
                .GetMethod("PreparedParametersRequireReprepare",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(command, null)!;

            Assert.Equal(expectReprepare, requiresReprepare);
            Assert.Equal(size, parameter.Size);
            Assert.Null(typeof(SqlCommand).GetField("_pendingParameterByteCapacities",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(command));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PreparedDeclaration_MissingOrChangedSchema_RequiresFreshPreparation(
            bool changedSchema)
        {
            using SqlCommand command = new();
            command.Parameters.Add("@p", SqlDbType.VarChar, 8000).Value = "value";
            if (changedSchema)
            {
                // A snapshot for an earlier parameter collection cannot describe this command.
                typeof(SqlCommand).GetField("_preparedParameterByteCapacities",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(command, Array.Empty<int>());
            }

            bool requiresReprepare = (bool)typeof(SqlCommand)
                .GetMethod("PreparedParametersRequireReprepare",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(command, null)!;

            Assert.True(requiresReprepare);
        }
    }
}
