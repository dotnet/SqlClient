﻿using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class AADFedAuthTokenRefreshTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAADPasswordConnStrSetup))]
        public void FedAuthTokenRefreshTest()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.AADPasswordConnectionString);
            string dataSourceStr = builder.DataSource;
            // Clean up tcp info as it will always be added later on and we don't want to duplicate if it is there already.
            dataSourceStr = dataSourceStr.Replace("tcp:", "");
            dataSourceStr = dataSourceStr.Replace(",1433", "");
            dataSourceStr = dataSourceStr.Replace(", 1433", "");

            // set user id and password from AADPasswordConnectionString
            string user = builder.UserID;
            string password = builder.Password;

            // Set Environment variables used for ActiveDirectoryDefault authentication type
            Environment.SetEnvironmentVariable("AZURE_USERNAME", $"{user}");
            Environment.SetEnvironmentVariable("AZURE_PASSWORD", $"{password}");

            string userEnvVar = Environment.GetEnvironmentVariable("AZURE_USERNAME");
            string passwordEnvVar = Environment.GetEnvironmentVariable("AZURE_PASSWORD");
            Assert.True($"{user}" == userEnvVar, @"AZURE_USERNAME environment variable must be set");
            Assert.True($"{password}" == passwordEnvVar, @"AZURE_PASSWORD environment variable must be set");

            // This is the format of connection string that works
            string connStr = $"Server=tcp:{dataSourceStr},1433;Persist Security Info=False;User ID={user};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Authentication=ActiveDirectoryDefault;Timeout=90";

            using var connection = new SqlConnection(connStr);
            connection.Open();

            // Set the token expiry to expire in 1 minute from now to force token refresh
            string tokenHash1 = "";
            DateTime? oldExpiry = GetOrSetTokenExpiryDateTime(connection, true, out tokenHash1);
            Assert.True(oldExpiry != null, "Failed to make token expiry to expire in one minute.");

            // Display old expiry in local time which should be in 1 minutes from now
            DateTime oldLocalTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)oldExpiry, TimeZoneInfo.Local);
            Console.WriteLine($"Token: {tokenHash1}   Old Expiry: {oldLocalTime}");
            TimeSpan diff = oldLocalTime - DateTime.Now;
            Assert.True(diff.TotalSeconds <= 60, "Failed to set expiry after 1 minute from current time.");

            // Check if connection is alive
            string result = "";
            var cmd = connection.CreateCommand();
            cmd.CommandText = "select @@version";
            result = $"{cmd.ExecuteScalar()}";
            Assert.True(result != string.Empty, "The connection's command must return a value");

            // The new connection will use the same FedAuthToken but will refresh first
            using var connection2 = new SqlConnection(connStr);
            connection2.Open();

            // Check again if connection is alive
            cmd = connection2.CreateCommand();
            cmd.CommandText = "select 1";
            result = $"{cmd.ExecuteScalar()}";
            Assert.True(result != string.Empty, "The connection's command must return a value after a token refresh.");

            // Get the refreshed token expiry
            string tokenHash2 = "";
            DateTime? newExpiry = GetOrSetTokenExpiryDateTime(connection2, false, out tokenHash2);
            // Display new expiry in local time
            DateTime newLocalTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)newExpiry, TimeZoneInfo.Local);
            Console.WriteLine($"Token: {tokenHash2}   New Expiry: {newLocalTime}");

            Assert.True(tokenHash1 == tokenHash2, "The FedAuthToken failed to refresh correctly.");
            Assert.True(newLocalTime > oldLocalTime, "The FedAuthToken failed to refresh correctly.");
            
            connection.Close();
            connection2.Close();
        }

        private DateTime? GetOrSetTokenExpiryDateTime(SqlConnection connection, bool setExpiry, out string tokenHash)
        {
            try
            {
                // Get the inner connection
                object innerConnectionObj = connection.GetType().GetProperty("InnerConnection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(connection);

                // Get the db connection pool
                object poolObj = innerConnectionObj.GetType().GetProperty("Pool", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(innerConnectionObj);

                // Get the Authentication Contexts
                IEnumerable authContextCollection = (IEnumerable)poolObj.GetType().GetProperty("AuthenticationContexts", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(poolObj, null);

                // Get the first authentication context
                object authContextObj = authContextCollection.Cast<object>().FirstOrDefault();

                // Get the token object from the authentication context
                object tokenObj = authContextObj.GetType().GetProperty("Value").GetValue(authContextObj, null);

                DateTime expiry = (DateTime)tokenObj.GetType().GetProperty("ExpirationTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tokenObj, null);

                if (setExpiry)
                {
                    // Token refresh trigger is within 10 minutes. So, 1 minute expiry should trigger token refresh.
                    expiry = DateTime.UtcNow.AddMinutes(1);

                    // Apply the expiry to the token object
                    FieldInfo expirationTime = tokenObj.GetType().GetField("_expirationTime", BindingFlags.NonPublic | BindingFlags.Instance);
                    expirationTime.SetValue(tokenObj, expiry);

                }

                byte[] tokenBytes = (byte[])tokenObj.GetType().GetProperty("AccessToken", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tokenObj, null);

                tokenHash = GetTokenHash(tokenBytes);

                return expiry;
            }
            catch (Exception)
            {
                tokenHash = "";
                return null;
            }
        }

        private string GetTokenHash(byte[] tokenBytes)
        {
            string token = Encoding.Unicode.GetString(tokenBytes);
            var bytesInUtf8 = Encoding.UTF8.GetBytes(token);
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytesInUtf8);
                return Convert.ToBase64String(hash);
            }
        }
    }
}
