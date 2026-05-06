// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.FunctionalTests.DataCommon
{
    /// <summary>
    /// Dummy class to override default Sql Authentication provider in functional tests.
    /// This type returns a dummy access token and is only used for registration test from app.config file.
    /// Since no actual connections are intended to be made in Functional tests, 
    /// this type is added by default to validate config file registration scenario.
    /// </summary>
    public class DummySqlAuthenticationProvider : SqlAuthenticationProvider
    {
        public static string DUMMY_TOKEN_STR = "dummy_access_token";

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        => Task.FromResult(new SqlAuthenticationToken(DUMMY_TOKEN_STR, new DateTimeOffset(DateTime.Now.AddHours(2))));

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
        {
            return authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive;
        }
    }
}
