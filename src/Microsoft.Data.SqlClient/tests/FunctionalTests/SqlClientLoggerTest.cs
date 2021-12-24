// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlClientLoggerTest
    {
        [Fact]
        public void LogWarning()
        {
            // There is not much to test here but to add the code coverage.
            SqlClientLogger logger = new();
            logger.LogWarning("test type", "test method", "test message");
        }

        [Fact]
        public void LogAssert()
        {
            SqlClientLogger logger = new();
            logger.LogAssert(true, "test type", "test method", "test message");
        }

        [Fact]
        public void LogError()
        {
            SqlClientLogger logger = new();
            logger.LogError("test type", "test method", "test message");
        }

        [Fact]
        public void LogInfo()
        {
            SqlClientLogger logger = new();
            logger.LogInfo("test type", "test method", "test message");
        }
    }
}
