// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// System.Half, and therefore SqlVector<Half>, is only available on .NET.
#if NET

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

#nullable enable

/// <summary>
/// Supplies the native vector test matrix for a <c>float16</c> column read and written as
/// <c>SqlVector&lt;Half&gt;</c>, which is the column's own base type and so travels without
/// conversion. The samples span the binary16 extremes, a subnormal, and a negative zero, and
/// every value is exactly representable, so it survives the round trip through the JSON
/// rendering that the string based read paths return.
/// </summary>
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

    // float16 is only exchanged in its binary form when the connection asks for the
    // feature extension version which covers it.
    public override string ConnectionString => DataTestUtility.VectorFloat16ConnectionString;
}

/// <summary>
/// Runs the full native vector matrix against a <c>float16</c> column using
/// <c>SqlVector&lt;Half&gt;</c>, the representation which matches the column's base type and
/// is therefore exchanged as the server sent it, with no per element conversion. Compiled
/// only for .NET, since <c>System.Half</c> does not exist on .NET Framework.
/// </summary>
[Trait("Set", "3")]
public sealed class NativeVectorFloat16Tests : NativeVectorTestsBase<Half, VectorFloat16TestData>
{
}

#endif
