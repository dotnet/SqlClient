// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlConfigurationManagerReliabilityTest
    {
        private readonly SqlConnectionReliabilityTest _connectionCRLTest;
        private static readonly SqlCommandReliabilityTest s_commandCRLTest = new SqlCommandReliabilityTest();

        private static readonly string s_upperCaseRetryMethodName_Fix = RetryLogicConfigHelper.RetryMethodName_Fix.ToUpper();
        private static readonly string s_upperCaseRetryMethodName_Inc = RetryLogicConfigHelper.RetryMethodName_Inc.ToUpper();
        private static readonly string s_upperCaseRetryMethodName_Exp = RetryLogicConfigHelper.RetryMethodName_Exp.ToUpper();

        private static string TcpCnnString => DataTestUtility.TCPConnectionString;
        private static string InvalidTcpCnnString => new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
        { InitialCatalog = SqlConnectionReliabilityTest.InvalidInitialCatalog, ConnectTimeout = 1 }.ConnectionString;

        public SqlConfigurationManagerReliabilityTest(ITestOutputHelper outputHelper)
        {
            _connectionCRLTest = new(outputHelper);
        }

        #region Internal Functions
        // Test relies on error 4060 for automatic retry, which is not returned when using AAD auth
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.TcpConnectionStringDoesNotUseAadAuth))]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Fix, RetryLogicConfigHelper.RetryMethodName_Inc)]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Inc, RetryLogicConfigHelper.RetryMethodName_Exp)]
        [InlineData(RetryLogicConfigHelper.RetryMethodName_Exp, RetryLogicConfigHelper.RetryMethodName_Fix)]
        public void LoadValidInternalTypes(string method1, string method2)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(method1);
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(method2,
                                           // Doesn't accept DML statements
                                           @"^\b(?!UPDATE|DELETE|TRUNCATE|INSERT( +INTO){0,1})\b");
            // for sake of reducing the retry time in total
            cnnCfg.NumberOfTries = 1;
            cmdCfg.NumberOfTries = 1;

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);
            RetryLogicConfigHelper.AssessProvider(cnnProvider, cnnCfg);
            RetryLogicConfigHelper.AssessProvider(cmdProvider, cmdCfg);

            // check the retry in action
            _connectionCRLTest.ConnectionRetryOpenInvalidCatalogFailed(TcpCnnString, cnnProvider);
            s_commandCRLTest.RetryExecuteFail(TcpCnnString, cmdProvider);
            if (DataTestUtility.IsNotAzureSynapse())
            {
                // @TODO: Why are we calling a test from another test?
                s_commandCRLTest.RetryExecuteUnauthorizedSqlStatementDml(TcpCnnString, cmdProvider);
            }
        }
        #endregion

        #region External Functions
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("Microsoft.Data.SqlClient.Tests.StaticCustomConfigurableRetryLogic, ExternalConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("Microsoft.Data.SqlClient.Tests.StructCustomConfigurableRetryLogic, ExternalConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("Microsoft.Data.SqlClient.Tests.StructCustomConfigurableRetryLogic, ExternalConfigurableRetryLogic", "GetDefaultRetry")]
        [InlineData("Microsoft.Data.SqlClient.Tests.CustomConfigurableRetryLogic, ExternalConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("Microsoft.Data.SqlClient.Tests.CustomConfigurableRetryLogic, ExternalConfigurableRetryLogic", "GetDefaultRetry")]
        [InlineData("Microsoft.Data.SqlClient.Tests.CustomConfigurableRetryLogic, ExternalConfigurableRetryLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "GetDefaultRetry")]
        [InlineData("Microsoft.Data.SqlClient.Tests.CustomConfigurableRetryLogicEx, ExternalConfigurableRetryLogic", "GetDefaultRetry")]
        public void LoadCustomMethod(string typeName, string methodName)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            // for sake of reducing the retry time in total
            cnnCfg.RetryLogicType = typeName;
            cmdCfg.RetryLogicType = typeName;
            // for sake of reducing the retry time in total
            cnnCfg.NumberOfTries = 1;
            cmdCfg.NumberOfTries = 3;

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);

            TestConnection(cnnProvider, cnnCfg);
            TestCommandExecute(cmdProvider, cmdCfg);
            TestCommandExecuteAsync(cmdProvider, cmdCfg).Wait();
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData("ClassLibrary.Invalid, ExternalConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("ClassLibrary.Invalid, ExternalConfigurableRetryLogic", "GetDefaultRetry")]
        [InlineData("Microsoft.Data.SqlClient.Tests.CustomConfigurableRetryLogic, ClassLibrary_Invalid", "GetDefaultRetry_static")]
        [InlineData("Microsoft.Data.SqlClient.Tests.StaticCustomConfigurableRetryLogic, ClassLibrary_Invalid", "GetDefaultRetry_static")]
        [InlineData("Microsoft.Data.SqlClient.Tests.StructCustomConfigurableRetryLogic, ClassLibrary_Invalid", "GetDefaultRetry")]
        // Type and method name are case sensitive.
        [InlineData("Microsoft.Data.SqlClient.Tests.StaticCustomConfigurableRetryLogic, ExternalConfigurableRetryLogic", "GETDEFAULTRETRY_STATIC")]
        [InlineData("Microsoft.Data.SqlClient.Tests.STRUCTCUSTOMCONFIGURABLERETRYLOGIC, ExternalConfigurableRetryLogic", "GetDefaultRetry")]
        [InlineData("MICROSOFT.DATA.SQLCLIENT.TESTS.CustomConfigurableRetryLogic, ExternalConfigurableRetryLogic", "GetDefaultRetry_static")]
        [InlineData("Microsoft.Data.SqlClient.Tests.CustomConfigurableRetryLogic, ExternalConfigurableRetryLogic", "getdefaultretry")]
        public void LoadInvalidCustomRetryLogicType(string typeName, string methodName)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(methodName);
            // for sake of reducing the retry time in total
            cnnCfg.RetryLogicType = typeName;
            cmdCfg.RetryLogicType = typeName;
            // for sake of reducing the retry time in total
            cnnCfg.NumberOfTries = 1;
            cmdCfg.NumberOfTries = 3;

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);

            _connectionCRLTest.DefaultOpenWithoutRetry(TcpCnnString, cnnProvider);
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

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);

            // none retriable logic applies.
            _connectionCRLTest.DefaultOpenWithoutRetry(TcpCnnString, cnnProvider);
            s_commandCRLTest.NoneRetriableExecuteFail(TcpCnnString, cmdProvider);
        }

        // Test relies on error 4060 for automatic retry, which is not returned when using AAD auth
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.TcpConnectionStringDoesNotUseAadAuth))]
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

            object loaderObj = RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider);
            Assert.NotNull(loaderObj);
            RetryLogicConfigHelper.AssessProvider(cnnProvider, cnnCfg);
            RetryLogicConfigHelper.AssessProvider(cmdProvider, cmdCfg);

            // internal type used to resolve the specified method
            _connectionCRLTest.ConnectionRetryOpenInvalidCatalogFailed(TcpCnnString, cnnProvider);
            s_commandCRLTest.RetryExecuteFail(TcpCnnString, cmdProvider);
        }

        [Theory]
        [MemberData(nameof(RetryLogicConfigHelper.GetIivalidTimes), MemberType = typeof(RetryLogicConfigHelper))]
        public void OutOfRangeTime(TimeSpan deltaTime, TimeSpan minTime, TimeSpan maxTime)
        {
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.DeltaTime = deltaTime;
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix, @"Don't care!");

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
            Assert.Equal(typeof(System.Configuration.ConfigurationErrorsException), ex.InnerException?.GetType());
            Assert.Equal(typeof(ArgumentException), ex.InnerException?.InnerException?.GetType());

            cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.MinTimeInterval = minTime;
            ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
            Assert.Equal(typeof(System.Configuration.ConfigurationErrorsException), ex.InnerException?.GetType());
            Assert.Equal(typeof(ArgumentException), ex.InnerException?.InnerException?.GetType());

            cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.MaxTimeInterval = maxTime;
            ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
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

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
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

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out SqlRetryLogicBaseProvider cmdProvider));
            Assert.Equal(typeof(System.Configuration.ConfigurationErrorsException), ex.InnerException?.GetType());
            Assert.Equal(typeof(ArgumentException), ex.InnerException?.InnerException?.GetType());
        }
        #endregion

        #region Valid Configurations
        [Theory]
        [InlineData("-1,1,2,3")]
        [InlineData("-1, 1, 2 , 3, -2")]
        [InlineData("")]
        public void ValidTransientError(string errors)
        {
            string[] transientErrorNumbers = errors.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            RetryLogicConfigs cnnCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix);
            cnnCfg.TransientErrors = errors;
            RetryLogicConfigs cmdCfg = RetryLogicConfigHelper.CreateRandomConfig(RetryLogicConfigHelper.RetryMethodName_Fix, @"Don't care!");

            RetryLogicConfigHelper.ReturnLoaderAndProviders(cnnCfg, cmdCfg, out SqlRetryLogicBaseProvider cnnProvider, out _);

            foreach(string errorString in transientErrorNumbers)
            {
                int errorNumber = int.Parse(errorString.Trim());
                SqlException transientException = RetryLogicConfigHelper.CreateSqlException(errorNumber);

                Assert.True(cnnProvider.RetryLogic.TransientPredicate(transientException), $"Error {errorNumber} is not considered transient by the predicate.");
            }
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
                if (DataTestUtility.IsNotAzureSynapse())
                {
                    cmd.CommandText = cmd.CommandText + " FOR XML AUTO";
                    ex = Assert.Throws<AggregateException>(() => cmd.ExecuteXmlReader());
                    Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                }
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
                if (DataTestUtility.IsNotAzureSynapse())
                {
                    cmd.CommandText = cmd.CommandText + " FOR XML AUTO";
                    ex = await Assert.ThrowsAsync<AggregateException>(() => cmd.ExecuteXmlReaderAsync());
                    Assert.Equal(cnfig.NumberOfTries, ex.InnerExceptions.Count);
                }
            }
        }
        #endregion
    }
}
