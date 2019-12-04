// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using Xunit;
using System.Collections;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlStatisticsTest
    {
        private static DateTime startTime = new DateTime();
        private static Guid clientConnectionId = Guid.Empty;

        #region TestMethods
        [CheckConnStrSetupFact]
        public static void TestRetrieveStatistics()
        {
            startTime = DateTime.Now;
            SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            string text = "SELECT TOP 2000 * from [dbo].[Order Details]";
            using (SqlCommand command = new SqlCommand(text))
            {
                connection.StatisticsEnabled = true;
                connection.Open();
                connection.StateChange += new StateChangeEventHandler(HandleStateChange);

                command.Connection = connection;
                using (SqlDataReader dr = command.ExecuteReader())
                {
                    IDictionary stats1 = connection.RetrieveStatistics();

                    // Ensure ConnectionTime is within a reasonable range
                    Assert.True((long)stats1["ConnectionTime"] < DateTime.Now.Subtract(startTime).TotalMilliseconds);
                    clientConnectionId = connection.ClientConnectionId;
                    Assert.True(clientConnectionId != Guid.Empty);

                    int row = 0;
                    while (dr.Read())
                    {
                        row++;
                    }
                }
            }
            // Ensure calling RetrieveStatistics multiple times do not affect the ConnectionTime
            connection.RetrieveStatistics();
            connection.RetrieveStatistics();
            connection.RetrieveStatistics();
            connection.RetrieveStatistics();
            connection.RetrieveStatistics();
            connection.RetrieveStatistics();
            connection.Close();
            IDictionary stats2 = connection.RetrieveStatistics();
            Assert.True((long)stats2["ConnectionTime"] < DateTime.Now.Subtract(startTime).TotalMilliseconds);
            // Ensure ClientConnectionId remains available even after the connection is closed
            Assert.True(connection.ClientConnectionId == clientConnectionId);
        }
        #endregion

        #region UtilityMethodsClasses
        private static void HandleStateChange(object sender, StateChangeEventArgs args)
        {
            if (args.CurrentState == ConnectionState.Closed)
            {
                System.Collections.IDictionary stats = ((SqlConnection)sender).RetrieveStatistics();
                Assert.True((long)stats["ConnectionTime"] < DateTime.Now.Subtract(startTime).TotalMilliseconds);
                Assert.True(((SqlConnection)sender).ClientConnectionId == clientConnectionId);
            }
        }
        #endregion
    }
}
