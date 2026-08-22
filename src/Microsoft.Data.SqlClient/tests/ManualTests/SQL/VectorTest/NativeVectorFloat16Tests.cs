// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// System.Half, and therefore SqlVector<Half>, is only available on .NET.
#if NET

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

#nullable enable

public sealed class VectorFloat16TestData : NativeVectorTestDataBase<Half>
{
    // Includes the extremes of the binary16 range, a subnormal, and a negative zero.
    // Every value is exactly representable, so it survives a round trip through the
    // JSON rendering the string based read paths return.
    public override Half[] SampleScalarData =>
    [
        (Half)1.5f,
        (Half)2.25f,
        (Half)(-3.75f),
        Half.MaxValue,
        Half.Epsilon,
        (Half)(-0.0f),
    ];

    public override Half[,] SampleDataSet
    {
        get
        {
            Half[,] sampleData = new Half[10, ValidSampleScalarDataLength];

            for (int i = 0; i < sampleData.GetLength(0); i++)
            {
                float baseValue = i * 10;

                for (int j = 0; j < sampleData.GetLength(1); j++)
                {
                    // Eighths are exactly representable in binary16 at this magnitude, so
                    // the values are unchanged by the round trip through the server.
                    sampleData[i, j] = (Half)(baseValue + (j * 0.125f));
                }
            }

            return sampleData;
        }
    }

    public override int IncorrectScalarDataParameterSize => 3234;

    public override bool IsSupported => DataTestUtility.IsSqlVectorFloat16Supported;

    public override string SqlServerTypeName => "float16";
}

[Trait("Set", "3")]
public sealed class NativeVectorFloat16Tests : NativeVectorTestsBase<Half, VectorFloat16TestData>
{
}

#endif
