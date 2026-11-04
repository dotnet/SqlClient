// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.DataTypeReaderRunner;

/// <summary>Combines single-type and mixed-row shapes without a redundant Cartesian product.</summary>
public sealed class ReaderCase
{
    public DataType Type { get; }
    public int ColumnGroups { get; }

    public ReaderCase(DataType type) => Type = type;

    public ReaderCase(int columnGroups) => ColumnGroups = columnGroups;

    public override string ToString() => Type?.ToString() ?? $"Mixed{ColumnGroups * 16}";
}
