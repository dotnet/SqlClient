// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

﻿using System;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlClient.ConnectionPool;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.Common.SystemDataInternals
{
    internal static class FedAuthTokenHelper
    {
        internal static DateTime? GetTokenExpiryDateTime(SqlConnection connection, out string tokenHash)
        {
            try
            {
                var authenticationContextValueObj = connection.InnerConnection.Pool.AuthenticationContexts.FirstOrDefault().Value;
                tokenHash = GetTokenHash(authenticationContextValueObj);

                return authenticationContextValueObj.ExpirationTime;
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
                var authenticationContextValueObj = connection.InnerConnection.Pool.AuthenticationContexts.FirstOrDefault().Value;
                tokenHash = GetTokenHash(authenticationContextValueObj);

                FieldInfo expirationTimeInfo = authenticationContextValueObj.GetType().GetField("_expirationTime", BindingFlags.NonPublic | BindingFlags.Instance);
                DateTime expirationTimeProperty = DateTime.UtcNow.AddMinutes(minutesToExpire);
                expirationTimeInfo.SetValue(authenticationContextValueObj, expirationTimeProperty);

                return expirationTimeProperty;
            }
            catch (Exception)
            {
                tokenHash = "";
                return null;
            }
        }

        internal static string GetTokenHash(DbConnectionPoolAuthenticationContext authenticationContextValueObj)
        {
            try
            {
                var sqlFedAuthTokenObj = new SqlFedAuthToken();
                sqlFedAuthTokenObj.accessToken = (byte[])authenticationContextValueObj.GetType().GetProperty("AccessToken", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(authenticationContextValueObj, null);;
                
                var activeDirectoryAuthenticationTimeoutRetryHelperObj = new ActiveDirectoryAuthenticationTimeoutRetryHelper();
                Type[] sqlFedAuthTokenTypeArray = new Type[] { typeof(SqlFedAuthToken) };
                MethodInfo tokenHashInfo = activeDirectoryAuthenticationTimeoutRetryHelperObj.GetType().GetMethod("GetTokenHash", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, sqlFedAuthTokenTypeArray, null);

                return (string)tokenHashInfo.Invoke(activeDirectoryAuthenticationTimeoutRetryHelperObj, new object[] { sqlFedAuthTokenObj });
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
