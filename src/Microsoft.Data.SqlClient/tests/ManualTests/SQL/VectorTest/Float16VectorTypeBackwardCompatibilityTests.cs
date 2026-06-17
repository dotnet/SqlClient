// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest
{
    /// <summary>
    /// Provides parameterized test data for backward compatibility tests that exchange
    /// float16 vector data as varchar(max) JSON strings.
    /// </summary>
    public static class Float16VarcharVectorTestData
    {
        // Values chosen to be exactly representable in IEEE-754 binary16 (float16),
        // so JSON round-trips through a vector(N, float16) column without precision loss.
        public static readonly float[] TestData = { 1.0f, 2.0f, 3.0f };

        /// <summary>
        /// Generates test cases for all 4 SqlParameter construction patterns x 2 value types (non-null + null).
        /// Each case yields: [int pattern, string jsonOrNull, float[] expectedData]
        /// where jsonOrNull is null when testing DBNull insertion.
        /// </summary>
        public static IEnumerable<object[]> GetVarcharVectorInsertTestData()
        {
            string json = JsonSerializer.Serialize(TestData);

            // Pattern 1-4 with non-null JSON value
            yield return new object[] { 1, json, TestData };
            yield return new object[] { 2, json, TestData };
            yield return new object[] { 3, json, TestData };
            yield return new object[] { 4, json, TestData };

            // Pattern 1-4 with null value
            yield return new object[] { 1, null, null };
            yield return new object[] { 2, null, null };
            yield return new object[] { 3, null, null };
            yield return new object[] { 4, null, null };
        }
    }

    public sealed class Float16VectorTypeBackwardCompatibilityTests : VectorBackwardCompatTestBase
    {
        public Float16VectorTypeBackwardCompatibilityTests(ITestOutputHelper output)
            : base(output, columnDefinition: "vector(3, float16)", namePrefix: "VectorF16")
        {
        }

        protected override float[] GetPrepareTestValues(int i) =>
            new float[] { i + 1, i + 2, i + 3 };

        #region Insert Tests

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
        [MemberData(nameof(Float16VarcharVectorTestData.GetVarcharVectorInsertTestData), MemberType = typeof(Float16VarcharVectorTestData), DisableDiscoveryEnumeration = true)]
        public void TestVectorDataInsertionAsVarchar(int pattern, string jsonValue, float[] expectedData)
            => InsertAndValidateAsVarchar(pattern, jsonValue, expectedData);

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
        [MemberData(nameof(Float16VarcharVectorTestData.GetVarcharVectorInsertTestData), MemberType = typeof(Float16VarcharVectorTestData), DisableDiscoveryEnumeration = true)]
        public async Task TestVectorDataInsertionAsVarcharAsync(int pattern, string jsonValue, float[] expectedData)
            => await InsertAndValidateAsVarcharAsync(pattern, jsonValue, expectedData);

        #endregion

        #region Stored Procedure Tests

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
        public void TestStoredProcParamsForVectorAsVarchar()
            => StoredProcRoundTrip(Float16VarcharVectorTestData.TestData);

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
        public async Task TestStoredProcParamsForVectorAsVarcharAsync()
            => await StoredProcRoundTripAsync(Float16VarcharVectorTestData.TestData);

        #endregion

        #region Bulk Copy Tests

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
        [InlineData(1)]
        [InlineData(2)]
        public void TestSqlBulkCopyForVectorAsVarchar(int bulkCopySourceMode)
            => BulkCopyRoundTrip(bulkCopySourceMode, Float16VarcharVectorTestData.TestData);

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
        [InlineData(1)]
        [InlineData(2)]
        public async Task TestSqlBulkCopyForVectorAsVarcharAsync(int bulkCopySourceMode)
            => await BulkCopyRoundTripAsync(bulkCopySourceMode, Float16VarcharVectorTestData.TestData);

        #endregion

        #region Prepared Statement Tests

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
        public void TestInsertVectorsAsVarcharWithPrepare()
            => PreparedInsertRoundTrip();

        #endregion
    }
}
