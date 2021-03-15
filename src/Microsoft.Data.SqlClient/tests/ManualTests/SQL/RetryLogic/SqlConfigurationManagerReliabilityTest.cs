// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;
using System.Data;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlConfigurationManagerReliabilityTest
    {
        private static readonly SqlConnectionReliabilityTest s_connectionCRLTest = new SqlConnectionReliabilityTest();
        private static readonly SqlCommandReliabilityTest s_commandCRLTest = new SqlCommandReliabilityTest();

        private static readonly string s_upperCaseRetryMethodName_Fix = RetryLogicConfigHelper.RetryMethodName_Fix.ToUpper();
        private static readonly string s_upperCaseRetryMethodName_Inc = RetryLogicConfigHelper.RetryMethodName_Inc.ToUpper();
        private static readonly string s_upperCaseRetryMethodName_Exp = RetryLogicConfigHelper.RetryMethodName_Exp.ToUpper();

        private static string TcpCnnString => DataTestUtility.TCPConnectionString;
        private static string InvalidTcpCnnString => new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
        { InitialCatalog = SqlConnectionReliabilityTest.InvalidInitialCatalog, ConnectTimeout = 1 }.ConnectionString;

        #region Internal Functions
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Fix, RetryLogicConfigHelper.RetryMethodName_Inc)]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Inc, RetryLogicConfigHelper.RetryMethodName_Exp)]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Exp, RetryLogicConfigHelper.RetryMethodName_Fix)]
        public void LoadValidInternalTypesAndEnableSwitch(string method1, string method2)
        {
            bool switchValue = true;

            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(method1);
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(method2,
                                           // Doesn't accept DML statements
                                           @"^\b(?!UPDATE|DELETE|TRUNCATE|INSERT( +INTO){0,1})\b");
            // for sake of reducing the retry time in total
            cnnCfg.NumberOfTries = 1;
            cmdCfg.NumberOfTries = 1;

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);
            RetryLogicConfigHelper.AssessProvider(cnnProvider, cnnCfg, switchValue);
            RetryLogicConfigHelper.AssessProvider(cmdProvider, cmdCfg, switchValue);

            // check the retry in action
            s_connectionCRLTest.ConnectionRetryOpenInvalidCatalogFailed(TcpCnnString, cnnProvider);
            s_commandCRLTest.RetryExecuteFail(TcpCnnString, cmdProvider);
            s_commandCRLTest.RetryExecuteUnauthorizedSqlStatementDML(TcpCnnString, cmdProvider);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Fix, RetryLogicConfigHelper.RetryMethodName_Inc)]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Inc, RetryLogicConfigHelper.RetryMethodName_Exp)]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Exp, RetryLogicConfigHelper.RetryMethodName_Fix)]
        public void LoadValidInternalTypesWithoutEnablingSwitch(string method1, string method2)
        {
            bool switchValue = false;
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(method1);
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(method2, @"Don't care!");

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);
            RetryLogicConfigHelper.AssessProvider(cnnProvider, cnnCfg, switchValue);
            RetryLogicConfigHelper.AssessProvider(cmdProvider, cmdCfg, switchValue);

            s_connectionCRLTest.DefaultOpenWithoutRetry(TcpCnnString, cnnProvider);
            s_commandCRLTest.NoneRetriableExecuteFail(TcpCnnString, cmdProvider);
        }
        #endregion

        #region External Functions
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("ClassLibrary.StaticCustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("ClassLibrary.StructCustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("ClassLibrary.StructCustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry")]
        [InlineData("ClassLibrary.CustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("ClassLibrary.CustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry")]
        [InlineData("ClassLibrary.CustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "GetDefaultRetry")]
        [InlineData("ClassLibrary.CustomConfigurableRetryLogicEx, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry")]
        public void LoadCustomMethod(string typeName, string methodName)
        {
            bool switchValue = true;

            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            // for sake of reducing the retry time in total
            cnnCfg.RetryLogicType = typeName;
            cmdCfg.RetryLogicType = typeName;
            // for sake of reducing the retry time in total
            cnnCfg.NumberOfTries = 1;
            cmdCfg.NumberOfTries = 3;

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);

            TestConnection(cnnProvider, cnnCfg);
            TestCommandExecute(cmdProvider, cmdCfg);
            TestCommandExecuteAsync(cmdProvider, cmdCfg).Wait();
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("ClassLibrary.Invalid, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("ClassLibrary.Invalid, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry")]
        [InlineData("ClassLibrary.CustomConfigurableRetryLogic, ClassLibrary_Invalid", "GetDefaultRetry_static")]
        [InlineData("ClassLibrary.StaticCustomConfigurableRetryLogic, ClassLibrary_Invalid", "GetDefaultRetry_static")]
        [InlineData("ClassLibrary.StructCustomConfigurableRetryLogic, ClassLibrary_Invalid", "GetDefaultRetry")]
        // Type and method name are case sensitive.
        [InlineData("ClassLibrary.StaticCustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic", "GETDEFAULTRETRY_STATIC")]
        [InlineData("ClassLibrary.STRUCTCUSTOMCONFIGURABLERETRYLOGIC, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry")]
        [InlineData("CLASSLIBRARY.CustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("ClassLibrary.CustomConfigurableRetryLogic, ClassLibrary_CustomConfigurableRetryLogic", "getdefaultretry")]
        public void LoadInvalidCustomRetryLogicType(string typeName, string methodName)
        {
            bool switchValue = true;

            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            // for sake of reducing the retry time in total
            cnnCfg.RetryLogicType = typeName;
            cmdCfg.RetryLogicType = typeName;
            // for sake of reducing the retry time in total
            cnnCfg.NumberOfTries = 1;
            cmdCfg.NumberOfTries = 3;

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);

            s_connectionCRLTest.DefaultOpenWithoutRetry(TcpCnnString, cnnProvider);
            s_commandCRLTest.NoneRetriableExecuteFail(TcpCnnString, cmdProvider);
        }
        #endregion

        #region Invalid Configurations
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("InvalidMethodName")]
        [InlineData("Fix")]
        [InlineData(null)]
        [MemberData(nameof(RetryLogicConfigHelper.GetInvalidInternalMethodNames), MemberType = typeof(RetryLogicConfigHelper))]
        public void InvalidRetryMethodName(string methodName)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName, @"Don't care!");

            bool switchValue = true;
            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);

            // none retriable logic applies.
            s_connectionCRLTest.DefaultOpenWithoutRetry(TcpCnnString, cnnProvider);
            s_commandCRLTest.NoneRetriableExecuteFail(TcpCnnString, cmdProvider);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("InvalidRetrylogicTypeName")]
        [InlineData("")]
        [InlineData(null)]
        // Specifying the internal type has the same effect
        [InlineData("Microsoft.Data.SqlClient.SqlConfigurableRetryFactory")]
        public void InvalidRetryLogicTypeWithValidInternalMethodName(string typeName)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.RetryLogicType = typeName;
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            // for sake of reducing the retry time in total
            cnnCfg.NumberOfTries = 1;
            cmdCfg.NumberOfTries = 1;

            bool switchValue = true;
            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);
            RetryLogicConfigHelper.AssessProvider(cnnProvider, cnnCfg, switchValue);
            RetryLogicConfigHelper.AssessProvider(cmdProvider, cmdCfg, switchValue);

            // internal type used to resolve the specified method
            s_connectionCRLTest.ConnectionRetryOpenInvalidCatalogFailed(TcpCnnString, cnnProvider);
            s_commandCRLTest.RetryExecuteFail(TcpCnnString, cmdProvider);
        }

        [Theory]
        [MemberData(nameof(RetryLogicConfigHelper.GetIivalidTimes), MemberType = typeof(RetryLogicConfigHelper))]
        public void OutOfRangeTime(TimeSpan deltaTime, TimeSpan minTime, TimeSpan maxTime)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.DeltaTime = deltaTime;
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix, @"Don't care!");

            bool switchValue = true;
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
            Assert.Equal(typeof(System.Configuration.ConfigurationErrorsException), ex.InnerException?.GetType());
            Assert.Equal(typeof(ArgumentException), ex.InnerException?.InnerException?.GetType());

            cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.MinTimeInterval = minTime;
            ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
            Assert.Equal(typeof(System.Configuration.ConfigurationErrorsException), ex.InnerException?.GetType());
            Assert.Equal(typeof(ArgumentException), ex.InnerException?.InnerException?.GetType());

            cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.MaxTimeInterval = maxTime;
            ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
            Assert.Equal(typeof(System.Configuration.ConfigurationErrorsException), ex.InnerException?.GetType());
            Assert.Equal(typeof(ArgumentException), ex.InnerException?.InnerException?.GetType());
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(61)]
        [InlineData(100)]
        public void InvalidNumberOfTries(int num)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.NumberOfTries = num;
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix, @"Don't care!");

            bool switchValue = true;
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
            Assert.Equal(typeof(System.Configuration.ConfigurationErrorsException), ex.InnerException?.GetType());
            Assert.Equal(typeof(ArgumentException), ex.InnerException?.InnerException?.GetType());
        }

        [Theory]
        [InlineData("invalid value")]
        [InlineData("1; 2; 3")]
        [InlineData("1/2/3")]
        [InlineData(",1,2,3")]
        [InlineData("1,2,3,")]
        [InlineData("1|2|3")]
        [InlineData("1+2+3")]
        [InlineData("1*2*3")]
        [InlineData(@"1\2\3")]
        [InlineData("1&2&3")]
        [InlineData("1.2.3")]
        [InlineData(@"~!@#$%^&*()_+={}[]|\""':;.><?/")]
        public void InvalidTransientError(string errors)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.TransientErrors = errors;
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix, @"Don't care!");

            bool switchValue = true;
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, switchValue, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
            Assert.Equal(typeof(System.Configuration.ConfigurationErrorsException), ex.InnerException?.GetType());
            Assert.Equal(typeof(ArgumentException), ex.InnerException?.InnerException?.GetType());
        }
