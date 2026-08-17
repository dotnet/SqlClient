// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

#nullable enable

public sealed class VectorFloat16AsSingleTestData : NativeVectorTestDataBase<float>
{
    // Every value is exactly representable in binary16, so it survives narrowing on the
    // way to the server and widening on the way back.
    public override float[] SampleScalarData => [1.5f, 2.25f, -3.75f, 65504f, 0.125f, -0.0f];

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
                    // Eighths are exactly representable in binary16 at this magnitude.
                    sampleData[i, j] = baseValue + (j * 0.125f);
                }
            }

            return sampleData;
        }
    }

    public override int IncorrectScalarDataParameterSize => 3234;

    public override bool IsSupported => DataTestUtility.IsSqlVectorFloat16Supported;

    public override string SqlServerTypeName => "float16";

    // The column's base type is float16, so single precision is a widening representation
    // which the caller has to ask for rather than the driver's default.
    public override bool IsDefaultRepresentation => false;
}

/// <summary>
/// Runs the full native vector matrix against a <c>float16</c> column using the single
/// precision representation. This is the only representation available on .NET Framework,
/// which has no <c>System.Half</c>, and is also where the hand written binary16 codec is
/// the production path rather than a test double.
/// </summary>
[Trait("Set", "3")]
public sealed class NativeVectorFloat16AsSingleTests : NativeVectorTestsBase<float, VectorFloat16AsSingleTestData>
{
}
