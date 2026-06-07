// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

#nullable enable

public sealed class VectorFloat32TestData : VectorTestDataBase<float>
{
}

public sealed class NativeVectorFloat32Tests : NativeVectorTestsBase<float, VectorFloat32TestData>
{
}
