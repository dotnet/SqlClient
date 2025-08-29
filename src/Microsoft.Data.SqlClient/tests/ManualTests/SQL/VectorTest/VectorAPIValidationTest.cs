// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest
{
    public sealed class VectorAPIValidationTest
    {
        // We need these testcases to validate ref assembly for vector APIs
        // Unit tests are covered under SqlVectorTest.cs
        [Fact]
        public void ValidateVectorSqlDbType()
        {
            // Validate that SqlVector<float> is a valid type and has valid SqlDbType
            Assert.True(typeof(SqlVector<float>).IsValueType, "SqlVector<float> should be a value type.");
            Assert.Equal(36, (int)SqlDbTypeExtensions.Vector);
        }

        [Fact]
        public void TestSqlVectorCreationAPIWithFloatArr()
        {
            // Validate ctor1 with float[] : public SqlVector(System.ReadOnlyMemory<T> memory) { }
            var testData = new float[] { 1.1f, 2.2f, 3.3f };
            var vector = new SqlVector<float>(testData);
            Assert.Equal(testData, vector.Memory.ToArray());
            Assert.Equal(3, vector.Length);
        }

        [Fact]
        public void TestSqlVectorCreationAPIWithROM()
        {
            // Validate ctor2 with ReadOnlyMemory<T> : public SqlVector(ReadOnlyMemory<T> memory) { }
            var testData = new ReadOnlyMemory<float>(new float[] { 1.1f, 2.2f, 3.3f });
            var vector = new SqlVector<float>(testData);
            Assert.Equal(testData.ToArray(), vector.Memory.ToArray());
            Assert.Equal(3, vector.Length);
        }

        [Fact]
        public void TestSqlVectorCreationAPICreateNull()
        {
            // Validate CreateNull method
            var vector = SqlVector<float>.CreateNull(5);
            Assert.True(vector.IsNull);
            Assert.Equal(5, vector.Length);
        }

        [Fact]
        public void TestIsNullProperty()
        {
            //Validate IsNull property
            var testData = new ReadOnlyMemory<float>(new float[] { 1.1f, 2.2f, 3.3f });
            var vector = new SqlVector<float>(testData);
            Assert.False(vector.IsNull, "IsNull should be false for non-null vector.");
            vector = SqlVector<float>.CreateNull(3);
            Assert.True(vector.IsNull, "IsNull should be true for null vector.");
        }

        [Fact]
        public void TestNullProperty()
        {
            // Validate Null property returns null
            Assert.Null(SqlVector<float>.Null);
        }

        [Fact]
        public void TestLengthProperty()
        {
            // Validate Length property is correctly populated for null and non-null vectors
            var testData = new float[] { 1.1f, 2.2f, 3.3f };
            var vector = new SqlVector<float>(testData);
            Assert.Equal(3, vector.Length);
            vector = SqlVector<float>.CreateNull(3);
            Assert.Equal(3, vector.Length);
        }

        [Fact]
        public void TestMemoryProperty()
        {
            // Validate Memory property is correctly populated for non-null and null vectors
            var testData = new float[] { 1.1f, 2.2f, 3.3f };
            var vector = new SqlVector<float>(testData);
            Assert.Equal(testData, vector.Memory.ToArray());
            vector = SqlVector<float>.CreateNull(3);
            Assert.True( vector.Memory.IsEmpty, "Null vector of given size point to empty ROM");
        }
    }
}
