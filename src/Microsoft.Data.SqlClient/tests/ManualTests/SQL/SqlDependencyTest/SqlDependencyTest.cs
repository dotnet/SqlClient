// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlDependencyTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void SqlDependencyStartStopTest()
        {
            try
            {
                SqlDependency.Start(DataTestUtility.TCPConnectionString);
                SqlDependency.Stop(DataTestUtility.TCPConnectionString);
            }
            catch (Exception e)
            {
                Assert.True(false, e.Message);
            }
        }
    }
}
