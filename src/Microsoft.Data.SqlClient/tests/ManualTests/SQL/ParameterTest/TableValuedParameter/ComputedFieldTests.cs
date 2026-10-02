// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.Server;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.TableValuedParameter;

[Trait("Set", "3")]
public class ComputedFieldTests
{
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputedFieldValues_ReturnedByServer(bool explicitlySpecifyValue) =>
        SendComputedFieldsAndAssert(explicitlySpecifyValue, recordCount: 20, markFieldAsComputed: true);

    [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
    public void UnmarkedComputedFieldValues_RejectedByServer()
    {
        Action sendTVP = () => SendComputedFieldsAndAssert(explicitlySpecifyValue: false, recordCount: 20, markFieldAsComputed: false);

        SqlException serverException = Assert.Throws<SqlException>(sendTVP);

        Assert.Equal(271, serverException.Number);
        Assert.Equal(1, serverException.State);
        Assert.Contains("The column \"Sum\" cannot be modified because it is either a computed column or is the result of a UNION operator.", serverException.Message);
        Assert.Contains("The column \"CastSum\" cannot be modified because it is either a computed column or is the result of a UNION operator.", serverException.Message);
    }

    [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
    public void MismatchedComputedFieldLength_IgnoredByServer() =>
        SendComputedFieldsAndAssert(explicitlySpecifyValue: false, recordCount: 20, markFieldAsComputed: true, nvarcharMaxLength: 1);

    private void SendComputedFieldsAndAssert(
        bool explicitlySpecifyValue,
        int recordCount,
        bool markFieldAsComputed,
        int nvarcharMaxLength = 50,
        [CallerMemberName] string callerName = nameof(SendComputedFieldsAndAssert))
    {
        // TombstoneValue must always be a value which is never equal to [i + i + 1] for any "i" value
        // between 0 and recordCount - 1. This is to ensure that the computed column value is not equal
        // to the tombstone value when explicitly specified.
        const int TombstoneValue = 0;

        // Arrange
        using SqlConnection conn = new(DataTestUtility.TCPConnectionString);
        using UserDefinedType udt = new(conn, callerName, @"
            TABLE
            (
                Value1 INT NOT NULL,
                Sum AS (Value1 + Value2),
                Value2 INT NOT NULL,
                CastSum AS (CAST((Value1 + Value2) AS NVARCHAR(50)))
            )");
        using StoredProcedure sp = new(conn, callerName, $@"
            @PrecedingValue INT,
            @tvp {udt.Name} READONLY,
            @SubsequentValue INT
            AS
            BEGIN
                SELECT Value1, Value2, Sum, CastSum FROM @tvp;
                SELECT @PrecedingValue AS PrecedingValue, @SubsequentValue AS SubsequentValue;
            END");
        SqlMetaData[] metaDatas = [
            new SqlMetaData("Value1", SqlDbType.Int),
            new SqlMetaData("Sum", SqlDbType.Int) { IsComputed = markFieldAsComputed },
            new SqlMetaData("Value2", SqlDbType.Int),
            new SqlMetaData("CastSum", SqlDbType.NVarChar, maxLength: nvarcharMaxLength) { IsComputed = markFieldAsComputed }
        ];
        List<SqlDataRecord> records = [];

        for (int i = 0; i < recordCount; i++)
        {
            SqlDataRecord record = new(metaDatas);
            record.SetInt32(0, i);
            record.SetInt32(2, i + 1);

            if (explicitlySpecifyValue)
            {
                record.SetInt32(1, TombstoneValue);
                record.SetString(3, TombstoneValue.ToString());
            }

            records.Add(record);
        }

        using SqlCommand cmd = new(sp.Name, conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@PrecedingValue", 50);

        SqlParameter tvpParam = cmd.Parameters.Add("@tvp", SqlDbType.Structured);
        tvpParam.TypeName = udt.Name;
        tvpParam.Value = records;

        cmd.Parameters.AddWithValue("@SubsequentValue", 100);

        // Act
        using SqlDataReader reader = cmd.ExecuteReader();
        int returnedRecordCount = 0;

        // Assert
        while (reader.Read())
        {
            int value1 = reader.GetInt32(0);
            int value2 = reader.GetInt32(1);
            int sum = reader.GetInt32(2);
            string castSum = reader.GetString(3);

            Assert.Equal(value1 + value2, sum);
            Assert.Equal((value1 + value2).ToString(), castSum);

            if (explicitlySpecifyValue)
            {
                Assert.NotEqual(TombstoneValue, sum);
                Assert.NotEqual(TombstoneValue.ToString(), castSum);
            }
            returnedRecordCount++;
        }

        bool nextResult = reader.NextResult();
        Assert.True(nextResult);

        nextResult = reader.Read();
        Assert.True(nextResult);

        Assert.Equal(cmd.Parameters["@PrecedingValue"].Value, reader.GetInt32(0));
        Assert.Equal(cmd.Parameters["@SubsequentValue"].Value, reader.GetInt32(1));

        Assert.Equal(recordCount, returnedRecordCount);
    }
}
