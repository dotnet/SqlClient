// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlCommandFireInfoMessageEventOnUserErrorsTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void FireInfoMessageEventOnUserErrorsShouldSucceed()
        {
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                string command = "print";
                string commandParam = "OK";

                connection.FireInfoMessageEventOnUserErrors = true;

                connection.InfoMessage += (sender, args) =>
                {
                    Assert.Equal(commandParam, args.Message);
                };

                connection.Open();

                using SqlCommand cmd = connection.CreateCommand();
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.CommandText = $"{command} '{commandParam}'";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
