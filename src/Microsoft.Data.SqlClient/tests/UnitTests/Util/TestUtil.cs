﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Util
{
    internal class TestUtil
    {
        /// <summary>
        /// Unless the NPGSQL_TEST_DB environment variable is defined, this is used as the connection string for the
        /// test database.
        /// </summary>
        public const string DefaultConnectionString =
            "Host=localhost;Username=sql_tests;Password=sql_tests;Database=sql_tests;";

        /// <summary>
        /// The connection string that will be used when opening the connection to the tests database.
        /// May be overridden in fixtures, e.g. to set special connection parameters
        /// </summary>
        public static string ConnectionString { get; }
            = Environment.GetEnvironmentVariable("SQL_TEST_DB") ?? DefaultConnectionString;
    }
}
