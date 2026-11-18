// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.Common.SystemDataInternals
{
    internal static class FedAuthTokenHelper
    {
        internal static DateTime? GetTokenExpiryDateTime(SqlConnection connection, out string tokenHash)
        {
            object authenticationContextValueObj = GetAuthenticationContextValue(connection);

            DateTime expirationTimeProperty = (DateTime)GetRequiredProperty(authenticationContextValueObj.GetType(), "ExpirationTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(authenticationContextValueObj, null);

            tokenHash = GetTokenHash(authenticationContextValueObj);

            return expirationTimeProperty;
        }

        internal static DateTime? SetTokenExpiryDateTime(SqlConnection connection, int minutesToExpire, out string tokenHash)
        {
            object authenticationContextValueObj = GetAuthenticationContextValue(connection);

            DateTime expirationTimeProperty = DateTime.UtcNow.AddMinutes(minutesToExpire);

            FieldInfo expirationTimeInfo = authenticationContextValueObj.GetType().GetField("_expirationTime", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(authenticationContextValueObj.GetType().FullName, "_expirationTime");
            expirationTimeInfo.SetValue(authenticationContextValueObj, expirationTimeProperty);

            tokenHash = GetTokenHash(authenticationContextValueObj);

            return expirationTimeProperty;
        }

        internal static string GetTokenHash(object authenticationContextValueObj)
        {
            Assembly sqlConnectionAssembly = typeof(SqlConnection).Assembly;

            Type sqlFedAuthTokenType = GetRequiredType(sqlConnectionAssembly, "Microsoft.Data.SqlClient.Parser.Login.SqlFedAuthToken");
            Type retryHelperType = GetRequiredType(sqlConnectionAssembly, "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationTimeoutRetryHelper");

            ConstructorInfo sqlFedAuthTokenConstructorInfo = sqlFedAuthTokenType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(byte[]), typeof(long) }, null)
                ?? throw new MissingMethodException(sqlFedAuthTokenType.FullName, ".ctor(byte[], long)");

            MethodInfo tokenHashInfo = retryHelperType.GetMethod("GetTokenHash", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { sqlFedAuthTokenType }, null)
                ?? throw new MissingMethodException(retryHelperType.FullName, "GetTokenHash");

            byte[] tokenBytes = (byte[])GetRequiredProperty(authenticationContextValueObj.GetType(), "AccessToken", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(authenticationContextValueObj, null);

            object sqlFedAuthTokenObj = sqlFedAuthTokenConstructorInfo.Invoke(new object[] { tokenBytes, 0L });

            return (string)tokenHashInfo.Invoke(null, new object[] { sqlFedAuthTokenObj });
        }

        private static Type GetRequiredType(Assembly assembly, string typeName) =>
            assembly.GetType(typeName) ?? throw new TypeLoadException($"Type '{typeName}' was not found in '{assembly.FullName}'.");

        private static PropertyInfo GetRequiredProperty(Type type, string propertyName, BindingFlags bindingFlags) =>
            type.GetProperty(propertyName, bindingFlags) ?? throw new MissingMemberException(type.FullName, propertyName);

        internal static object GetAuthenticationContextValue(SqlConnection connection)
        {
            object innerConnectionObj = connection.GetType().GetProperty("InnerConnection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(connection);

            object databaseConnectionPoolObj = innerConnectionObj.GetType().GetProperty("Pool", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(innerConnectionObj);

            IEnumerable authenticationContexts = (IEnumerable)databaseConnectionPoolObj.GetType().GetProperty("AuthenticationContexts", BindingFlags.Public | BindingFlags.Instance).GetValue(databaseConnectionPoolObj, null);

            object authenticationContextObj = authenticationContexts.Cast<object>().FirstOrDefault();

            object authenticationContextValueObj = authenticationContextObj.GetType().GetProperty("Value").GetValue(authenticationContextObj, null);

            return authenticationContextValueObj;
        }
    }
}
