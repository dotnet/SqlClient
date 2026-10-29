// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Microsoft.Data.SqlClient.Tests.Common.ConfigurableRetryLogic;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class RetryLogicConfigHelper
    {
        public const string RetryMethodName_Fix = "CreateFixedRetryProvider";
        public const string RetryMethodName_Inc = "CreateIncrementalRetryProvider";
        public const string RetryMethodName_Exp = "CreateExponentialRetryProvider";
        public const string RetryMethodName_None = "CreateNoneRetryProvider";

        private const string SqlRetryLogicTypeName = "Microsoft.Data.SqlClient.SqlRetryLogic";

        private const string CreateExceptionMethodName = "CreateException";
        private const string AddMethodName = "Add";

        private static readonly Random s_random = new Random();

        private static readonly Assembly s_sqlClientAssembly = typeof(SqlConnection).Assembly;
        private static readonly Type s_sqlretrylogicType = s_sqlClientAssembly.GetType(SqlRetryLogicTypeName);
        private static readonly Type s_sqlErrorType = typeof(SqlError);
        private static readonly Type s_sqlErrorCollectionType = typeof(SqlErrorCollection);
        private static readonly Type[] s_sqlErrorParamsType = new Type[]
        {
            typeof(int), typeof(byte), typeof(byte),
            typeof(string), typeof(string), typeof(string),
            typeof(int), typeof(Exception)
        };
        private static readonly ConstructorInfo s_sqlErrorCtorInfo = s_sqlErrorType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, s_sqlErrorParamsType, null);
        private static readonly ConstructorInfo s_sqlErrorCollectionCtorInfo = s_sqlErrorCollectionType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        public static void AssessProvider(SqlRetryLogicBaseProvider provider, RetryLogicConfigs option) 
            => AssessRetryLogic(provider.RetryLogic, option);

        public static void AssessRetryLogic(SqlRetryLogicBase retryLogic, RetryLogicConfigs option)
        {
            Assert.Equal(option.DeltaTime, retryLogic.RetryIntervalEnumerator.GapTimeInterval);
            Assert.Equal(option.MinTimeInterval, retryLogic.RetryIntervalEnumerator.MinTimeInterval);
            Assert.Equal(option.MaxTimeInterval, retryLogic.RetryIntervalEnumerator.MaxTimeInterval);
            Assert.Equal(option.NumberOfTries, retryLogic.NumberOfTries);
            var preCondition = GetValue<Predicate<string>>(retryLogic, s_sqlretrylogicType, "PreCondition");
            if (string.IsNullOrEmpty(option.AuthorizedSqlCondition))
            {
                Assert.Null(preCondition);
            }
            else
            {
                Assert.NotNull(preCondition);
            }
        }

        public static T GetValue<T>(object obj, Type type, string propName, BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            => (T)type.GetProperty(propName, flags)?.GetValue(obj);

        public static void SetValue<T>(object obj, Type type, string propName, T value, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
            => type.GetProperty(propName, flags)?.SetValue(obj, value);

        public static IEnumerable<object[]> GetInvalidInternalMethodNames()
        {
            yield return new object[] { RetryMethodName_Fix.ToUpper() };
            yield return new object[] { RetryMethodName_Inc.ToUpper() };
            yield return new object[] { RetryMethodName_Exp.ToUpper() };
            yield return new object[] { RetryMethodName_Fix.ToLower() };
            yield return new object[] { RetryMethodName_Inc.ToLower() };
            yield return new object[] { RetryMethodName_Exp.ToLower() };
        }

        public static IEnumerable<object[]> GetIivalidTimes()
        {
            var start = TimeSpan.FromSeconds(121);
            var end = TimeSpan.FromHours(24);
            for (int i = 0; i < 10; i++)
            {
                yield return new object[] { GenerateTimeSpan(start, end), GenerateTimeSpan(start, end), GenerateTimeSpan(start, end) };
            }
        }

        public static SqlException CreateSqlException(int errorNumber)
        {
            MethodInfo addSqlErrorMethod = typeof(SqlErrorCollection).GetMethod(AddMethodName, BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo createExceptionMethod = typeof(SqlException).GetMethod(CreateExceptionMethodName, BindingFlags.Static | BindingFlags.NonPublic,
                null, new Type[] { typeof(SqlErrorCollection), typeof(string) }, null);

            SqlError sqlError = s_sqlErrorCtorInfo.Invoke(new object[] { errorNumber, (byte)0, (byte)0, string.Empty, string.Empty, string.Empty, 0, null }) as SqlError;
            SqlErrorCollection sqlErrorCollection = s_sqlErrorCollectionCtorInfo.Invoke(new object[0] { }) as SqlErrorCollection;

            addSqlErrorMethod.Invoke(sqlErrorCollection, new object[] { sqlError });

            SqlException sqlException = createExceptionMethod.Invoke(null, new object[] { sqlErrorCollection, string.Empty }) as SqlException;

            return sqlException;
        }

        private static TimeSpan GenerateTimeSpan(TimeSpan start, TimeSpan end)
        {
            int max = (int)(end - start).TotalSeconds;
            return start.Add(TimeSpan.FromSeconds(s_random.Next(max)));
        }
    }
}
