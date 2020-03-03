// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class ConnectionTest
    {        
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnectionStringPasswordIncluded))]
        public static void ConnectionStringPersistantInfoTest()
        {
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            connectionStringBuilder.PersistSecurityInfo = false;
            string cnnString = connectionStringBuilder.ConnectionString;

            connectionStringBuilder.Clear();
            using (SqlConnection sqlCnn = new SqlConnection(cnnString))
            {
                sqlCnn.Open();
                connectionStringBuilder.ConnectionString = sqlCnn.ConnectionString;
                Assert.True(connectionStringBuilder.Password == string.Empty, "Password must not persist according to set the PersistSecurityInfo by false!");
            }
        }
    }
}
