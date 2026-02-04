// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for output parameters.
    /// </summary>
    public class OutputParameterTests
    {
        /// <summary>
        /// Tests that setting an output SqlParameter to an invalid value (e.g. a string in a decimal param)
        /// doesn't throw, since the value is cleared before execution starts.
        /// The output value should be correctly set by SQL Server after execution.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void InvalidValueInOutputParameter_ShouldSucceed()
        {
            using var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            // Command simply sets the output param
            using var command = new SqlCommand("SET @decimal = 1.23", connection);

            // Create valid param
            var decimalParam = new SqlParameter("decimal", new decimal(2.34))
            {
                SqlDbType = SqlDbType.Decimal,
                Direction = ParameterDirection.Output,
                Scale = 2,
                Precision = 5
            };
            command.Parameters.Add(decimalParam);

            // Set value of param to invalid value (string instead of decimal)
            decimalParam.Value = "Not a decimal";

            // Execute - should not throw
            command.ExecuteNonQuery();

            // Validate - the output value should be set correctly by SQL Server
            Assert.Equal(new decimal(1.23), (decimal)decimalParam.Value);
        }
    }
}