#endregion

        #region AppContextSwitchManager
        [Theory]
        [InlineData("Switch.Microsoft.Data.SqlClient.EnableRetryLogic", true)]
        [InlineData("Switch.Microsoft.Data.SqlClient.EnableRetryLogic", false)]
        public void ContextSwitchMangerTest(string name, bool value)
        {
            RetryLogicConfigHelper.ApplyContextSwitchByManager(name, value);
            AppContext.TryGetSwitch(name, out bool result);
            Assert.Equal(value, result);
        }
        #endregion

        #region private methods
        private void TestConnection(SqlRetryLogicBaseProvider provider, RetryLogicConfigs cnfig)
        {
            using (SqlConnection cnn = new SqlConnection(InvalidTcpCnnString))
            {
                cnn.RetryLogicProvider = provider;
                var ex = Assert.Throws<AggregateException>(() => cnn.Open());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                var tex = Assert.ThrowsAsync<AggregateException>(() => cnn.OpenAsync());
                Assert.Equal(cnfig.NumberOfTries, tex.Result.InnerExceptions.Count);
            }
        }

        private void TestCommandExecute(SqlRetryLogicBaseProvider provider, RetryLogicConfigs cnfig)
        {
            using (SqlConnection cnn = new SqlConnection(TcpCnnString))
            using (SqlCommand cmd = new SqlCommand())
            {
                cnn.Open();
                cmd.Connection = cnn;
                cmd.RetryLogicProvider = provider;
                cmd.CommandText = "SELECT bad command";
                var ex = Assert.Throws<AggregateException>(() => cmd.ExecuteScalar());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteReader());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteReader(CommandBehavior.Default));
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                cmd.CommandText = cmd.CommandText + " FOR XML AUTO";
                ex = Assert.Throws<AggregateException>(() => cmd.ExecuteXmlReader());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
            }
        }

        private async Task TestCommandExecuteAsync(SqlRetryLogicBaseProvider provider, RetryLogicConfigs cnfig)
        {
            using (SqlConnection cnn = new SqlConnection(TcpCnnString))
            using (SqlCommand cmd = new SqlCommand())
            {
                cnn.Open();
                cmd.Connection = cnn;
                cmd.RetryLogicProvider = provider;
                cmd.CommandText = "SELECT bad command";
                var ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteScalarAsync());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteReaderAsync(CommandBehavior.Default));
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                cmd.CommandText = cmd.CommandText + " FOR XML AUTO";
                ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteXmlReaderAsync());
                Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
            }
        }
        #endregion
    }
}
