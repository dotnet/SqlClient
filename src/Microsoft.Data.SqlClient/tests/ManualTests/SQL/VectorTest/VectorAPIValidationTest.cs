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
        // We need this testcase to validate ref assembly for vector APIs
        // Unit tests are covered under SqlVectorTest.cs
        [Fact]
        public void VectorAPITest()
        {
            // Validate that SqlVector<float> is a valid type and has valid SqlDbType
            Assert.True(typeof(SqlVector<float>).IsValueType, "SqlVector<float> should be a value type.");
            Assert.Equal(36, (int)SqlDbTypeExtensions.Vector);

            // Validate ctor1 with float[] : public SqlVector(System.ReadOnlyMemory<T> memory) { }
            var vector = new SqlVector<float>(VectorFloat32TestData.testData);
            Assert.Equal(VectorFloat32TestData.testData, vector.Memory.ToArray());
            Assert.Equal(3, vector.Length);

            // Validate ctor2 with ReadOnlyMemory<T> : public SqlVector(ReadOnlyMemory<T> memory) { }
            vector = new SqlVector<float>(new ReadOnlyMemory<float>(VectorFloat32TestData.testData));
            Assert.Equal(VectorFloat32TestData.testData, vector.Memory.ToArray());
            Assert.Equal(3, vector.Length);

            //Validate IsNull property
            Assert.False(vector.IsNull, "IsNull should be false for non-null vector.");

            // Validate Null property returns null
            Assert.Null(SqlVector<float>.Null);

            //Validate length property
            Assert.Equal(3, vector.Length);

            // Validate CreateNull method
            vector = SqlVector<float>.CreateNull(5);
            Assert.True(vector.IsNull);
            Assert.Equal(5, vector.Length);
        }
    }
}
