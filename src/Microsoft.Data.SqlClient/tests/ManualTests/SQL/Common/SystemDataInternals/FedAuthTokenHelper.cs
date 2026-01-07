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
            try
            {
                object authenticationContextValueObj = GetAuthenticationContextValue(connection);

                DateTime expirationTimeProperty = (DateTime)authenticationContextValueObj.GetType().GetProperty("ExpirationTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(authenticationContextValueObj, null);

                tokenHash = GetTokenHash(authenticationContextValueObj);

                return expirationTimeProperty;
            }
            catch (Exception)
            {
                tokenHash = "";
                return null;
            }
        }

        internal static DateTime? SetTokenExpiryDateTime(SqlConnection connection, int minutesToExpire, out string tokenHash)
        {
            try
            {
                object authenticationContextValueObj = GetAuthenticationContextValue(connection);

                DateTime expirationTimeProperty = (DateTime)authenticationContextValueObj.GetType().GetProperty("ExpirationTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(authenticationContextValueObj, null);

                expirationTimeProperty = DateTime.UtcNow.AddMinutes(minutesToExpire);

                FieldInfo expirationTimeInfo = authenticationContextValueObj.GetType().GetField("_expirationTime", BindingFlags.NonPublic | BindingFlags.Instance);
                expirationTimeInfo.SetValue(authenticationContextValueObj, expirationTimeProperty);

                tokenHash = GetTokenHash(authenticationContextValueObj);

                return expirationTimeProperty;
            }
            catch (Exception)
            {
                tokenHash = "";
                return null;
            }
        }

        internal static string GetTokenHash(object authenticationContextValueObj)
        {
            try
            {
                Assembly sqlConnectionAssembly = Assembly.GetAssembly(typeof(SqlConnection));

                Type sqlFedAuthTokenType = sqlConnectionAssembly.GetType("Microsoft.Data.SqlClient.SqlFedAuthToken");

                Type[] sqlFedAuthTokenTypeArray = new Type[] { sqlFedAuthTokenType };

                ConstructorInfo sqlFedAuthTokenConstructorInfo = sqlFedAuthTokenType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);

                Type activeDirectoryAuthenticationTimeoutRetryHelperType = sqlConnectionAssembly.GetType("Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationTimeoutRetryHelper");

                ConstructorInfo activeDirectoryAuthenticationTimeoutRetryHelperConstructorInfo = activeDirectoryAuthenticationTimeoutRetryHelperType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Type.EmptyTypes, null);

                object activeDirectoryAuthenticationTimeoutRetryHelperObj = activeDirectoryAuthenticationTimeoutRetryHelperConstructorInfo.Invoke(new object[] { });

                MethodInfo tokenHashInfo = activeDirectoryAuthenticationTimeoutRetryHelperObj.GetType().GetMethod("GetTokenHash", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, sqlFedAuthTokenTypeArray, null);

                byte[] tokenBytes = (byte[])authenticationContextValueObj.GetType().GetProperty("AccessToken", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(authenticationContextValueObj, null);

                object sqlFedAuthTokenObj = sqlFedAuthTokenConstructorInfo.Invoke(new object[] { });
                FieldInfo accessTokenInfo = sqlFedAuthTokenObj.GetType().GetField("accessToken", BindingFlags.NonPublic | BindingFlags.Instance);
                accessTokenInfo.SetValue(sqlFedAuthTokenObj, tokenBytes);

                string tokenHash = (string)tokenHashInfo.Invoke(activeDirectoryAuthenticationTimeoutRetryHelperObj, new object[] { sqlFedAuthTokenObj });

                return tokenHash;
            }
            catch (Exception)
            {
                return "";
            }
        }

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
