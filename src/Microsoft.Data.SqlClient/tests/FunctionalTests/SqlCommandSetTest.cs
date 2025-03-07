using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlCommandSetTest
    {
        private static Assembly mds = Assembly.GetAssembly(typeof(SqlConnection));

        [Theory]
        [InlineData("BatchCommand")]
        [InlineData("CommandList")]
        public void GetDisposedProperty_Throws(string propertyName)
        {
            var cmdSet = CreateInstance();
            CallMethod(cmdSet, "Dispose");
            Exception ex = GetProperty_Throws(cmdSet, propertyName);
            VerifyException<ObjectDisposedException>(ex, "disposed");
        }

        [Fact]
        public void AppendCommandWithEmptyString_Throws()
        {
            var cmdSet = CreateInstance();
            SqlCommand cmd = new SqlCommand("");
            Exception ex = CallMethod_Throws(cmdSet, "Append", cmd);
            VerifyException<InvalidOperationException>(ex, "CommandText property has not been initialized");
        }

        public static IEnumerable<object[]> CommandTypeData()
        {
            return new object[][]
            {
                new object[] { CommandType.TableDirect },
                new object[] { (CommandType)5 }
            };
        }

        [Theory]
        [MemberData(
            nameof(CommandTypeData)
#if NETFRAMEWORK
            // .NET Framework puts system enums in something called the Global
            // Assembly Cache (GAC), and xUnit refuses to serialize enums that
            // live there.  So for .NET Framework, we disable enumeration of the
            // test data to avoid warnings on the console when running tests.
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void AppendBadCommandType_Throws(CommandType commandType)
        {
            var cmdSet = CreateInstance();
            SqlCommand cmd = GenerateBadCommand(commandType);
            Exception ex = CallMethod_Throws(cmdSet, "Append", cmd);
            VerifyException<ArgumentOutOfRangeException>(ex, "CommandType");
        }

        [Fact]
        public void AppendBadParameterName_Throws()
        {
            var cmdSet = CreateInstance();
            SqlCommand cmd = new SqlCommand("Test");
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SqlParameter("Test1;=", "1"));
            Exception ex = CallMethod_Throws(cmdSet, "Append", cmd);
            VerifyException<ArgumentException>(ex, "not valid");
        }

        [Theory]
        [InlineData(new byte[] { 1, 2, 3 })]
        [InlineData(new char[] { '1', '2', '3' })]
        public void AppendParameterArrayWithSize(object array)
        {
            var cmdSet = CreateInstance();
            SqlCommand cmd = new SqlCommand("Test");
            cmd.CommandType = CommandType.StoredProcedure;
            SqlParameter parameter = new SqlParameter("@array", array);
            parameter.Size = 2;
            cmd.Parameters.Add(parameter);
            CallMethod(cmdSet, "Append", cmd);
            object p = CallMethod(cmdSet, "GetParameter", 0, 0);
            SqlParameter result = p as SqlParameter;
            Assert.NotNull(result);
            Assert.Equal("@array", result.ParameterName);
            Assert.Equal(2, result.Size);
        }

        [Fact]
        public void GetParameter()
        {
            var cmdSet = CreateInstance();
            SqlCommand cmd = new SqlCommand("Test");
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SqlParameter("@text", "value"));
            CallMethod(cmdSet, "Append", cmd);
            object p = CallMethod(cmdSet, "GetParameter", 0, 0);
            SqlParameter result = p as SqlParameter;
            Assert.NotNull(result);
            Assert.Equal("@text", result.ParameterName);
            Assert.Equal("value", (string)result.Value);
        }

        [Fact]
        public void GetParameterCount()
        {
            var commandSetType = mds.GetType("Microsoft.Data.SqlClient.SqlCommandSet");
            var cmdSet = Activator.CreateInstance(commandSetType, true);
            SqlCommand cmd = new SqlCommand("Test");
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SqlParameter("@abc", "1"));
            cmd.Parameters.Add(new SqlParameter("@test", "2"));
            commandSetType.GetMethod("Append", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(cmdSet, new object[] { cmd });
            int index = 0;
            int count = (int)commandSetType.GetMethod("GetParameterCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(cmdSet, new object[] { index });
            Assert.Equal(2, count);
        }

        [Fact]
        public void InvalidCommandBehaviorValidateCommandBehavior_Throws()
        {
            var cmdSet = CreateInstance();
            Exception ex = CallMethod_Throws(cmdSet, "ValidateCommandBehavior", "ExecuteNonQuery", (CommandBehavior)64);
            VerifyException<ArgumentOutOfRangeException>(ex, "CommandBehavior");
        }

        [Fact]
        public void NotSupportedCommandBehaviorValidateCommandBehavior_Throws()
        {
            var cmdSet = CreateInstance();
            Exception ex = CallMethod_Throws(cmdSet, "ValidateCommandBehavior", "ExecuteNonQuery", CommandBehavior.KeyInfo);
            VerifyException<ArgumentOutOfRangeException>(ex, "not supported");
        }

        #region private methods

        private object CallMethod(object instance, string methodName, params object[] values)
        {
            var commandSetType = mds.GetType("Microsoft.Data.SqlClient.SqlCommandSet");
            object returnValue = commandSetType.GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(instance, values);
            return returnValue;
        }

        private object CallMethod(object instance, string methodName)
        {
            var commandSetType = mds.GetType("Microsoft.Data.SqlClient.SqlCommandSet");
            object returnValue = commandSetType.GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(instance, new object[] { });
            return returnValue;
        }

        private Exception CallMethod_Throws(object instance, string methodName, params object[] values)
        {
            var commandSetType = mds.GetType("Microsoft.Data.SqlClient.SqlCommandSet");
            Exception ex = Assert.ThrowsAny<Exception>(() =>
            {
                commandSetType.GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(instance, values);
            });
            return ex;
        }

        private object CreateInstance()
        {
            var commandSetType = mds.GetType("Microsoft.Data.SqlClient.SqlCommandSet");
            object cmdSet = Activator.CreateInstance(commandSetType, true);
            return cmdSet;
        }

        private Exception GetProperty_Throws(object instance, string propertyName)
        {
            var commandSetType = mds.GetType("Microsoft.Data.SqlClient.SqlCommandSet");
            var cmdSet = instance;
            Exception ex = Assert.ThrowsAny<Exception>(() =>
            {
                commandSetType.GetProperty(propertyName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetGetMethod(true).Invoke(cmdSet, new object[] { });
            });

            return ex;
        }

        private SqlCommand GenerateBadCommand(CommandType cType)
        {
            SqlCommand cmd = new SqlCommand("Test");
            Type sqlCommandType = cmd.GetType();
            // There's validation done on the CommandType property, but we need to create one that avoids the check for the test case.
            sqlCommandType.GetField("_commandType", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cmd, cType);

            return cmd;
        }

        private void VerifyException<T>(Exception ex, string contains)
        {
            Assert.NotNull(ex);
            Assert.IsType<T>(ex.InnerException);
            Assert.Contains(contains, ex.InnerException.Message, StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
