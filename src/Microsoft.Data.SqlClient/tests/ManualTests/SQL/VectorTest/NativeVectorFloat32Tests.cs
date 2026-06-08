// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

#nullable enable

public sealed class VectorFloat32TestData : NativeVectorTestDataBase<float>
{
    public override float[] SampleScalarData => [1.1f, 2.2f, 3.3f, 1.01f, float.MinValue, -0.0f];

    public override float[,] SampleDataSet
    {
        get
        {
            float[,] sampleData = new float[10, ValidSampleScalarDataLength];

            for (int i = 0; i < sampleData.GetLength(0); i++)
            {
                float baseValue = i * 10;

                for (int j = 0; j < sampleData.GetLength(1); j++)
                {
                    sampleData[i, j] = baseValue + (j * 0.1f);
                }
            }

            return sampleData;
        }
    }

    public override int IncorrectScalarDataParameterSize => 3234;

    public override bool IsSupported => DataTestUtility.IsSqlVectorSupported;

    public override string SqlServerTypeName => "float32";
}

[Trait("Set", "3")]
public sealed class NativeVectorFloat32Tests : NativeVectorTestsBase<float, VectorFloat32TestData>
{
}
