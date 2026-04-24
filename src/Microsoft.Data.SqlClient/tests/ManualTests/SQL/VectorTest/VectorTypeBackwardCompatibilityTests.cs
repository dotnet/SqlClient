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
    /// vector data as varchar(max) JSON strings.
    /// </summary>
    public static class VarcharVectorTestData
    {
        public static readonly float[] TestData = { 1.1f, 2.2f, 3.3f };

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

    public sealed class VectorTypeBackwardCompatibilityTests : VectorBackwardCompatTestBase
    {
        public VectorTypeBackwardCompatibilityTests(ITestOutputHelper output)
            : base(output, columnDefinition: "vector(3)", namePrefix: "Vector")
        {
        }

        protected override float[] GetPrepareTestValues(int i) =>
            new float[] { i + 0.1f, i + 0.2f, i + 0.3f };

        #region Insert Tests

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        [MemberData(nameof(VarcharVectorTestData.GetVarcharVectorInsertTestData), MemberType = typeof(VarcharVectorTestData), DisableDiscoveryEnumeration = true)]
        public void TestVectorDataInsertionAsVarchar(int pattern, string jsonValue, float[] expectedData)
            => InsertAndValidateAsVarchar(pattern, jsonValue, expectedData);

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        [MemberData(nameof(VarcharVectorTestData.GetVarcharVectorInsertTestData), MemberType = typeof(VarcharVectorTestData), DisableDiscoveryEnumeration = true)]
        public async Task TestVectorDataInsertionAsVarcharAsync(int pattern, string jsonValue, float[] expectedData)
            => await InsertAndValidateAsVarcharAsync(pattern, jsonValue, expectedData);

        #endregion

        #region Stored Procedure Tests

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        public void TestStoredProcParamsForVectorAsVarchar()
            => StoredProcRoundTrip(VarcharVectorTestData.TestData);

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        public async Task TestStoredProcParamsForVectorAsVarcharAsync()
            => await StoredProcRoundTripAsync(VarcharVectorTestData.TestData);

        #endregion

        #region Bulk Copy Tests

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        [InlineData(1)]
        [InlineData(2)]
        public void TestSqlBulkCopyForVectorAsVarchar(int bulkCopySourceMode)
            => BulkCopyRoundTrip(bulkCopySourceMode, VarcharVectorTestData.TestData);

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        [InlineData(1)]
        [InlineData(2)]
        public async Task TestSqlBulkCopyForVectorAsVarcharAsync(int bulkCopySourceMode)
            => await BulkCopyRoundTripAsync(bulkCopySourceMode, VarcharVectorTestData.TestData);

        #endregion

        #region Prepared Statement Tests

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        public void TestInsertVectorsAsVarcharWithPrepare()
            => PreparedInsertRoundTrip();

        #endregion
    }
}
