// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for output parameters.
    /// </summary>
    public class OutputParameterTests
    {
        /// <summary>
        /// Test data indicating which collations do not encode the character é to 0xE9.
        /// </summary>
        public static TheoryData<string, int> OutputParameterCodePages =>
            // Code page 936 and 65001/UTF8 do not encode "é" to 0xE9. CP936 encodes it to [0xA8, 0xA6], UTF8 encodes it to [0xC3, 0xA9]
            // Chinese_PRC_CI_AI and Albanian_100_CI_AI_SC_UTF8 are the alphabetically first collations which use these two code pages.
            DataTestUtility.IsUTF8Supported()
                ? new() { { "Chinese_PRC_CI_AI", 936 }, { "Albanian_100_CI_AI_SC_UTF8", 65001 } }
                : new() { { "Chinese_PRC_CI_AI", 936 } };

        /// <summary>
        /// Tests that setting an output SqlParameter to an invalid value (e.g. a string in a decimal param)
        /// doesn't throw, since the value is cleared before execution starts.
        /// The output value should be correctly set by SQL Server after execution.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void InvalidValueInOutputParameter_ShouldSucceed()
        {
            // Arrange
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

            // Act
            // Execute - should not throw
            command.ExecuteNonQuery();

            // Assert
            // Validate - the output value should be set correctly by SQL Server
            Assert.Equal(new decimal(1.23), (decimal)decimalParam.Value);
        }

        /// <summary>
        /// Tests that text with sample collations roundtrips.
        /// </summary>
        /// <param name="collation">Name of a SQL Server collation which encodes text in the given code page.</param>
        /// <param name="codePage">ID of the codepage which should be used by SQL Server and the driver to encode and decode text.</param>
        [Theory]
        [MemberData(nameof(OutputParameterCodePages))]
        public void CollatedStringInOutputParameter_DecodesSuccessfully(string collation, int codePage)
        {
            const string SampleText = "Text with an accented é varies by encoding";

            using SqlConnection sqlConnection = new(DataTestUtility.TCPConnectionString);
            using SqlCommand roundtripCollationCommand = new($"SELECT @Output_Varchar = convert(varchar(max), '{SampleText}') COLLATE {collation}, " +
                $"@Output_Varbinary = convert(varbinary(max), convert(varchar(max), '{SampleText}') COLLATE {collation})", sqlConnection);
            SqlParameter outputVarcharParameter = new("@Output_Varchar", SqlDbType.VarChar, 8000)
            { Direction = ParameterDirection.Output };
            SqlParameter outputVarbinaryParameter = new("@Output_Varbinary", SqlDbType.VarBinary, 8000)
            { Direction = ParameterDirection.Output };
            Encoding codePageEncoding = Encoding.GetEncoding(codePage);

            roundtripCollationCommand.Parameters.Add(outputVarcharParameter);
            roundtripCollationCommand.Parameters.Add(outputVarbinaryParameter);

            sqlConnection.Open();
            roundtripCollationCommand.ExecuteNonQuery();

            string clientSideDecodedString = codePageEncoding.GetString((byte[])outputVarbinaryParameter.Value);
            byte[] clientSideStringBytes = codePageEncoding.GetBytes(outputVarcharParameter.Value.ToString());

            // Verify that the varchar value has been decoded correctly and matches the sample text,
            // then verify that the varbinary value roundtrips properly.
            Assert.Equal(SampleText, outputVarcharParameter.Value.ToString());
            Assert.Equal(outputVarcharParameter.Value.ToString(), clientSideDecodedString);
            Assert.Equal((byte[])outputVarbinaryParameter.Value, clientSideStringBytes);
        }
    }
}
