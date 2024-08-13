// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Util
{
    internal class TestUtil
    {
        /// <summary>
        /// Unless the SQL_TEST_DB environment variable is defined, this is used as the connection string for the
        /// test database.
        /// </summary>
        public const string DefaultConnectionString =
            "Server=localhost;User Id=sql_tests;Password=sql_tests;Database=sql_tests;Connection Timeout=0;PoolBlockingPeriod=NeverBlock";

        /// <summary>
        /// The connection string that will be used when opening the connection to the tests database.
        /// May be overridden in fixtures, e.g. to set special connection parameters
        /// </summary>
        public static string ConnectionString { get; }
            = Environment.GetEnvironmentVariable("SQL_TEST_DB") ?? DefaultConnectionString;
    }
}
